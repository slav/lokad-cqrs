using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class MemoryDocumentReaderWriter< TKey, TEntity > : IDocumentReader< TKey, TEntity >, IDocumentWriter< TKey, TEntity >
	{
		private readonly IDocumentStrategy _strategy;
		private readonly ConcurrentDictionary< string, byte[] > _store;

		public MemoryDocumentReaderWriter( IDocumentStrategy strategy, ConcurrentDictionary< string, byte[] > store )
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
			byte[] bytes;
			if( this._store.TryGetValue( name, out bytes ) )
			{
				using( var mem = new MemoryStream( bytes ) )
				{
					entity = this._strategy.Deserialize< TEntity >( mem );
					return true;
				}
			}
			entity = default( TEntity );
			return false;
		}

		public Task< Maybe< TEntity > > GetAsync( TKey key )
		{
			var name = this.GetName( key );
			byte[] bytes;
			var taskCompletionSource = new TaskCompletionSource< Maybe< TEntity > >();
			if( this._store.TryGetValue( name, out bytes ) )
			{
				using( var mem = new MemoryStream( bytes ) )
				{
					var entity = this._strategy.Deserialize< TEntity >( mem );
					taskCompletionSource.SetResult( entity );
				}
			}
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
				using( var memory = new MemoryStream() )
				{
					this._strategy.Serialize( result, memory );
					return memory.ToArray();
				}
			}, ( s2, bytes ) =>
			{
				TEntity entity;
				using( var memory = new MemoryStream( bytes ) )
					entity = this._strategy.Deserialize< TEntity >( memory );
				result = update( entity );
				using( var memory = new MemoryStream() )
				{
					this._strategy.Serialize( result, memory );
					return memory.ToArray();
				}
			} );
			return result;
		}

		public void Save( TKey key, TEntity entity )
		{
			using( var memory = new MemoryStream() )
			{
				this._strategy.Serialize( entity, memory );
				this._store[ this.GetName( key ) ] = memory.ToArray();
			}
		}

		public bool TryDelete( TKey key )
		{
			byte[] bytes;
			return this._store.TryRemove( this.GetName( key ), out bytes );
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