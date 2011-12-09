﻿using System;
using System.Collections.Generic;
using Lokad.Cqrs.Core.Inbox;

namespace Lokad.Cqrs.Core.Dispatch.Events
{
    [Serializable]
    public sealed class EnvelopeAcked : ISystemEvent
    {
        public string QueueName { get; private set; }
        public string EnvelopeId { get; private set; }
        public EnvelopeTransportContext Context { get; private set; }

        public EnvelopeAcked(string queueName, string envelopeId, EnvelopeTransportContext attributes)
        {
            QueueName = queueName;
            EnvelopeId = envelopeId;
            Context = attributes;
        }

        public override string ToString()
        {
            return string.Format("[{0}] acked at '{1}'", EnvelopeId, QueueName);
        }
    }
    [Serializable]
    public sealed class EnvelopeInboxFailed : ISystemEvent
    {
        public Exception Exception { get; private set; }
        public string InboxName { get; private set; }
        public EnvelopeInboxFailed(Exception exception, string inboxName)
        {
            Exception = exception;
            InboxName = inboxName;
        }

        public override string ToString()
        {
            return string.Format("Failed to retrieve message from {0}: {1}.", InboxName, Exception.Message);
        }
    }
}