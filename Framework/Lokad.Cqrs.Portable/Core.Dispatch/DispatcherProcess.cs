#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 

// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Cqrs.Core.Dispatch.Events;
using Lokad.Cqrs.Core.Inbox;

namespace Lokad.Cqrs.Core.Dispatch
{
    /// <summary>
    /// Engine process that coordinates pulling messages from queues and
    /// dispatching them to the specified handlers
    /// </summary>
    public sealed class DispatcherProcess : IEngineProcess
    {
        readonly Action<ImmutableEnvelope> _dispatcher;
        readonly ISystemObserver _observer;
        readonly IPartitionInbox _inbox;
        readonly IEnvelopeQuarantine _quarantine;
        readonly IEnvelopeStreamer _streamer;

        public DispatcherProcess(
            ISystemObserver observer,
            Action<ImmutableEnvelope> dispatcher, 
            IPartitionInbox inbox,
            IEnvelopeQuarantine quarantine,
            MessageDuplicationManager manager, 
            IEnvelopeStreamer streamer)
        {
            _dispatcher = dispatcher;
            _quarantine = quarantine;
            _streamer = streamer;
            _observer = observer;
            _inbox = inbox;
            _memory = manager.GetOrAdd(this);
        }

        public void Dispose()
        {
            _disposal.Dispose();
        }

        public void Initialize()
        {
            _inbox.Init();
        }

        readonly CancellationTokenSource _disposal = new CancellationTokenSource();
        readonly MessageDuplicationMemory _memory;

        public Task Start(CancellationToken token)
        {
            return Task.Factory
                .StartNew(() =>
                    {
                        try
                        {
                            ReceiveMessages(token);
                        }
                        catch(ObjectDisposedException)
                        {
                            // suppress
                        }
                    }, token);
        }

        void ReceiveMessages(CancellationToken outer)
        {
            using (var source = CancellationTokenSource.CreateLinkedTokenSource(_disposal.Token, outer))
            {
                while (true)
                {
                    EnvelopeTransportContext context;
                    try
                    {
                        if (!_inbox.TakeMessage(source.Token, out context))
                        {
                            // we didn't retrieve message within the token lifetime.
                            // it's time to shutdown the server
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // unexpected but possible: retry
                        _observer.Notify(new EnvelopeInboxFailed(ex, _inbox.ToString()));
                        continue;
                    }
                    
                    try
                    {
                        ProcessMessage(context);
                    }
                    catch (ThreadAbortException)
                    {
                         // Nothing. we are being shutdown
                    }
                    catch(Exception ex)
                    {
                        var e = new DispatchRecoveryFailed(ex, context, context.QueueName);
                        _observer.Notify(e);
                    }
                }
            }
        }

        void ProcessMessage(EnvelopeTransportContext context) 
        {
            var processed = false;
            try
            {
                if (_memory.DoWeRemember(context.EnvelopeId))
                {
                    _observer.Notify(new EnvelopeDuplicateDiscarded(context.QueueName, context.EnvelopeId));
                }
                else
                {
                    var envelope = _streamer.ReadAsEnvelopeData(context.Unpacked);
                    _dispatcher(envelope);
                    _memory.Memorize(context.EnvelopeId);
                }

                processed = true;
            }
            catch (ThreadAbortException)
            {
                // we are shutting down. Stop immediately
                return;
            }
            catch (Exception dispatchEx)
            {
                // if the code below fails, it will just cause everything to be reprocessed later,
                // which is OK (duplication manager will handle this)

                _observer.Notify(new EnvelopeDispatchFailed(context, context.QueueName, dispatchEx));
                // quarantine is atomic with the processing
                if (_quarantine.TryToQuarantine(context, dispatchEx))
                {
                    _observer.Notify(new EnvelopeQuarantined(dispatchEx, context, context.QueueName));
                    // acking message is the last step!
                    _inbox.AckMessage(context);
                }
                else
                {
                    _inbox.TryNotifyNack(context);
                }
            }
            try
            {
                if (processed)
                {
                    // 1st step - dequarantine, if present
                    _quarantine.TryRelease(context);
                    // 2nd step - ack.
                    _inbox.AckMessage(context);
                    // 3rd - notify.
                    _observer.Notify(new EnvelopeAcked(context.QueueName, context.EnvelopeId, context));
                }
            }
            catch (ThreadAbortException)
            {
                // nothing. We are going to sleep
            }
            catch (Exception ex)
            {
                // not a big deal. Message will be processed again.
                _observer.Notify(new EnvelopeAckFailed(ex, context.EnvelopeId, context.QueueName));
            }
        }
    }
}