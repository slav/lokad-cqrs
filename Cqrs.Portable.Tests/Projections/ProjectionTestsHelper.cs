using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Projections;

namespace Cqrs.Portable.Tests.Projections
{
	public class ProjectionTestsHelper
	{
		public static Type GetView( int viewNumber )
		{
			Type viewType;
			switch( viewNumber )
			{
				case 1:
					viewType = typeof( ViewMock1 );
					break;
				case 2:
					viewType = typeof( ViewMock2 );
					break;
				case 3:
					viewType = typeof( ViewMock3 );
					break;
				case 4:
					viewType = typeof( ViewMock4 );
					break;
				case 5:
					viewType = typeof( ViewMock5 );
					break;
				case 6:
					viewType = typeof( ViewMock6 );
					break;
				case 7:
					viewType = typeof( ViewMock7 );
					break;
				default:
					throw new InvalidOperationException( "Unknown view number" );
			}
			return viewType;
		}

		public static IEnumerable< Type > GetViews( params int[] viewNumbers )
		{
			return viewNumbers.Select( GetView );
		}

		public static IEnumerable< ViewInfo > GetViewInfos( params int[] viewNumbers )
		{
			return viewNumbers.Select( n => new ViewInfo( GetView( n ), "view" + n ) );
		}

		public static IEnumerable< object > GetProjections( IDocumentStore store, params int[] projectionNumbers )
		{
			foreach( var projectionNumber in projectionNumbers )
			{
				yield return GetProjection( projectionNumber, store );
			}
		}

		public static object GetProjection( int projectionNumber, IDocumentStore store = null )
		{
			switch( projectionNumber )
			{
				case 1:
					return new ProjectionMock1( store != null ? store.GetWriter< int, ViewMock1 >() : null );
				case 2:
					return new ProjectionMock2( store != null ? store.GetWriter< int, ViewMock2 >() : null );
				case 3:
					return new ProjectionMock3( store != null ? store.GetWriter< int, ViewMock3 >() : null );
				case 4:
					return new ProjectionMock4( store != null ? store.GetWriter< int, ViewMock4 >() : null );
				case 5:
					return new ProjectionMock5( store != null ? store.GetWriter< int, ViewMock5 >() : null );
				case 6:
					return new ProjectionMock6( store != null ? store.GetWriter< int, ViewMock6 >() : null );
				case 7:
					return new ProjectionMock7( store != null ? store.GetWriter< int, ViewMock7 >() : null );
				default:
					throw new InvalidOperationException( "Unknown projection number" );
			}
		}
	}

	public class TestEvent
	{
	}

	[ DataContract ]
	public class OnEvent : TestEvent
	{
		[ DataMember( Order = 1 ) ]
		public string ViewName { get; set; }
	}

	[ DataContract ]
	public class OffEvent : TestEvent
	{
		[ DataMember( Order = 1 ) ]
		public string ViewName { get; set; }
	}

	public class ProjectionMock1
	{
		private readonly IDocumentWriter< int, ViewMock1 > _writer;

		public ProjectionMock1( IDocumentWriter< int, ViewMock1 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "1" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 1, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "1" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 1, v => v.Flag = false );
		}
	}

	public class ProjectionMock2
	{
		private readonly IDocumentWriter< int, ViewMock2 > _writer;

		public ProjectionMock2( IDocumentWriter< int, ViewMock2 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "2" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 2, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "2" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 2, v => v.Flag = false );
		}
	}

	public class ProjectionMock3
	{
		private readonly IDocumentWriter< int, ViewMock3 > _writer;

		public ProjectionMock3( IDocumentWriter< int, ViewMock3 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "3" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 3, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "3" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 3, v => v.Flag = false );
		}
	}

	public class ProjectionMock4
	{
		private readonly IDocumentWriter< int, ViewMock4 > _writer;

		public ProjectionMock4( IDocumentWriter< int, ViewMock4 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "4" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 4, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "4" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 4, v => v.Flag = false );
		}
	}

	public class ProjectionMock5
	{
		private readonly IDocumentWriter< int, ViewMock5 > _writer;

		public ProjectionMock5( IDocumentWriter< int, ViewMock5 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "5" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 5, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "5" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 5, v => v.Flag = false );
		}
	}

	public class ProjectionMock6
	{
		private readonly IDocumentWriter< int, ViewMock6 > _writer;

		public ProjectionMock6( IDocumentWriter< int, ViewMock6 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "6" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 6, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "6" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 6, v => v.Flag = false );
		}
	}

	public class ProjectionMock7
	{
		private readonly IDocumentWriter< int, ViewMock7 > _writer;

		public ProjectionMock7( IDocumentWriter< int, ViewMock7 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "7" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 7, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "7" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 7, v => v.Flag = false );
		}
	}

	[ DataContract ]
	public class ViewMock1
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}

	[ DataContract ]
	public class ViewMock2
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}

	[ DataContract ]
	public class ViewMock3
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}

	[ DataContract ]
	public class ViewMock4
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}

	[ DataContract ]
	public class ViewMock5
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}

	[ DataContract ]
	public class ViewMock6
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}

	[ DataContract ]
	public class ViewMock7
	{
		[ DataMember( Order = 1 ) ]
		public bool Flag { get; set; }
	}
}

namespace Cqrs.Portable.Tests.Projections.DifferentHash
{
	public class ProjectionMock2
	{
		private readonly IDocumentWriter< int, ViewMock1 > _writer;

		public ProjectionMock2( IDocumentWriter< int, ViewMock1 > writer )
		{
			this._writer = writer;
		}

		public void When( OnEvent e )
		{
			if( e.ViewName.Contains( "1" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 2, v => v.Flag = true );
		}

		public void When( OffEvent e )
		{
			if( e.ViewName.Contains( "1" ) || e.ViewName.Contains( "all" ) )
				this._writer.UpdateEnforcingNew( 2, v => v.Flag = false );
		}
	}
}