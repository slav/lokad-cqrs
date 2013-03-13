using System.IO;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;

namespace Cqrs.Portable.Tests.TapeStorage.LockingInMemoryCacheTests
{
    [TestFixture]
    public sealed class when_doing_concurrent_append : LockingInMemoryHelpers
    {
        [Test]
        public void given_empty_cache_and_valid_commit_function()
        {
            // TODO: fill this
        }

        [Test]
        public void given_reloaded_cache_and_commit_function_that_fails()
        {
            var cache = new LockingInMemoryCache();

            cache.ReloadEverything(CreateFrames("stream", "otherStream"));

            Assert.Throws<FileNotFoundException>(
                () => cache.ConcurrentAppend("stream", new byte[1], (version, storeVersion) =>
                    {
                        throw new FileNotFoundException();
                    }));

            Assert.AreEqual(2, cache.StoreVersion);
        }

        [Test]
        public void given_reloaded_cache_and_non_specified_version_expectation()
        {
            // TODO: fill this
        }

        [Test]
        public void given_reloaded_and_appended_cache_with_specified_version_expectation()
        {
            
        }

       


        [Test]
        public void given_reloaded_cache_and_non_matching_expected_version()
        {
            var cache = new LockingInMemoryCache();

            cache.ReloadEverything(CreateFrames("stream", "otherStream"));

            bool commitWasCalled = false;

            Assert.Throws<AppendOnlyStoreConcurrencyException>(
                () =>
                    cache.ConcurrentAppend("stream", new byte[1],
                        (streamVersion, storeVersion) => commitWasCalled = true, 2));

            Assert.IsFalse(commitWasCalled, "commit should not be called");

        }

        [Test]
        public void given_empty_cache_and_matching_version_expectation()
        {
            var cache = new LockingInMemoryCache();
            long? commitStoreVersion = null;
            long? commitStreamVersion = null;

            cache.ConcurrentAppend("stream", new byte[1],(version, storeVersion) =>
                {
                    commitStoreVersion = 1;
                    commitStreamVersion = 1;
                },0 );
            Assert.AreEqual(1, commitStoreVersion, "commitStoreVersion");
            Assert.AreEqual(1, commitStreamVersion, "commitStreamVersion");
        }

        [Test]
        public void given_reloaded_cache_and_matching_stream_version()
        {
            var cache = new LockingInMemoryCache();

            cache.ReloadEverything(CreateFrames("stream", "otherStream"));

            long? commitStoreVersion = null;
            long? commitStreamVersion = null;

            cache.ConcurrentAppend("stream", new byte[1], (streamVersion, storeVersion) =>
                {
                    commitStoreVersion = storeVersion;
                    commitStreamVersion = streamVersion;
                }, 1 );

            Assert.AreEqual(3, commitStoreVersion, "commitStoreVersion");
            Assert.AreEqual(2, commitStreamVersion, "commitStreamVersion");
        }





    }
}