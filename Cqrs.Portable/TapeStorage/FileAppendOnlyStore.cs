using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Lokad.Cqrs.TapeStorage
{
    /// <summary>
    /// Simple embedded append-only store that uses Riak.Bitcask model
    /// for keeping records
    /// </summary>
    public class FileAppendOnlyStore : IAppendOnlyStore
    {
        readonly DirectoryInfo _info;

        // used to synchronize access between threads within a process

        
        // used to prevent writer access to store to other processes
        FileStream _lock;
        FileStream _currentWriter;

        readonly LockingInMemoryCache _cache = new LockingInMemoryCache();
        // caches
        

        public void Initialize()
        {
            _info.Refresh();
            if (!_info.Exists)
                _info.Create();
            // grab the ownership
            _lock = new FileStream(Path.Combine(_info.FullName, "lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                8,
                FileOptions.DeleteOnClose);

            LoadCaches();
        }

        long _storeVersion = 0;

        public void LoadCaches()
        {
            _storeVersion = _cache.ReloadEverything(EnumerateHistory());
        }

        IEnumerable<StorageFrameDecoded> EnumerateHistory()
        {
            // cleanup old pending files
            // load indexes
            // build and save missing indexes
            var datFiles = _info.EnumerateFiles("*.dat");

            foreach (var fileInfo in datFiles.OrderBy(fi => fi.Name))
            {
                // quick cleanup
                if (fileInfo.Length == 0)
                {
                    fileInfo.Delete();
                    continue;
                }

                using (var reader = fileInfo.OpenRead())
                {
                    StorageFrameDecoded result;
                    while (StorageFramesEvil.TryReadFrame(reader, out result))
                    {
                        yield return result;
                    }
                }
            }
        }


        public void Dispose()
        {
            if (!_closed)
                Close();
        }

        public FileAppendOnlyStore(DirectoryInfo info)
        {
            _info = info;
        }

        public void Append(string streamName, byte[] data, long expectedStreamVersion = -1)
        {
            // should be locked
            try
            {
                _cache.Append(streamName, data, _storeVersion + 1, streamVersion =>
                    {
                        EnsureWriterExists(_storeVersion);
                        PersistInFile(streamName, data, streamVersion);
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

        void PersistInFile(string key, byte[] buffer, long streamVersion)
        {
            StorageFramesEvil.WriteFrame(key, streamVersion, buffer, _currentWriter);
            // make sure that we persist
            // NB: this is not guaranteed to work on Linux
            _currentWriter.Flush(true);
        }

        void EnsureWriterExists(long storeVersion)
        {
            if (_currentWriter != null) return;

            var fileName = string.Format("{0:00000000}-{1:yyyy-MM-dd-HHmmss}.dat", storeVersion, DateTime.UtcNow);
            _currentWriter = File.OpenWrite(Path.Combine(_info.FullName, fileName));
        }

        

       
        
      

        public IEnumerable<DataWithVersion> ReadRecords(string streamName, long afterVersion, int maxCount)
        {
            return _cache.ReadRecords(streamName, afterVersion, maxCount);
        }

        public IEnumerable<DataWithKey> ReadRecords(long afterVersion, int maxCount)
        {
            return _cache.ReadRecords(afterVersion, maxCount);
            
        }

        bool _closed;

        public void Close()
        {
            using (_lock)
            using (_currentWriter)
            {
                _currentWriter = null;
                _closed = true;
                
            }
        }

        public void ResetStore()
        {
            Close();

            Directory.Delete(_info.FullName, true);
            _cache.Clear();
            Initialize();
        }


        public long GetCurrentVersion()
        {
            return _storeVersion;
        }
    }

    public sealed class LockingInMemoryCache
    {

        readonly ReaderWriterLockSlim _thread = new ReaderWriterLockSlim();
        readonly ConcurrentDictionary<string, DataWithVersion[]> _cacheByKey = new ConcurrentDictionary<string, DataWithVersion[]>();
        DataWithKey[] _cacheFull = new DataWithKey[0];

        public long ReloadEverything(IEnumerable<StorageFrameDecoded> sfd)
        {
            _thread.EnterWriteLock();
            try
            {
                _cacheFull = new DataWithKey[0];

                // [abdullin]: known performance problem identified by Nicolas Mehlei
                // creating new immutable array on each line will kill performance
                // We need to at least do some batching here

                long storeVersion = 0;
                foreach (var record in sfd)
                {
                    storeVersion += 1;
                    AddToCaches(record.Name, record.Bytes, record.Stamp, storeVersion);
                }
                return storeVersion;
            }
            finally
            {
                _thread.ExitWriteLock();
            }
        }

        void AddToCaches(string key, byte[] buffer, long streamVersion, long storeVersion)
        {
            var record = new DataWithVersion(streamVersion, buffer, storeVersion);
            _cacheFull = ImmutableAdd(_cacheFull, new DataWithKey(key, buffer, streamVersion, storeVersion));
            _cacheByKey.AddOrUpdate(key, s => new[] { record }, (s, records) => ImmutableAdd(records, record));
        }

        static T[] ImmutableAdd<T>(T[] source, T item)
        {
            var copy = new T[source.Length + 1];

            Array.Copy(source, copy, source.Length);
            copy[source.Length] = item;
            

            return copy;
        }

        public void Append(string streamName, byte[] data, long newStoreVersion, Action<long> commitStreamVersion, long expectedStreamVersion = -1)
        {
            _thread.EnterWriteLock();

            try
            {
                var list = _cacheByKey.GetOrAdd(streamName, s => new DataWithVersion[0]);
                if (expectedStreamVersion >= 0)
                {
                    if (list.Length != expectedStreamVersion)
                        throw new AppendOnlyStoreConcurrencyException(expectedStreamVersion, list.Length, streamName);
                }
                long newStreamVersion = list.Length + 1;
                AddToCaches(streamName, data, newStreamVersion, newStoreVersion);

                commitStreamVersion(newStreamVersion);
            }
            finally
            {
                _thread.ExitWriteLock();
            }
            
        }

        public IEnumerable<DataWithVersion> ReadRecords(string streamName, long afterVersion, int maxCount)
        {
            if (afterVersion < 0)
                throw new ArgumentOutOfRangeException("afterVersion", "Must be zero or greater.");

            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be more than zero.");

            // no lock is needed.
            DataWithVersion[] list;
            var result = _cacheByKey.TryGetValue(streamName, out list) ? list : Enumerable.Empty<DataWithVersion>();

            return result.Skip((int)afterVersion).Take(maxCount);

        }

        public IEnumerable<DataWithKey> ReadRecords(long afterVersion, int maxCount)
        {
            // collection is immutable so we don't care about locks
            return _cacheFull.Skip((int)afterVersion).Take(maxCount);
        }

        public void Clear()
        {
            _thread.EnterWriteLock();
            try
            {
                _cacheFull = new DataWithKey[0];
                _cacheByKey.Clear();
            }
            finally
            {
                _thread.ExitWriteLock();
            }
        }
    }
}