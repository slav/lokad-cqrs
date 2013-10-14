#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Text;
using System.Threading;
using Lokad.Cqrs;
using Lokad.Cqrs.Feature.AzurePartition;
using Lokad.Cqrs.Feature.AzurePartition.Inbox;
using Lokad.Cqrs.Partition;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.Partition
{
	public class AzureQueueWriteReadTest
	{
		private StatelessAzureQueueReader _statelessReader;
		private AzureQueueReader _queueReader;
		private StatelessAzureQueueWriter _queueWriter;
		private string _name;
		private CloudBlobClient _cloudBlobClient;
		private CloudBlobContainer _blobContainer;
		private CloudQueue _queue;

		[ SetUp ]
		public void Setup()
		{
			this._name = Guid.NewGuid().ToString().ToLowerInvariant();
			var cloudStorageAccount = ConnectionConfig.StorageAccount;

			this._cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
			this._queue = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference( this._name );
			var container = this._cloudBlobClient.GetBlobDirectory( this._name );

			this._blobContainer = this._cloudBlobClient.GetContainerReference( this._name );
			var poisonQueue = new Lazy< CloudQueue >( () =>
			{
				var queueReference = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference( this._name + "-poison" );
				queueReference.CreateIfNotExists();
				return queueReference;
			}, LazyThreadSafetyMode.ExecutionAndPublication );
			this._statelessReader = new StatelessAzureQueueReader( "azure-read-write-message", this._queue, container, poisonQueue, TimeSpan.FromMinutes( 1 ) );
			this._queueReader = new AzureQueueReader( new[] { this._statelessReader }, x => TimeSpan.FromMinutes( x ) );
			this._queueWriter = new StatelessAzureQueueWriter( this._blobContainer, this._queue, "azure-read-write-message" );
			this._queueWriter.Init();
		}

		[ TearDown ]
		public void Teardown()
		{
			this._blobContainer.Delete();
			this._queue.Delete();
		}

		[ Test ]
		[ ExpectedException( typeof( NullReferenceException ) ) ]
		public void when_put_null_message()
		{
			this._queueWriter.PutMessage( null );
		}

		[ Test ]
		public void when_get_added_message()
		{
			this._queueWriter.PutMessage( Encoding.UTF8.GetBytes( "message" ) );
			var msg = this._statelessReader.TryGetMessage();

			Assert.AreEqual( GetEnvelopeResultState.Success, msg.State );
			Assert.AreEqual( "message", Encoding.UTF8.GetString( msg.Message.Unpacked ) );
			Assert.AreEqual( "azure-read-write-message", msg.Message.QueueName );
		}

		[ Test ]
		[ ExpectedException( typeof( ArgumentNullException ) ) ]
		public void when_ack_null_message()
		{
			this._statelessReader.AckMessage( null );
		}

		[ Test ]
		public void when_ack_messages_by_name()
		{
			this._queueWriter.PutMessage( Encoding.UTF8.GetBytes( "message" ) );
			var msg = this._statelessReader.TryGetMessage();

			var transportContext = new MessageTransportContext(
				msg.Message.TransportMessage
				, msg.Message.Unpacked
				, msg.Message.QueueName );
			this._statelessReader.AckMessage( transportContext );
			var msg2 = this._statelessReader.TryGetMessage();

			Assert.AreEqual( GetEnvelopeResultState.Empty, msg2.State );
		}

		[ Test ]
		public void when_reader_ack_messages()
		{
			this._queueWriter.PutMessage( Encoding.UTF8.GetBytes( "message" ) );
			var msg = this._statelessReader.TryGetMessage();

			var transportContext = new MessageTransportContext(
				msg.Message.TransportMessage
				, msg.Message.Unpacked
				, msg.Message.QueueName );

			this._queueReader.AckMessage( transportContext );
			var msg2 = this._statelessReader.TryGetMessage();

			Assert.AreEqual( GetEnvelopeResultState.Empty, msg2.State );
		}

		[ Test ]
		public void when_reader_take_messages()
		{
			this._queueWriter.PutMessage( Encoding.UTF8.GetBytes( "message" ) );
			MessageTransportContext msg;
			var cancellationToken = new CancellationToken( false );
			var result = this._queueReader.TakeMessage( cancellationToken, out msg );

			Assert.IsTrue( result );
			Assert.AreEqual( "message", Encoding.UTF8.GetString( msg.Unpacked ) );
			Assert.AreEqual( "azure-read-write-message", msg.QueueName );
		}
	}
}