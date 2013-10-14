using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CuttingEdge.Conditions;
using Lokad.Cqrs.AtomicStorage;

namespace Lokad.Cqrs.Projections
{
	public class BufferedDocumentStore : IDocumentStore
	{
		private readonly IDocumentStore _innerDocumentStore;
		private readonly IDocumentStrategy _strategy;
		private readonly List< IBufferedDocumentWriter > _documentWriters;

		public IBufferedDocumentWriter LatestWriter { get; private set; }

		public BufferedDocumentStore( IDocumentStore innerDocumentStore )
		{
			Condition.Requires( innerDocumentStore, "innerDocumentStore" ).IsNotNull();
			this._innerDocumentStore = innerDocumentStore;
			this._strategy = innerDocumentStore.Strategy;
			this._documentWriters = new List< IBufferedDocumentWriter >();
		}

		public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
		{
			// find existing writer if one exists
			var bufferedWriter = ( BufferedDocumentWriter< TKey, TEntity > )this._documentWriters.FirstOrDefault( w => w is BufferedDocumentWriter< TKey, TEntity > );
			if( bufferedWriter != null )
				return bufferedWriter;

			// create writer since one doesn't exist
			var innerWriter = this._innerDocumentStore.GetWriter< TKey, TEntity >();
			var innerReader = this._innerDocumentStore.GetReader< TKey, TEntity >();
			bufferedWriter = new BufferedDocumentWriter< TKey, TEntity >( innerReader, innerWriter, _strategy );
			this._documentWriters.Add( bufferedWriter );
			LatestWriter = bufferedWriter;

			return bufferedWriter;
		}

		public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
		{
			return this._innerDocumentStore.GetReader< TKey, TEntity >();
		}

		public IDocumentStrategy Strategy
		{
			get { return this._innerDocumentStore.Strategy; }
		}

		public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
		{
			return this._innerDocumentStore.EnumerateContents( bucket );
		}

		public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
		{
			this._innerDocumentStore.WriteContents( bucket, records );
		}

		public Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token )
		{
			return this._innerDocumentStore.WriteContentsAsync( bucket, records, token );
		}

		public void Reset( string bucket )
		{
			this._innerDocumentStore.Reset( bucket );
		}

		public Task ResetAsync( string bucket )
		{
			return this._innerDocumentStore.ResetAsync( bucket );
		}
	}
}