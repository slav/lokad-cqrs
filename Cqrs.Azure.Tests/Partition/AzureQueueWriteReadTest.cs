#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Text;
using System.Threading;
using Lokad.Cqrs.Feature.AzurePartition;
using Lokad.Cqrs.Feature.AzurePartition.Inbox;
using Lokad.Cqrs.Partition;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.Partition
{
    public class AzureQueueWriteReadTest
    {
        private StatelessAzureQueueReader _statelessReader;
        private StatelessAzureQueueWriter _writer;
        private AzureQueueReader _reader;

        [SetUp]
        public void Setup()
        {
            var name = Guid.NewGuid().ToString().ToLowerInvariant();
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var queue = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference(name);
            var container = cloudStorageAccount.CreateCloudBlobClient().GetBlobDirectoryReference(name);
            var blobContainer = cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(name);
            var poisonQueue = new Lazy<CloudQueue>(() =>
               {
                   var queueReference = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference(name + "-poison");
                   queueReference.CreateIfNotExist();
                   return queueReference;
               }, LazyThreadSafetyMode.ExecutionAndPublication);
            _statelessReader = new StatelessAzureQueueReader("azure-read-write-message", queue, container, poisonQueue, TimeSpan.FromMinutes(1));
            _reader = new AzureQueueReader(new[] {_statelessReader}, x => TimeSpan.FromMinutes(x));
            _writer = new StatelessAzureQueueWriter(blobContainer, queue, "azure-read-write-message");
            _writer.Init();
        }

        [Test, ExpectedException(typeof(NullReferenceException))]
        public void when_put_null_message()
        {
            _writer.PutMessage(null);
        }

        [Test]
        public void when_get_added_message()
        {
            _writer.PutMessage(Encoding.UTF8.GetBytes("message"));
            var msg = _statelessReader.TryGetMessage();

            Assert.AreEqual(GetEnvelopeResultState.Success, msg.State);
            Assert.AreEqual("message", Encoding.UTF8.GetString(msg.Message.Unpacked));
            Assert.AreEqual("azure-read-write-message", msg.Message.QueueName);
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void when_ack_null_message()
        {
            _statelessReader.AckMessage(null);
        }

        [Test]
        public void when_ack_messages_by_name()
        {
            _writer.PutMessage(Encoding.UTF8.GetBytes("message"));
            var msg = _statelessReader.TryGetMessage();

            var transportContext = new MessageTransportContext(
                msg.Message.TransportMessage
                , msg.Message.Unpacked
                ,msg.Message.QueueName);
            _statelessReader.AckMessage(transportContext);
            var msg2 = _statelessReader.TryGetMessage();

            Assert.AreEqual(GetEnvelopeResultState.Empty, msg2.State);
        }

        [Test]
        public void when_reader_ack_messages()
        {
            _writer.PutMessage(Encoding.UTF8.GetBytes("message"));
            var msg = _statelessReader.TryGetMessage();

            var transportContext = new MessageTransportContext(
                msg.Message.TransportMessage
                , msg.Message.Unpacked
                , msg.Message.QueueName);

            _reader.AckMessage(transportContext);
            var msg2 = _statelessReader.TryGetMessage();

            Assert.AreEqual(GetEnvelopeResultState.Empty, msg2.State);
        }

        [Test]
        public void when_reader_take_messages()
        {
            _writer.PutMessage(Encoding.UTF8.GetBytes("message"));
            MessageTransportContext msg;
            var cancellationToken = new CancellationToken(false);
            var result = _reader.TakeMessage(cancellationToken, out msg);
            
            Assert.IsTrue(result);
            Assert.AreEqual("message", Encoding.UTF8.GetString(msg.Unpacked));
            Assert.AreEqual("azure-read-write-message", msg.QueueName);
        }
    }
}