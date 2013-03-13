using System.Linq;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;

namespace Cqrs.Portable.Tests.TapeStorage.LockingInMemoryCacheTests
{
    [TestFixture]
    public sealed class when_enumerating_entire_store : LockingInMemoryHelpers
    {
        [Test]
        public void given_empty_store_with_full_range()
        {
            var cache = new LockingInMemoryCache();

            CollectionAssert.IsEmpty(cache.ReadRecords(0, int.MaxValue));
        }

        [Test]
        public void given_reloaded_store_and_non_matching_range()
        {
            var cache = new LockingInMemoryCache();
            cache.ReloadEverything(CreateFrames("stream1", "stream2"));
            CollectionAssert.IsEmpty(cache.ReadRecords(2, 10));
        }

        [Test]
        public void given_reloaded_store_and_intersecting_range()
        {
            var cache = new LockingInMemoryCache();
            cache.ReloadEverything(CreateFrames("stream1", "stream2","stream3"));
            var dataWithKeys = cache.ReadRecords(1, 1).ToArray();
            DataAssert.AreEqual(new[] { CreateKey(2, 1, "stream2") }, dataWithKeys);
        }


        [Test]
        public void given_reloaded_store_and_matching_range()
        {
            var cache = new LockingInMemoryCache();
            cache.ReloadEverything(CreateFrames("stream1", "stream2", "stream1"));
            var dataWithKeys = cache.ReadRecords(0, 3).ToArray();
            DataAssert.AreEqual(new[]
                {
                    CreateKey(1, 1, "stream1"),
                    CreateKey(2, 1, "stream2"),
                    CreateKey(3, 2, "stream1")
                }, dataWithKeys);
        }


        [Test]
        public void given_reloaded_and_appended_store_and_matching_range()
        {
            var cache = new LockingInMemoryCache();

            
            cache.ReloadEverything(CreateFrames("stream1", "stream2"));
            var frame = GetEventBytes(3);
            cache.ConcurrentAppend("stream2", frame, (version, storeVersion) => { });

            var dataWithKeys = cache.ReadRecords(0, 3);
            DataAssert.AreEqual(new[]
                {
                    CreateKey(1,1,"stream1"),
                    CreateKey(2,1,"stream2"),
                    CreateKey(3,2,"stream2"), 
                }, dataWithKeys);
        }


    }
}