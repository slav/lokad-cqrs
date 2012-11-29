#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Linq;
using System.Text;
using Lokad.Cqrs.AppendOnly;
using Lokad.Cqrs.TapeStorage;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.AppendOnly
{
    public class BlobAppendOnlyStoreTest
    {
        string _path;
        private const int DataFileCount = 10;
        private const int FileMessagesCount = 5;
        private BlobAppendOnlyStore _appendOnly;

        [SetUp]
        public void Setup()
        {
            _path = Guid.NewGuid().ToString().ToLowerInvariant();
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var blobCLient = cloudStorageAccount.CreateCloudBlobClient();
            var blobContainer = blobCLient.GetContainerReference(_path);
            _appendOnly = new BlobAppendOnlyStore(blobContainer);
            _appendOnly.InitializeWriter();
        }

        [TearDown]
        public void Teardown()
        {
            _appendOnly.Close();
        }

        [Test]
        public void when_append_and_read()
        {
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message1"));
            _appendOnly.Append("stream2", Encoding.UTF8.GetBytes("test message2"));
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message3"));

            var recordsSteam1 = _appendOnly.ReadRecords("stream1", 0, Int32.MaxValue).ToArray();
            var recordsSteam2 = _appendOnly.ReadRecords("stream2", 0, Int32.MaxValue).ToArray();

            Assert.AreEqual(2, recordsSteam1.Length);
            Assert.AreEqual(1, recordsSteam2.Length);
            Assert.AreEqual("test message1", Encoding.UTF8.GetString(recordsSteam1[0].Data));
            Assert.AreEqual("test message3", Encoding.UTF8.GetString(recordsSteam1[1].Data));
            Assert.AreEqual("test message2", Encoding.UTF8.GetString(recordsSteam2[0].Data));
        }

        [Test]
        public void when_read_after_version()
        {
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message1"));
            _appendOnly.Append("stream2", Encoding.UTF8.GetBytes("test message2"));
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message3"));

            var recordsSteam1 = _appendOnly.ReadRecords("stream1", 1, Int32.MaxValue).ToArray();

            Assert.AreEqual(1, recordsSteam1.Length);
            Assert.AreEqual("test message3", Encoding.UTF8.GetString(recordsSteam1[0].Data));
        }

        [Test]
        public void when_read_than_set_max_records()
        {
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message1"));
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message2"));
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message3"));

            var recordsSteam1 = _appendOnly.ReadRecords("stream1", 0, 2).ToArray();

            Assert.AreEqual(2, recordsSteam1.Length);
            Assert.AreEqual("test message1", Encoding.UTF8.GetString(recordsSteam1[0].Data));
            Assert.AreEqual("test message2", Encoding.UTF8.GetString(recordsSteam1[1].Data));
        }

        [Test]
        public void when_reads_record()
        {
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message1"));
            _appendOnly.Append("stream2", Encoding.UTF8.GetBytes("test message2"));
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message3"));

            var recordsSteam = _appendOnly.ReadRecords(0, Int32.MaxValue).ToArray();

            Assert.AreEqual(3, recordsSteam.Length);
            Assert.AreEqual(1, recordsSteam[0].StoreVersion);
            Assert.AreEqual(2, recordsSteam[1].StoreVersion);
            Assert.AreEqual(3, recordsSteam[2].StoreVersion);
            Assert.AreEqual(1, recordsSteam[0].StreamVersion);
            Assert.AreEqual(1, recordsSteam[1].StreamVersion);
            Assert.AreEqual(2, recordsSteam[2].StreamVersion);
            Assert.AreEqual("test message1", Encoding.UTF8.GetString(recordsSteam[0].Data));
            Assert.AreEqual("test message2", Encoding.UTF8.GetString(recordsSteam[1].Data));
            Assert.AreEqual("test message3", Encoding.UTF8.GetString(recordsSteam[2].Data));
        }

        [Test, ExpectedException(typeof(AppendOnlyStoreConcurrencyException))]
        public void append_data_when_set_version_where_does_not_correspond_real_version()
        {
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message1"), 100);
        }

        [Test]
        public void get_current_version()
        {
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message1"));
            _appendOnly.Append("stream2", Encoding.UTF8.GetBytes("test message2"));
            _appendOnly.Append("stream1", Encoding.UTF8.GetBytes("test message3"));

            Assert.AreEqual(3, _appendOnly.GetCurrentVersion());
        }

        void CreateCacheFiles()
        {
            const string msg = "test messages";
            for (int index = 0; index < DataFileCount; index++)
            {
                for (int i = 0; i < FileMessagesCount; i++)
                {
                    _appendOnly.Append("test-key" + index, Encoding.UTF8.GetBytes(msg + i));
                }
            }
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var blobCLient = cloudStorageAccount.CreateCloudBlobClient();
            var blobContainer = blobCLient.GetContainerReference(_path);
            _appendOnly = new BlobAppendOnlyStore(blobContainer);
            _appendOnly.InitializeWriter();
        }

        [Test]
        public void load_cache()
        {
            CreateCacheFiles();
            for (int j = 0; j < DataFileCount; j++)
            {
                var key = "test-key" + j;
                var data = _appendOnly.ReadRecords(key, -1, Int32.MaxValue).ToArray();
                Assert.AreEqual(FileMessagesCount, data.Length);
                int i = 0;
                foreach (var dataWithKey in data)
                {
                    Assert.AreEqual("test messages" + i, Encoding.UTF8.GetString(dataWithKey.Data));
                    i++;
                }
            }
        }
    }
}