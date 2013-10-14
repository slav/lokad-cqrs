#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Linq;
using Lokad.Cqrs;
using Lokad.Cqrs.Feature.StreamingStorage;
using Lokad.Cqrs.StreamingStorage;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.StreamingStorage
{
	public class BlobStreamingContainerTest
	{
		private string _name;
		private BlobStreamingContainer _streamContainer;

		[ SetUp ]
		public void Setup()
		{
			this._name = Guid.NewGuid().ToString().ToLowerInvariant();
			var cloudStorageAccount = ConnectionConfig.StorageAccount;

			var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
			var container = cloudBlobClient.GetBlobDirectory( this._name );
			this._streamContainer = new BlobStreamingContainer( container );
			this._streamContainer.Create();
		}

		[ TearDown ]
		public void Teardown()
		{
			this._streamContainer.Delete();
		}

		[ Test ]
		public void when_not_created_container()
		{
			var cloudStorageAccount = ConnectionConfig.StorageAccount;
			var client = cloudStorageAccount.CreateCloudBlobClient();
			var streamContainer = new BlobStreamingContainer( client.GetBlobDirectory( "blob-streaming-directory-nothing" ) );

			Assert.IsFalse( streamContainer.Exists() );
		}

		[ Test ]
		public void when_created_container()
		{
			Assert.IsTrue( this._streamContainer.Exists() );
		}

		[ Test ]
		public void when_delete_container()
		{
			this._streamContainer.Delete();
			Assert.IsFalse( this._streamContainer.Exists() );
		}

		[ Test ]
		public void when_create_sub_container()
		{
			var subContainer = this._streamContainer.GetContainer( "sub" );
			subContainer.Create();

			Assert.IsTrue( subContainer.Exists() );
			Assert.AreEqual( this._streamContainer.FullPath.Trim( '/' ) + "/sub", subContainer.FullPath.Trim( '/' ) );
		}

		[ Test ]
		public void when_nothing_blob()
		{
			Assert.IsFalse( this._streamContainer.Exists( "example.txt" ) );
		}

		[ Test ]
		public void when_existing_blob()
		{
			using( var stream = this._streamContainer.OpenWrite( "example.txt" ) )
				stream.WriteByte( 1 );

			Assert.IsTrue( this._streamContainer.Exists( "example.txt" ) );
		}

		[ Test ]
		public void when_write_read_blob()
		{
			using( var stream = this._streamContainer.OpenWrite( "example.txt" ) )
				stream.WriteByte( 1 );
			int result;
			using( var readStream = this._streamContainer.OpenRead( "example.txt" ) )
				result = readStream.ReadByte();

			Assert.AreEqual( 1, result );
		}

		[ Test ]
		public void when_delete_exist_blob()
		{
			using( var stream = this._streamContainer.OpenWrite( "example.txt" ) )
				stream.WriteByte( 1 );
			this._streamContainer.TryDelete( "example.txt" );

			Assert.IsFalse( this._streamContainer.Exists( "example.txt" ) );
		}

		[ Test ]
		public void when_delete_nothing_blob()
		{
			this._streamContainer.TryDelete( "example.txt" );

			Assert.IsFalse( this._streamContainer.Exists( "example.txt" ) );
		}

		[ Test ]
		public void when_existing_blob_items()
		{
			using( var stream = this._streamContainer.OpenWrite( "example1.txt" ) )
				stream.WriteByte( 1 );
			using( var stream = this._streamContainer.OpenWrite( "example2.txt" ) )
				stream.WriteByte( 1 );

			var items = this._streamContainer.ListAllNestedItems().ToArray();

			Assert.AreEqual( 2, items.Length );
			Assert.AreEqual( "example1.txt", items[ 0 ] );
			Assert.AreEqual( "example2.txt", items[ 1 ] );
		}

		[ Test ]
		[ ExpectedException( typeof( StreamContainerNotFoundException ) ) ]
		public void when_nothing_blob_items()
		{
			var cloudStorageAccount = ConnectionConfig.StorageAccount;
			var client = cloudStorageAccount.CreateCloudBlobClient();
			var streamContainer = new BlobStreamingContainer( client.GetBlobDirectory( "blob-streaming-directory-nothing" ) );

			var existSubContainer = streamContainer.Exists();
			streamContainer.ListAllNestedItems().ToArray();

			Assert.IsFalse( existSubContainer );
		}

		[ Test ]
		public void when_existing_blob_detail_items()
		{
			using( var stream = this._streamContainer.OpenWrite( "example1.txt" ) )
				stream.WriteByte( 1 );
			using( var stream = this._streamContainer.OpenWrite( "example2.txt" ) )
				stream.WriteByte( 1 );

			var items = this._streamContainer.ListAllNestedItemsWithDetail().ToArray();

			Assert.AreEqual( 2, items.Length );
			Assert.AreEqual( "example1.txt", items[ 0 ].Name );
			Assert.AreEqual( "example2.txt", items[ 1 ].Name );
		}

		[ Test ]
		[ ExpectedException( typeof( StreamContainerNotFoundException ) ) ]
		public void when_nothing_blob_detail_items()
		{
			var cloudStorageAccount = ConnectionConfig.StorageAccount;
			var client = cloudStorageAccount.CreateCloudBlobClient();
			var streamContainer = new BlobStreamingContainer( client.GetBlobDirectory( "blob-streaming-directory-nothing" ) );

			var existSubContainer = streamContainer.Exists();
			streamContainer.ListAllNestedItemsWithDetail().ToArray();

			Assert.IsFalse( existSubContainer );
		}
	}
}