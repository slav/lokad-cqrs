#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 

// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence

#endregion

using System.Collections.Concurrent;
using System.Linq;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Partition;

// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global
namespace Lokad.Cqrs
{

    public static class NonserializingMemoryStorage
    {
        public static NonserializingMemoryStorageConfig CreateConfig()
        {
            return new NonserializingMemoryStorageConfig();
        }

        /// <summary>
        /// Creates the simplified nuclear storage wrapper around Atomic storage.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="strategy">The atomic storage strategy.</param>
        /// <returns></returns>
        public static NuclearStorage CreateNuclear(this NonserializingMemoryStorageConfig dictionary, IDocumentStrategy strategy)
        {
            var container = new NonserializingMemoryDocumentStore(dictionary.Data, strategy);
            return new NuclearStorage(container);
        }
        

        public static MemoryQueueReader CreateInbox(this NonserializingMemoryStorageConfig storageConfig,  params string[] queueNames)
        {
            var queues = queueNames
                .Select(n => storageConfig.Queues.GetOrAdd(n, s => new BlockingCollection<byte[]>()))
                .ToArray();

            return new MemoryQueueReader(queues, queueNames);
        }

        public static IQueueWriter CreateQueueWriter(this NonserializingMemoryStorageConfig storageConfig, string queueName)
        {
            var collection = storageConfig.Queues.GetOrAdd(queueName, s => new BlockingCollection<byte[]>());
            return new MemoryQueueWriter(collection, queueName);
        }

        public static MessageSender CreateMessageSender(this NonserializingMemoryStorageConfig storageConfig, IEnvelopeStreamer streamer, string queueName)
        {
            return new MessageSender(streamer, CreateQueueWriter(storageConfig, queueName));
        }
    }
}