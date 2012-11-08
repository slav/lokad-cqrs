using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lokad.Cqrs;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;

namespace Sample.CQRS.Portable
{
    public class FileAppendOnlyStoreTest
    {
        private string _storePath = Path.Combine(Path.GetTempPath(), "Lokad-CQRS", Guid.NewGuid().ToString());
        private FileAppendOnlyStore _store;
        private const int DataFileCount = 10;
        private const int FileMessagesCount = 5;


        void CreateCacheFiles()
        {
            string msg = "test messages";
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

        [SetUp]
        public void SetUp()
        {
            _store = new FileAppendOnlyStore(new DirectoryInfo(_storePath));
        }

        [Test]
        public void load_cache()
        {
            CreateCacheFiles();
            _store.LoadCaches();
            var data = _store.ReadRecords(0, Int32.MaxValue).ToArray();

            Assert.AreEqual(DataFileCount * FileMessagesCount, data.Length);
            int index = 0;
            int i = 0;
            foreach (DataWithKey dataWithKey in data)
            {
                Assert.AreEqual("test-key" + index, dataWithKey.Key);
                Assert.AreEqual(i, dataWithKey.StreamVersion);
                Assert.AreEqual("test messages" + i, Encoding.UTF8.GetString(dataWithKey.Data));

                i++;
                if (i >= FileMessagesCount)
                {
                    i = 0;
                    index++;
                }
            }
        }

        [Test]
        public void load_cache_when_exist_empty_file()
        {
            var currentPath = Path.Combine(_storePath, "EmptyCache");
            Directory.CreateDirectory(currentPath);
            var store = new FileAppendOnlyStore(new DirectoryInfo(currentPath));

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
            _store.Initialize();
            _store.Append("stream1", Encoding.UTF8.GetBytes("test message1"));
            _store.Append("stream1", Encoding.UTF8.GetBytes("test message2"));

            var data = _store.ReadRecords("stream1", -1, 2).ToArray();

            Assert.AreEqual(2, data.Length);
            Assert.AreEqual("test message1", Encoding.UTF8.GetString(data[0].Data));
            Assert.AreEqual("test message2", Encoding.UTF8.GetString(data[1].Data));
        }

        [Test]
        public void append_data_when_set_version_where_does_not_correspond_real_version()
        {
            _store.Initialize();
            _store.Append("stream1", Encoding.UTF8.GetBytes("test message1"),100);

            var data = _store.ReadRecords("stream1", -1, 2).ToArray();
            CollectionAssert.IsEmpty(data);
        }
    }
}