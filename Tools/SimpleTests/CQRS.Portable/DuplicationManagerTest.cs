using System.Threading;
using Lokad.Cqrs.Envelope;
using NUnit.Framework;

namespace Sample.CQRS.Portable
{
    public class DuplicationManagerTest
    {
        [Test]
        public void when_get()
        {
            var manager = new DuplicationManager();
            var memory1 = manager.GetOrAdd("dispatcher");
            var memory2 = manager.GetOrAdd("dispatcher");

            Assert.AreEqual(memory1, memory2);
        }
    }
}