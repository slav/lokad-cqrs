#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lokad.Cqrs.Build
{
	public sealed class AzureStorageConfigurationBuilder
	{
		private readonly CloudStorageAccount _account;

		private Action< CloudQueueClient > _queueConfig;
		private Action< CloudBlobClient > _blobConfig;
		private Action< CloudTableClient > _tableConfig;
		private string _accountId;

		public AzureStorageConfigurationBuilder ConfigureQueueClient( Action< CloudQueueClient > configure,
			bool replaceOld = false )
		{
			if( replaceOld )
				this._queueConfig = configure;
			else
				this._queueConfig += configure;
			return this;
		}

		public AzureStorageConfigurationBuilder ConfigureBlobClient( Action< CloudBlobClient > configure,
			bool replaceOld = false )
		{
			if( replaceOld )
				this._blobConfig = configure;
			else
				this._blobConfig += configure;
			return this;
		}

		public AzureStorageConfigurationBuilder ConfigureTableClient( Action< CloudTableClient > configure,
			bool replaceOld = false )
		{
			if( replaceOld )
				this._tableConfig = configure;
			else
				this._tableConfig += configure;
			return this;
		}

		public void Named( string accountId )
		{
			this._accountId = accountId;
		}

		public AzureStorageConfigurationBuilder( CloudStorageAccount account )
		{
			// defaults
			// Retry up to 450 times with max 2 seconds delay between each retry (so wait up to 15 mins)
			var retry = new ExponentialRetry( TimeSpan.FromSeconds( 0.2 ), 15 );
			this._queueConfig = client =>
			{
				client.RetryPolicy = retry;
				client.MaximumExecutionTime = TimeSpan.FromMinutes( 15 );
				client.ServerTimeout = TimeSpan.FromMinutes( 15 );
			};
			this._blobConfig = client =>
			{
				client.RetryPolicy = retry;
				client.MaximumExecutionTime = TimeSpan.FromMinutes( 15 );
				client.ServerTimeout = TimeSpan.FromMinutes( 15 );
			};
			this._tableConfig = client => client.RetryPolicy = retry;

			if( account.Credentials.AccountName == "devstoreaccount1" )
			{
				this._blobConfig += client =>
				{
					// http://stackoverflow.com/questions/4897826/
					// local dev store works poorly with multi-thread uploads
					client.ParallelOperationThreadCount = 1;
				};
			}

			this._account = account;
			this._accountId = account.Credentials.AccountName;
		}

		internal AzureStorageConfig Build()
		{
			return new AzureStorageConfig( this._account,
				this._queueConfig,
				this._blobConfig,
				this._tableConfig,
				this._accountId );
		}
	}
}