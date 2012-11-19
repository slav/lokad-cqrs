using System;
using System.IO;
using Lokad.Cqrs;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Partition;
using NUnit.Framework;
using SaaS;
using SaaS.Wires;

namespace Sample.CQRS.Portable
{
    public class TypedMessageSenderTest
    {
        private MessageSender _commandRouter;
        private MessageSender _functionalRecorder;
        private TypedMessageSender _typedMessageSender;
        private TestEnvelopeStreamer _commandEnvelopeStreamer, _functionalEnvelopeStreamer;
        private TestQueueWriter _commandQueueWriter;
        private TestQueueWriter _functionalQueueWriter;

        [SetUp]
        public void SetUp()
        {
            _commandEnvelopeStreamer = new TestEnvelopeStreamer(new byte[] { 1, 2, 3 });
            _commandQueueWriter = new TestQueueWriter();
            _commandRouter = new MessageSender(_commandEnvelopeStreamer, _commandQueueWriter);
            _functionalEnvelopeStreamer = new TestEnvelopeStreamer(new byte[] { 4, 5, 6 });
            _functionalQueueWriter = new TestQueueWriter();
            _functionalRecorder = new MessageSender(_functionalEnvelopeStreamer, _functionalQueueWriter);
            _typedMessageSender = new TypedMessageSender(_commandRouter, _functionalRecorder);
        }

        [Test]
        public void when_send_command()
        {
            var testCommand = new TestCommand();
            _typedMessageSender.SendCommand(testCommand);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _commandQueueWriter.Envelope);
            Assert.AreEqual(testCommand, _commandEnvelopeStreamer.Envelope.Message);
            CollectionAssert.IsEmpty(_commandEnvelopeStreamer.Envelope.Attributes);
            Assert.IsNull(_functionalQueueWriter.Envelope);
        }

        [Test]
        public void when_hashed_send_command()
        {
            var testCommand = new TestCommand();
            _typedMessageSender.SendCommand(testCommand, true);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _commandQueueWriter.Envelope);
            Assert.AreEqual(testCommand, _commandEnvelopeStreamer.Envelope.Message);
            CollectionAssert.IsEmpty(_commandEnvelopeStreamer.Envelope.Attributes);
            Assert.IsNull(_functionalQueueWriter.Envelope);
        }

        [Test]
        public void when_send_from_client()
        {
            var testCommand = new TestCommand();
            var messageAttributes = new[] { new MessageAttribute("key", "value"), };
            _typedMessageSender.SendFromClient(testCommand, "ID", messageAttributes);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _commandQueueWriter.Envelope);
            Assert.AreEqual(testCommand, _commandEnvelopeStreamer.Envelope.Message);
            CollectionAssert.AreEquivalent(messageAttributes, _commandEnvelopeStreamer.Envelope.Attributes);
            Assert.AreEqual("ID", _commandEnvelopeStreamer.Envelope.EnvelopeId);
            Assert.IsNull(_functionalQueueWriter.Envelope);
        }

        [Test]
        public void when_publish_from_client_hashed()
        {
            var testFuncEvent = new TestFuncEvent();
            var messageAttributes = new[] { new MessageAttribute("key", "value"), };
            _typedMessageSender.PublishFromClientHashed(testFuncEvent, messageAttributes);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _functionalQueueWriter.Envelope);
            Assert.AreEqual(testFuncEvent, _functionalEnvelopeStreamer.Envelope.Message);
            //CollectionAssert.AreEquivalent(messageAttributes, _functionalEnvelopeStreamer.Envelope.Attributes);
            Assert.IsNull(_commandQueueWriter.Envelope);
        }

        [Test]
        public void when_publish_event()
        {
            var testFuncEvent = new TestFuncEvent();
            _typedMessageSender.Publish(testFuncEvent);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _functionalQueueWriter.Envelope);
            Assert.AreEqual(testFuncEvent, _functionalEnvelopeStreamer.Envelope.Message);
            Assert.IsNull(_commandQueueWriter.Envelope);
        }

        [Test]
        public void when_send_func_command()
        {
            var testFuncCommand = new TestFuncCommand();
            _typedMessageSender.Send(testFuncCommand);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _commandQueueWriter.Envelope);
            Assert.AreEqual(testFuncCommand, _commandEnvelopeStreamer.Envelope.Message);
            CollectionAssert.IsEmpty(_commandEnvelopeStreamer.Envelope.Attributes);
            Assert.IsNull(_functionalQueueWriter.Envelope);
        }

        [Test]
        public void when_send_hashed_func_command()
        {
            var testFuncCommand = new TestFuncCommand();
            _typedMessageSender.SendHashed(testFuncCommand);

            CollectionAssert.AreEquivalent(new byte[] { 1, 2, 3 }, _commandQueueWriter.Envelope);
            Assert.AreEqual(testFuncCommand, _commandEnvelopeStreamer.Envelope.Message);
            CollectionAssert.IsEmpty(_commandEnvelopeStreamer.Envelope.Attributes);
            Assert.IsNull(_functionalQueueWriter.Envelope);
        }
    }

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
            return new ImmutableEnvelope("EnvId", DateTime.UtcNow, "Test meesage", new[] { new MessageAttribute("key", "value"), });
        }
    }

    public class TestQueueWriter : IQueueWriter
    {
        public byte[] Envelope { get; private set; }

        public string Name { get { return "TestQueueWriter"; } }
        public void PutMessage(byte[] envelope)
        {
            Envelope = envelope;
        }
    }

    public class TestCommand : ICommand
    {}

    public class TestFuncCommand : IFuncCommand
    { }

    public class TestFuncEvent : IFuncEvent
    {}
}