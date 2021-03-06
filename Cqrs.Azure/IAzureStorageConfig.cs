﻿#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lokad.Cqrs
{
	public interface IAzureStorageConfig
	{
		string AccountName { get; }

		CloudStorageAccount UnderlyingAccount { get; }

		CloudBlobClient CreateBlobClient();
		CloudQueueClient CreateQueueClient();
		CloudTableClient CreateTableClient();
	}
}