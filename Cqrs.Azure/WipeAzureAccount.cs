#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Lokad.Cqrs
{
	public static class WipeAzureAccount
	{
		private static Action< IAsyncResult > EndDelete( CloudBlobContainer c )
		{
			return result =>
			{
				try
				{
					c.EndDelete( result );
				}
				catch( StorageException ex )
				{
					if( ex.RequestInformation.HttpStatusCode == ( int )HttpStatusCode.NotFound )
						return;
					throw;
				}
			};
		}

		private static Action< IAsyncResult > EndDelete( CloudQueue c )
		{
			return result =>
			{
				try
				{
					c.EndDelete( result );
				}
				catch( StorageException ex )
				{
					if( ex.RequestInformation.HttpStatusCode == ( int )HttpStatusCode.NotFound )
						return;
					throw;
				}
			};
		}

		public static void Fast( Predicate< string > name, params IAzureStorageConfig[] configs )
		{
			var items = configs
				.Select( c => c.CreateBlobClient() )
				.SelectMany( c => c.ListContainers().Where( _ => name( _.Name ) ) )
				.Select( c => c.DeleteAsync() );

			var queues = configs
				.Select( c => c.CreateQueueClient() )
				.SelectMany( c => c.ListQueues().Where( _ => name( _.Name ) ) )
				.Select( c => c.DeleteAsync() );

			var all = items.Concat( queues ).ToArray();

			Task.WaitAll( all );
		}
	}
}