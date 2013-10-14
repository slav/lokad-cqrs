#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class MemoryDocumentStore : IDocumentStore
	{
		private ConcurrentDictionary< string, ConcurrentDictionary< string, byte[] > > _store;
		private readonly IDocumentStrategy _strategy;

		public MemoryDocumentStore( ConcurrentDictionary< string, ConcurrentDictionary< string, byte[] > > store, IDocumentStrategy strategy )
		{
			this._store = store;
			this._strategy = strategy;
		}

		public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
		{
			var bucket = this._strategy.GetEntityBucket< TEntity >();
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, byte[] >() );
			return new MemoryDocumentReaderWriter< TKey, TEntity >( this._strategy, store );
		}

		public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
		{
			var pairs = records.Select( r => new KeyValuePair< string, byte[] >( r.Key, r.Read() ) );
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, byte[] >() );

			store.Clear();

			foreach( var pair in pairs )
			{
				store[ pair.Key ] = pair.Value;
			}
		}

		public Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token )
		{
			var pairs = records.Select( r => new KeyValuePair< string, byte[] >( r.Key, r.Read() ) );
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, byte[] >() );

			store.Clear();

			foreach( var pair in pairs )
			{
				store[ pair.Key ] = pair.Value;
			}
			return Task.FromResult( true );
		}

		public void ResetAll()
		{
			this._store.Clear();
		}

		public void Reset( string bucket )
		{
			if( this._store.ContainsKey( bucket ) )
				this._store[ bucket ].Clear();
		}

		public Task ResetAsync( string bucket )
		{
			if( this._store.ContainsKey( bucket ) )
				this._store[ bucket ].Clear();
			return Task.FromResult( true );
		}

		public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
		{
			var bucket = this._strategy.GetEntityBucket< TEntity >();
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, byte[] >() );
			return new MemoryDocumentReaderWriter< TKey, TEntity >( this._strategy, store );
		}

		public IDocumentStrategy Strategy
		{
			get { return this._strategy; }
		}

		public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
		{
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, byte[] >() );
			return store.Select( p => new DocumentRecord( p.Key, () => p.Value ) ).ToArray();
		}
	}
}