#region Copyright (c) 2012 LOKAD SAS. All rights reserved
// You must not remove this notice, or any other, from this software.
// This document is the property of LOKAD SAS and must not be disclosed
#endregion

using System;
using System.Globalization;
using Netco.Logging;

namespace Lokad.Cqrs
{
	public static class SystemObserver
	{
		private static IObserver< ISystemEvent >[] _observers = new IObserver< ISystemEvent >[ 0 ];
		private static ILogger _logger = NetcoLogger.GetLogger( typeof( SystemObserver ) );

		private sealed class ActionObserver : IObserver< ISystemEvent >
		{
			private readonly Action< ISystemEvent > _action;

			public ActionObserver( Action< ISystemEvent > action )
			{
				this._action = action;
			}

			public void OnNext( ISystemEvent value )
			{
				this._action( value );
			}

			public void OnError( Exception error )
			{
			}

			public void OnCompleted()
			{
			}
		}

		public static IObserver< ISystemEvent >[] Swap( params IObserver< ISystemEvent >[] swap )
		{
			var old = _observers;
			_observers = swap;
			return old;
		}

		public static void Put( Action< ISystemEvent > se )
		{
			_observers = new IObserver< ISystemEvent >[] { new ActionObserver( se ), };
		}

		public static void Notify( ISystemEvent @event )
		{
			foreach( var observer in _observers )
			{
				try
				{
					observer.OnNext( @event );
				}
				catch( Exception ex )
				{
					var message = string.Format( "Observer {0} failed with {1}", observer, ex );
					_logger.Error( ex, "Error while trying to notify about events: {0}", message );
				}
			}
		}

		public static void Notify( string message, params object[] args )
		{
			Notify( new MessageEvent( string.Format( CultureInfo.InvariantCulture, message, args ) ) );
		}

		public static void Error( Exception x, string message, params object[] args )
		{
			Notify( new ExceptionEvent( x, string.Format( CultureInfo.InvariantCulture, message, args ) ) );
		}

		public sealed class MessageEvent : ISystemEvent
		{
			public MessageEvent( string message )
			{
				this.Message = message;
			}

			public readonly string Message;

			public override string ToString()
			{
				return this.Message;
			}
		}

		public sealed class ExceptionEvent : ISystemEvent
		{
			public ExceptionEvent( Exception x, string message )
			{
				this.Message = message;
				this.Exception = x;
			}

			public readonly string Message;
			public readonly Exception Exception;

			public override string ToString()
			{
				return this.Message + Environment.NewLine + this.Exception;
			}
		}

		public static void Complete()
		{
			foreach( var observer in _observers )
			{
				observer.OnCompleted();
			}
		}
	}
}