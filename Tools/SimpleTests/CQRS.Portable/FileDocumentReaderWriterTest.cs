using System;
using System.Collections.Generic;
using System.IO;
using Lokad.Cqrs.AtomicStorage;
using NUnit.Framework;
using SaaS;
using SaaS.Wires;

namespace Sample.CQRS.Portable
{
    public class FileDocumentReaderWriterTest
    {
        private FileDocumentReaderWriter<Guid, int> _container;
        [SetUp]
        public void Setup()
        {
            var tmpPath = Path.GetTempPath();
            _container = new FileDocumentReaderWriter<Guid, int>(tmpPath, new DocumentStrategy());
        }

        [Test]
        public void get_not_created_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            int entity;

            //WHEN
            Assert.AreEqual(false, _container.TryGet(key, out entity));
        }

        [Test]
        public void deleted_not_created_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();

            //WHEN
            Assert.AreEqual(false, _container.TryDelete(key));
        }

        [Test]
        public void created_new_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();

            //WHEN
            Assert.AreEqual(1, _container.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist));
        }

        [Test]
        public void update_exist_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            _container.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist);

            //WHEN
            Assert.AreEqual(5, _container.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist));
        }

        [Test]
        public void get_new_created_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            _container.AddOrUpdate(key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist);

            //WHEN
            int result;
            Assert.AreEqual(true, _container.TryGet(key, out result));
            Assert.AreEqual(1, result);
        }

        [Test]
        public void get_updated_entity()
        {
            //GIVEN
            var key = Guid.NewGuid();
            _container.AddOrUpdate(key, () => 1, i => 2, AddOrUpdateHint.ProbablyDoesNotExist);
            _container.AddOrUpdate(key, () => 3, i => 4, AddOrUpdateHint.ProbablyDoesNotExist);

            //WHEN
            int result;
            Assert.AreEqual(true, _container.TryGet(key, out result));
            Assert.AreEqual(4, result);
        }
    }
}