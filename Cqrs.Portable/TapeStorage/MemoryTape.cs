using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Lokad.Cqrs.TapeStorage
{
    public sealed class MemoryAppendOnlyStore : IAppendOnlyStore
    {
        readonly ConcurrentDictionary<string, IList<DataWithVersion>> _dict = new ConcurrentDictionary<string, IList<DataWithVersion>>();
        IList<DataWithKey> _all = new List<DataWithKey>(); 
 

        public void InitializeForWriting()
        {
            
        }

        public void Append(string streamName, byte[] data, long expectedStreamVersion = -1)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (data.Length == 0)
                throw new ArgumentException("Buffer must contain at least one byte.");

            
            var result = _dict.AddOrUpdate(streamName, s =>
            {

                if (expectedStreamVersion >= 0)
                {
                    if (expectedStreamVersion != 0)
                        throw new AppendOnlyStoreConcurrencyException(expectedStreamVersion, 0, streamName);
                }
                var records = new List<DataWithVersion> { new DataWithVersion(1, data,1) };
                return records;
            }, (s, list) =>
            {
                var version = list.Count;
                if (expectedStreamVersion >= 0)
                {
                    if (expectedStreamVersion != version)
                        throw new AppendOnlyStoreConcurrencyException(expectedStreamVersion, version, streamName);
                }
                return list.Concat(new[] { new DataWithVersion(version + 1, data, _all.Count+1) }).ToList();
            });
            
            _all = new List<DataWithKey>(_all) { new DataWithKey(streamName, data, result.Count, _all.Count+1)};
        }

        public IEnumerable<DataWithVersion> ReadRecords(string streamName, long startingFrom, int maxCount)
        {
            if (startingFrom < 0)
                throw new ArgumentOutOfRangeException("startingFrom", "Must be zero or greater.");

            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be more than zero.");

            IList<DataWithVersion> bytes;
            if (_dict.TryGetValue(streamName, out bytes))
            {
                foreach (var bytese in bytes.Skip((int)startingFrom).Take(maxCount))
                {
                    yield return bytese;
                }
            }
        }

        public IEnumerable<DataWithKey> ReadRecords(long startingFrom, int maxCount)
        {
            return _all.Skip((int) startingFrom).Take(maxCount);
        }

        public void Close()
        {
            
        }

        public void ResetStore()
        {
            _dict.Clear();
            _all.Clear();
        }

        public long GetCurrentVersion()
        {
            return _all.Count;
        }

        public void Dispose()
        {
            
        }
    }
}