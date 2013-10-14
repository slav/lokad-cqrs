#region Copyright (c) 2012 LOKAD SAS. All rights reserved
// You must not remove this notice, or any other, from this software.
// This document is the property of LOKAD SAS and must not be disclosed
#endregion

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Netco.ActionPolicyServices;
using Netco.Logging;

namespace Lokad.Cqrs.AtomicStorage
{
	/// <summary>
	/// Azure implementation of the view reader/writer
	/// </summary>
	/// <typeparam name="TEntity">The type of the view.</typeparam>
	public sealed class AzureAtomicReader< TKey, TEntity > : IDocumentReader< TKey, TEntity >
	{
		private readonly AzureBlobDirectory _containerDirectory;
		private readonly IDocumentStrategy _strategy;
		private readonly ILogger _logger;
		private readonly ActionPolicyAsync _ap;
		private readonly Random _random = new Random();

		public AzureAtomicReader( CloudBlobClient storageClient, IDocumentStrategy strategy )
		{
			this._strategy = strategy;
			var folder = strategy.GetEntityBucket< TEntity >();
			this._containerDirectory = storageClient.GetBlobDirectory( folder );
			this._logger = NetcoLogger.GetLogger( this.GetType() );
			this._ap = ActionPolicyAsync.From( ( exception =>
			{
				var storageException = exception as StorageException;
				if( storageException == null )
					return false;

				switch( storageException.RequestInformation.HttpStatusCode )
				{
					case ( int )HttpStatusCode.InternalServerError:
					case ( int )HttpStatusCode.ServiceUnavailable:
						return true;
					default:
						return false;
				}
			})).Retry( 200, ( ex, i ) =>
			{
				this._logger.Log().Trace( ex, "Retrying Azure API GET call: {0}/200", i );
				var secondsDelay = 0.2 + 0.1 * _random.Next( -1, 1 ); // randomize wait time
				Task.Delay( TimeSpan.FromSeconds( secondsDelay ) ).Wait();
			} );
		}

		private CloudBlockBlob GetBlobReference( TKey key )
		{
			return this._containerDirectory.GetBlockBlobReference( this._strategy.GetEntityLocation< TEntity >( key ) );
		}

		public bool TryGet( TKey key, out TEntity entity )
		{
			var blob = this.GetBlobReference( key );
			try
			{
				// blob request options are cloned from the config
				// atomic entities should be small, so we can use the simple method
				using( var msGet = new MemoryStream() )
				{
					blob.DownloadToStream( msGet );
					msGet.Position = 0;
					entity = this._strategy.Deserialize< TEntity >( msGet );
					return true;
				}
			}
			catch( StorageException ex )
			{
				switch( ex.RequestInformation.HttpStatusCode )
				{
					case ( int )HttpStatusCode.NotFound:
						entity = default( TEntity );
						return false;
					default:
						var additionalInfo = ex.GetStorageExceptionAdditionalDetails();
						this.Log().Error( ex, "Error loading {0}. {1}", key.ToString(), additionalInfo );
						throw;
				}
			}
		}

		public async Task< Maybe< TEntity > > GetAsync( TKey key )
		{
			var blob = this.GetBlobReference( key );
			try
			{
				// blob request options are cloned from the config
				// atomic entities should be small, so we can use the simple method
				using( var msGet = new MemoryStream() )
				{
					await _ap.Do( () => blob.DownloadToStreamAsync( msGet ) ).ConfigureAwait( false );
					msGet.Position = 0;
					return this._strategy.Deserialize< TEntity >( msGet );
				}
			}
			catch( StorageException ex )
			{
				switch( ex.RequestInformation.HttpStatusCode )
				{
					case ( int )HttpStatusCode.NotFound:
						return Maybe< TEntity >.Empty;
					default:
						var additionalInfo = ex.GetStorageExceptionAdditionalDetails();
						this.Log().Error( ex, "Error loading {0}. {1}", key.ToString(), additionalInfo );
						throw;
				}
			}
		}
	}
}