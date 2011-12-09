﻿#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 

// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

namespace Lokad.Cqrs.Core.Inbox
{
    /// <summary>
    /// Describes retrieved message along with the queue name and some transport info.
    /// </summary>
    /// <remarks>It is used to send ACK/NACK back to the originating queue.</remarks>
    public sealed class EnvelopeTransportContext
    {
        public readonly object TransportMessage;
        public readonly byte[] Unpacked;
        public readonly string QueueName;
        public readonly string EnvelopeId;

        public EnvelopeTransportContext(object transportMessage, byte[] unpacked, string queueName, string envelopeId)
        {
            TransportMessage = transportMessage;
            QueueName = queueName;
            EnvelopeId = envelopeId;
            Unpacked = unpacked;
        }
    }
}