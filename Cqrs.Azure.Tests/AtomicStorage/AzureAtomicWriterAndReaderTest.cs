#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Runtime.Serialization;
using Lokad.Cqrs.AppendOnly;
using Lokad.Cqrs.AtomicStorage;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;
using SaaS.Wires;

namespace Cqrs.Azure.Tests.AtomicStorage
{
    public class AzureAtomicWriterAndReaderTest
    {
        IDocumentStore _store;
        private AzureAtomicWriter<Guid, TestView> _writer;
        private DocumentStrategy _documentStrategy;
        private AzureAtomicReader<Guid, TestView> _reader;

        [SetUp]
        public void Setup()
        {
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            var blobCLient = cloudStorageAccount.CreateCloudBlobClient();

            _documentStrategy = new DocumentStrategy();
            _store = new AzureDocumentStore(_documentStrategy, blobCLient);
            _writer = new AzureAtomicWriter<Guid, TestView>(blobCLient, _documentStrategy);
            _reader = new AzureAtomicReader<Guid, TestView>(blobCLient, _documentStrategy);
        }

        [TearDown]
        public void Teardown()
        {
            var bucket = _documentStrategy.GetEntityBucket<TestView>();
            _store.Reset(bucket);
        }

        [Test]
        public void when_delete_than_not_key()
        {
            Assert.IsFalse(_writer.TryDelete(Guid.NewGuid()));
        }

        [Test]
        public void when_delete_than_exist_key()
        {
            _writer.InitializeIfNeeded();
            var id = Guid.NewGuid();
            _writer.AddOrUpdate(id, () => new TestView(id)
                , old =>
                    {
                        old.Data++;
                        return old;
                    }
                , AddOrUpdateHint.ProbablyExists);

            Assert.IsTrue(_writer.TryDelete(id));
        }

        [Test]
        public void when_write_read()
        {
            var id = Guid.NewGuid();
            _writer.AddOrUpdate(id, () => new TestView(id)
                , old =>
                    {
                        old.Data++;
                        return old;
                    }
                , AddOrUpdateHint.ProbablyExists);

            TestView entity;
            var result = _reader.TryGet(id, out entity);

            Assert.IsTrue(result);
            Assert.AreEqual(id, entity.Id);
            Assert.AreEqual(0, entity.Data);
        }

        [Test]
        public void when_read_nothing_key()
        {
            var id = Guid.NewGuid();

            TestView entity;
            var result = _reader.TryGet(id, out entity);

            Assert.IsFalse(result);
        }

        [Test]
        public void when_write_exist_key_and_read()
        {
            var id = Guid.NewGuid();
            _writer.AddOrUpdate(id, () => new TestView(id)
                , old =>
                {
                    old.Data++;
                    return old;
                }
                , AddOrUpdateHint.ProbablyExists);
            _writer.AddOrUpdate(id, () => new TestView(id)
                , old =>
                {
                    old.Data++;
                    return old;
                }
                , AddOrUpdateHint.ProbablyExists);

            TestView entity;
            var result = _reader.TryGet(id, out entity);

            Assert.IsTrue(result);
            Assert.AreEqual(id, entity.Id);
            Assert.AreEqual(1, entity.Data);
        }
    }

    [DataContract(Name = "test-view")]
    public class TestView
    {
        [DataMember(Order = 1)]
        public Guid Id { get; set; }
        [DataMember(Order = 2)]
        public int Data { get; set; }

        public TestView()
        { }

        public TestView(Guid id)
        {
            Id = id;
            Data = 0;
        }
    }
}