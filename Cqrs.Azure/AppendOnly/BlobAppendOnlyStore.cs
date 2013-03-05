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

        readonly LockingInMemoryCache _cache = new LockingInMemoryCache();


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

        long _storeVersion = 0;

        public void Append(string streamName, byte[] data, long expectedStreamVersion = -1)
        {
            // should be locked
            try
            {
                _cache.Append(streamName, data, _storeVersion + 1, streamVersion =>
                {
                    EnsureWriterExists(_storeVersion);
                    Persist(streamName, data, streamVersion);
                }, expectedStreamVersion);

                _storeVersion += 1;

            }
            catch (AppendOnlyStoreConcurrencyException)
            {
                //store is OK when AOSCE is thrown. This is client's problem
                // just bubble it upwards
                throw;
            }
            catch
            {
                // store probably corrupted. Close it and then rethrow exception
                // so that clien will have a chance to retry.
                Close();
                throw;
            }
        }

        public IEnumerable<DataWithVersion> ReadRecords(string streamName, long startingFrom, int maxCount)
        {
            return _cache.ReadRecords(streamName, startingFrom, maxCount);
        }

        public IEnumerable<DataWithKey> ReadRecords(long startingFrom, int maxCount)
        {
            return _cache.ReadRecords(startingFrom, maxCount);

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
            Close();
            _cache.Clear(() =>
                {
                    var blobs = _container.ListBlobs().OfType<CloudPageBlob>().Where(item => item.Uri.ToString().EndsWith(".dat"));
            
                    blobs
                        .AsParallel().ForAll(i => i.DeleteIfExists());
                    _storeVersion = 0;
                });
        }

        public long GetCurrentVersion()
        {
            return _storeVersion;
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
            _storeVersion = _cache.ReloadEverything(EnumerateHistory());
        }

        void Persist(string key, byte[] buffer, long commit)
        {
            var frame = StorageFramesEvil.EncodeFrame(key, buffer, commit);
            if (!_currentWriter.Fits(frame.Data.Length + frame.Hash.Length))
            {
                CloseWriter();
                EnsureWriterExists(_storeVersion);
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