#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Threading;
using Cqrs.Azure.Tests.AtomicStorage;
using Lokad.Cqrs.AppendOnly;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Feature.AzurePartition;
using Lokad.Cqrs.Feature.AzurePartition.Inbox;
using Lokad.Cqrs.Feature.StreamingStorage;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using NUnit.Framework;
using SaaS;
using SaaS.Wires;

namespace Cqrs.Azure.Tests
{
    public class BaseTestClass
    {
        public BlobStreamingContainer _streamContainer;
        public StatelessAzureQueueReader _statelessReader;
        public StatelessAzureQueueWriter _queueWriter;
        public AzureQueueReader _queueReader;
        public IDocumentStore _store;
        public BlobAppendOnlyStore _appendOnly;
        public  string name =Guid.NewGuid().ToString().ToLowerInvariant();
        public AzureAtomicWriter<Guid, TestView> _writer;
        public DocumentStrategy _documentStrategy;
        public AzureAtomicReader<Guid, TestView> _reader;
        private CloudBlobClient _cloudBlobClient;
        private BlobStreamingContainer _sampleDocStreamContainer;


        [SetUp]
        public void Setup()
        {
            CloudStorageAccount cloudStorageAccount = CloudStorageAccount.DevelopmentStorageAccount;

            _cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
            var queue = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference(name);
            var container = _cloudBlobClient.GetBlobDirectoryReference(name);
            _streamContainer = new BlobStreamingContainer(container);
            _streamContainer.Create();

            var sampleDocContainer = _cloudBlobClient.GetBlobDirectoryReference(Conventions.DocsFolder);
            _sampleDocStreamContainer = new BlobStreamingContainer(sampleDocContainer);
            _sampleDocStreamContainer.Create();

            var blobContainer = _cloudBlobClient.GetContainerReference(name);
            var poisonQueue = new Lazy<CloudQueue>(() =>
            {
                var queueReference = cloudStorageAccount.CreateCloudQueueClient().GetQueueReference(name + "-poison");
                queueReference.CreateIfNotExist();
                return queueReference;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
            _statelessReader = new StatelessAzureQueueReader("azure-read-write-message", queue, container, poisonQueue, TimeSpan.FromMinutes(1));
            _queueReader = new AzureQueueReader(new[] { _statelessReader }, x => TimeSpan.FromMinutes(x));
            _queueWriter = new StatelessAzureQueueWriter(blobContainer, queue, "azure-read-write-message");
            _queueWriter.Init();

            _appendOnly = new BlobAppendOnlyStore(blobContainer);
            _appendOnly.InitializeWriter();

            _documentStrategy = new DocumentStrategy();
            _store = new AzureDocumentStore(_documentStrategy, _cloudBlobClient);
            _writer = new AzureAtomicWriter<Guid, TestView>(_cloudBlobClient, _documentStrategy);
            _reader = new AzureAtomicReader<Guid, TestView>(_cloudBlobClient, _documentStrategy);
        }


        [TearDown]
        public void Teardown()
        {
            _appendOnly.Close();
            _streamContainer.Delete();
            _sampleDocStreamContainer.Delete();
        }
    }
}