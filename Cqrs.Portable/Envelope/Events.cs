#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Collections.Generic;
using Lokad.Cqrs.Evil;

namespace Lokad.Cqrs.Envelope.Events
{
	/// <summary>
	/// Raised when something goes wrong with the envelope deserialization (i.e.: unknown format or contract)
	/// </summary>
	[ Serializable ]
	public sealed class EnvelopeDeserializationFailed : ISystemEvent
	{
		public Exception Exception { get; private set; }
		public string Origin { get; private set; }

		public EnvelopeDeserializationFailed( Exception exception, string origin )
		{
			this.Exception = exception;
			this.Origin = origin;
		}

		public override string ToString()
		{
			return string.Format( "Failed to deserialize in '{0}': '{1}'", this.Origin, this.Exception.ToString() );
		}
	}

	/// <summary>
	/// Is published whenever an event is sent.
	/// </summary>
	[ Serializable ]
	public sealed class EnvelopeSent : ISystemEvent
	{
		public readonly string QueueName;
		public readonly string EnvelopeId;
		public readonly string MappedTypes;
		public readonly ICollection< MessageAttribute > Attributes;

		public EnvelopeSent( string queueName, string envelopeId, string mappedTypes, ICollection< MessageAttribute > attributes )
		{
			this.QueueName = queueName;
			this.EnvelopeId = envelopeId;
			this.MappedTypes = mappedTypes;
			this.Attributes = attributes;
		}

		public override string ToString()
		{
			return string.Format( "Sent {0} to '{1}' as [{2}]",
				this.MappedTypes,
				this.QueueName,
				this.EnvelopeId );
		}
	}

	[ Serializable ]
	public sealed class EnvelopeQuarantined : ISystemEvent
	{
		public Exception LastException { get; private set; }
		public string Dispatcher { get; private set; }
		public ImmutableEnvelope Envelope { get; private set; }
		public byte[] Message { get; private set; }

		public EnvelopeQuarantined( Exception lastException, string dispatcher, ImmutableEnvelope envelope, byte[] message )
		{
			this.LastException = lastException;
			this.Dispatcher = dispatcher;
			this.Envelope = envelope;
			this.Message = message;
		}

		public override string ToString()
		{
			return string.Format( "Quarantined '{0}'<{2}>: {1}", this.Envelope.EnvelopeId, this.LastException.Message, Convert.ToBase64String( Message ) );
		}

		public string GetName()
		{
			return ContractEvil.GetContractReference( Envelope.Message.GetType() );
		}
	}

	[ Serializable ]
	public sealed class EnvelopeCleanupFailed : ISystemEvent
	{
		public Exception Exception { get; private set; }
		public string Dispatcher { get; private set; }
		public ImmutableEnvelope Envelope { get; private set; }

		public EnvelopeCleanupFailed( Exception exception, string dispatcher, ImmutableEnvelope envelope )
		{
			this.Exception = exception;
			this.Dispatcher = dispatcher;
			this.Envelope = envelope;
		}
	}

	[ Serializable ]
	public sealed class EnvelopeDuplicateDiscarded : ISystemEvent
	{
		public string EnvelopeId { get; private set; }

		public EnvelopeDuplicateDiscarded( string envelopeId )
		{
			this.EnvelopeId = envelopeId;
		}

		public override string ToString()
		{
			return string.Format( "[{0}] duplicate discarded", this.EnvelopeId );
		}
	}

	[ Serializable ]
	public sealed class EnvelopeDispatched : ISystemEvent
	{
		public ImmutableEnvelope Envelope { get; private set; }
		public string Dispatcher { get; private set; }

		public EnvelopeDispatched( ImmutableEnvelope envelope, string dispatcher )
		{
			this.Envelope = envelope;
			this.Dispatcher = dispatcher;
		}

		public override string ToString()
		{
			return string.Format( "Envelope '{0}' was dispatched by '{1}'", this.Envelope.EnvelopeId, this.Dispatcher );
		}
	}
}