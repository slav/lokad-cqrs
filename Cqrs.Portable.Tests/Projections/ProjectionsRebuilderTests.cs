using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Lokad.Cqrs;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Evil;
using Lokad.Cqrs.Projections;
using Lokad.Cqrs.TapeStorage;
using Netco.Extensions;
using NUnit.Framework;
using ProtoBuf.Meta;

namespace Cqrs.Portable.Tests.Projections
{
	public class ProjectionsRebuilderTests
	{
		private ConcurrentDictionary< string, ConcurrentDictionary< string, byte[] > > _storeDictionary;
		private CancellationToken _token;
		private MemoryDocumentStore _documentStorage;
		private NuclearStorage _storage;
		private MessageStore _eventStore;

		[ SetUp ]
		public void Init()
		{
			this._storeDictionary = new ConcurrentDictionary< string, ConcurrentDictionary< string, byte[] > >();
			this._token = new CancellationToken();
			this._documentStorage = new MemoryDocumentStore( this._storeDictionary, new DocumentStrategy() );
			this._storage = new NuclearStorage( this._documentStorage );

			this._eventStore = new MessageStore( new MemoryAppendOnlyStore(), new DataSerializer( new List< Type > { typeof( TestEvent ), typeof( OnEvent ), typeof( OffEvent ) } ) );
		}

		[ Test ]
		public async void NewProjection_RebuildWithoutEvents()
		{
			//------------ Arrange
			var rebuilder = this.CreateRebuilder( 1, 2 );

			//------------ Act
			var builtProjectionsInfo = await rebuilder.CheckAndRebuildProjections( this._token );

			//------------ Assert
			// both projections were rebuilt
			var persistedProjectionsInfo = this.FetchProjectionsInfo( builtProjectionsInfo, 2 );
			this.CheckEventVersion( builtProjectionsInfo, persistedProjectionsInfo, new Dictionary< int, long > { { 1, 0 }, { 2, 0 } } );
			// No events, so view shouldn't have been saved
			this._storage.GetEntity< ViewMock1 >( 1 ).HasValue.Should().BeFalse();
			this._storage.GetEntity< ViewMock2 >( 2 ).HasValue.Should().BeFalse();
		}

		[ Test ]
		public async void NewProjection_RebuildWithEvents()
		{
			//------------ Arrange
			this.On();
			this.Off();
			this.On();
			var rebuilder = this.CreateRebuilder( 1, 2 );

			//------------ Act
			var builtProjectionsInfo = await rebuilder.CheckAndRebuildProjections( this._token );

			//------------ Assert
			var persistedProjectionsInfo = this.FetchProjectionsInfo( builtProjectionsInfo, 2 );
			this.CheckEventVersion( builtProjectionsInfo, persistedProjectionsInfo, new Dictionary< int, long > { { 1, 3 }, { 2, 3 } } );

			this._storage.GetEntityOrThrow< ViewMock1 >( 1 ).Flag.Should().BeTrue();
			this._storage.GetEntityOrThrow< ViewMock2 >( 2 ).Flag.Should().BeTrue();
		}

		[ Test ]
		public async void ExistingProjections_ViewAdded_NewViewRebuilt()
		{
			//------------ Arrange
			this.On();
			await this.BuildViews( 1, 2 );
			this.Off();
			var rebuilder = this.CreateRebuilder( 1, 2, 3 );

			//------------ Act
			var builtProjectionsInfo = await rebuilder.CheckAndRebuildProjections( this._token );

			//------------ Assert
			var persistedProjectionsInfo = this.FetchProjectionsInfo( builtProjectionsInfo, 3 );
			this.CheckEventVersion( builtProjectionsInfo, persistedProjectionsInfo, new Dictionary< int, long > { { 1, 1 }, { 2, 1 }, { 3, 2 } } );
			
			this._storage.GetEntityOrThrow< ViewMock1 >( 1 ).Flag.Should().BeTrue();
			this._storage.GetEntityOrThrow< ViewMock2 >( 2 ).Flag.Should().BeTrue();
			this._storage.GetEntityOrThrow< ViewMock3 >( 3 ).Flag.Should().BeFalse();
		}

		[ Test ]
		public async void ExistingProjections_ViewModified_ViewRebuilt()
		{
			//------------ Arrange
			this.On();
			await this.BuildViews( 1, 2 );
			this.Off();
			var rebuilder = this.CreateRebuilder( 1, 3 );

			//------------ Act
			var builtProjectionsInfo = await rebuilder.CheckAndRebuildProjections( this._token );

			//------------ Assert
			var persistedProjectionsInfo = this.FetchProjectionsInfo( builtProjectionsInfo, 2 );
			this.CheckEventVersion( builtProjectionsInfo, persistedProjectionsInfo, new Dictionary< int, long > { { 1, 1 }, { 3, 2 } } );

			this._storage.GetEntityOrThrow< ViewMock1 >( 1 ).Flag.Should().BeTrue();
			this._storage.GetEntity< ViewMock2 >( 2 ).HasValue.Should().BeFalse();
			this._storage.GetEntityOrThrow< ViewMock3 >( 3 ).Flag.Should().BeFalse();
		}

