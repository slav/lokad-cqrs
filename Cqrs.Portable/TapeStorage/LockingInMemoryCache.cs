using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Lokad.Cqrs.TapeStorage
{
    /// <summary>
    /// Simple in-memory thread-safe cache
    /// </summary>
    public sealed class LockingInMemoryCache
    {
        readonly ReaderWriterLockSlim _thread = new ReaderWriterLockSlim();
        ConcurrentDictionary<string, List< DataWithKey >> _cacheByKey = new ConcurrentDictionary<string, List< DataWithKey >>();
        List< DataWithKey > _cacheFull = new List< DataWithKey >();

        public void LoadHistory(IEnumerable<StorageFrameDecoded> sfd)
        {
            _thread.EnterWriteLock();
            try
            {
                if (StoreVersion != 0)
                    throw new InvalidOperationException("Must clear cache before loading history");

                _cacheFull = new List< DataWithKey >();

                // [abdullin]: known performance problem identified by Nicolas Mehlei
                // creating new immutable array on each line will kill performance
                // We need to at least do some batching here

                var cacheFullBuilder = new List<DataWithKey>();
                var streamPointerBuilder = new Dictionary<string, List<DataWithKey>>();

                long newStoreVersion = 0;
                foreach (var record in sfd)
                {
                    newStoreVersion += 1;

			    if( record.Name == "audit")
				    continue;

                    List<DataWithKey> list;
                    if (!streamPointerBuilder.TryGetValue(record.Name, out list))
                    {
                        streamPointerBuilder.Add(record.Name, list = new List<DataWithKey>());
                    }

                    var newStreamVersion = list.Count + 1;

                    var data = new DataWithKey(record.Name, record.Bytes, newStreamVersion, newStoreVersion);
                    list.Add(data);
                    cacheFullBuilder.Add(data);
                }

                _cacheFull = cacheFullBuilder;
                _cacheByKey = new ConcurrentDictionary<string, List< DataWithKey >>(streamPointerBuilder.Select(p => new KeyValuePair<string, List< DataWithKey >>(p.Key, p.Value)));
                StoreVersion = newStoreVersion;
            }
            finally
            {
                _thread.ExitWriteLock();
            }
        }

        public long StoreVersion { get; private set; }

        public delegate void OnCommit(long streamVersion, long storeVersion);

        public void ConcurrentAppend(string streamName, byte[] data, OnCommit commit, long expectedStreamVersion = -1)
        {
            _thread.EnterWriteLock();

            try
            {
                var list = _cacheByKey.GetOrAdd(streamName, s => new List< DataWithKey >());
                var actualStreamVersion = list.Count;

                if (expectedStreamVersion >= 0)
                {
                    if (actualStreamVersion != expectedStreamVersion)
                        throw new AppendOnlyStoreConcurrencyException(expectedStreamVersion, actualStreamVersion, streamName);
                }
                long newStreamVersion = actualStreamVersion + 1;
                long newStoreVersion = StoreVersion + 1;

                commit(newStreamVersion, newStoreVersion);

                // update in-memory cache only after real commit completed
	            if( streamName != "audit" )
	            {
		            var dataWithKey = new DataWithKey( streamName, data, newStreamVersion, newStoreVersion );
		            _cacheFull.Add( dataWithKey );
		            _cacheByKey.GetOrAdd( streamName, s => new List< DataWithKey > { dataWithKey } ).Add( dataWithKey );
	            }
	            StoreVersion = newStoreVersion;

            }
            finally
            {
                _thread.ExitWriteLock();
            }

        }

        public IEnumerable<DataWithKey> ReadStream(string streamName, long afterStreamVersion, int maxCount)
        {
            if (null == streamName)
                throw new ArgumentNullException("streamName");
            if (afterStreamVersion < 0)
                throw new ArgumentOutOfRangeException("afterStreamVersion", "Must be zero or greater.");

            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be more than zero.");

            List< DataWithKey > list;
            var result = new LockedListWrapper< DataWithKey >( _cacheByKey.TryGetValue(streamName, out list) ? list : new List<DataWithKey>() );

            return result.Where(version => version.StreamVersion > afterStreamVersion).Take(maxCount);

        }

        public IEnumerable<DataWithKey> ReadAll(long afterStoreVersion, int maxCount)
        {
            if (afterStoreVersion < 0)
                throw new ArgumentOutOfRangeException("afterStoreVersion", "Must be zero or greater.");

            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount", "Must be more than zero.");

            return new LockedListWrapper< DataWithKey >( _cacheFull ).Where(key => key.StoreVersion > afterStoreVersion).Take(maxCount);
        }

        public void Clear(Action executeWhenCommitting)
        {
            _thread.EnterWriteLock();
            try
            {
                executeWhenCommitting();
                _cacheFull = new List< DataWithKey >();
                _cacheByKey.Clear();
                StoreVersion = 0;
            }
            finally
            {
                _thread.ExitWriteLock();
            }
        }
    }

	public class LockedListWrapper< T > : IEnumerable< T >
	{
		public List< T > List { get; private set; }
		public int Count { get; private set; }

		public LockedListWrapper( List< T > list )
		{
			this.List = list;
			this.Count = list.Count;
		}

		public IEnumerator< T > GetEnumerator()
		{
			return new LockedListEnumerator< T >( List, Count );
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}

	public struct LockedListEnumerator< T > : IEnumerator< T >, IDisposable, IEnumerator
	{
		private List< T > _list;
		private int _index;
		private T _current;
		private int _length;

		/// <summary>
		/// Gets the element at the current position of the enumerator.
		/// </summary>
		/// 
		/// <returns>
		/// The element in the <see cref="T:System.Collections.Generic.List`1"/> at the current position of the enumerator.
		/// </returns>
		public T Current
		{
			get { return this._current; }
		}

		object IEnumerator.Current
		{
			get
			{
				if( this._index == 0 || this._index >= this._length + 1 )
					throw new InvalidOperationException( "Current is outside of the accessible index" );
				return this.Current;
			}
		}

		public LockedListEnumerator( List< T > list, int length )
		{
			this._list = list;
			this._index = 0;
			this._length = length;
			this._current = default ( T );
		}

		/// <summary>
		/// Releases all resources used by the <see cref="T:System.Collections.Generic.List`1.Enumerator"/>.
		/// </summary>
		public void Dispose()
		{
		}

		/// <summary>
		/// Advances the enumerator to the next element of the <see cref="T:System.Collections.Generic.List`1"/>.
		/// </summary>
		/// 
		/// <returns>
		/// true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.
		/// </returns>
		/// <exception cref="T:System.InvalidOperationException">The collection was modified after the enumerator was created. </exception>
		public bool MoveNext()
		{
			if( ( uint )this._index >= ( uint )this._length )
				return this.MoveBeyondLength();
			this._current = this._list[ this._index ];
			++this._index;
			return true;
		}

		private bool MoveBeyondLength()
		{
			this._index = this._index + 1;
			this._current = default ( T );
			return false;
		}

		void IEnumerator.Reset()
		{
			this._index = 0;
			this._current = default ( T );
		}
	}
}