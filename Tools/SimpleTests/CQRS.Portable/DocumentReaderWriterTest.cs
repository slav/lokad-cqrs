using System;
using System.Collections.Concurrent;
using System.IO;
using Lokad.Cqrs.AtomicStorage;
using NUnit.Framework;
using SaaS.Wires;

namespace Sample.CQRS.Portable
{
    public class FileDocumentReaderWriterTest : DocumentReaderWriterTest
    {
        [SetUp]
        public void Setup()
        {
            var tmpPath = Path.GetTempPath();
            var documentStrategy = new DocumentStrategy();
            _reader = new FileDocumentReaderWriter<Guid, int>(tmpPath, documentStrategy);
            _writer = new FileDocumentReaderWriter<Guid, int>(tmpPath, documentStrategy);
        }
    }

    public class MemoryDocumentReaderWriterTest : DocumentReaderWriterTest
    {
        [SetUp]
        public void Setup()
        {
            var documentStrategy = new DocumentStrategy();
            var concurrentDictionary = new ConcurrentDictionary<string, byte[]>();
            _reader = new MemoryDocumentReaderWriter<Guid, int>(documentStrategy, concurrentDictionary);
            _writer = new MemoryDocumentReaderWriter<Guid, int>(documentStrategy, concurrentDictionary);
        }
    }

    public abstract class DocumentReaderWriterTest
    {
        public IDocumentReader<Guid, int> _reader;
        public IDocumentWriter<Guid, int> _writer;
        
        [Test]
        public void get_not_created_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            int entity;

            //WHEN
            Assert.AreEqual(false, _reader.TryGet(key, out entity));
        }

        [Test]
        public void deleted_not_created_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();

            //WHEN
            Assert.AreEqual(false, _writer.TryDelete(key));
        }

        [Test]
        public void created_new_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();

            //WHEN
            Assert.AreEqual(1, _writer.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist));
        }

        [Test]
        public void update_exist_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            _writer.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist);

            //WHEN
            Assert.AreEqual(5, _writer.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist));
        }

        [Test]
        public void get_new_created_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            _writer.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist);

            //WHEN
            int result;
            Assert.AreEqual(true, _reader.TryGet(key, out result));
            Assert.AreEqual(1, result);
        }

        [Test]
        public void get_updated_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            _writer.AddOrUpdate(key, () => 1, i => 2, AddOrUpdateHint.ProbablyDoesNotExist);
            _writer.AddOrUpdate(key, () => 3, i => 4, AddOrUpdateHint.ProbablyDoesNotExist);

            //WHEN
            int result;
            Assert.AreEqual(true, _reader.TryGet(key, out result));
            Assert.AreEqual(4, result);
        } 
    }
}