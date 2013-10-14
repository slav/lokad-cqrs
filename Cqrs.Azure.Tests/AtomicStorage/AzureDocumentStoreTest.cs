#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lokad.Cqrs.AtomicStorage;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.AtomicStorage
{
	public class AzureDocumentStoreTest
	{
		private IDocumentStore _store;
		private string _name;
		private CloudBlobContainer _container;

		[ SetUp ]
		public void Setup()
		{
			this._name = Guid.NewGuid().ToString().ToLowerInvariant();
			var cloudStorageAccount = ConnectionConfig.StorageAccount;
			var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
			var documentStrategy = new DocumentStrategy( this._name );
			this._store = new AzureDocumentStore( documentStrategy, cloudBlobClient );

			this._container = cloudBlobClient.GetContainerReference( this._name );
			this._container.CreateIfNotExists();
		}

		[ TearDown ]
		public void Teardown()
		{
			this._container.Delete();
		}

		[ Test ]
		public void when_get_not_created_bucket()
		{
			//GIVEN
			var bucket = Guid.NewGuid().ToString();

			//WHEN
			CollectionAssert.IsEmpty( this._store.EnumerateContents( bucket ) );
		}

		[ Test ]
		public void when_write_bucket()
		{
			//GIVEN
			var records = new List< DocumentRecord >
			{
				new DocumentRecord( "first", () => Encoding.UTF8.GetBytes( "test message 1" ) ),
				new DocumentRecord( "second", () => Encoding.UTF8.GetBytes( "test message 2" ) ),
			};
			this._store.WriteContents( this._name, records );

			//WHEN
			var actualRecords = this._store.EnumerateContents( this._name ).ToList();
			Assert.AreEqual( records.Count, actualRecords.Count );
			for( var i = 0; i < records.Count; i++ )
			{
				Assert.AreEqual( 1, actualRecords.Count( x => x.Key == records[ i ].Key ) );
				Assert.AreEqual( Encoding.UTF8.GetString( records[ i ].Read() ),
					Encoding.UTF8.GetString( actualRecords.First( x => x.Key == records[ i ].Key ).Read() ) );
			}
		}

		[ Test ]
		public void when_reset_bucket()
		{
			//GIVEN
			var records = new List< DocumentRecord >
			{
				new DocumentRecord( "first", () => Encoding.UTF8.GetBytes( "test message 1" ) ),
			};
			this._store.WriteContents( this._name, records );
			var result1 = this._store.EnumerateContents( this._name ).ToList();
			CollectionAssert.IsNotEmpty( result1 );
			this._store.Reset( this._name );

			//WHEN
			var result2 = this._store.EnumerateContents( this._name ).ToList();
			CollectionAssert.IsEmpty( result2 );
		}

		[ Test ]
		public void when_read_exist_entity()
		{
			var writer = this._store.GetWriter< Guid, TestView >();
			var reader = this._store.GetReader< Guid, TestView >();
			var key = Guid.NewGuid();
			var entity = writer.Add( key, new TestView( key ) );
			TestView savedView;
			var result = reader.TryGet( key, out savedView );

			Assert.IsTrue( result );
			Assert.AreEqual( key, entity.Id );
			Assert.AreEqual( key, savedView.Id );
		}

		[ Test ]
		public void when_read_nothing_entity()
		{
			var reader = this._store.GetReader< Guid, TestView >();
			var key = Guid.NewGuid();
			TestView savedView;
			var result = reader.TryGet( key, out savedView );

			Assert.IsFalse( result );
		}
	}
}