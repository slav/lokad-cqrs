using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Cqrs.AtomicStorage;
using Netco.Extensions;

namespace Lokad.Cqrs.Projections
{
	public class ProjectionsRebuilder< TEvent > where TEvent : class
	{
		private readonly string _name;
		private readonly IDocumentStore _targetContainer;
		private readonly MessageStore _eventsStore;
		private readonly Func< IDocumentStore, IEnumerable< object > > _projectors;
		private readonly NuclearStorage _storage;

		public ProjectionsRebuilder( string name, IDocumentStore targetContainer, MessageStore eventsStore, Func< IDocumentStore, IEnumerable< object > > projectors )
		{
			this._name = name;
			this._targetContainer = targetContainer;
			this._eventsStore = eventsStore;
			this._projectors = projectors;
			this._storage = new NuclearStorage( targetContainer );
		}

		public async Task< ProjectionsInfo > CheckAndRebuildProjections( CancellationToken token )
		{
			var persistedProjectionsInfo = await this.LoadProjectionsInfo();

			var strategy = this._targetContainer.Strategy;
			var memory = new NonserializingMemoryStorageConfig();
			var memoryContainer = memory.CreateNuclear( strategy ).Container;

			var generatedProjectionInfos = this.GeneratedProjectionInfosFromProjectors( token, memoryContainer, strategy );

			var partitionedProjections = PartitionedProjectionsInfo.Partition( persistedProjectionsInfo.Infos, generatedProjectionInfos, this._storage );

			this.PrintProjectionsStatus( partitionedProjections );

			await this.DeleteObsolete( partitionedProjections.Obsolete, this._targetContainer );

			await this.RebuildProjections( partitionedProjections.NeedRebuild, memoryContainer, token );

			if( partitionedProjections.NeedRebuild.Count > 0 || partitionedProjections.Obsolete.Count > 0 )
			{
				var newProjectionsInfo = new ProjectionsInfo();
				newProjectionsInfo.Infos.UnionWith( partitionedProjections.ReadyForUse );
				newProjectionsInfo.Infos.UnionWith( partitionedProjections.NeedRebuild );
				await this._storage.SaveEntityAsync( this._name, newProjectionsInfo );
				persistedProjectionsInfo = newProjectionsInfo;
			}
			return persistedProjectionsInfo;
		}

		private async Task< ProjectionsInfo > LoadProjectionsInfo()
		{
			return ( await this._storage.GetEntityAsync< ProjectionsInfo >( this._name ) ).GetValue( () => new ProjectionsInfo() );
		}

		private IEnumerable< ProjectionInfo > GeneratedProjectionInfosFromProjectors( CancellationToken token, IDocumentStore memoryContainer, IDocumentStrategy strategy )
		{
			var trackingStore = new ProjectionInspectingStore( memoryContainer );

			var generatedProjectionInfos = new List< ProjectionInfo >();
			foreach( var projection in this._projectors( trackingStore ) )
			{
				var views = trackingStore.GetNewViewsAndReset();
				var projectionInfo = new ProjectionInfo( views, projection, strategy );
				if( generatedProjectionInfos.Any( p => p.ProjectionName == projectionInfo.ProjectionName ) )
					throw new InvalidOperationException( "Duplicate projection encountered: " + projectionInfo.ProjectionName );

				generatedProjectionInfos.Add( projectionInfo );
			}
			trackingStore.ValidateSanity();
			token.ThrowIfCancellationRequested();

			if( trackingStore.Views.Count != generatedProjectionInfos.Count )
				throw new InvalidOperationException( "Projections count mismatch" );
			return generatedProjectionInfos;
		}

		private Task DeleteObsolete( IEnumerable< ProjectionInfo > obsoleteProjections, IDocumentStore store )
		{
			return Task.WhenAll( obsoleteProjections.Select( p => p.ResetUnitialized( store ) ) );
		}

