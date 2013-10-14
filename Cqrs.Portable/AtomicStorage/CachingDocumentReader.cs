using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	/// <summary>
	/// Caches loading data from the reader.
	/// </summary>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	/// <typeparam name="TEntity">The type of the entity.</typeparam>
	public class CachingDocumentReader< TKey, TEntity > : IDocumentReader< TKey, TEntity >
	{
		private readonly IDocumentReader< TKey, TEntity > _reader;
		private readonly IDocumentStrategy _strategy;
		private readonly ConcurrentDictionary< string, Task< Maybe< TEntity > > > _cache;

		public CachingDocumentReader( IDocumentReader< TKey, TEntity > reader, IDocumentStrategy strategy )
		{
			this._reader = reader;
			this._strategy = strategy;
			this._cache = new ConcurrentDictionary< string, Task< Maybe< TEntity > > >();
		}

		public bool TryGet( TKey key, out TEntity view )
		{
			var keyString = this.GetKeyString( key );

			// try to get value from cache
			Task< Maybe< TEntity > > valueTask;
			if( this._cache.TryGetValue( keyString, out valueTask ) )
			{
				var cachedValue = valueTask.Result;
				view = cachedValue.HasValue ? cachedValue.Value : default ( TEntity );
				return cachedValue.HasValue;
			}

			// need to load view. Load synchronously, but provide Task to indicate progress
			var tcs = new TaskCompletionSource< Maybe< TEntity > >();
			this._cache.TryAdd( keyString, tcs.Task );
			try
			{
				var isViewLoaded = this._reader.TryGet( key, out view );
				tcs.SetResult( isViewLoaded ? view : Maybe< TEntity >.Empty );
				return isViewLoaded;
			}
			catch( Exception x )
			{
				tcs.SetException( x );
				throw;
			}

//			try
//			{
//				var result = this.GetAsync( key ).Result;
//				view = result.HasValue ? result.Value : default ( TEntity );
//				return result.HasValue;
//			}
//			catch( AggregateException x )
//			{
//				if( x.InnerExceptions.Count == 1 )
//					throw x.InnerExceptions[ 0 ];
//				throw;
//			}
		}

		public Task< Maybe< TEntity > > GetAsync( TKey key )
		{
			var keyString = this.GetKeyString( key );
			return this._cache.GetOrAdd( keyString, ks => this._reader.GetAsync( key ) );
		}

		private string GetKeyString( TKey key )
		{
			var keyString = this._strategy.GetEntityLocation< TEntity >( key );
			return keyString;
		}
	}
}