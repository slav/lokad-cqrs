using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Lokad.Cqrs.AtomicStorage;

namespace Lokad.Cqrs.Projections
{
	public interface IBufferedDocumentWriter
	{
		Task Flush();
		void Clear();
	}

	public class BufferedDocumentWriter< TKey, TEntity > : IDocumentWriter< TKey, TEntity >, IBufferedDocumentWriter
	{
		private readonly IDocumentReader< TKey, TEntity > _reader;
		private readonly IDocumentWriter< TKey, TEntity > _writer;
		private readonly IDocumentStrategy _strategy;
		private readonly ConcurrentDictionary< string, TEntity > _viewsCache;
		private readonly ConcurrentDictionary< string, bool > _viewsToDelete;
		private readonly ConcurrentDictionary< string, TKey > _keys;

		public BufferedDocumentWriter( IDocumentReader< TKey, TEntity > reader, IDocumentWriter< TKey, TEntity > writer, IDocumentStrategy strategy )
		{
			this._reader = reader;
			this._writer = writer;
			this._strategy = strategy;
			this._viewsCache = new ConcurrentDictionary< string, TEntity >();
			this._viewsToDelete = new ConcurrentDictionary< string, bool >();
			this._keys = new ConcurrentDictionary< string, TKey >();
		}

		public TEntity AddOrUpdate( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint = AddOrUpdateHint.ProbablyExists )
		{
			return this.AddOrUpdateAsync( key, addFactory, update, hint ).Result;
		}

		public async Task< TEntity > AddOrUpdateAsync( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint = AddOrUpdateHint.ProbablyExists )
		{
			var keyString = this.GetKeyString( key );
			if( this._viewsCache.ContainsKey( keyString ) )
				return this._viewsCache.AddOrUpdate( keyString, k => addFactory(), ( k, e ) => update( e ) );

			var loadedEntity = await this._reader.GetAsync( key );
			loadedEntity.IfValue( e => this._viewsCache.TryAdd( keyString, e ) );
			this.RemoveFromDelete( keyString );
			return this._viewsCache.AddOrUpdate( keyString, k => addFactory(), ( k, e ) => update( e ) );
		}

		public void Save( TKey key, TEntity entity )
		{
			this.SaveAsync( key, entity ).Wait();
		}

		public bool TryDelete( TKey key )
		{
			return this.TryDeleteAsync( key ).Result;
		}

		public Task SaveAsync( TKey key, TEntity entity )
		{
			var keyString = this.GetKeyString( key );
			this.RemoveFromDelete( keyString );
			this._viewsCache[ keyString ] = entity;
			return Task.FromResult( true );
		}

		public Task< bool > TryDeleteAsync( TKey key )
		{
			var keyString = this.GetKeyString( key );
			this.AddToDelete( keyString );
			this.RemoveFromViewCache( keyString );
			return Task.FromResult( true );
		}

		private void RemoveFromViewCache( string key )
		{
			TEntity view;
			this._viewsCache.TryRemove( key, out view );
		}

		private void AddToDelete( string key )
		{
			this._viewsToDelete.TryAdd( key, true );
		}

		private void RemoveFromDelete( string key )
		{
			bool val;
			this._viewsToDelete.TryRemove( key, out val );
		}

		public async Task Flush()
		{
			try
			{
				await this.FlushViewsDelete();
				await this.FlushViewsSave();
			}
			finally
			{
				this.Clear();
			}
		}

		public void Clear()
		{
			this._viewsCache.Clear();
			this._viewsToDelete.Clear();
			this._keys.Clear();
		}

		private Task FlushViewsDelete()
		{
			if( this._viewsToDelete.IsEmpty )
				return Task.FromResult( true );

			var deleteTasks = this._viewsToDelete.Select(
				viewToDelete => this._writer.TryDeleteAsync( this._keys[ viewToDelete.Key ] ) );

			return Task.WhenAll( deleteTasks );
		}

		private Task FlushViewsSave()
		{
			if( this._viewsCache.IsEmpty )
				return Task.FromResult( true );

			var saveTasks = this._viewsCache.Select(
				view => this._writer.SaveAsync( this._keys[ view.Key ], view.Value ) );

			return Task.WhenAll( saveTasks );
		}

		private string GetKeyString( TKey key )
		{
			var keyString = this._strategy.GetEntityLocation< TEntity >( key );
			this._keys.TryAdd( keyString, key );
			return keyString;
		}
	}
}