		private Task RebuildProjections( List< ProjectionInfo > needRebuild, IDocumentStore memoryContainer, CancellationToken token )
		{
			if( needRebuild.Count == 0 )
				return Task.FromResult( true );

			var updateTimer = new Stopwatch();
			updateTimer.Start();

			// observe projections
			var watch = Stopwatch.StartNew();

			var wire = new RedirectToDynamicEvent();
			needRebuild.ForEach( x => wire.WireToWhen( x.GetTempProjection() ) );

			var handlersWatch = Stopwatch.StartNew();

			var eventStoreVersion = this.ObserveWhileCan( this._eventsStore.EnumerateAllItems( 0, int.MaxValue ), wire, token );

			if( token.IsCancellationRequested )
			{
				SystemObserver.Notify( "[warn]\t{0}\tShutdown. Aborting projections before anything was changed.", this._name );
				return Task.FromResult( true );
			}

			var timeTotal = watch.Elapsed.TotalSeconds;
			var handlerTicks = handlersWatch.ElapsedTicks;
			var timeInHandlers = Math.Round( TimeSpan.FromTicks( handlerTicks ).TotalSeconds, 1 );
			SystemObserver.Notify( "[observe]\t{2}\t{0}sec ({1}sec in handlers) - Replayed events from", Math.Round( timeTotal, 0 ), timeInHandlers, this._name );

			var rebuildTasks = needRebuild.Select( projectionInfo => this.RebuildProjection( eventStoreVersion, projectionInfo, memoryContainer, token ) );
			return Task.WhenAll( rebuildTasks );
		}

		private async Task RebuildProjection( long eventStoreVersion, ProjectionInfo projectionInfo, IDocumentStore memoryContainer, CancellationToken cancellationToken )
		{
			await projectionInfo.Reset();
			var saveTasks = new List< Task >( projectionInfo.ViewBuckets.Length );
			foreach( var bucket in projectionInfo.ViewBuckets )
			{
				var sw = new Stopwatch();
				sw.Start();
				var contents = memoryContainer.EnumerateContents( bucket );
				var bucket1 = bucket;
				saveTasks.Add( this._targetContainer.WriteContentsAsync( bucket, contents, cancellationToken ).ContinueWith( t =>
				{
					sw.Stop();
					SystemObserver.Notify( "[good]\t{0}\t{1}\t{2:g}\tSaved View bucket {3}", this._name, projectionInfo.ProjectionName, sw.Elapsed, bucket1 );
				}, cancellationToken, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current ) );
			}
			await Task.WhenAll( saveTasks );
			await projectionInfo.UpdateEventStreamVersion( eventStoreVersion );
		}

		private long ObserveWhileCan( IEnumerable< StoreRecord > records, RedirectToDynamicEvent wire, CancellationToken token )
		{
			var watch = Stopwatch.StartNew();
			var count = 0;
			long eventStoreVersion = 0;
			foreach( var record in records )
			{
				count += 1;

				if( token.IsCancellationRequested )
					return eventStoreVersion;
				if( count % 50000 == 0 )
				{
					SystemObserver.Notify( "[observing]\t{0}\t{1} records in {2} seconds", this._name, count, Math.Round( watch.Elapsed.TotalSeconds, 2 ) );
					watch.Restart();
				}
				foreach( var message in record.Items.OfType< TEvent >() )
				{
					wire.InvokeEvent( message );
				}
				eventStoreVersion = record.StoreVersion;
			}
			return eventStoreVersion;
		}

		private void PrintProjectionsStatus( PartitionedProjectionsInfo partitionedProjections )
		{
			partitionedProjections.ReadyForUse.SelectMany( pr => pr.ViewBuckets ).ForEach( b => SystemObserver.Notify( "[good]\t{0} is up to date", b ) );
			partitionedProjections.NeedRebuild.SelectMany( pr => pr.ViewBuckets ).ForEach( b => SystemObserver.Notify( "[warn]\t{0} needs rebuild", b ) );
			partitionedProjections.Obsolete.SelectMany( pr => pr.ViewBuckets ).ForEach( b => SystemObserver.Notify( "[warn]\t{0} is obsolete", b ) );
		}

		private sealed class ProjectionInspectingStore : IDocumentStore
		{
			private readonly IDocumentStore _real;

