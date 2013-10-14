#region (c) 2010-2011 Lokad CQRS - New BSD License 
// Copyright (c) Lokad SAS 2010-2011 (http://www.lokad.com)
// This code is released as Open Source under the terms of the New BSD Licence
// Homepage: http://lokad.github.com/lokad-cqrs/
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace Lokad.Cqrs.AtomicStorage
{
	/// <summary>
	/// Azure implementation of the view reader/writer
	/// </summary>
	/// <typeparam name="TEntity">The type of the view.</typeparam>
	/// <typeparam name="TKey">the type of the key</typeparam>
	public sealed class AzureAtomicWriter< TKey, TEntity > : IDocumentWriter< TKey, TEntity >
		//where TEntity : IAtomicEntity<TKey>
	{
		private readonly AzureBlobDirectory _containerDirectory;
		private readonly IDocumentStrategy _strategy;

		public AzureAtomicWriter( CloudBlobClient storageClient, IDocumentStrategy strategy )
		{
			this._strategy = strategy;
			var folder = strategy.GetEntityBucket< TEntity >();
			this._containerDirectory = storageClient.GetBlobDirectory( folder );
		}

		public void InitializeIfNeeded()
		{
			this._containerDirectory.Container.CreateIfNotExists();
		}

		#region AddOrUpdate
		public TEntity AddOrUpdate( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint )
		{
			return this.AddOrUpdateAsync( key, addFactory, update, hint ).Result;
		}

		public async Task< TEntity > AddOrUpdateAsync( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint = AddOrUpdateHint.ProbablyExists )
		{
			var blob = this.GetBlobReference( key );
			var viewLoadResult = await this.TryLoadExistingView( addFactory, update, blob );
			// atomic entities should be small, so we can use the simple method
			// http://toolheaven.net/post/Azure-and-blob-write-performance.aspx
			await this.SaveViewIfNeeded( viewLoadResult.Item1, viewLoadResult.Item3, blob, viewLoadResult.Item2 );
			return viewLoadResult.Item1;
		}

		private async Task< Tuple< TEntity, byte[], string > > TryLoadExistingView( Func< TEntity > addViewFactory, Func< TEntity, TEntity > updateViewFactory, CloudBlockBlob blob )
		{
			TEntity view;
			var existingBytes = new byte[ 0 ];
			string etag = null;

			try
			{
				// atomic entities should be small, so we can use the simple method

				using( var msGet = new MemoryStream() )
				{
					await blob.DownloadToStreamAsync( msGet );
					existingBytes = msGet.ToArray();
				}
				using( var stream = new MemoryStream( existingBytes ) )
					view = this._strategy.Deserialize< TEntity >( stream );

				view = updateViewFactory( view );
				etag = blob.Properties.ETag;
			}
			catch( StorageException ex )
			{
				var requestInformation = ex.RequestInformation;
				if( requestInformation.ExtendedErrorInformation != null )
				{
					switch( requestInformation.ExtendedErrorInformation.ErrorCode )
					{
						case StorageErrorCodeStrings.ContainerNotFound:
							var s = string.Format(
								"Container '{0}' does not exist. You need to initialize this atomic storage and ensure that '{1}' is known to '{2}'.",
								blob.Container.Name, typeof( TEntity ).Name, this._strategy.GetType().Name );
							throw new InvalidOperationException( s, ex );
						case StorageErrorCodeStrings.ResourceNotFound:
						case BlobErrorCodeStrings.BlobNotFound:
							view = addViewFactory();
							break;
						default:
							throw;
					}
				}
				else
				{
					switch( ex.RequestInformation.HttpStatusCode )
					{
						case ( int )HttpStatusCode.NotFound:
							view = addViewFactory();
							break;
						default:
							throw;
					}
				}
			}
			return Tuple.Create( view, existingBytes, etag );
		}

		private Task SaveViewIfNeeded( TEntity view, string etag, CloudBlockBlob blob, IEnumerable< byte > existingBytes )
		{
			using( var memory = new MemoryStream() )
			{
				this._strategy.Serialize( view, memory );
				// note that upload from stream does weird things
				//				var accessCondition = etag != null
				//					? AccessCondition.GenerateIfMatchCondition( etag )
				//					: AccessCondition.GenerateIfNoneMatchCondition( "*" );

				// make sure that upload is not rejected due to cashed content MD5
				// http://social.msdn.microsoft.com/Forums/hu-HU/windowsazuredata/thread/4764e38f-b200-4efe-ada2-7de442dc4452
				blob.Properties.ContentMD5 = null;
				var newBytes = memory.ToArray();

				// upload only if content has changed
				if( !newBytes.SequenceEqual( existingBytes ) )
					return blob.UploadFromByteArrayAsync( newBytes, 0, newBytes.Length );
				else
					return Task.FromResult( true );
			}
		}
		#endregion

		#region Save
		public void Save( TKey key, TEntity entity )
		{
			var blob = this.GetBlobReference( key );
			// atomic entities should be small, so we can use the simple method
			// http://toolheaven.net/post/Azure-and-blob-write-performance.aspx
			using( var memory = new MemoryStream() )
			{
				this._strategy.Serialize( entity, memory );

				// make sure that upload is not rejected due to cashed content MD5
				// http://social.msdn.microsoft.com/Forums/hu-HU/windowsazuredata/thread/4764e38f-b200-4efe-ada2-7de442dc4452
				blob.Properties.ContentMD5 = null;

				var bytes = memory.ToArray();

				blob.UploadFromByteArray( bytes, 0, bytes.Length );
			}
		}

		public Task SaveAsync( TKey key, TEntity entity )
		{
			var blob = this.GetBlobReference( key );
			// atomic entities should be small, so we can use the simple method
			// http://toolheaven.net/post/Azure-and-blob-write-performance.aspx
			using( var memory = new MemoryStream() )
			{
				this._strategy.Serialize( entity, memory );

				// make sure that upload is not rejected due to cashed content MD5
				// http://social.msdn.microsoft.com/Forums/hu-HU/windowsazuredata/thread/4764e38f-b200-4efe-ada2-7de442dc4452
				blob.Properties.ContentMD5 = null;

				var bytes = memory.ToArray();
				return blob.UploadFromByteArrayAsync( bytes, 0, bytes.Length );
			}
		}
		#endregion

		#region TryDelete
		public bool TryDelete( TKey key )
		{
			var blob = this.GetBlobReference( key );
			return blob.DeleteIfExists();
		}

		public Task< bool > TryDeleteAsync( TKey key )
		{
			var blob = this.GetBlobReference( key );
			return blob.DeleteIfExistsAsync();
		}
		#endregion

		private CloudBlockBlob GetBlobReference( TKey key )
		{
			return this._containerDirectory.GetBlockBlobReference( this._strategy.GetEntityLocation< TEntity >( key ) );
		}
	}
}