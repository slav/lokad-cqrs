#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Threading;
using Lokad.Cqrs.Feature.AzurePartition;
using Lokad.Cqrs.Partition;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;

namespace Cqrs.Azure.Tests.Partition
{
    public class StatelessAzureQueueReaderTest
    {
        private StatelessAzureQueueReader _reader;

        //[SetUp]
        //public void Setup()
        //{
        //    var name = Guid.NewGuid().ToString().ToLowerInvariant();
        //    CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;
        //    var queue = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference(name);
        //    var container = cloudStorageAccount.CreateCloudBlobClient().GetBlobDirectoryReference(name);
        //    var blobContainer = cloudStorageAccount.CreateCloudBlobClient().GetContainerReference(name);
        //     var poisonQueue = new Lazy<CloudQueue>(() =>
        //        {
        //            var queueReference = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference(name + "-poison");
        //            queueReference.CreateIfNotExist();
        //            return queueReference;
        //        }, LazyThreadSafetyMode.ExecutionAndPublication);
        //    _reader = new StatelessAzureQueueReader("Name", queue, container, poisonQueue, TimeSpan.FromMinutes(1));
        //    var writer=new StatelessAzureQueueWriter()
        //}

        //[Test, ExpectedException(typeof(ArgumentNullException))]
        //public void when_ack_null_message()
        //{
        //    _reader.AckMessage(null);
        //}

        //[Test]
        //public void when_ack_messages_by_name()
        //{
        //    var message=new MessageTransportContext()
        //    _reader.AckMessage();
        //}
    }

    public class AzureQueueReaderTest
    {
        //public void  
    }
}