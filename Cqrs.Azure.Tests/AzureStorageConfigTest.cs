#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Linq;
using System.Text;
using System.Threading;
using Lokad.Cqrs;
using Lokad.Cqrs.Build;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Partition;
using Microsoft.WindowsAzure;
using NUnit.Framework;

namespace Cqrs.Azure.Tests
{
    //public class AzureStorageConfigTest
    //{
    //    private IAzureStorageConfig _azureDevStorageConfig;
    //    private IAzureStorageConfig _azureStorageConfig;

    //    [SetUp]
    //    public void SetUp()
    //    {
    //        CloudStorageAccount account = ConnectionConfig.StorageAccount;
    //        _azureDevStorageConfig = AzureStorage.CreateConfigurationForDev();
    //        _azureStorageConfig = AzureStorage.CreateConfig(account);
    //    }

    //    [TearDown]
    //    public void Teardown()
    //    {
            
    //    }

    //    [Test]
    //    public void when_dev_account_name()
    //    {
    //        Assert.AreEqual("azure-dev", _azureDevStorageConfig.AccountName);
    //    }

    //    [Test]
    //    public void when_account_name()
    //    {
    //        Assert.AreEqual("devstoreaccount1", _azureStorageConfig.AccountName);
    //    }

    //    [Test]
    //    public void when_create_queue_client()
    //    {

    //    }

    //    [Test]
    //    public void when_create_queue_writer_reader()
    //    {
    //        var writer = _azureStorageConfig.CreateQueueWriter("queue-name");
    //        var reader = _azureStorageConfig.CreateQueueReader("queue-name");
    //        writer.Init();
    //        writer.PutMessage(Encoding.UTF8.GetBytes("test"));

    //        reader.InitIfNeeded();
    //        MessageTransportContext context;
    //        reader.TakeMessage(new CancellationToken(false), out context);

    //        Assert.AreEqual("queue-name", context.QueueName);
    //        Assert.AreEqual("test", Encoding.UTF8.GetString(context.Unpacked));
    //    }

    //    [Test]
    //    public void when_create_append_only_store()
    //    {
    //        var appendOnlyStore = _azureStorageConfig.CreateAppendOnlyStore("append-only");
    //        var version = appendOnlyStore.GetCurrentVersion();
    //        appendOnlyStore.Append("stream1", Encoding.UTF8.GetBytes("test"));

    //        var records = appendOnlyStore.ReadRecords("stream1", version, Int32.MaxValue).ToArray();

    //        Assert.AreEqual(1, records.Length);
    //        Assert.AreEqual("test", Encoding.UTF8.GetString(records[0].Data));
    //    }

    //    [Test]
    //    public void when_create_message_sender()
    //    {
    //        var testEnvelopeStreamer = new TestEnvelopeStreamer();
    //        var sender = _azureStorageConfig.CreateMessageSender(testEnvelopeStreamer, "name");
    //        sender.Send("test message");
    //        var reader = _azureStorageConfig.CreateQueueReader("name");
    //        MessageTransportContext context;
    //        reader.TakeMessage(new CancellationToken(false), out context);

    //        Assert.AreEqual("name", context.QueueName);
    //        CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, context.Unpacked);
    //        CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, testEnvelopeStreamer.Buffer);
    //        Assert.AreEqual("test message", testEnvelopeStreamer.Envelope.Message.ToString());
    //    }
    //}

    public class TestEnvelopeStreamer : IEnvelopeStreamer
    {
        public ImmutableEnvelope Envelope { get; private set; }
        public byte[] Buffer { get; set; }

        public TestEnvelopeStreamer()
        { }

        public TestEnvelopeStreamer(byte[] buffer)
        {
            Buffer = buffer;
        }

        public byte[] SaveEnvelopeData(ImmutableEnvelope envelope)
        {
            Envelope = envelope;
            Buffer = new byte[] { 1, 2, 3 };

            return Buffer;
        }

        public ImmutableEnvelope ReadAsEnvelopeData(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            return new ImmutableEnvelope("EnvId", DateTime.UtcNow, "Test meesage", new[] { new MessageAttribute("key", "value"), });
        }
    }
}