using System;
using System.IO;
using System.Linq;
using System.Text;
using Lokad.Cqrs;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;

namespace Cqrs.Portable.Tests.TapeStorage
{
    public class FileAppendOnlyStoreTest
    {
        private readonly string _storePath = Path.Combine(Path.GetTempPath(), "Lokad-CQRS");
        private const int DataFileCount = 10;
        private const int FileMessagesCount = 5;


        void CreateCacheFiles()
        {
            const string msg = "test messages";
            Directory.CreateDirectory(_storePath);
            for (int index = 0; index < DataFileCount; index++)
            {
                using (var stream = new FileStream(Path.Combine(_storePath, index + ".dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    for (int i = 0; i < FileMessagesCount; i++)
                    {
                        StorageFramesEvil.WriteFrame("test-key" + index, i, Encoding.UTF8.GetBytes(msg + i), stream);
                    }
                }
            }
        }

        [Test]
        public void load_cache()
        {
            CreateCacheFiles();
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.LoadCaches();

                for (int j = 0; j < DataFileCount; j++)
                {
                    var key = "test-key" + j;
                    var data = store.ReadRecords(key, -1, Int32.MaxValue);

                    int i = 0;
                    foreach (var dataWithKey in data)
                    {
                        Assert.AreEqual("test messages" + i, Encoding.UTF8.GetString(dataWithKey.Data));
                        i++;
                    }
                    Assert.AreEqual(FileMessagesCount, i);
                }
            }
        }

        [Test]
        public void load_cache_when_exist_empty_file()
        {
            var currentPath = Path.Combine(_storePath, "EmptyCache");
            Directory.CreateDirectory(currentPath);
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(currentPath)))
            {
                //write frame
                using (var stream = new FileStream(Path.Combine(currentPath, "0.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    StorageFramesEvil.WriteFrame("test-key", 0, Encoding.UTF8.GetBytes("test message"), stream);

                //create empty file
                using (var sw = new StreamWriter(Path.Combine(currentPath, "1.dat")))
                    sw.Write("");

                store.LoadCaches();
                var data = store.ReadRecords(0, Int32.MaxValue).ToArray();


                Assert.AreEqual(1, data.Length);
                Assert.AreEqual("test-key", data[0].Key);
                Assert.AreEqual(0, data[0].StreamVersion);
                Assert.AreEqual("test message", Encoding.UTF8.GetString(data[0].Data));
                Assert.IsFalse(File.Exists(Path.Combine(currentPath, "1.dat")));
            }
        }

        [Test]
        public void load_cache_when_incorrect_data_file()
        {
            var currentPath = Path.Combine(_storePath, "EmptyCache");
            Directory.CreateDirectory(currentPath);
            var store = new FileAppendOnlyStore(new DirectoryInfo(currentPath));

            //write frame
            using (var stream = new FileStream(Path.Combine(currentPath, "0.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                StorageFramesEvil.WriteFrame("test-key", 0, Encoding.UTF8.GetBytes("test message"), stream);

            //write incorrect frame
            using (var sw = new StreamWriter(Path.Combine(currentPath, "1.dat")))
                sw.Write("incorrect frame data");

            store.LoadCaches();
            var data = store.ReadRecords(0, Int32.MaxValue).ToArray();


            Assert.AreEqual(1, data.Length);
            Assert.AreEqual("test-key", data[0].Key);
            Assert.AreEqual(0, data[0].StreamVersion);
            Assert.AreEqual("test message", Encoding.UTF8.GetString(data[0].Data));
            Assert.IsTrue(File.Exists(Path.Combine(currentPath, "1.dat")));
        }

        [Test]
        public void append_data()
        {
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();
                const int messagesCount = 3;
                for (int i = 0; i < messagesCount; i++)
                {
                    store.Append("stream1", Encoding.UTF8.GetBytes("test message" + i));
                }

                var data = store.ReadRecords("stream1", currentVersion, Int32.MaxValue).ToArray();

                for (int i = 0; i < messagesCount; i++)
                {
                    Assert.AreEqual("test message" + i, Encoding.UTF8.GetString(data[i].Data));
                }

                Assert.AreEqual(messagesCount, data.Length);
            }
        }

        [Test, ExpectedException(typeof(AppendOnlyStoreConcurrencyException))]
        public void append_data_when_set_version_where_does_not_correspond_real_version()
        {
            var key = Guid.NewGuid().ToString();

            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                store.Append(key, Encoding.UTF8.GetBytes("test message1"), 100);
            }
        }

        [Test]
        public void get_current_version()
        {
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();
                store.Append("versiontest", Encoding.UTF8.GetBytes("test message1"));
                store.Append("versiontest", Encoding.UTF8.GetBytes("test message2"));
                store.Append("versiontest", Encoding.UTF8.GetBytes("test message3"));

                Assert.AreEqual(currentVersion + 3, store.GetCurrentVersion());
            }
        }

        [Test]
        public void read_all_records_by_stream()
        {
            var stream = Guid.NewGuid().ToString();

            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                for (int i = 0; i < 2; i++)
                    store.Append(stream, Encoding.UTF8.GetBytes("test message" + i));

                var records = store.ReadRecords(stream, -1, Int32.MaxValue).ToArray();

                Assert.AreEqual(2, records.Length);

                for (int i = 0; i < 2; i++)
                {
                    Assert.AreEqual("test message" + i, Encoding.UTF8.GetString(records[i].Data));
                    Assert.AreEqual(i + 1, records[i].StreamVersion);
                }
            }
        }

        [Test]
        public void read_records_by_stream_after_version()
        {
            var stream = Guid.NewGuid().ToString();
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();

                for (int i = 0; i < 2; i++)
                    store.Append(stream, Encoding.UTF8.GetBytes("test message" + i));

                var records = store.ReadRecords(stream, currentVersion + 1, Int32.MaxValue).ToArray();

                Assert.AreEqual(1, records.Length);
                Assert.AreEqual("test message1", Encoding.UTF8.GetString(records[0].Data));
                Assert.AreEqual(2, records[0].StreamVersion);
            }
        }

        [Test]
        public void read_store_all_records()
        {
            var stream = Guid.NewGuid().ToString();
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();
                for (int i = 0; i < 2; i++)
                    store.Append(stream, Encoding.UTF8.GetBytes("test message" + i));

                var records = store.ReadRecords(-1, Int32.MaxValue).ToArray();

                Assert.AreEqual(currentVersion + 2, records.Length);

                for (var i = currentVersion; i < currentVersion + 2; i++)
                {
                    Assert.AreEqual("test message" + (i - currentVersion), Encoding.UTF8.GetString(records[i].Data));
                    Assert.AreEqual(i - currentVersion + 1, records[i].StreamVersion);
                    Assert.AreEqual(i+1, records[i].StoreVersion);
                }
            }
        }

        [Test]
        public void read_store_records_after_version()
        {
            var stream = Guid.NewGuid().ToString();
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();
                for (int i = 0; i < 2; i++)
                    store.Append(stream, Encoding.UTF8.GetBytes("test message" + i));

                var records = store.ReadRecords(currentVersion+1, Int32.MaxValue).ToArray();

                Assert.AreEqual(1, records.Length);
                Assert.AreEqual("test message1" , Encoding.UTF8.GetString(records[0].Data));
                Assert.AreEqual(2, records[0].StreamVersion);
                Assert.AreEqual(currentVersion+2, records[0].StoreVersion);
            }
        }
    }
}