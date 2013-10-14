using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Projections;
using NSubstitute;
using NUnit.Framework;
using ProtoBuf;

namespace Cqrs.Portable.Tests.Projections
{
	public class ProjectionInfoTests
	{
		public class GetLastEventVersion
		{
			private ProjectionCheckpoint _savedCheckpoint;
			private IDocumentStore _storeMock;
			private NuclearStorage _storage;

			[ SetUp ]
			public void Init()
			{
				this._storeMock = Substitute.For< IDocumentStore >();
				this._storage = new NuclearStorage( this._storeMock );
			}

			[ Test ]
			public void NewInfoObject()
			{
				//------------ Act
				var info = new ProjectionInfo( ProjectionTestsHelper.GetViewInfos( 1 ), ProjectionTestsHelper.GetProjection( 1 ), new DocumentStrategy() );

				//------------ Assert
				info.GetEventStreamVersion().Should().Be( 0 );
			}

			[ Test ]
			public void LoadedInfo()
			{
				//------------ Arrange
				this.MockSaveCheckpoint();
				this.MockLoadCheckpoint();

				var originalInfo = new ProjectionInfo( ProjectionTestsHelper.GetViewInfos( 1 ), ProjectionTestsHelper.GetProjection( 1 ), new DocumentStrategy() );
				originalInfo.Initialize( this._storage );
				const int checkpoint = 1521516;
				originalInfo.UpdateEventStreamVersion( checkpoint ).Wait();

				//------------ Act
				var info = this.ReloadProjectionInfo( originalInfo );

				//------------ Assert
				info.GetEventStreamVersion().Should().Be( checkpoint );
			}

			private void MockSaveCheckpoint()
			{
				this._storeMock.GetWriter< object, ProjectionCheckpoint >().SaveAsync( Arg.Any< string >(), Arg.Any< ProjectionCheckpoint >() ).Returns( c =>
				{
					this._savedCheckpoint = ( ProjectionCheckpoint )c[ 1 ];
					return Task.FromResult( true );
				} );
			}

			private void MockLoadCheckpoint()
			{
				ProjectionCheckpoint checkpointOut;
				this._storeMock.GetReader< object, ProjectionCheckpoint >().TryGet( Arg.Any< string >(), out checkpointOut ).Returns( c =>
				{
					c[ 1 ] = this._savedCheckpoint;
					return true;
				} );
			}

			private ProjectionInfo ReloadProjectionInfo( ProjectionInfo originalInfo )
			{
				ProjectionInfo info;
				using( var ms = new MemoryStream() )
				{
					Serializer.Serialize( ms, originalInfo );
					ms.Flush();
					ms.Position = 0;

					info = Serializer.Deserialize< ProjectionInfo >( ms );
				}
				info.Initialize( this._storage );
				return info;
			}
		}
	}
}