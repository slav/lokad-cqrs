﻿#region Copyright (c) 2006-2012 LOKAD SAS. All rights reserved
// You must not remove this notice, or any other, from this software.
// This document is the property of LOKAD SAS and must not be disclosed
#endregion

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	public interface IDocumentStore
	{
		IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >();
		IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >();
		IDocumentStrategy Strategy { get; }
		IEnumerable< DocumentRecord > EnumerateContents( string bucket );
		void WriteContents( string bucket, IEnumerable< DocumentRecord > records );
		Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token );
		void Reset( string bucket );
		Task ResetAsync( string bucket );
	}

	public sealed class DocumentRecord
	{
		/// <summary>
		/// Path of the view in the subfolder, using '/' as split on all platforms
		/// </summary>
		public readonly string Key;

		public readonly Func< byte[] > Read;

		public DocumentRecord( string key, Func< byte[] > read )
		{
			this.Key = key;
			this.Read = read;
		}
	}
}