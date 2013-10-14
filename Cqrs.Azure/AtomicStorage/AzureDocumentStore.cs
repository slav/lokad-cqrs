#region (c) 2010-2012 Lokad - CQRS Sample for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2012, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Netco.Logging;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class AzureDocumentStore : IDocumentStore
	{
		public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
		{
			var writer = new AzureAtomicWriter< TKey, TEntity >( this._client, this._strategy );

			var value = Tuple.Create( typeof( TKey ), typeof( TEntity ) );
			if( this._initialized.Add( value ) )
			{
				// we've added a new record. Need to initialize
				writer.InitializeIfNeeded();
			}
			return writer;
		}

		public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
		{
			return new AzureAtomicReader< TKey, TEntity >( this._client, this._strategy );
		}

		public IDocumentStrategy Strategy
		{
			get { return this._strategy; }
		}

		public void Reset( string bucket )
		{
			ResetAsync( bucket ).Wait();
		}

		public async Task ResetAsync( string bucket )
		{
			var blobs = await this._client.GetBlobDirectory( bucket ).ListBlobsAsync( true );
			var deleteTasks = new List< Task >();
			foreach( var blob in blobs.OfType< CloudBlockBlob >() )
			{
				deleteTasks.Add( AP.Async.Do( blob.DeleteIfExistsAsync ) );

				if( deleteTasks.Count == 500 )
				{
					this.Log().Trace( "{0}\tWaiting to delete {1} tapes", bucket, deleteTasks.Count );
					await Task.WhenAll( deleteTasks );
					this.Log().Trace( "{0}\tDeleted {1} tapes", bucket, deleteTasks.Count );

					deleteTasks.Clear();
				}
			}
			this.Log().Trace( "{0}\tWaiting to delete {1} tapes", bucket, deleteTasks.Count );
			await Task.WhenAll( deleteTasks );
			this.Log().Trace( "{0}\tDeleted {1} tapes", bucket, deleteTasks.Count );
		}

		public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
		{
			var directory = this._client.GetBlobDirectory( bucket );
			if( !directory.Container.Exists() )
				yield break;

			var items = directory.ListBlobs( true );

			foreach( var item in items )
			{
				var blob = item as CloudBlockBlob;
				if( blob == null )
					continue;

				var rel = directory.MakeRelativeUri( item );
				yield return new DocumentRecord( rel.Replace( '\\', '/' ), () =>
				{
					using( var ms = new MemoryStream() )
					{
						blob.DownloadToStream( ms );
						return ms.ToArray();
					}
				} );
			}
		}

		public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
		{
			var retryPolicy = new ExponentialRetry( TimeSpan.FromSeconds( 0.5 ), 100 );

			var directory = this._client.GetBlobDirectory( bucket );
			directory.Container.CreateIfNotExists();

			var tasks = new List< Task >();
			var fullSw = new Stopwatch();
			var fullCounter = 0;
			try
			{
				var counter = 0;
				var sw = new Stopwatch();
				fullSw.Start();
				sw.Start();
				foreach( var record in records )
				{
					var data = record.Read();
					var blob = directory.GetBlockBlobReference( record.Key );
					tasks.Add( AP.LongAsync.Do( async () => await blob.UploadFromByteArrayAsync( data, 0, data.Length, null, new BlobRequestOptions { RetryPolicy = retryPolicy, MaximumExecutionTime = TimeSpan.FromMinutes( 15 ), ServerTimeout = TimeSpan.FromMinutes( 15 ) }, null ).ConfigureAwait( false ) ) );
					counter++;
					fullCounter++;

					if( tasks.Count == 200 )
					{
						SystemObserver.Notify( "{0}: Added {1}({2}) records to save", bucket, tasks.Count, counter );
						Task.WaitAll( tasks.ToArray() );
						sw.Stop();
						SystemObserver.Notify( "{0}: Total {1}({2}) records saved in {3}", bucket, tasks.Count, counter, sw.Elapsed );
						tasks.Clear();
						sw.Restart();
					}
				}
				SystemObserver.Notify( "{0}: Added {1}({2}) records to save", bucket, tasks.Count, counter );
				Task.WaitAll( tasks.ToArray() );
				sw.Stop();
				SystemObserver.Notify( "{0}: Total {1}({2}) records saved in {3}", bucket, tasks.Count, counter, sw.Elapsed );
			}
			finally
			{
				fullSw.Stop();
				SystemObserver.Notify( "{0}: Saved total {1} records in {2}", bucket, fullCounter, fullSw.Elapsed );
			}
		}

		public async Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token )
		{
			var retryPolicy = new ExponentialRetry( TimeSpan.FromSeconds( 0.5 ), 100 );

			var directory = this._client.GetBlobDirectory( bucket );
			await directory.Container.CreateIfNotExistsAsync( token );

			var tasks = new List< Task >();
			var fullSw = new Stopwatch();
			var fullCounter = 0;
			try
			{
				var counter = 0;
				var sw = new Stopwatch();
				fullSw.Start();
				sw.Start();
				foreach( var record in records )
				{
					var data = record.Read();
					var blob = directory.GetBlockBlobReference( record.Key );
					tasks.Add( AP.LongAsync.Do( () => blob.UploadFromByteArrayAsync( data, 0, data.Length, null, new BlobRequestOptions { RetryPolicy = retryPolicy, MaximumExecutionTime = TimeSpan.FromMinutes( 15 ), ServerTimeout = TimeSpan.FromMinutes( 15 ) }, null, token ) ) );
					counter++;
					fullCounter++;

					if( tasks.Count < 200 )
						continue;

					await WaitForViewsToSave( bucket, tasks, counter, sw );
					tasks.Clear();
					sw.Restart();
				}
				await WaitForViewsToSave( bucket, tasks, counter, sw );
			}
			finally
			{
				fullSw.Stop();
				SystemObserver.Notify( "{0}: Saved total {1} records in {2}", bucket, fullCounter, fullSw.Elapsed );
			}
		}

		private static async Task WaitForViewsToSave( string bucket, List< Task > tasks, int counter, Stopwatch sw )
		{
			SystemObserver.Notify( "{0}: Added {1}({2}) records to save", bucket, tasks.Count, counter );
			await Task.WhenAll( tasks );
			sw.Stop();
			SystemObserver.Notify( "{0}: Total {1}({2}) records saved in {3}", bucket, tasks.Count, counter, sw.Elapsed );
		}

		private readonly IDocumentStrategy _strategy;

		private readonly HashSet< Tuple< Type, Type > > _initialized = new HashSet< Tuple< Type, Type > >();
		private readonly CloudBlobClient _client;

		public AzureDocumentStore( IDocumentStrategy strategy, CloudBlobClient client )
		{
			this._strategy = strategy;
			this._client = client;
		}
	}
}