		/// <summary>
		/// Checks the event version.
		/// </summary>
		/// <param name="builtProjectionsInfo">The built projections info.</param>
		/// <param name="persistedProjectionsInfo">The persisted projections info.</param>
		/// <param name="expectations">The expectations in format { viewNumber, expectedEvents }.</param>
		private void CheckEventVersion( ProjectionsInfo builtProjectionsInfo, ProjectionsInfo persistedProjectionsInfo, IDictionary< int, long > expectations )
		{
			var expectedProjectionVersions = ExpectedProjectionVersion.Create( expectations );
			// check projections match expectations by name
			persistedProjectionsInfo.Infos.Should().HaveCount( expectations.Count );
			persistedProjectionsInfo.Infos.Should().Contain( p => expectedProjectionVersions.Any( e => e.CheckpointFile == p.ProjectionName ) );
			builtProjectionsInfo.Infos.Should().HaveCount( expectations.Count );
			builtProjectionsInfo.Infos.Should().Contain( p => expectedProjectionVersions.Any( e => e.CheckpointFile == p.ProjectionName ) );

			// load checkpoint for each projection and confirm they match
			foreach( var expectation in expectedProjectionVersions )
			{
				var persistedProjectionInfo = persistedProjectionsInfo.Infos.First( p => p.ProjectionName == expectation.CheckpointFile );
				var checkpoint = this._storage.GetEntityOrThrow< ProjectionCheckpoint >( persistedProjectionInfo.ProjectionName );
				checkpoint.LatestEventVersion.Should().Be( expectation.ExpectedEventVersion );

				var builtProjectionInfo = builtProjectionsInfo.Infos.First( p => p.ProjectionName == expectation.CheckpointFile );
				builtProjectionInfo.GetEventStreamVersion().Should().Be( expectation.ExpectedEventVersion );
				builtProjectionInfo.ProjectionName.Should().Be( expectation.ProjectionType.Name );
			}
		}

		private ProjectionsInfo FetchProjectionsInfo( ProjectionsInfo builtProjectionsInfo, int expectedCount )
		{
			var persistedProjectionsInfo = this._storage.GetEntityOrThrow< ProjectionsInfo >( "test" );
			builtProjectionsInfo.Should().Be( persistedProjectionsInfo );
			persistedProjectionsInfo.Infos.Should().HaveCount( expectedCount );
			return persistedProjectionsInfo;
		}

		private Task BuildViews( params int[] viewNumbers )
		{
			var rebuilder = this.CreateRebuilder( viewNumbers );
			return rebuilder.CheckAndRebuildProjections( this._token );
		}

		private ProjectionsRebuilder< TestEvent > CreateRebuilder( params int[] projectionNumbers )
		{
			return new ProjectionsRebuilder< TestEvent >( "test", this._documentStorage, this._eventStore, s =>
				ProjectionTestsHelper.GetProjections( s, projectionNumbers ) );
		}

		private void On( params int[] projectionNumbers )
		{
			if( projectionNumbers.Length == 0 )
				this.RecordEvent( new OnEvent { ViewName = "all" } );
			else
				projectionNumbers.ForEach( n => new OnEvent { ViewName = n.ToString( CultureInfo.InvariantCulture ) } );
		}

		private void Off( params int[] projectionNumbers )
		{
			if( projectionNumbers.Length == 0 )
				this.RecordEvent( new OffEvent { ViewName = "all" } );
			else
				projectionNumbers.ForEach( n => new OffEvent { ViewName = n.ToString( CultureInfo.InvariantCulture ) } );
		}

		private void RecordEvent( TestEvent e )
		{
			this._eventStore.RecordMessage( "testStream", new ImmutableEnvelope( Guid.NewGuid().ToString(), DateTime.UtcNow, e, new MessageAttribute[ 0 ] ) );
		}

		private class ExpectedProjectionVersion
		{
			public string CheckpointFile { get; private set; }
			public long ExpectedEventVersion { get; private set; }
			public Type ProjectionType { get; private set; }

			private ExpectedProjectionVersion( int projectionNumber, long expectedEventVersion )
			{
				var projection = ProjectionTestsHelper.GetProjection( projectionNumber );
				this.ProjectionType = projection.GetType();
				this.CheckpointFile = this.ProjectionType.Name;
				this.ExpectedEventVersion = expectedEventVersion;
			}

			public static IEnumerable< ExpectedProjectionVersion > Create( IEnumerable< KeyValuePair< int, long > > expectations )
			{
				return expectations.Select( e => new ExpectedProjectionVersion( e.Key, e.Value ) );
			}
		}
	}

	public class DataSerializer : AbstractMessageSerializer
	{
		public DataSerializer( ICollection< Type > knownTypes ) : base( knownTypes )
		{
			RuntimeTypeModel.Default[ typeof( DateTimeOffset ) ].Add( "m_dateTime", "m_offsetMinutes" );
		}

		protected override Formatter PrepareFormatter( Type type )
		{
			var name = ContractEvil.GetContractReference( type );
			var formatter = RuntimeTypeModel.Default.CreateFormatter( type );
			return new Formatter( name, type, formatter.Deserialize, ( o, stream ) => formatter.Serialize( stream, o ) );
		}
	}
}