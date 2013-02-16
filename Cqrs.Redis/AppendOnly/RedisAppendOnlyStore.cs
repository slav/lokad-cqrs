#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using Lokad.Cqrs.TapeStorage;
using ServiceStack.Redis;

namespace Cqrs.Redis.AppendOnly
{
    public class RedisAppendOnlyStore : IAppendOnlyStore
    {
        private const string StoreStreamKey = "StoreKey";

        /// <summary>
        /// Used to synchronize access between multiple threads within one process
        /// </summary>
        readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();

        bool _closed;
        private RedisClient _client;

        public RedisAppendOnlyStore(RedisClient client)
        {
            _client = client;
        }

        public void Dispose()
        {
            if (!_closed)
                Close();
            _client.Dispose();
        }

        public void Append(string streamName, byte[] data, long expectedStreamVersion = -1)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (data.Length == 0)
                throw new ArgumentException("Buffer must contain at least one byte.");

            _cacheLock.EnterWriteLock();
            try
            {
                var storeVersion = _client.LLen(StoreStreamKey) + 1;
                var streamVersion = _client.LLen(streamName) + 1;

                if (expectedStreamVersion >= 0)
                {
                    if (streamVersion != expectedStreamVersion)
                        throw new AppendOnlyStoreConcurrencyException(expectedStreamVersion, streamVersion, streamName);
                }

                using (var trans = _client.CreateTransaction())
                {
                    trans.QueueCommand(r => ((RedisNativeClient)r).RPush(StoreStreamKey, new DataWithKey(streamName, data, streamVersion, storeVersion).ToBinary()));
                    trans.QueueCommand(r => ((RedisNativeClient)r).RPush(streamName, new DataWithVersion(streamVersion, data, storeVersion).ToBinary()));

                    trans.Commit();
                }
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
            if (maxCount < 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be zero or greater.");
            if (maxCount == 0)
                yield break;

            var start = afterVersion < 0 ? 0 : afterVersion;
            var end = maxCount == Int32.MaxValue ? maxCount : maxCount + afterVersion - 1;
            var items = _client.LRange(streamName, (int)start, (int)end);

            foreach (var item in items)
            {
                yield return DataWithVersion.TryGetFromBinary(item);
            }
        }

        public IEnumerable<DataWithKey> ReadRecords(long afterVersion, int maxCount)
        {
            if (maxCount < 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be zero or greater.");
            if (maxCount == 0)
                yield break;

            var start = afterVersion < 0 ? 0 : afterVersion;
            var end = maxCount == Int32.MaxValue ? maxCount : maxCount + afterVersion - 1;
            var items = _client.LRange(StoreStreamKey, (int)start, (int)end);

            foreach (var item in items)
            {
                yield return DataWithKey.TryGetFromBinary(item);
            }
        }

        public void Close()
        {
            _closed = true;

            if (_client == null)
                return;

            var tmp = _client;
            _client = null;
            tmp.Dispose();
        }

        public void ResetStore()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                if (_client != null && !_closed)
                    _client.FlushDb();
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        public long GetCurrentVersion()
        {
            return _client.LLen(StoreStreamKey);
        }
    }
}