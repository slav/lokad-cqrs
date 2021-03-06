﻿#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Partition;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Lokad.Cqrs.Feature.AzurePartition
{
	public sealed class StatelessAzureQueueWriter : IQueueWriter
	{
		public string Name { get; private set; }

		public void PutMessage( byte[] envelope )
		{
			var packed = this.PrepareCloudMessage( envelope );
			this._queue.AddMessage( packed );
		}

		private CloudQueueMessage PrepareCloudMessage( byte[] buffer )
		{
			if( buffer.Length < AzureMessageOverflows.CloudQueueLimit )
			{
				// write queue to queue
				return new CloudQueueMessage( buffer );
			}
			// ok, we didn't fit, so create reference queue
			var referenceId = DateTimeOffset.UtcNow.ToString( DateFormatInBlobName ) + "-" + Guid.NewGuid().ToString().ToLowerInvariant();
			this._cloudBlob.GetBlockBlobReference( referenceId ).UploadFromByteArray( buffer, 0, buffer.Length );
			var reference = new EnvelopeReference( this._cloudBlob.Uri.ToString(), referenceId );
			var blob = AzureMessageOverflows.SaveEnvelopeReference( reference );
			return new CloudQueueMessage( blob );
		}

		public StatelessAzureQueueWriter( CloudBlobContainer container, CloudQueue queue, string name )
		{
			this._cloudBlob = container;
			this._queue = queue;
			this.Name = name;
		}

		public static StatelessAzureQueueWriter Create( IAzureStorageConfig config, string name )
		{
			var queue = config.CreateQueueClient().GetQueueReference( name );
			var container = config.CreateBlobClient().GetContainerReference( name );
			var v = new StatelessAzureQueueWriter( container, queue, name );
			v.Init();
			return v;
		}

		public void Init()
		{
			this._queue.CreateIfNotExists();
			this._cloudBlob.CreateIfNotExists();
		}

		private const string DateFormatInBlobName = "yyyy-MM-dd-HH-mm-ss-ffff";
		private readonly CloudBlobContainer _cloudBlob;
		private readonly CloudQueue _queue;
	}
}