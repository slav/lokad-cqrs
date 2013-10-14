using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Cqrs.TapeStorage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Netco.ActionPolicyServices;
using Netco.Logging;

namespace Lokad.Cqrs.AppendOnly
{
	/// <summary>
	/// <para>This is embedded append-only store implemented on top of cloud page blobs 
	/// (for persisting data with one HTTP call).</para>
	/// <para>This store ensures that only one writer exists and writes to a given event store</para>
	/// </summary>
	public sealed class BlobAppendOnlyStore : IAppendOnlyStore
	{
		// Caches
		private readonly CloudBlobContainer _container;

		private readonly LockingInMemoryCache _cache = new LockingInMemoryCache();

		private bool _closed;

		private static readonly ActionPolicy _policy = ActionPolicy.Handle< Exception >().Retry( 2000, ( x, i ) =>
		{
			NetcoLogger.GetLogger( typeof( BlobAppendOnlyStore ) ).Log().Error( x, "Error encountered while working with Azure. Retry count: {0}", i );
			Task.Delay( 500 ).Wait();
		} );

		/// <summary>
		/// Currently open file
		/// </summary>
		private AppendOnlyStream _currentWriter;

		public BlobAppendOnlyStore( CloudBlobContainer container )
		{
			this._container = container;
		}

		public void Dispose()
		{
			if( !this._closed )
				this.Close();
		}

		public void InitializeWriter()
		{
			CreateIfNotExists( this._container, TimeSpan.FromSeconds( 240 ) );
			this.LoadCaches();
		}

		public void InitializeReader()
		{
			CreateIfNotExists( this._container, TimeSpan.FromSeconds( 240 ) );
			this.LoadCaches();
		}

		private int _pageSizeMultiplier = 1024 * 512;

		public void Append( string streamName, byte[] data, long expectedStreamVersion = -1 )
		{
			// should be locked
			try
			{
				this._cache.ConcurrentAppend( streamName, data, ( streamVersion, storeVersion ) =>
				{
					this.EnsureWriterExists( storeVersion );
					this.Persist( streamName, data, streamVersion );
				}, expectedStreamVersion );
			}
			catch( AppendOnlyStoreConcurrencyException )
			{
				//store is OK when AOSCE is thrown. This is client's problem
				// just bubble it upwards
				throw;
			}
			catch
			{
				// store probably corrupted. Close it and then rethrow exception
				// so that clien will have a chance to retry.
				this.Close();
				throw;
			}
		}

		public IEnumerable< DataWithKey > ReadRecords( string streamName, long afterVersion, int maxCount )
		{
			return this._cache.ReadStream( streamName, afterVersion, maxCount );
		}

		public IEnumerable< DataWithKey > ReadRecords( long afterVersion, int maxCount )
		{
			return this._cache.ReadAll( afterVersion, maxCount );
		}

		public void Close()
		{
			this._closed = true;

			if( this._currentWriter == null )
				return;

			var tmp = this._currentWriter;
			this._currentWriter = null;
			tmp.Dispose();
		}

		public void ResetStore()
		{
			this.Close();
			this._cache.Clear( () =>
			{
				var blobs = this._container.ListBlobs().OfType< CloudPageBlob >().Where( item => item.Uri.ToString().EndsWith( ".dat" ) );

				blobs.AsParallel().ForAll( i => i.DeleteIfExists() );
			} );
		}

		public long GetCurrentVersion()
		{
			return this._cache.StoreVersion;
		}

		private IEnumerable< StorageFrameDecoded > EnumerateHistory()
		{
			// cleanup old pending files
			// load indexes
			// build and save missing indexes
			var datFiles = this._container
				.ListBlobs( blobListingDetails : BlobListingDetails.Metadata )
				.OrderBy( s => s.Uri.ToString() )
				.OfType< CloudPageBlob >()
				.Where( s => s.Name.EndsWith( ".dat" ) );
			var fileFrameCollections = new List< BlockingCollection< StorageFrameDecoded > >();
			var tapeLoadTasks = new List< Task >();

			foreach( var fileInfo in datFiles )
			{
				var fileFrames = new BlockingCollection< StorageFrameDecoded >();
				fileFrameCollections.Add( fileFrames );

				tapeLoadTasks.Add( this.LoadTapeFile( fileInfo, fileFrames ) );

				if( tapeLoadTasks.Count == 500 )
				{
					this.Log().Trace( "{0}\tWaiting on {1} tapes", this._container.Name, tapeLoadTasks.Count );
					Task.WaitAll( tapeLoadTasks.ToArray() );
					this.Log().Trace( "{0}\t{1} tapes loaded", this._container.Name, tapeLoadTasks.Count );

					tapeLoadTasks.Clear();
				}
			}
			
			this.Log().Trace( "{0}\tWaiting on {1} tapes", this._container.Name, tapeLoadTasks.Count );
			Task.WaitAll( tapeLoadTasks.ToArray() );
			this.Log().Trace( "{0}\t{1} tapes loaded", this._container.Name, tapeLoadTasks.Count );

			var loadedFrames = fileFrameCollections.SelectMany( fileFrameCollection => fileFrameCollection.GetConsumingEnumerable() );
			return loadedFrames;
		}

