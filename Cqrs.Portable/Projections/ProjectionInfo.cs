using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using CuttingEdge.Conditions;
using Lokad.Cqrs.AtomicStorage;

namespace Lokad.Cqrs.Projections
{
	public interface IEventStoreCheckpoint
	{
		long GetEventStreamVersion();
		Task UpdateEventStreamVersion( long checkpoint );
	}

	[ DataContract ]
	public class ProjectionInfo : IEquatable< ProjectionInfo >, IEventStoreCheckpoint
	{
		[ DataMember( Order = 1 ) ]
		public ProjectionHash Hash { get; private set; }

		[ DataMember( Order = 2 ) ]
		public string[] ViewBuckets { get; private set; }

		[ DataMember( Order = 3 ) ]
		public string ProjectionName { get; private set; }

		private ProjectionCheckpoint _checkpoint;
		private NuclearStorage _store;

		/// <summary>
		/// The temp projection used only for rebuilding.
		/// </summary>
		private readonly object _tempProjection;

		public ProjectionInfo( IEnumerable< ViewInfo > views, object tempProjection, IDocumentStrategy strategy )
		{
			Condition.Requires( views, "views" ).IsNotNull();
			Condition.Requires( tempProjection, "tempProjection" ).IsNotNull();
			Condition.Requires( strategy, "strategy" ).IsNotNull();

			this.ViewBuckets = views.Select( v => v.StoreBucket ).ToArray();
			var viewTypes = views.Select( v => v.EntityType );

			var projectionType = tempProjection.GetType();
			this.Hash = new ProjectionHash( projectionType, viewTypes, strategy.GetType() );
			this.ProjectionName = projectionType.Name;

			this._checkpoint = new ProjectionCheckpoint();
			this._tempProjection = tempProjection;
		}

		private ProjectionInfo()
		{
		}

		public object GetTempProjection()
		{
			Condition.WithExceptionOnFailure< InvalidOperationException >().Requires( this._tempProjection ).IsNotNull( "Temp projection was not initialized" );
			return this._tempProjection;
		}

		public void Initialize( NuclearStorage storage )
		{
			Condition.Requires( storage, "storage" ).IsNotNull();
			if( this._checkpoint == null )
				this._checkpoint = storage.GetEntityOrThrow< ProjectionCheckpoint >( this.ProjectionName );

			this._store = storage;
		}

		public bool IsInitialized()
		{
			return this._store != null && this._checkpoint != null;
		}

		public async Task Reset()
		{
			await this._store.TryDeleteEntityAsync< ProjectionCheckpoint >( this.ProjectionName );
			await Task.WhenAll( this.ViewBuckets.Select( this._store.Container.ResetAsync ) );
			this._checkpoint = new ProjectionCheckpoint();
		}

		public async Task ResetUnitialized( IDocumentStore documentStore )
		{
			Condition.WithExceptionOnFailure< InvalidOperationException >().Requires( this._store, "_store" ).IsNull( "ProjectionInfo is initialized, but is being reset with different document store" );

			var storage = new NuclearStorage( documentStore );
			await storage.TryDeleteEntityAsync< ProjectionCheckpoint >( this.ProjectionName );
			await Task.WhenAll( this.ViewBuckets.Select( documentStore.ResetAsync ) );
			this._checkpoint = new ProjectionCheckpoint();
		}

		#region Checkpoint
		public long GetEventStreamVersion()
		{
			return this._checkpoint.LatestEventVersion;
		}

		public async Task UpdateEventStreamVersion( long checkpoint )
		{
			this._checkpoint.LatestEventVersion = checkpoint;
			await this._store.SaveEntityAsync( this.ProjectionName, this._checkpoint );
		}
		#endregion

		public bool Equals( ProjectionInfo other )
		{
			if( ReferenceEquals( null, other ) )
				return false;
			if( ReferenceEquals( this, other ) )
				return true;
			return this.Hash.Equals( other.Hash );
		}

		public override bool Equals( object obj )
		{
			if( ReferenceEquals( null, obj ) )
				return false;
			if( ReferenceEquals( this, obj ) )
				return true;
			if( obj.GetType() != this.GetType() )
				return false;
			return this.Equals( ( ProjectionInfo )obj );
		}

		public override int GetHashCode()
		{
			return this.Hash.GetHashCode();
		}

		public static bool operator ==( ProjectionInfo left, ProjectionInfo right )
		{
			return Equals( left, right );
		}

		public static bool operator !=( ProjectionInfo left, ProjectionInfo right )
		{
			return !Equals( left, right );
		}
	}

	public class ViewInfo
	{
		public string StoreBucket { get; private set; }
		public Type EntityType { get; private set; }

		public ViewInfo( Type entityType, string storeBucket )
		{
			Condition.Requires( storeBucket, "bucket" ).IsNotNullOrWhiteSpace();
			Condition.Requires( entityType, "type" ).IsNotNull();
			this.StoreBucket = storeBucket;
			this.EntityType = entityType;
		}
	}
}