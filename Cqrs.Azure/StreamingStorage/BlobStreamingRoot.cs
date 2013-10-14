#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System.Collections.Generic;
using System.Linq;
using Lokad.Cqrs.StreamingStorage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Lokad.Cqrs.Feature.StreamingStorage
{
	/// <summary>
	/// Windows Azure implementation of storage 
	/// </summary>
	public sealed class BlobStreamingRoot : IStreamRoot
	{
		private readonly CloudBlobClient _client;

		/// <summary>
		/// Initializes a new instance of the <see cref="BlobStreamingRoot"/> class.
		/// </summary>
		/// <param name="client">The client.</param>
		public BlobStreamingRoot( CloudBlobClient client )
		{
			this._client = client;
		}

		public IStreamContainer GetContainer( string name )
		{
			return new BlobStreamingContainer( this._client.GetBlobDirectory( name ) );
		}

		public IEnumerable< string > ListContainers( string prefix )
		{
			if( string.IsNullOrEmpty( prefix ) )
				return this._client.ListContainers().Select( c => c.Name );
			return this._client.ListContainers( prefix ).Select( c => c.Name );
		}
	}
}