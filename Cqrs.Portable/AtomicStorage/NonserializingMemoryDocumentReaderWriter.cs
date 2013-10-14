using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

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

		public Task< Maybe< TEntity > > GetAsync( TKey key )
		{
			var name = this.GetName( key );
			object savedEntity;
			var taskCompletionSource = new TaskCompletionSource< Maybe< TEntity > >();
			if( this._store.TryGetValue( name, out savedEntity ) )
				taskCompletionSource.SetResult( ( TEntity )savedEntity );
			else
				taskCompletionSource.SetResult( Maybe< TEntity >.Empty );
			return taskCompletionSource.Task;
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

		public void Save( TKey key, TEntity entity )
		{
			this._store[ this.GetName( key ) ] = entity;
		}

		public bool TryDelete( TKey key )
		{
			object savedEntity;
			return this._store.TryRemove( this.GetName( key ), out savedEntity );
		}

		public Task< TEntity > AddOrUpdateAsync( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint = AddOrUpdateHint.ProbablyExists )
		{
			var result = this.AddOrUpdate( key, addFactory, update, hint );
			return Task.FromResult( result );
		}

		public Task SaveAsync( TKey key, TEntity entity )
		{
			this.Save( key, entity );
			return Task.FromResult( true );
		}

		public Task< bool > TryDeleteAsync( TKey key )
		{
			return Task.FromResult( this.TryDelete( key ) );
		}
	}
}