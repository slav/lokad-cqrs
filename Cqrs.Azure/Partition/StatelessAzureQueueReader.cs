#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.IO;
using System.Threading;
using Lokad.Cqrs.Dispatch.Events;
using Lokad.Cqrs.Envelope;
using Lokad.Cqrs.Partition;
using Lokad.Cqrs.Partition.Events;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Lokad.Cqrs.Feature.AzurePartition
{
	public sealed class StatelessAzureQueueReader
	{
		private readonly TimeSpan _visibilityTimeout;

		private readonly AzureBlobDirectory _cloudBlob;
		private readonly Lazy< CloudQueue > _posionQueue;
		private readonly CloudQueue _queue;
		private readonly string _queueName;

		public string Name
		{
			get { return this._queueName; }
		}

		public StatelessAzureQueueReader(
			string name,
			CloudQueue primaryQueue,
			AzureBlobDirectory container,
			Lazy< CloudQueue > poisonQueue,
			TimeSpan visibilityTimeout )
		{
			this._cloudBlob = container;
			this._queue = primaryQueue;
			this._posionQueue = poisonQueue;
			this._queueName = name;
			this._visibilityTimeout = visibilityTimeout;
		}

		private bool _initialized;

		public void InitIfNeeded()
		{
			if( this._initialized )
				return;
			this._queue.CreateIfNotExists();
			this._cloudBlob.Container.CreateIfNotExists();
			this._initialized = true;
		}

		public GetEnvelopeResult TryGetMessage()
		{
			CloudQueueMessage message;
			try
			{
				message = this._queue.GetMessage( this._visibilityTimeout );
			}
			catch( ThreadAbortException )
			{
				// we are stopping
				return GetEnvelopeResult.Empty;
			}
			catch( Exception ex )
			{
				SystemObserver.Notify( new FailedToReadMessage( ex, this._queueName ) );
				return GetEnvelopeResult.Error();
			}

			if( null == message )
				return GetEnvelopeResult.Empty;

			try
			{
				var unpacked = this.DownloadPackage( message );
				return GetEnvelopeResult.Success( unpacked );
			}
			catch( StorageException ex )
			{
				SystemObserver.Notify( new FailedToAccessStorage( ex, this._queue.Name, message.Id ) );
				return GetEnvelopeResult.Retry;
			}
			catch( Exception ex )
			{
				SystemObserver.Notify( new MessageInboxFailed( ex, this._queue.Name, message.Id ) );
				// new poison details
				this._posionQueue.Value.AddMessage( message );
				this._queue.DeleteMessage( message );
				return GetEnvelopeResult.Retry;
			}
		}

		private MessageTransportContext DownloadPackage( CloudQueueMessage message )
		{
			var buffer = message.AsBytes;

			EnvelopeReference reference;
			if( AzureMessageOverflows.TryReadAsEnvelopeReference( buffer, out reference ) )
			{
				if( reference.StorageContainer != this._cloudBlob.Uri.ToString() )
					throw new InvalidOperationException( "Wrong container used!" );
				var blob = this._cloudBlob.GetBlockBlobReference( reference.StorageReference );
				using( var ms = new MemoryStream() )
				{
					blob.DownloadToStream( ms );
					buffer = ms.ToArray();
				}
			}
			return new MessageTransportContext( message, buffer, this._queueName );
		}

		/// <summary>
		/// ACKs the message by deleting it from the queue.
		/// </summary>
		/// <param name="message">The message context to ACK.</param>
		public void AckMessage( MessageTransportContext message )
		{
			if( message == null )
				throw new ArgumentNullException( "message" );
			this._queue.DeleteMessage( ( CloudQueueMessage )message.TransportMessage );
		}
	}
}