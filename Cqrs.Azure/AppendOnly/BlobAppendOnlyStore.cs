using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Lokad.Cqrs.TapeStorage;
using Microsoft.WindowsAzure.StorageClient;

namespace Lokad.Cqrs.AppendOnly
{
    /// <summary>
    /// <para>This is embedded append-only store implemented on top of cloud page blobs 
    /// (for persisting data with one HTTP call).</para>
    /// <para>This store ensures that only one writer exists and writes to a given event store</para>
    /// </summary>
    public sealed class BlobAppendOnlyStore : IAppendOnlyStore
    {
        // Caches
        readonly CloudBlobContainer _container;
        readonly ConcurrentDictionary<string, DataWithVersion[]> _items = new ConcurrentDictionary<string, DataWithVersion[]>();
        DataWithKey[] _all = new DataWithKey[0];

        /// <summary>
        /// Used to synchronize access between multiple threads within one process
        /// </summary>
        readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();


        bool _closed;

        /// <summary>
        /// Currently open file
        /// </summary>
        AppendOnlyStream _currentWriter;

        public BlobAppendOnlyStore(CloudBlobContainer container)
        {
            _container = container;
        }

        public void Dispose()
        {
            if (!_closed)
                Close();
        }

        public void InitializeWriter()
        {
            CreateIfNotExists(_container, TimeSpan.FromSeconds(60));
            LoadCaches();
        }
        public void InitializeReader()
        {
            CreateIfNotExists(_container, TimeSpan.FromSeconds(60));
            LoadCaches();
        }

        public void Append(string streamName, byte[] data, long expectedStreamVersion = -1)
        {

            // should be locked
            try
            {
                _cacheLock.EnterWriteLock();

                var list = _items.GetOrAdd(streamName, s => new DataWithVersion[0]);
                if (expectedStreamVersion >= 0)
                {
                    if (list.Length != expectedStreamVersion)
                        throw new AppendOnlyStoreConcurrencyException(expectedStreamVersion, list.Length, streamName);
                }

                EnsureWriterExists(_all.Length);
                long commit = list.Length + 1;

                Persist(streamName, data, commit);
                AddToCaches(streamName, data, commit);
            }
            catch
            {
                Close();
                throw;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        public IEnumerable<DataWithVersion> ReadRecords(string streamName, long afterVersion, int maxCount)
        {
            // no lock is needed, since we are polling immutable object.
            DataWithVersion[] list;
            return _items.TryGetValue(streamName, out list) ? list.Skip((int)afterVersion).Take(maxCount) : Enumerable.Empty<DataWithVersion>();
        }

        public IEnumerable<DataWithKey> ReadRecords(long afterVersion, int maxCount)
        {
            if (afterVersion < 0)
                throw new ArgumentOutOfRangeException("afterVersion", "Must be zero or greater.");

            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be more than zero.");

            // collection is immutable so we don't care about locks
            return _all.Skip((int)afterVersion).Take(maxCount);
        }

        public void Close()
        {
            _closed = true;

            if (_currentWriter == null)
                return;

            var tmp = _currentWriter;
            _currentWriter = null;
            tmp.Dispose();
        }

        public void ResetStore()
        {
            // should be locked
            try
            {
                _cacheLock.EnterWriteLock();
                Close();
                _all = new DataWithKey[0];
                foreach (KeyValuePair<string, DataWithVersion[]> dataWithVersionse in _items)
                {
                    var blob = _container.GetPageBlobReference(dataWithVersionse.Key);
                    blob.DeleteIfExists();
                }
                _items.Clear();
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        public long GetCurrentVersion()
        {
            return _all.Length;
        }

        IEnumerable<StorageFrameDecoded> EnumerateHistory()
        {
            // cleanup old pending files
            // load indexes
            // build and save missing indexes
            var datFiles = _container
                .ListBlobs()
                .OrderBy(s => s.Uri.ToString())
                .OfType<CloudPageBlob>();

            foreach (var fileInfo in datFiles)
            {
                using (var stream = new MemoryStream(fileInfo.DownloadByteArray()))
                {
                    StorageFrameDecoded result;
                    while (StorageFramesEvil.TryReadFrame(stream, out result))
                    {
                        yield return result;
                    }
                }
            }
        }


        void LoadCaches()
        {
            try
            {
                _cacheLock.EnterWriteLock();

                foreach (var record in EnumerateHistory())
                {
                    AddToCaches(record.Name, record.Bytes, record.Stamp);
                }
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        void AddToCaches(string key, byte[] buffer, long commit)
        {
            var storeVersion = _all.Length + 1;
            var record = new DataWithVersion(commit, buffer, storeVersion);
            _all = AddToNewArray(_all, new DataWithKey(key, buffer, commit, storeVersion));
            _items.AddOrUpdate(key, s => new[] { record }, (s, records) => AddToNewArray(records, record));
        }

        static T[] AddToNewArray<T>(T[] source, T item)
        {
            var copy = new T[source.Length + 1];
            Array.Copy(source, copy, source.Length);
            copy[source.Length] = item;
            return copy;
        }

        void Persist(string key, byte[] buffer, long commit)
        {
            var frame = StorageFramesEvil.EncodeFrame(key, buffer, commit);
            if (!_currentWriter.Fits(frame.Data.Length + frame.Hash.Length))
            {
                CloseWriter();
                EnsureWriterExists(_all.Length);
            }

            _currentWriter.Write(frame.Data);
            _currentWriter.Write(frame.Hash);
            _currentWriter.Flush();
        }

        void CloseWriter()
        {
            _currentWriter.Dispose();
            _currentWriter = null;
        }

        void EnsureWriterExists(long version)
        {
            if (_currentWriter != null)
                return;

            var fileName = string.Format("{0:00000000}-{1:yyyy-MM-dd-HHmmss}.dat", version, DateTime.UtcNow);
            var blob = _container.GetPageBlobReference(fileName);
            blob.Create(1024 * 512);

            _currentWriter = new AppendOnlyStream(512, (i, bytes) => blob.WritePages(bytes, i), 1024 * 512);
        }

        static void CreateIfNotExists(CloudBlobContainer container, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                try
                {
                    container.CreateIfNotExist();
                    return;
                }
                catch (StorageClientException e)
                {
                    // container is being deleted
                    if (!(e.ErrorCode == StorageErrorCode.ResourceAlreadyExists && e.StatusCode == HttpStatusCode.Conflict))
                        throw;
                }
                Thread.Sleep(500);
            }

            throw new TimeoutException(string.Format("Can not create container within {0} seconds.", timeout.TotalSeconds));
        }
    }
}