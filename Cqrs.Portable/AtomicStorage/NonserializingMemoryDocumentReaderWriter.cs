using System;
using System.Collections.Concurrent;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class NonserializingMemoryDocumentReaderWriter< TKey, TEntity > : IDocumentReader< TKey, TEntity >, IDocumentWriter< TKey, TEntity >
	{
		private readonly IDocumentStrategy _strategy;
		private readonly ConcurrentDictionary< string, object > _store;

		public NonserializingMemoryDocumentReaderWriter( IDocumentStrategy strategy, ConcurrentDictionary< string, object > store )
		{
			this._store = store;
			this._strategy = strategy;
		}

		private string GetName( TKey key )
		{
			return this._strategy.GetEntityLocation< TEntity >( key );
		}

		public bool TryGet( TKey key, out TEntity entity )
		{
			var name = this.GetName( key );
			object savedEntity;
			if( this._store.TryGetValue( name, out savedEntity ) )
			{
				entity = ( TEntity )savedEntity;
				return true;
			}
			entity = default( TEntity );
			return false;
		}

		public TEntity AddOrUpdate( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint )
		{
			var result = default( TEntity );
			this._store.AddOrUpdate( this.GetName( key ), s =>
				{
					result = addFactory();
					return result;
				}, ( s2, savedEntity ) =>
					{
						var entity = ( TEntity )savedEntity;
						result = update( entity );
						return result;
					} );
			return result;
		}

		public bool TryDelete( TKey key )
		{
			object savedEntity;
			return this._store.TryRemove( this.GetName( key ), out savedEntity );
		}
	}
}