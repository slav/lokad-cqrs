using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Lokad.Cqrs
{
	public static class AzureBlobStorageExtensions
	{
		public static async Task< IEnumerable< IListBlobItem > > ListBlobsAsync(
			this CloudBlobContainer container,
			string prefix = null,
			bool useFlatBlobListing = false,
			int? pageSize = null,
			BlobListingDetails details = BlobListingDetails.None,
			BlobRequestOptions options = null,
			OperationContext operationContext = null,
			IProgress< IEnumerable< IListBlobItem > > progress = null,
			CancellationToken cancellationToken = default( CancellationToken ) )
		{
			options = options ?? new BlobRequestOptions();
			var results = new List< IListBlobItem >();
			BlobContinuationToken continuation = null;
			do
			{
				var segment = await container.ListBlobsSegmentedAsync( prefix, useFlatBlobListing, details, pageSize, continuation, options, operationContext, cancellationToken );
				if( progress != null )
					progress.Report( segment.Results );
				results.AddRange( segment.Results );
				continuation = segment.ContinuationToken;
			} while( continuation != null );

			return results;
		}

		public static async Task< IEnumerable< IListBlobItem > > ListBlobsAsync(
			this CloudBlobContainer directory,
			IProgress< IEnumerable< IListBlobItem > > progress = null,
			CancellationToken cancellationToken = default( CancellationToken ) )
		{
			var results = new List< IListBlobItem >();
			BlobContinuationToken continuation = null;
			do
			{
				var segment = await directory.ListBlobsSegmentedAsync( continuation, cancellationToken );
				if( progress != null )
					progress.Report( segment.Results );
				results.AddRange( segment.Results );
				continuation = segment.ContinuationToken;
			} while( continuation != null );

			return results;
		}

		public static async Task< IEnumerable< IListBlobItem > > ListBlobsAsync(
			this CloudBlobDirectory container,
			bool useFlatBlobListing = false,
			int? pageSize = null,
			BlobListingDetails details = BlobListingDetails.None,
			BlobRequestOptions options = null,
			OperationContext operationContext = null,
			IProgress< IEnumerable< IListBlobItem > > progress = null,
			CancellationToken cancellationToken = default( CancellationToken ) )
		{
			options = options ?? new BlobRequestOptions();
			var results = new List< IListBlobItem >();
			BlobContinuationToken continuation = null;
			do
			{
				var segment = await container.ListBlobsSegmentedAsync( useFlatBlobListing, details, pageSize, continuation, options, operationContext, cancellationToken );
				if( progress != null )
					progress.Report( segment.Results );
				results.AddRange( segment.Results );
				continuation = segment.ContinuationToken;
			} while( continuation != null );

			return results;
		}

		public static async Task< IEnumerable< IListBlobItem > > ListBlobsAsync(
			this CloudBlobDirectory directory,
			IProgress< IEnumerable< IListBlobItem > > progress = null,
			CancellationToken cancellationToken = default( CancellationToken ) )
		{
			var results = new List< IListBlobItem >();
			BlobContinuationToken continuation = null;
			do
			{
				var segment = await directory.ListBlobsSegmentedAsync( continuation, cancellationToken );
				if( progress != null )
					progress.Report( segment.Results );
				results.AddRange( segment.Results );
				continuation = segment.ContinuationToken;
			} while( continuation != null );

			return results;
		}
	}
}