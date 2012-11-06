using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lokad.Cqrs.AtomicStorage;
using NUnit.Framework;
using SaaS.Wires;

namespace Sample.CQRS.Portable
{
    public class MemoryDocumentStoreTest
    {
        private MemoryDocumentStore _store;
        private ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>> _storeDictionary;

        [SetUp]
        public void Setup()
        {
            _storeDictionary = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte[]>>();
            _store = new MemoryDocumentStore(_storeDictionary, new DocumentStrategy());
        }

        [Test]
        public void get_not_created_bucket()
        {
            //GIVEN
            var bucket = Guid.NewGuid().ToString();

            //WHEN
            CollectionAssert.IsEmpty(_store.EnumerateContents(bucket));
        }


        [Test]
        public void write_bucket()
        {
            //GIVEN
            var bucket = "test-bucket";
            var records = new List<DocumentRecord>
                                      {
                                          new DocumentRecord("first", () => Encoding.UTF8.GetBytes("test message 1")),
                                          new DocumentRecord("second", () => Encoding.UTF8.GetBytes("test message 2")),
                                      };
            _store.WriteContents(bucket, records);

            //WHEN
            var actualRecords = _store.EnumerateContents(bucket).ToList();
            Assert.AreEqual(records.Count, actualRecords.Count);
            for (int i = 0; i < records.Count; i++)
            {
                Assert.AreEqual(true, actualRecords.Count(x => x.Key == records[i].Key) == 1);
                Assert.AreEqual(Encoding.UTF8.GetString(records[i].Read()), Encoding.UTF8.GetString(actualRecords.First(x => x.Key == records[i].Key).Read()));
            }
        }

        [Test]
        public void reset_bucket()
        {
            //GIVEN
            var bucket1 = "test-bucket1";
            var bucket2 = "test-bucket2";
            var records = new List<DocumentRecord>
                                      {
                                          new DocumentRecord("first", () => Encoding.UTF8.GetBytes("test message 1")),
                                      };
            _store.WriteContents(bucket1, records);
            _store.WriteContents(bucket2, records);
            _store.Reset(bucket1);

            //WHEN
            var result1 = _store.EnumerateContents(bucket1).ToList();
            var result2 = _store.EnumerateContents(bucket2).ToList();
            CollectionAssert.IsEmpty(result1);
            Assert.AreEqual(records.Count, result2.Count);
        }

        [Test]
        public void reset_all_bucket()
        {
            //GIVEN
            var bucket1 = "test-bucket1";
            var bucket2 = "test-bucket2";
            var records = new List<DocumentRecord>
                                      {
                                          new DocumentRecord("first", () => Encoding.UTF8.GetBytes("test message 1")),
                                      };
            _store.WriteContents(bucket1, records);
            _store.WriteContents(bucket2, records);
            _store.ResetAll();

            //WHEN
            var result1 = _store.EnumerateContents(bucket1).ToList();
            var result2 = _store.EnumerateContents(bucket2).ToList();
            CollectionAssert.IsEmpty(result1);
            CollectionAssert.IsEmpty(result2);
        }
    }
}