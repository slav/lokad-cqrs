﻿#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Linq;
using System.Text;
using Lokad.Cqrs.AppendOnly;
using Lokad.Cqrs.TapeStorage;
using Microsoft.WindowsAzure.Storage.Blob;
using Netco.Logging;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.AppendOnly
{
	public class BlobAppendOnlyStoreTest
	{
		private const int DataFileCount = 10;
		private const int FileMessagesCount = 5;
		private BlobAppendOnlyStore _appendOnly;
		private CloudBlobContainer _blobContainer;
		private string _name;

		[ SetUp ]
		public void Setup()
		{
			NetcoLogger.LoggerFactory = new ConsoleLoggerFactory();
			this._name = Guid.NewGuid().ToString().ToLowerInvariant();
			this.InitStore();
		}

		private void InitStore()
		{
			var cloudStorageAccount = ConnectionConfig.StorageAccount;
			var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
			this._blobContainer = cloudBlobClient.GetContainerReference( this._name );

			this._appendOnly = new BlobAppendOnlyStore( this._blobContainer );
			this._appendOnly.InitializeWriter();
		}

		[ TearDown ]
		public void Teardown()
		{
			this._appendOnly.Close();
			this._blobContainer.Delete();
		}

		[ Test ]
		public void when_append_and_read()
		{
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message1" ) );
			this._appendOnly.Append( "stream2", Encoding.UTF8.GetBytes( "test message2" ) );
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message3" ) );

			var recordsSteam1 = this._appendOnly.ReadRecords( "stream1", 0, Int32.MaxValue ).ToArray();
			var recordsSteam2 = this._appendOnly.ReadRecords( "stream2", 0, Int32.MaxValue ).ToArray();

			Assert.AreEqual( 2, recordsSteam1.Length );
			Assert.AreEqual( 1, recordsSteam2.Length );
			Assert.AreEqual( "test message1", Encoding.UTF8.GetString( recordsSteam1[ 0 ].Data ) );
			Assert.AreEqual( "test message3", Encoding.UTF8.GetString( recordsSteam1[ 1 ].Data ) );
			Assert.AreEqual( "test message2", Encoding.UTF8.GetString( recordsSteam2[ 0 ].Data ) );
		}

		[ Test ]
		public void when_read_after_version()
		{
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message1" ) );
			this._appendOnly.Append( "stream2", Encoding.UTF8.GetBytes( "test message2" ) );
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message3" ) );

			var recordsSteam1 = this._appendOnly.ReadRecords( "stream1", 1, Int32.MaxValue ).ToArray();

			Assert.AreEqual( 1, recordsSteam1.Length );
			Assert.AreEqual( "test message3", Encoding.UTF8.GetString( recordsSteam1[ 0 ].Data ) );
		}

		[ Test ]
		public void when_read_than_set_max_records()
		{
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message1" ) );
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message2" ) );
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message3" ) );

			var recordsSteam1 = this._appendOnly.ReadRecords( "stream1", 0, 2 ).ToArray();

			Assert.AreEqual( 2, recordsSteam1.Length );
			Assert.AreEqual( "test message1", Encoding.UTF8.GetString( recordsSteam1[ 0 ].Data ) );
			Assert.AreEqual( "test message2", Encoding.UTF8.GetString( recordsSteam1[ 1 ].Data ) );
		}

		[ Test ]
		public void when_reads_record()
		{
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message1" ) );
			this._appendOnly.Append( "stream2", Encoding.UTF8.GetBytes( "test message2" ) );
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message3" ) );

			var recordsSteam = this._appendOnly.ReadRecords( 0, Int32.MaxValue ).ToArray();

			Assert.AreEqual( 3, recordsSteam.Length );
			Assert.AreEqual( 1, recordsSteam[ 0 ].StoreVersion );
			Assert.AreEqual( 2, recordsSteam[ 1 ].StoreVersion );
			Assert.AreEqual( 3, recordsSteam[ 2 ].StoreVersion );
			Assert.AreEqual( 1, recordsSteam[ 0 ].StreamVersion );
			Assert.AreEqual( 1, recordsSteam[ 1 ].StreamVersion );
			Assert.AreEqual( 2, recordsSteam[ 2 ].StreamVersion );
			Assert.AreEqual( "test message1", Encoding.UTF8.GetString( recordsSteam[ 0 ].Data ) );
			Assert.AreEqual( "test message2", Encoding.UTF8.GetString( recordsSteam[ 1 ].Data ) );
			Assert.AreEqual( "test message3", Encoding.UTF8.GetString( recordsSteam[ 2 ].Data ) );
		}

		[ Test ]
		[ ExpectedException( typeof( AppendOnlyStoreConcurrencyException ) ) ]
		public void append_data_when_set_version_where_does_not_correspond_real_version()
		{
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message1" ), 100 );
		}

		[ Test ]
		public void get_current_version()
		{
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message1" ) );
			this._appendOnly.Append( "stream2", Encoding.UTF8.GetBytes( "test message2" ) );
			this._appendOnly.Append( "stream1", Encoding.UTF8.GetBytes( "test message3" ) );

			Assert.AreEqual( 3, this._appendOnly.GetCurrentVersion() );
		}

		private void CreateCacheFiles()
		{
			const string msg = "test messages";
			for( var index = 0; index < DataFileCount; index++ )
			{
				for( var i = 0; i < FileMessagesCount; i++ )
				{
					this._appendOnly.Append( "test-key" + index, Encoding.UTF8.GetBytes( msg + i ) );
				}
			}
			InitStore();
		}

		[ Test ]
		public void load_cache()
		{
			this.CreateCacheFiles();
			for( var j = 0; j < DataFileCount; j++ )
			{
				var key = "test-key" + j;
				var data = this._appendOnly.ReadRecords( key, 0, Int32.MaxValue ).ToArray();
				Assert.AreEqual( FileMessagesCount, data.Length );
				var i = 0;
				foreach( var dataWithKey in data )
				{
					Assert.AreEqual( "test messages" + i, Encoding.UTF8.GetString( dataWithKey.Data ) );
					i++;
				}
			}
		}

		[ Test ]
		public void when_reset_store()
		{
			var stream = Guid.NewGuid().ToString();

			for( var i = 0; i < 10; i++ )
			{
				this._appendOnly.Append( stream, Encoding.UTF8.GetBytes( "test message" + i ) );
			}

			var version = this._appendOnly.GetCurrentVersion();
			this._appendOnly.ResetStore();
			var versionAfterReset = this._appendOnly.GetCurrentVersion();

			Assert.GreaterOrEqual( 10, version );
			Assert.AreEqual( 0, versionAfterReset );
		}

		[ Test ]
		public void when_append_after_reset_store()
		{
			var stream = Guid.NewGuid().ToString();

			for( var i = 0; i < 10; i++ )
			{
				this._appendOnly.Append( stream, Encoding.UTF8.GetBytes( "test message" + i ) );
			}
			this._appendOnly.ResetStore();
			for( var i = 0; i < 10; i++ )
			{
				this._appendOnly.Append( stream, Encoding.UTF8.GetBytes( "test message" + i ) );
			}

			var version = this._appendOnly.GetCurrentVersion();

			Assert.GreaterOrEqual( 10, version );
		}
	}
}