using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CuttingEdge.Conditions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Lokad.Cqrs
{
	public static class AzureStorageUtils
	{
		public static AzureFolderParts CutFolder( string folderName, string delimiter )
		{
			Condition.Requires( folderName, "folderName" ).IsNotNullOrWhiteSpace();
			Condition.Requires( delimiter, "delimiter" ).IsNotNullOrWhiteSpace();

			var folderParts = folderName.Split( new[] { delimiter }, 2, StringSplitOptions.None );
			Condition.Ensures( folderParts, "folderParts" )
				.IsNotEmpty( "{0} should have at least container name container name" )
				.IsShorterOrEqual( 2, "{0} should have only two parts: container name and actual folder name" );

			// Create parts, puting null for the folder name if only container name without subfolters was supplied
			return new AzureFolderParts( folderParts[ 0 ], folderParts.Length > 1 ? folderParts[ 1 ] : null );
		}

		private static AzureFolderParts CutFolder( this CloudBlobClient client, string folderName )
		{
			return CutFolder( folderName, client.DefaultDelimiter );
		}

		public static AzureBlobDirectory GetBlobDirectory( this CloudBlobClient client, string folderName )
		{
			var parts = client.CutFolder( folderName );
			var container = client.GetContainerReference( parts.ContainerName );

			return parts.Subfolder != null
				? new AzureBlobDirectory( container, container.GetDirectoryReference( parts.Subfolder ) )
				: new AzureBlobDirectory( container, null );
		}

		private static string MakeRelativeUriFixingTrailingSlash( Uri baseUri, Uri relativeUri )
		{
			var fullDirUri = baseUri.ToString().EndsWith( "/" ) ? baseUri : new Uri( baseUri + "/" );
			return fullDirUri.MakeRelativeUri( relativeUri ).OriginalString;
		}

		public static string MakeRelativeUri( this IListBlobItem blobItem, Uri uri )
		{
			return MakeRelativeUriFixingTrailingSlash( blobItem.Uri, uri );
		}

		public static string MakeRelativeUri( this IListBlobItem blobItem, IListBlobItem relatedItem )
		{
			return MakeRelativeUri( blobItem, relatedItem.Uri );
		}

		public static string GetStorageExceptionAdditionalDetails( this StorageException x )
		{
			var info = x.RequestInformation.ExtendedErrorInformation;
			if( info == null )
				return string.Empty;

			var sb = new StringBuilder();
			sb.AppendLine( "Request ID: " + x.RequestInformation.ServiceRequestID );
			sb.AppendLine( "Error Code: " + info.ErrorCode );
			sb.Append( "Error Message: " + info.ErrorMessage );
			foreach( var additionalDetail in info.AdditionalDetails )
			{
				sb.AppendLine();
				sb.Append( additionalDetail.Key + " : " + additionalDetail.Value );
			}
			return sb.ToString();
		}
	}

	public class AzureFolderParts
	{
		public string ContainerName { get; private set; }
		public string Subfolder { get; private set; }

		public AzureFolderParts( string containerName, string subfolder )
		{
			this.ContainerName = containerName;
			this.Subfolder = subfolder;
		}
	}

	public class AzureBlobDirectory : IListBlobItem
	{
		public CloudBlobContainer Container { get; private set; }
		public CloudBlobDirectory Directory { get; private set; }

		public Uri Uri
		{
			get { return this.Directory != null ? this.Directory.Uri : this.Container.Uri; }
		}

		public CloudBlobDirectory Parent
		{
			get { return this.Directory != null ? this.Directory.Parent : null; }
		}

		public AzureBlobDirectory( CloudBlobContainer container, CloudBlobDirectory directory )
		{
			Condition.Requires( container, "container" ).IsNotNull();
			this.Container = container;
			this.Directory = directory;
		}

		public IEnumerable< IListBlobItem > ListBlobs( bool useFlatBlobListing = false )
		{
			return this.Directory != null ? this.Directory.ListBlobs( useFlatBlobListing ) : this.Container.ListBlobs( useFlatBlobListing : useFlatBlobListing );
		}

		public Task< IEnumerable< IListBlobItem > > ListBlobsAsync( bool useFlatBlobListing = false )
		{
			return this.Directory != null ? this.Directory.ListBlobsAsync( useFlatBlobListing ) : this.Container.ListBlobsAsync( useFlatBlobListing: useFlatBlobListing );
		}
		public CloudBlockBlob GetBlockBlobReference( string blobName )
		{
			return this.Directory != null ? this.Directory.GetBlockBlobReference( blobName ) : this.Container.GetBlockBlobReference( blobName );
		}

		public CloudBlobDirectory GetSubdirectoryReference( string name )
		{
			return this.Directory != null ? this.Directory.GetSubdirectoryReference( name ) : this.Container.GetDirectoryReference( name );
		}

		public AzureBlobDirectory GetAzureSubdirectory( string name )
		{
			return new AzureBlobDirectory( this.Container, this.GetSubdirectoryReference( name ) );
		}
	}
}