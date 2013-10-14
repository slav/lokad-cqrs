using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CuttingEdge.Conditions;
using Lokad.Cqrs.AtomicStorage;

namespace Lokad.Cqrs.Projections
{
	public class ProjectionsService< TEvent > where TEvent : class
	{
		private readonly IDocumentStore _viewsStorage;
		private readonly string _name;
		private readonly MessageStore _messageStore;
		private readonly Func< IDocumentStore, IEnumerable< object > > _projectors;
		private readonly ProjectionsRebuilder< TEvent > _rebuilder;
		private ProjectionsInfo _projectionsInfo;
		private readonly BufferedDocumentStore _bufferedDocumentStore;

		public ProjectionsService( string name, MessageStore messageStore, IDocumentStore viewsStorage, Func< IDocumentStore, IEnumerable< object > > projectors )
		{
			Condition.Requires( name, "name" ).IsNotNullOrWhiteSpace();
			Condition.Requires( messageStore, "messageStore" ).IsNotNull();
			Condition.Requires( viewsStorage, "viewsStorage" ).IsNotNull();
			Condition.Requires( projectors, "projectors" ).IsNotNull();

			this._name = name;
			this._messageStore = messageStore;
			this._viewsStorage = viewsStorage;
			this._projectors = projectors;
			this._rebuilder = new ProjectionsRebuilder< TEvent >( name, viewsStorage, this._messageStore, s => this._projectors( s ) );
			this._bufferedDocumentStore = new BufferedDocumentStore( viewsStorage );
		}

		public async Task Init( CancellationToken token )
		{
			this._projectionsInfo = await this._rebuilder.CheckAndRebuildProjections( token );
		}

		public Task StartChasingEvents( CancellationToken token, int chasingProjectionsDelayInMilliseconds )
		{
			Condition.WithExceptionOnFailure< InvalidOperationException >().Requires( this._projectionsInfo ).IsNotNull( "ProjectionsService was not initialized" );

			var eventChasers = new List< EventsChasingService< TEvent > >();
			foreach( var projection in this._projectors( this._bufferedDocumentStore ) )
			{
				var projectionName = projection.GetType().Name;
				var projectionCheckpoint = this._projectionsInfo.Infos.First( pi => pi.ProjectionName == projectionName );
				var eventChasingService = new EventsChasingService< TEvent >( this._name, projectionCheckpoint, projection, this._bufferedDocumentStore.LatestWriter, this._viewsStorage, this._messageStore );
				eventChasers.Add( eventChasingService );
			}

			var chasingTasks = eventChasers.Select( ec => Task.Factory.StartNew( () => ec.ChaseEvents( token, chasingProjectionsDelayInMilliseconds ),
				TaskCreationOptions.LongRunning ) );
			return Task.WhenAll( chasingTasks );
		}
	}
}