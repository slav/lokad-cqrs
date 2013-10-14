using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CuttingEdge.Conditions;

namespace Lokad.Cqrs.AtomicStorage
{
	/// <summary>
	/// Document store with support for caching loaded documents via <see cref="CachingDocumentReader{TKey,TEntity}"/>
	/// </summary>
	/// <remarks>This class is useful to cache data when during calculations the same view might be requested several times. However, it should expire
	/// quickly to avoid stale views. It should be re-create per request if used in web client.</remarks>
	public class CachingDocumentStore : IDocumentStore
	{
		private readonly IDocumentStore _store;

		/// <summary>
		/// Cache for all created document readers. Each document reader will have it's own cache.
		/// </summary>
		private readonly ConcurrentDictionary< ReaderKey, object > _readersCache;

		public IDocumentStrategy Strategy
		{
			get { return this._store.Strategy; }
		}

		public CachingDocumentStore( IDocumentStore store )
		{
			Condition.Requires( store, "store" ).IsNotNull();

			this._store = store;
			this._readersCache = new ConcurrentDictionary< ReaderKey, object >();
		}

		public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
		{
			return this._store.GetWriter< TKey, TEntity >();
		}

		public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
		{
			var key = ReaderKey.Create< TKey, TEntity >();
			var cachedReader = _readersCache.GetOrAdd( key, k => new CachingDocumentReader< TKey, TEntity >( this._store.GetReader< TKey, TEntity >(), this.Strategy ) );
			return ( IDocumentReader< TKey, TEntity > )cachedReader;
		}

		public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
		{
			return this._store.EnumerateContents( bucket );
		}

		public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
		{
			this._store.WriteContents( bucket, records );
		}

		public Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token )
		{
			return this._store.WriteContentsAsync( bucket, records, token );
		}

		public void Reset( string bucket )
		{
			this._store.Reset( bucket );
		}

		public Task ResetAsync( string bucket )
		{
			return this._store.ResetAsync( bucket );
		}

		private class ReaderKey : IEquatable< ReaderKey >
		{
			public Type KeyType { get; private set; }
			public Type EntityType { get; private set; }

			private ReaderKey( Type keyType, Type entityType )
			{
				KeyType = keyType;
				EntityType = entityType;
			}

			public static ReaderKey Create< TKey, TEntity >()
			{
				return new ReaderKey( typeof( TKey ), typeof( TEntity ) );
			}

			public bool Equals( ReaderKey other )
			{
				if( ReferenceEquals( null, other ) )
					return false;
				if( ReferenceEquals( this, other ) )
					return true;
				return this.KeyType == other.KeyType && this.EntityType == other.EntityType;
			}

			public override bool Equals( object obj )
			{
				if( ReferenceEquals( null, obj ) )
					return false;
				if( ReferenceEquals( this, obj ) )
					return true;
				if( obj.GetType() != this.GetType() )
					return false;
				return Equals( ( ReaderKey )obj );
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return ( this.KeyType.GetHashCode() * 397 ) ^ this.EntityType.GetHashCode();
				}
			}

			public static bool operator ==( ReaderKey left, ReaderKey right )
			{
				return Equals( left, right );
			}

			public static bool operator !=( ReaderKey left, ReaderKey right )
			{
				return !Equals( left, right );
			}
		}
	}
}