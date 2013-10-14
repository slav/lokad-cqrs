#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using Lokad.Cqrs.StreamingStorage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Lokad.Cqrs.Feature.StreamingStorage
{
	/// <summary>
	/// Windows Azure implementation of storage 
	/// </summary>
	public sealed class BlobStreamingContainer : IStreamContainer
	{
		private readonly AzureBlobDirectory _directory;

		/// <summary>
		/// Initializes a new instance of the <see cref="BlobStreamingContainer"/> class.
		/// </summary>
		/// <param name="directory">The directory.</param>
		public BlobStreamingContainer( AzureBlobDirectory directory )
		{
			this._directory = directory;
		}

		public IStreamContainer GetContainer( string name )
		{
			if( name == null )
				throw new ArgumentNullException( "name" );

			return new BlobStreamingContainer( this._directory.GetAzureSubdirectory( name ) );
		}

		public Stream OpenRead( string name )
		{
			return this._directory.GetBlockBlobReference( name ).OpenRead();
		}

		public Stream OpenWrite( string name )
		{
			return this._directory.GetBlockBlobReference( name ).OpenWrite();
		}

		public void TryDelete( string name )
		{
			this._directory.GetBlockBlobReference( name ).DeleteIfExists();
		}

		public bool Exists( string name )
		{
			try
			{
				this._directory.GetBlockBlobReference( name ).FetchAttributes();
				return true;
			}
			catch( StorageException )
			{
				return false;
			}
		}

		public IStreamContainer Create()
		{
			this._directory.Container.CreateIfNotExists();
			return this;
		}

		/// <summary>
		/// Deletes this container
		/// </summary>
		public void Delete()
		{
			if( this._directory.Uri.ToString().Trim( '/' ) == this._directory.Container.Uri.ToString().Trim( '/' ) )
				this._directory.Container.DeleteIfExists();
			else
			{
				this._directory.ListBlobs().AsParallel().ForAll( l =>
				{
					var name = l.Parent.MakeRelativeUri( l );
					var r = this._directory.GetBlockBlobReference( name );
					r.BeginDeleteIfExists( ar =>
					{
					}, null );
				} );
			}
		}

		public IEnumerable< string > ListAllNestedItems()
		{
			try
			{
				return this._directory.ListBlobs()
					.Select( item => this._directory.MakeRelativeUri( item ) )
					.ToArray();
			}
			catch( StorageException e )
			{
				if( e.RequestInformation.HttpStatusCode == (int) HttpStatusCode.NotFound )
				{
					var message = string.Format( CultureInfo.InvariantCulture, "Storage container was not found: '{0}'.",
						this.FullPath );
					throw new StreamContainerNotFoundException( message, e );
				}
				throw;
			}
		}

		public IEnumerable< StreamItemDetail > ListAllNestedItemsWithDetail()
		{
			try
			{
				return this._directory.ListBlobs()
					.OfType< CloudBlockBlob >()
					.Select( item => new StreamItemDetail
					{
						Name = this._directory.MakeRelativeUri( item ),
						LastModifiedUtc = ( item.Properties.LastModified ?? ( DateTimeOffset? )DateTimeOffset.MinValue ).Value.DateTime,
						Length = item.Properties.Length
					} )
					.ToArray();
			}
			catch( StorageException e )
			{
				if( e.RequestInformation.HttpStatusCode == (int) HttpStatusCode.NotFound )
				{
					var message = string.Format( CultureInfo.InvariantCulture, "Storage container was not found: '{0}'.",
						this.FullPath );
					throw new StreamContainerNotFoundException( message, e );
				}
				throw;
			}
		}

		public bool Exists()
		{
			return this._directory.Container.Exists();
		}

		public string FullPath
		{
			get { return this._directory.Uri.ToString(); }
		}
	}
}