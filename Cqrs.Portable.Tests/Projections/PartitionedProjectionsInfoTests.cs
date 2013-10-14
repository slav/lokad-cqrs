using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Projections;
using NSubstitute;
using NUnit.Framework;
using ProtoBuf;

namespace Cqrs.Portable.Tests.Projections
{
	public class PartitionedProjectionsInfoTests
	{
		private IDocumentStore _storeMock;
		private NuclearStorage _storage;

		[ SetUp ]
		public void Init()
		{
			this._storeMock = Substitute.For< IDocumentStore >();

			ProjectionCheckpoint chetkpointOut;
			this._storeMock.GetReader< object, ProjectionCheckpoint >().TryGet( Arg.Any< object >(), out chetkpointOut ).Returns( x =>
			{
				x[ 1 ] = new ProjectionCheckpoint();
				return true;
			} );
			this._storage = new NuclearStorage( this._storeMock );
		}

		[ Test ]
		public void DifferentProjections()
		{
			//------------ Arrange
			var loadedProjections = LoadInfoList( 1, 2 );
			var generatedProjections = GenerateInfoList( 3, 4 );
			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			projectionsPartition.ReadyForUse.Should().BeEmpty();
			projectionsPartition.NeedRebuild.Should().Equal( GenerateInfoList( 3, 4 ) );
			projectionsPartition.Obsolete.Should().Equal( GenerateInfoList( 1, 2 ) );
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		[ Test ]
		public void SameProjections()
		{
			//------------ Arrange
			var loadedProjections = LoadInfoList( 1, 2 );
			var generatedProjections = GenerateInfoList( 1, 2 );
			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			projectionsPartition.ReadyForUse.Should().Equal( GenerateInfoList( 1, 2 ) );
			projectionsPartition.NeedRebuild.Should().BeEmpty();
			projectionsPartition.Obsolete.Should().BeEmpty();
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		[ Test ]
		public void ProjectionsIntersect()
		{
			//------------ Arrange
			var loadedProjections = LoadInfoList( 1, 2, 6, 7 );
			var generatedProjections = GenerateInfoList( 7, 6, 3, 4 );
			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			projectionsPartition.ReadyForUse.Should().BeEquivalentTo( GenerateInfoList( 6, 7 ) );
			projectionsPartition.NeedRebuild.Should().Equal( GenerateInfoList( 3, 4 ) );
			projectionsPartition.Obsolete.Should().Equal( GenerateInfoList( 1, 2 ) );
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		[ Test ]
		public void LoadedProjectionEmpty()
		{
			//------------ Arrange
			var loadedProjections = Enumerable.Empty< ProjectionInfo >();
			var generatedProjections = GenerateInfoList( 1, 2 );
			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			projectionsPartition.ReadyForUse.Should().BeEmpty();
			projectionsPartition.NeedRebuild.Should().Equal( GenerateInfoList( 1, 2 ) );
			projectionsPartition.Obsolete.Should().BeEmpty();
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		[ Test ]
		public void GeneratedProjectionEmpty()
		{
			//------------ Arrange
			var loadedProjections = LoadInfoList( 1, 2 );
			var generatedProjections = Enumerable.Empty< ProjectionInfo >();
			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			projectionsPartition.ReadyForUse.Should().BeEmpty();
			projectionsPartition.NeedRebuild.Should().BeEmpty();
			projectionsPartition.Obsolete.Should().Equal( GenerateInfoList( 1, 2 ) );
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		[ Test ]
		public void ExceptionOnCheckpointLoad_GoesToRebuildAndInitialized()
		{
			//------------ Arrange
			var loadedProjections = LoadInfoList( 1, 2, 5, 6, 7 );
			var generatedProjections = GenerateInfoList( 3, 4, 1, 2, 7 );

			ProjectionCheckpoint checkpointOut;
			this._storeMock.GetReader< object, ProjectionCheckpoint >().TryGet( Arg.Is< string >( s => s.Contains( "7" ) ), out checkpointOut ).Returns( arg =>
			{
				throw new Exception( "test" );
			} );

			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			projectionsPartition.ReadyForUse.Should().Equal( GenerateInfoList( 1, 2 ) );
			projectionsPartition.NeedRebuild.Should().Equal( GenerateInfoList( 3, 4, 7 ) );
			projectionsPartition.Obsolete.Should().Equal( GenerateInfoList( 5, 6 ) );
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		[ Test ]
		public void ProjectionHashChanges_ProjectionRebuilt()
		{
			//------------ Arrange
			var loadedProjections = LoadInfoList( 2 );
			var generatedProjections = new List< ProjectionInfo > { new ProjectionInfo( ProjectionTestsHelper.GetViewInfos( 2 ), new DifferentHash.ProjectionMock2( null ), new DocumentStrategy() ) };

			//------------ Act
			var projectionsPartition = PartitionedProjectionsInfo.Partition( loadedProjections, generatedProjections, this._storage );

			//------------ Assert
			var expectedGeneratedProjections = new List< ProjectionInfo > { new ProjectionInfo( ProjectionTestsHelper.GetViewInfos( 2 ), new DifferentHash.ProjectionMock2( null ), new DocumentStrategy() ) };

			projectionsPartition.ReadyForUse.Should().BeEmpty();
			projectionsPartition.NeedRebuild.Should().Equal( expectedGeneratedProjections );
			projectionsPartition.Obsolete.Should().BeEmpty();
			AssertProjectionInfosInitialized( projectionsPartition );
		}

		private static void AssertProjectionInfosInitialized( PartitionedProjectionsInfo projectionsPartition )
		{
			if( projectionsPartition.ReadyForUse.Count > 0 )
				projectionsPartition.ReadyForUse.Should().OnlyContain( pi => pi.IsInitialized() );

			if( projectionsPartition.NeedRebuild.Count > 0 )
				projectionsPartition.NeedRebuild.Should().OnlyContain( pi => pi.IsInitialized() );

			if( projectionsPartition.Obsolete.Count > 0 )
				projectionsPartition.Obsolete.Should().OnlyContain( pi => !pi.IsInitialized() );
		}

		private static IEnumerable< ProjectionInfo > GenerateInfoList( params int[] viewNumbers )
		{
			var projectionInfos = new List< ProjectionInfo >( viewNumbers.Length );
			projectionInfos.AddRange( viewNumbers.Select( CreateInfo ) );
			return projectionInfos;
		}

		private static IEnumerable< ProjectionInfo > LoadInfoList( params int[] viewNumbers )
		{
			var generatedInfos = GenerateInfoList( viewNumbers );
			var projectionsInfo = new ProjectionsInfo();
			projectionsInfo.Infos.UnionWith( generatedInfos );
			using( var ms = new MemoryStream() )
			{
				Serializer.Serialize( ms, projectionsInfo );
				ms.Flush();
				ms.Position = 0;
				return Serializer.Deserialize< ProjectionsInfo >( ms ).Infos;
			}
		}

		private static ProjectionInfo CreateInfo( int i )
		{
			return new ProjectionInfo( ProjectionTestsHelper.GetViewInfos( i ), ProjectionTestsHelper.GetProjection( i ), new DocumentStrategy() );
		}
	}
}