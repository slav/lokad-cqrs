using System;
using System.IO;
using Lokad.Cqrs;
using NUnit.Framework;

namespace Sample.CQRS.Portable
{
    public class FileStorageConfigTest
    {
        private FileStorageConfig _config;
        private string _configPath;
        [SetUp]
        public void Setup()
        {
            var tmpPath = Path.GetTempPath();
            _configPath = Path.Combine(tmpPath, "lokad-cqrs-test", Guid.NewGuid().ToString());
            _config = new FileStorageConfig(new DirectoryInfo(_configPath), "testaccount");
        }

        [Test]
        public void create_config()
        {

            //WHEN
            Assert.AreEqual(_configPath, _config.Folder.FullName);
            Assert.AreEqual("testaccount", _config.AccountName);
        }

        [Test]
        public void create_directory()
        {
            //GIVEN
            if (Directory.Exists(_configPath))
                Directory.Delete(_configPath, true);
            _config.EnsureDirectory();

            //WHEN
            Assert.AreEqual(true, Directory.Exists(_configPath));
        }

        [Test]
        public void wipe_directory()
        {
            //GIVEN
            _config.EnsureDirectory();
            _config.Wipe();

            //WHEN
            Assert.AreEqual(false, Directory.Exists(_configPath));
        }

        [Test]
        public void reset_when_folder_exist()
        {
            //GIVEN
            _config.EnsureDirectory();
            _config.Reset();

            //WHEN
            Assert.AreEqual(true, Directory.Exists(_configPath));
        }

        [Test]
        public void reset_when_folder_not_exist()
        {
            //GIVEN
            _config.Wipe();
            _config.Reset();

            //WHEN
            Assert.AreEqual(true, Directory.Exists(_configPath));
        }

        [Test]
        public void create_sub_config_with_new_account()
        {
            //GIVEN
            var subConfig = _config.SubFolder("sub", "newtestaccount");

            //WHEN
            Assert.AreEqual("newtestaccount", subConfig.AccountName);
            Assert.AreEqual(Path.Combine(_configPath, "sub"), subConfig.FullPath);
        }

        [Test]
        public void create_sub_config_with_old_account()
        {
            //GIVEN
            var subConfig = _config.SubFolder("sub");

            //WHEN
            Assert.AreEqual("testaccount-sub", subConfig.AccountName);
            Assert.AreEqual(Path.Combine(_configPath, "sub"), subConfig.FullPath);
        }

        [Test]
        public void wipe_config_when_created_sub_config()
        {
            //GIVEN
            _config.EnsureDirectory();
            var subConfig = _config.SubFolder("sub", "subaccount");
            subConfig.EnsureDirectory();
            _config.Wipe();

            //WHEN
            Assert.AreEqual(false, Directory.Exists(_configPath));
        }
    }
}