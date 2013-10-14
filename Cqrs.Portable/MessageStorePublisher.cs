using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using Lokad.Cqrs.AtomicStorage;
using System.Linq;
using Netco.Logging;

namespace Lokad.Cqrs
{
    /// <summary>
    /// Is responsible for publishing events from the event store
    /// </summary>
    public sealed class MessageStorePublisher
    {
	    private readonly string _name;
	    readonly MessageStore _store;
        readonly MessageSender _sender;
        readonly NuclearStorage _storage;
        readonly Predicate<StoreRecord> _recordShouldBePublished;
        readonly int _waitOnNoWorkInMilliseconds;

        public MessageStorePublisher( string name, MessageStore store, MessageSender sender, NuclearStorage storage, Predicate< StoreRecord > recordShouldBePublished, int waitOnNoWorkInMilliseconds = 500 )
        {
	        _name = name;
	        _store = store;
            _sender = sender;
            _storage = storage;
            _recordShouldBePublished = recordShouldBePublished;
            _waitOnNoWorkInMilliseconds = waitOnNoWorkInMilliseconds;
        }

        public sealed class PublishResult
        {
            public readonly long InitialPosition;
            public readonly long FinalPosition;
            public readonly bool Changed;
            public readonly bool HasMoreWork;

            public PublishResult(long initialPosition, long finalPosition, int requestedBatchSize)
            {
                InitialPosition = initialPosition;
                FinalPosition = finalPosition;


                Changed = InitialPosition != FinalPosition;
                // thanks to Slav Ivanyuk for fixing finding this typo
                HasMoreWork = (FinalPosition - InitialPosition) >= requestedBatchSize;
            }
        }

        PublishResult PublishEventsIfAnyNew(long initialPosition, int count)
        {
            var records = _store.EnumerateAllItems(initialPosition, count);
            var currentPosition = initialPosition;
            var publishedCount = 0;
            foreach (var e in records)
            {
                if (e.StoreVersion < currentPosition)
                {
                    throw new InvalidOperationException(string.Format("Retrieved record with position less than current. Store versions {0} <= current position {1}", e.StoreVersion, currentPosition));
                }
                if (_recordShouldBePublished(e))
                {
                    for (int i = 0; i < e.Items.Length; i++)
                    {
                        // predetermined id to kick in event deduplication
                        // if server crashes somehow
                        var envelopeId = "esp-" + e.StoreVersion + "-" + i;
                        var item = e.Items[i];

                        publishedCount += 1;
                        _sender.Send(item, envelopeId);
                    }
                }
                currentPosition = e.StoreVersion;
            }
            var result = new PublishResult(initialPosition, currentPosition, count);
            if (result.Changed)
            {
                SystemObserver.Notify("{0}\t[sys] Message store pointer moved to {1} ({2} published)", _name, result.FinalPosition, publishedCount);
            }
            return result;
        }

        public void Run(CancellationToken token)
        {
            long? currentPosition = null;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // reinitialize state from persistent store, if absent
                    if (currentPosition == null)
                    {
                        // if we fail here, we'll get into retry
                        currentPosition = _storage.GetSingletonOrNew<PublishCounter>().Position;
                    }
                    // publish events, if any
                    var publishResult = PublishEventsIfAnyNew(currentPosition.Value, 25);
                    if (publishResult.Changed)
                    {
                        // ok, we are changed, persist that to survive crashes
                        var output = _storage.UpdateSingletonEnforcingNew<PublishCounter>(c =>
                        {
                            if (c.Position != publishResult.InitialPosition)
                            {
                                throw new InvalidOperationException("Somebody wrote in parallel. Blow up!");
                            }
                            // we are good - update ES
                            c.Position = publishResult.FinalPosition;

                        });
                        currentPosition = output.Position;
                    }
                    if (!publishResult.HasMoreWork)
                    {
                        // wait for a few ms before polling ES again
                        token.WaitHandle.WaitOne(_waitOnNoWorkInMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    // we messed up, roll back
                    currentPosition = null;
                    this.Log().Error( ex, "Message publishing encountered error" );
                    token.WaitHandle.WaitOne(5000);
                }
            }
        }

        public void VerifyEventStreamSanity()
        {
            var result = _storage.GetSingletonOrNew<PublishCounter>();
            if (result.Position != 0)
            {
                SystemObserver.Notify( _name + "\tContinuing work with existing event store");
                return;
            }
            var store = _store.EnumerateAllItems(0, 100).ToArray();
            if (store.Length == 0)
            {
                SystemObserver.Notify( _name + "\tOpening new event stream");
                //_sender.SendHashed(new EventStreamStarted());
                return;
            }
            if (store.Length == 100)
            {
                throw new InvalidOperationException(
                    "It looks like event stream really went ahead (or storage pointer was reset). Do you REALLY mean to resend all events?");
            }
        }

        /// <summary>  Storage contract used to persist current position  </summary>
        [DataContract]
        public sealed class PublishCounter
        {
            [DataMember(Order = 1)]
            public long Position { get; set; }
        }
    }
}