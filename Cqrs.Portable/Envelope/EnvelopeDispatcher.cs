using System;
using System.Threading;
using Lokad.Cqrs.Envelope.Events;

namespace Lokad.Cqrs.Envelope
{
	public sealed class EnvelopeDispatcher
	{
		private readonly Action< ImmutableEnvelope > _action;
		private readonly IEnvelopeQuarantine _quarantine;
		private readonly DuplicationMemory _manager;
		private readonly IEnvelopeStreamer _streamer;
		private readonly string _dispatcherName;

		public EnvelopeDispatcher( Action< ImmutableEnvelope > action, IEnvelopeStreamer streamer, IEnvelopeQuarantine quarantine, DuplicationManager manager, string dispatcherName )
		{
			this._action = action;
			this._quarantine = quarantine;
			this._dispatcherName = dispatcherName;
			this._manager = manager.GetOrAdd( this );
			this._streamer = streamer;
		}

		public void Dispatch( byte[] message )
		{
			ImmutableEnvelope envelope;
			try
			{
				envelope = this._streamer.ReadAsEnvelopeData( message );
			}
			catch( Exception ex )
			{
				// permanent quarantine for serialization problems
				this._quarantine.Quarantine( message, ex );
				SystemObserver.Notify( new EnvelopeDeserializationFailed( ex, "dispatch" ) );
				return;
			}

			if( this._manager.DoWeRemember( envelope.EnvelopeId ) )
			{
				SystemObserver.Notify( new EnvelopeDuplicateDiscarded( envelope.EnvelopeId ) );
				return;
			}

			try
			{
				this._action( envelope );
				// non-essential but recommended
				this.CleanupDispatchedEnvelope( envelope );
			}
			catch( ThreadAbortException )
			{
				return;
			}
			catch( Exception ex )
			{
				if( this._quarantine.TryToQuarantine( envelope, ex ) )
				{
					SystemObserver.Notify( new EnvelopeQuarantined( ex, this._dispatcherName, envelope, message ) );
					// message quarantined. Swallow
					return;
				}
				// if we are on a persistent queue, this will tell to retry
				throw;
			}
		}

		private void CleanupDispatchedEnvelope( ImmutableEnvelope envelope )
		{
			try
			{
				this._manager.Memorize( envelope.EnvelopeId );
			}
			catch( ThreadAbortException )
			{
				// continue;
				throw;
			}
			catch( Exception ex )
			{
				SystemObserver.Notify( new EnvelopeCleanupFailed( ex, this._dispatcherName, envelope ) );
			}

			try
			{
				this._quarantine.TryRelease( envelope );
			}
			catch( ThreadAbortException )
			{
				// continue
				throw;
			}
			catch( Exception ex )
			{
				SystemObserver.Notify( new EnvelopeCleanupFailed( ex, this._dispatcherName, envelope ) );
			}

			SystemObserver.Notify( new EnvelopeDispatched( envelope, this._dispatcherName ) );
		}
	}
}