using Lokad.Cqrs;
using NUnit.Framework;

namespace Cqrs.Portable.Tests.Envelope
{
    public class EnvelopeQuarantineTest
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
            IEnvelopeStreamer streamer = new TestEnvelopeStreamer();
        }
    }
}