		private async Task LoadTapeFile( CloudPageBlob fileBlob, BlockingCollection< StorageFrameDecoded > fileFrames )
		{
			var retryPolicy = new ExponentialRetry( TimeSpan.FromSeconds( 0.5 ), 100 );

			try
			{
				var tapeStream = await _policy.Get( () => fileBlob.OpenReadAsync( null, new BlobRequestOptions { RetryPolicy = retryPolicy, MaximumExecutionTime = TimeSpan.FromMinutes( 30 ), ServerTimeout = TimeSpan.FromMinutes( 30 ) }, null ) ).ConfigureAwait( false );

				using( var ms = new MemoryStream() )
				{
					tapeStream.CopyToAsync( ms ).Wait();
					ms.Position = 0;

					var potentiallyNonTruncatedChunk = ms.Length % this._pageSizeMultiplier == 0;
					long lastValidPosition = 0;
					StorageFrameDecoded result;
					while( StorageFramesEvil.TryReadFrame( ms, out result ) )
					{
						lastValidPosition = ms.Position;
						fileFrames.Add( result );
					}
					var haveSomethingToTruncate = ms.Length - lastValidPosition >= 512;
					if( potentiallyNonTruncatedChunk & haveSomethingToTruncate )
						this.TruncateBlob( lastValidPosition, fileBlob, retryPolicy );
				}
			}
			catch( Exception x )
			{
				this.Log().Error( x, "Error loading tape {0}", fileBlob.Name );
				throw;
			}
			finally
			{
				fileFrames.CompleteAdding();
			}
		}

		private void TruncateBlob( long lastValidPosition, CloudPageBlob fileInfo, ExponentialRetry retryPolicy )
		{
			var trunc = lastValidPosition;
			var remainder = lastValidPosition % 512;
			if( remainder > 0 )
				trunc += 512 - remainder;
			if( trunc == 0 )
			{
				this.Log().Error( "Truncating {0} to 0 looks suspcious. Aborting!", fileInfo.Name );
				return;
			}
			this.Log().Trace( string.Format( "Truncating {0} to {1}", fileInfo.Name, trunc ) );
			this.BackupFile( fileInfo, retryPolicy );

			SetLength( fileInfo, trunc, retryPolicy );
		}

		private void BackupFile( CloudPageBlob fileInfo, ExponentialRetry retryPolicy )
		{
			var backupBlobName = fileInfo.Name + ".bak";
			var backupBlob = this._container.GetPageBlobReference( backupBlobName );
			backupBlob.StartCopyFromBlob( fileInfo, options : new BlobRequestOptions { RetryPolicy = retryPolicy } );

			while( true ) // wait for copy operation to complete
			{
				backupBlob.FetchAttributes();
				switch( backupBlob.CopyState.Status )
				{
					case CopyStatus.Pending:
						Thread.Sleep( 500 );
						break;
					case CopyStatus.Success:
						return;
					case CopyStatus.Aborted:
						this.BackupFile( fileInfo, retryPolicy ); // try again
						return;
					case CopyStatus.Invalid:
					case CopyStatus.Failed:
						throw new InvalidOperationException( string.Format( "Failed to backup {0} to {1}. Status: {2}. Description: {3}",
							fileInfo.Name, backupBlobName, backupBlob.CopyState.Status, backupBlob.CopyState.StatusDescription ) );
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		private static void SetLength( CloudPageBlob blob, long newLength, ExponentialRetry retryPolicy )
		{
			TimeSpan? leaseTime = TimeSpan.FromSeconds( 15 );
			var options = new BlobRequestOptions { RetryPolicy = retryPolicy };
			var leaseId = blob.AcquireLease( leaseTime, null, options : options );
			blob.Resize( newLength, new AccessCondition { LeaseId = leaseId }, options : options );

			blob.ReleaseLease( new AccessCondition { LeaseId = leaseId }, options : options );
		}

		private void LoadCaches()
		{
			this._cache.LoadHistory( this.EnumerateHistory() );
		}

		private void Persist( string key, byte[] buffer, long commit )
		{
			var frame = StorageFramesEvil.EncodeFrame( key, buffer, commit );
			if( !this._currentWriter.Fits( frame.Data.Length + frame.Hash.Length ) )
			{
				this.CloseWriter();
				this.EnsureWriterExists( this._cache.StoreVersion );
			}

			this._currentWriter.Write( frame.Data );
			this._currentWriter.Write( frame.Hash );
			this._currentWriter.Flush();
		}

		private void CloseWriter()
		{
			this._currentWriter.Dispose();
			this._currentWriter = null;
		}

		private void EnsureWriterExists( long version )
		{
			if( this._currentWriter != null )
				return;
			
			var azureOptions = new BlobRequestOptions
			{
				MaximumExecutionTime = TimeSpan.FromMinutes( 30 ),
				ServerTimeout = TimeSpan.FromMinutes( 30 ),
				RetryPolicy = new LinearRetry( TimeSpan.FromSeconds( 0.2 ), 100 )
			};
			var fileName = string.Format( "{0:00000000}-{1:yyyy-MM-dd-HHmmss}.dat", version, DateTime.UtcNow );
			var blob = _policy.Get( () => this._container.GetPageBlobReference( fileName ) );
			_policy.Do( ( () => blob.Create( this._pageSizeMultiplier, options: azureOptions ) ) );

			this._currentWriter = new AppendOnlyStream( 512, ( i, bytes ) => blob.WritePages( bytes, i, options: azureOptions ), this._pageSizeMultiplier );
		}

		private static void CreateIfNotExists( CloudBlobContainer container, TimeSpan timeout )
		{
			var sw = Stopwatch.StartNew();
			while( sw.Elapsed < timeout )
			{
				try
				{
					container.CreateIfNotExists();
					return;
				}
				catch( StorageException e )
				{
					if( e.RequestInformation.HttpStatusCode != ( int )HttpStatusCode.Conflict )
						throw;
				}
				Thread.Sleep( 500 );
			}

			throw new TimeoutException( string.Format( "Can not create container within {0} seconds.", timeout.TotalSeconds ) );
		}
	}
}