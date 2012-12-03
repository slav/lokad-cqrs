using System;
using System.IO;
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
        IMessageSerializer _serializer;
        private MessageStore _store;
        private MessageSender _sender;
        private NuclearStorage _nuclearStorage;

        [SetUp]
        public void SetUp()
        {
            _serializer = new TestMessageSerializer(new[] { typeof(SerializerTest1), typeof(SerializerTest2), typeof(string) });
            _path = Path.Combine(Path.GetTempPath(), "MessageStorePublisher", Guid.NewGuid().ToString());
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);
            _appendOnlyStore = new FileAppendOnlyStore(new DirectoryInfo(_path));

            _store = new MessageStore(_appendOnlyStore, _serializer);
            var streamer = new EnvelopeStreamer(_serializer);
            var queueWriter = new TestQueueWriter();
            _sender = new MessageSender(streamer, queueWriter);
            var store = new FileDocumentStore(Path.Combine(_path, "lokad-cqrs-test"), new DocumentStrategy());
            _nuclearStorage = new NuclearStorage(store);
        }

        static bool DoWePublishThisRecord(StoreRecord storeRecord)
        {
            return storeRecord.Key != "audit";
        }

        public void when_publish_events()
        {
            //var publicher = new MessageStorePublisher(_store, _sender, _nuclearStorage, DoWePublishThisRecord);
            //publicher.
        }
    }
}