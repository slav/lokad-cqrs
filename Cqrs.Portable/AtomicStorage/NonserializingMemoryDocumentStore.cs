#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class NonserializingMemoryDocumentStore : IDocumentStore
	{
		private readonly ConcurrentDictionary< string, ConcurrentDictionary< string, object > > _store;
		private readonly IDocumentStrategy _strategy;

		public NonserializingMemoryDocumentStore( ConcurrentDictionary< string, ConcurrentDictionary< string, object > > store, IDocumentStrategy strategy )
		{
			this._store = store;
			this._strategy = strategy;
		}

		public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
		{
			var bucket = this._strategy.GetEntityBucket< TEntity >();
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, object >() );
			return new NonserializingMemoryDocumentReaderWriter< TKey, TEntity >( this._strategy, store );
		}

		public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
		{
			var pairs = records.Select( r => new KeyValuePair< string, byte[] >( r.Key, r.Read() ) );
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, object >() );

			store.Clear();

			foreach( var pair in pairs )
			{
				store[ pair.Key ] = pair.Value;
			}
		}

		public void ResetAll()
		{
			this._store.Clear();
		}

		public void Reset( string bucketNames )
		{
			this._store[ bucketNames ].Clear();
		}

		public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
		{
			var bucket = this._strategy.GetEntityBucket< TEntity >();
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, object >() );
			return new NonserializingMemoryDocumentReaderWriter< TKey, TEntity >( this._strategy, store );
		}

		public IDocumentStrategy Strategy
		{
			get { return this._strategy; }
		}

		public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
		{
			var store = this._store.GetOrAdd( bucket, s => new ConcurrentDictionary< string, object >() );
			return store.Select( p => new DocumentRecord( p.Key, () => 
				{
					byte[] byteContent = p.Value as byte[];
					if( byteContent != null )
						return byteContent;
					using( var memStream = new MemoryStream() )
					{
						this._strategy.Serialize( p.Value, memStream );
						return memStream.ToArray();
					} 
				}) ).ToArray();
		}
	}
}