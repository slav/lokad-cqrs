using System;
using System.IO;
using Lokad.Cqrs;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Partition;
using NUnit.Framework;

namespace Sample.CQRS.Portable
{
    public class MessageSenderTest
    {
        private string _path;
        [SetUp]
        public void Setup()
        {
            _path = Path.Combine(Path.GetTempPath(), "MessageSender", Guid.NewGuid().ToString());
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_path))
                Directory.Delete(_path, true);
        }

        [Test]
        public void when_send_message()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(SerializerTest1), typeof(SerializerTest2), });
            var streamer = new EnvelopeStreamer(serializer);
            var queueWriter = new FileQueueWriter(new DirectoryInfo(_path), "test");
            var sender = new MessageSender(streamer, queueWriter);
            sender.Send(new SerializerTest1("Name1"), "EnvId", new[] { new MessageAttribute("key1", "val1"), new MessageAttribute("key2", "val2"), });
            sender.Send(new SerializerTest1("Name1"), "EnvId");

            Assert.AreEqual(2, Directory.GetFiles(_path).Length);
        }
    }
}