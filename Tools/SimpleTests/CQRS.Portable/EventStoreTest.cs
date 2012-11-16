using System;
using System.Collections.ObjectModel;
using System.IO;
using Lokad.Cqrs;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;
using SaaS;
using SaaS.Wires;

namespace Sample.CQRS.Portable
{
    public class EventStoreTest
    {
        private string _path;
        private FileAppendOnlyStore _appendOnlyStore;
        IMessageSerializer _serializer;
        private MessageStore _messageStore;

        [SetUp]
        public void SetUp()
        {
            _serializer = new TestMessageSerializer(new[] { typeof(TestEvent) });
            _path = Path.Combine(Path.GetTempPath(), "MessageStore", Guid.NewGuid().ToString());
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);
            _appendOnlyStore = new FileAppendOnlyStore(new DirectoryInfo(_path));
            _appendOnlyStore.Initialize();

            _messageStore = new MessageStore(_appendOnlyStore, _serializer);
        }

        [Test]
        public void when_append_and_read_event()
        {
            var eventStore = new EventStore(_messageStore);
            var testEvent1 = new TestEvent("Event1");
            var testEvent2 = new TestEvent("Event2");
            eventStore.AppendEventsToStream(null, -1, new[] { testEvent1 });
            eventStore.AppendEventsToStream(null, -1, new[] { testEvent2 });
            eventStore.AppendEventsToStream(null, -1, new[] { testEvent1, testEvent2 });
            var stream = eventStore.LoadEventStream(null);


            Assert.AreEqual(3, stream.StreamVersion);
            Assert.AreEqual(4, stream.Events.Count);
            Assert.AreEqual("Event1", (stream.Events[0] as TestEvent).Name);
            Assert.AreEqual("Event2", (stream.Events[1] as TestEvent).Name);
            Assert.AreEqual("Event1", (stream.Events[2] as TestEvent).Name);
            Assert.AreEqual("Event2", (stream.Events[3] as TestEvent).Name);
        }

        class TestEvent : IEvent
        {
            public string Name { get; set; }

            public TestEvent(string name)
            {
                Name = name;
            }
        }
    }
}