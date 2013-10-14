using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CuttingEdge.Conditions;
using Lokad.Cqrs.AtomicStorage;
using Netco.Logging;

namespace Lokad.Cqrs.Projections
{
	/// <summary>
	/// Chases events for a single projection.
	/// </summary>
	public sealed class EventsChasingService< TEvent > where TEvent : class
	{
		private const int EventsBatchSize = 1000;
		private const int MaxErrorDelay = 30000;
		private readonly IEventStoreCheckpoint _storeChekpoint;
		private readonly IBufferedDocumentWriter _writer;
		private readonly MessageStore _eventsStore;
		private readonly RedirectToDynamicEvent _eventHandlers;
		private readonly string _projectionName;
		private readonly string _name;

		public EventsChasingService( string name, IEventStoreCheckpoint storeChekpoint, object projection, IBufferedDocumentWriter writer, IDocumentStore viewsStorage, MessageStore eventsStore )
		{
			Condition.Requires( name, "name" ).IsNotNull();
			Condition.Requires( eventsStore, "eventsStore" ).IsNotNull();
			Condition.Requires( projection, "projection" ).IsNotNull();
			Condition.Requires( writer, "writer" ).IsNotNull();
			Condition.Requires( viewsStorage, "viewsStorage" ).IsNotNull();
			Condition.Requires( eventsStore, "eventsStore" ).IsNotNull();

			this._name = name;
			this._storeChekpoint = storeChekpoint;
			this._writer = writer;
			this._eventsStore = eventsStore;
			this._projectionName = projection.GetType().Name;

			this._eventHandlers = new RedirectToDynamicEvent();
			this._eventHandlers.WireToWhen( projection );
		}

		public void ChaseEvents( CancellationToken token, int chasingProjectionsDelayInMilliseconds )
		{
			SystemObserver.Notify( "[chase] \t {0}\t{1}", this._name, this._projectionName );
			int errorsCount = 0;

			while( !token.IsCancellationRequested )
			{
				var errorEncountered = false;
				var eventsProcessed = false;

				try
				{
					var currentEventVersion = this._storeChekpoint.GetEventStreamVersion();
					var loadedRecords = this._eventsStore.EnumerateAllItems( currentEventVersion, EventsBatchSize );
					foreach( var record in loadedRecords )
					{
						this.CallHandlers( record.Items );
						currentEventVersion = record.StoreVersion;
					}
					if( currentEventVersion > this._storeChekpoint.GetEventStreamVersion() )
					{
						var watch = Stopwatch.StartNew();

						try
						{
							Task.Run( async () => await this.FlushChanges( currentEventVersion ) ).Wait();
						}
						finally
						{
							watch.Stop();
							var seconds = watch.Elapsed.TotalSeconds;
							if( seconds > 10 )
								SystemObserver.Notify( "[warn] {0}\t{1}\tTook {2}s to save projection", this._name, this._projectionName, watch.Elapsed.TotalSeconds );
						}
						eventsProcessed = true;
					}
				}
				catch( AggregateException x )
				{
					foreach( var innerException in x.InnerExceptions )
					{
						this.Log().Error( innerException, "[Error] {0}\t{1}\tError encountered while chasing events for projection", this._name, this._projectionName );
					}
					errorEncountered = true;
				}
				catch( Exception x )
				{
					this.Log().Error( x, "[Error] {0}\t{1}\tError encountered while chasing events for projection", this._name, this._projectionName );
					errorEncountered = true;
				}

				if( errorEncountered )
				{
					this._writer.Clear();
					errorsCount++;
					var errorDelay = chasingProjectionsDelayInMilliseconds * errorsCount;
					token.WaitHandle.WaitOne( errorDelay < MaxErrorDelay ? errorDelay : MaxErrorDelay );
				}
				else
					errorsCount = 0;

				if( !eventsProcessed )
					token.WaitHandle.WaitOne( chasingProjectionsDelayInMilliseconds );
			}
		}

		private async Task FlushChanges( long currentEventVersion )
		{
			await this.FlushChanges();
			await this._storeChekpoint.UpdateEventStreamVersion( currentEventVersion );
		}

		private Task FlushChanges()
		{
			return this._writer.Flush();
		}

		private void CallHandlers( IEnumerable< object > recordItems )
		{
			foreach( var e in recordItems.OfType< TEvent >() )
			{
				var watch = Stopwatch.StartNew();
				this._eventHandlers.InvokeEvent( e );
				watch.Stop();

				var seconds = watch.Elapsed.TotalSeconds;

				if( seconds > 10 )
				{
					SystemObserver.Notify( "[Warn] {0}\t{1}\t{2} took {3:0.0} seconds for handlers to process event {4}",
						this._name, this._projectionName, e.GetType().Name, seconds, e.ToString() );
				}
			}
		}
	}
}