			public ProjectionInspectingStore( IDocumentStore real )
			{
				this._real = real;
			}

			public readonly List< ViewInfo > Views = new List< ViewInfo >();
			private List< ViewInfo > _newViews = new List< ViewInfo >();

			public IEnumerable< ViewInfo > GetNewViewsAndReset()
			{
				var newProjections = this._newViews;
				this._newViews = new List< ViewInfo >();
				return newProjections;
			}

			public void ValidateSanity()
			{
				if( this.Views.Count == 0 )
					return;
				// TODO: add sanity check	throw new InvalidOperationException( "There were no projections registered" );

				var viewsWithMultipleProjections = this.Views.GroupBy( e => e.EntityType ).Where( g => g.Count() > 1 ).ToList();
				if( viewsWithMultipleProjections.Count > 0 )
				{
					var builder = new StringBuilder();
					builder.AppendLine( "Can only define only one projection per view. These views were referenced more than once:" );
					foreach( var projection in viewsWithMultipleProjections )
					{
						builder.AppendLine( "  " + projection.Key );
					}
					builder.AppendLine( "NB: you can use partials or dynamics in edge cases" );
					throw new InvalidOperationException( builder.ToString() );
				}

				var viewsWithSimilarBuckets = this.Views
					.GroupBy( e => e.StoreBucket.ToLowerInvariant() )
					.Where( g => g.Count() > 1 )
					.ToArray();

				if( viewsWithSimilarBuckets.Length > 0 )
				{
					var builder = new StringBuilder();
					builder.AppendLine( "Following views will be stored in same location, which will cause problems:" );
					foreach( var i in viewsWithSimilarBuckets )
					{
						var @join = string.Join( ",", i.Select( x => x.EntityType ) );
						builder.AppendFormat( " {0} : {1}", i.Key, @join ).AppendLine();
					}
					throw new InvalidOperationException( builder.ToString() );
				}
			}

			public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
			{
				var projection = new ViewInfo( typeof( TEntity ), this._real.Strategy.GetEntityBucket< TEntity >() );
				this.Views.Add( projection );
				this._newViews.Add( projection );

				return this._real.GetWriter< TKey, TEntity >();
			}

			public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
			{
				return this._real.GetReader< TKey, TEntity >();
			}

			public IDocumentStrategy Strategy
			{
				get { return this._real.Strategy; }
			}

			public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
			{
				return this._real.EnumerateContents( bucket );
			}

			public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
			{
				this._real.WriteContents( bucket, records );
			}

			public Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token )
			{
				return this._real.WriteContentsAsync( bucket, records, token );
			}

			public void Reset( string bucket )
			{
				this._real.Reset( bucket );
			}

			public Task ResetAsync( string bucket )
			{
				return this._real.ResetAsync( bucket );
			}
		}
	}

	[ DataContract ]
	public class ProjectionsInfo : IEquatable< ProjectionsInfo >
	{
		[ DataMember( Order = 1 ) ]
		public HashSet< ProjectionInfo > Infos { get; private set; }

		public ProjectionsInfo()
		{
			this.Infos = new HashSet< ProjectionInfo >();
		}

		public bool Equals( ProjectionsInfo other )
		{
			if( ReferenceEquals( null, other ) )
				return false;
			if( ReferenceEquals( this, other ) )
				return true;
			return this.Infos.SequenceEqual( other.Infos );
		}

		public override bool Equals( object obj )
		{
			if( ReferenceEquals( null, obj ) )
				return false;
			if( ReferenceEquals( this, obj ) )
				return true;
			if( obj.GetType() != this.GetType() )
				return false;
			return Equals( ( ProjectionsInfo )obj );
		}

		public override int GetHashCode()
		{
			return this.Infos.GetHashCode();
		}

		public static bool operator ==( ProjectionsInfo left, ProjectionsInfo right )
		{
			return Equals( left, right );
		}

		public static bool operator !=( ProjectionsInfo left, ProjectionsInfo right )
		{
			return !Equals( left, right );
		}
	}
}