using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cqrs.Portable.Tests.Envelope;
using Lokad.Cqrs;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;

namespace Cqrs.Portable.Tests
{
	public class MessageStorePublisherTest
	{
		private string _path;
		private FileAppendOnlyStore _appendOnlyStore;
		private IMessageSerializer _serializer;
		private MessageStore _store;
		private MessageSender _sender;
		private NuclearStorage _nuclearStorage;
		private MessageStorePublisher _publisher;
		private static List< StoreRecord > _storeRecords;

		[ SetUp ]
		public void SetUp()
		{
			_storeRecords = new List< StoreRecord >();
			this._serializer = new TestMessageSerializer( new[] { typeof( SerializerTest1 ), typeof( SerializerTest2 ), typeof( string ) } );
			this._path = Path.Combine( Path.GetTempPath(), "MessageStorePublisher", Guid.NewGuid().ToString() );
			this._appendOnlyStore = new FileAppendOnlyStore( new DirectoryInfo( this._path ) );
			this._appendOnlyStore.Initialize();
			this._store = new MessageStore( this._appendOnlyStore, this._serializer );
			var streamer = new EnvelopeStreamer( this._serializer );
			var queueWriter = new TestQueueWriter();
			this._sender = new MessageSender( streamer, queueWriter );
			var store = new FileDocumentStore( Path.Combine( this._path, "lokad-cqrs-test" ), new DocumentStrategy() );
			this._nuclearStorage = new NuclearStorage( store );

			this._publisher = new MessageStorePublisher( "test-tape", this._store, this._sender, this._nuclearStorage, DoWePublishThisRecord );
		}

		[ TearDown ]
		public void Teardown()
		{
			this._appendOnlyStore.Close();

			try
			{
				Directory.Delete( this._path, true );
			}
			catch( IOException x )
			{
			}
		}

		private static bool DoWePublishThisRecord( StoreRecord storeRecord )
		{
			var result = storeRecord.Key != "audit";
			if( result )
				_storeRecords.Add( storeRecord );
			return result;
		}

		[ Test ]
		public void when_verify_events_where_empty_store()
		{
			this._publisher.VerifyEventStreamSanity();
			var records = this._store.EnumerateAllItems( 0, 100 ).ToArray();

			Assert.AreEqual( 0, records.Length );
		}

		[ Test ]
		public void when_verify_events_where_events_count_less_100()
		{
			this._store.AppendToStore( "stream1", new List< MessageAttribute >(), 0, new List< object > { new SerializerTest1( "message1" ) } );
			this._store.AppendToStore( "stream1", new List< MessageAttribute >(), 1, new List< object > { new SerializerTest1( "message1" ) } );

			var records = this._store.EnumerateAllItems( 0, 100 ).ToArray();
			this._publisher.VerifyEventStreamSanity();

			Assert.AreEqual( 2, records.Length );
		}

		[ Test ]
		[ ExpectedException( typeof( InvalidOperationException ) ) ]
		public void when_verify_events_where_events_count_more_100()
		{
			for( var i = 0; i < 101; i++ )
			{
				this._store.AppendToStore( "stream1", new List< MessageAttribute >(), i, new List< object > { new SerializerTest1( "message1" ) } );
			}

			var records = this._store.EnumerateAllItems( 0, 1000 ).ToArray();

			Assert.AreEqual( 101, records.Length );
			this._publisher.VerifyEventStreamSanity();
		}

		[ Test ]
		public void when_run()
		{
			for( var i = 0; i < 50; i++ )
			{
				this._store.AppendToStore( "stream1", new List< MessageAttribute >(), i, new List< object > { new SerializerTest1( "message" + i ) } );
			}

			var cancellationToken = new CancellationToken();

			ThreadPool.QueueUserWorkItem( state => this._publisher.Run( cancellationToken ) );
			while( _storeRecords.Count < 50 )
				cancellationToken.WaitHandle.WaitOne( 10 );

			foreach( var storeRecord in _storeRecords )
			{
				Assert.AreEqual( "stream1", storeRecord.Key );
				Assert.AreEqual( 1, storeRecord.Items.Length );
				Assert.AreEqual( typeof( SerializerTest1 ), storeRecord.Items[ 0 ].GetType() );
			}
		}
	}
}