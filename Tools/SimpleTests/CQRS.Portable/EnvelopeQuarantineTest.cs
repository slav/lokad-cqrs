using Lokad.Cqrs;
using Lokad.Cqrs.Envelope;
using NUnit.Framework;
using SaaS.Wires;

namespace Sample.CQRS.Portable
{
    public class EnvelopeQuarantineTest
    {
        [SetUp]
        public void SetUp()
        {
            var serializer = new TestMessageSerializer(new[] { typeof(SerializerTest1), typeof(SerializerTest2), });
            IEnvelopeStreamer streamer = new EnvelopeStreamer(serializer);
            //TypedMessageSender writer = 
            //, IStreamContainer root
        }
    }
}