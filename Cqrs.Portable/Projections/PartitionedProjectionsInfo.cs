using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Lokad.Cqrs.AtomicStorage;
using Netco.Logging;

namespace Lokad.Cqrs.Projections
{
	public class PartitionedProjectionsInfo
	{
		public List< ProjectionInfo > ReadyForUse { get; private set; }
		public List< ProjectionInfo > NeedRebuild { get; private set; }
		public List< ProjectionInfo > Obsolete { get; private set; }
		private static readonly ILogger _logger = NetcoLogger.GetLogger( typeof( PartitionedProjectionsInfo ) );

		public PartitionedProjectionsInfo( List< ProjectionInfo > readyForUse, List< ProjectionInfo > obsolete, List< ProjectionInfo > needRebuild )
		{
			this.ReadyForUse = readyForUse;
			this.Obsolete = obsolete;
			this.NeedRebuild = needRebuild;
		}

		public static PartitionedProjectionsInfo Partition( IEnumerable< ProjectionInfo > loadedInfos, IEnumerable< ProjectionInfo > generatedInfos, NuclearStorage documentStore )
		{
			var readyForUse = loadedInfos.Where( li => generatedInfos.Any( gi => li == gi ) ).ToList();
			var needRebuild = generatedInfos.Where( gi => readyForUse.All( r => r != gi ) ).ToList();

			// filter out all ready for use and any that needs to be rebuilt (by name, since hash is going to be different)
			var obsolete = loadedInfos.Where( li => readyForUse.All( r => r != li ) && needRebuild.Select( r => r.ProjectionName ).All( r => !r.Equals( li.ProjectionName, StringComparison.OrdinalIgnoreCase ) ) ).ToList();

			InitializeProjections( documentStore, generatedInfos, readyForUse, needRebuild );
			ValidateSanity( readyForUse, needRebuild, obsolete );

			return new PartitionedProjectionsInfo( readyForUse, obsolete, needRebuild );
		}

		private static void ValidateSanity( IEnumerable< ProjectionInfo > readyForUse, IEnumerable< ProjectionInfo > needRebuild, IEnumerable< ProjectionInfo > obsolete )
		{
			if( readyForUse.Select( pi => pi.ProjectionName ).Intersect( needRebuild.Select( pi => pi.ProjectionName ) ).Any() )
				throw new ProjectionsPartitioningException( "ReadyForUse intersects with NeedRebuild" );

			if( readyForUse.Select( pi => pi.ProjectionName ).Intersect( obsolete.Select( pi => pi.ProjectionName ) ).Any() )
				throw new ProjectionsPartitioningException( "ReadyForUse intersects with Obsolete" );

			if( needRebuild.Select( pi => pi.ProjectionName ).Intersect( obsolete.Select( pi => pi.ProjectionName ) ).Any() )
				throw new ProjectionsPartitioningException( "NeedRebuild intersects with Obsolete" );

			var uninitializedRebuildProjections = needRebuild.Where( p => !p.IsInitialized() ).ToList();
			if( uninitializedRebuildProjections.Count > 0 )
			{
				foreach( var projection in uninitializedRebuildProjections )
				{
					_logger.Error( "Rebuild projection {0} is uninitialized after partitioning", projection.ProjectionName );
				}
				throw new ProjectionsPartitioningException( "Rebuild projections were not fully initialized" );
			}

			var uninitializedReadyForUseExceptions = readyForUse.Where( p => !p.IsInitialized() ).ToList();
			if( uninitializedReadyForUseExceptions.Count > 0 )
			{
				foreach( var projection in uninitializedReadyForUseExceptions )
				{
					_logger.Error( "ReadyForUse projection {0} is uninitialized after partitioning", projection.ProjectionName );
				}
				throw new ProjectionsPartitioningException( "ReadyForUse projections were not fully initialized" );
			}
		}

		private static void InitializeProjections( NuclearStorage documentStore, IEnumerable< ProjectionInfo > generatedInfos, List< ProjectionInfo > readyForUse, List< ProjectionInfo > needRebuild )
		{
			needRebuild.ForEach( p => p.Initialize( documentStore ) );

			// for loaded projection initialize document store and projection
			// if there's a problem - switch to using correlating generated projection
			for( var i = 0; i < readyForUse.Count; i++ )
			{
				var loadedProjectionInfo = readyForUse[ i ];
				var relatedGeneratedProjection = generatedInfos.First( pi => pi == loadedProjectionInfo );
				try
				{
					loadedProjectionInfo.Initialize( documentStore );
				}
				catch( Exception x )
				{
					NetcoLogger.GetLogger( typeof( PartitionedProjectionsInfo ) ).Log().Error( x, "Error encountered while initializing projection info: {0}", loadedProjectionInfo.ProjectionName );
					// switch to using generated projection info for this projection to force rebuild
					readyForUse.RemoveAt( i );
					i--;
					relatedGeneratedProjection.Initialize( documentStore );
					needRebuild.Add( relatedGeneratedProjection );
				}
			}
		}
	}

	[ Serializable ]
	public class ProjectionsPartitioningException : Exception
	{
		//
		// For guidelines regarding the creation of new exception types, see
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
		// and
		//    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
		//

		public ProjectionsPartitioningException()
		{
		}

		public ProjectionsPartitioningException( string message ) : base( message )
		{
		}

		public ProjectionsPartitioningException( string message, Exception inner ) : base( message, inner )
		{
		}

		protected ProjectionsPartitioningException(
			SerializationInfo info,
			StreamingContext context ) : base( info, context )
		{
		}
	}
}