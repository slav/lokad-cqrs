#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Lokad.Cqrs;
using Lokad.Cqrs.AtomicStorage;
using Microsoft.WindowsAzure.Storage.Blob;
using NUnit.Framework;
using ProtoBuf;

namespace Cqrs.Azure.Tests.AtomicStorage
{
	public class AzureAtomicWriterAndReaderTest
	{
		private AzureAtomicWriter< Guid, TestView > _writer;
		private DocumentStrategy _documentStrategy;
		private AzureAtomicReader< Guid, TestView > _reader;
		private CloudBlobClient _cloudBlobClient;
		private string name;
		private CloudBlobContainer _cloudBlobContainer;

		[ SetUp ]
		public void Setup()
		{
			var cloudStorageAccount = ConnectionConfig.StorageAccount;
			this.name = Guid.NewGuid().ToString().ToLowerInvariant();
			this._cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
			this._cloudBlobContainer = this._cloudBlobClient.GetContainerReference( this.name );
			this._cloudBlobContainer.CreateIfNotExists();
			this._documentStrategy = new DocumentStrategy( this.name );
			this._writer = new AzureAtomicWriter< Guid, TestView >( this._cloudBlobClient, this._documentStrategy );
			this._reader = new AzureAtomicReader< Guid, TestView >( this._cloudBlobClient, this._documentStrategy );
		}

		[ TearDown ]
		public void TearDown()
		{
			this._cloudBlobContainer.Delete();
		}

		[ Test ]
		public void when_delete_than_not_key()
		{
			Assert.IsFalse( this._writer.TryDelete( Guid.NewGuid() ) );
		}

		[ Test ]
		public void when_delete_than_exist_key()
		{
			this._writer.InitializeIfNeeded();
			var id = Guid.NewGuid();
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );

			Assert.IsTrue( this._writer.TryDelete( id ) );
		}

		[ Test ]
		public async Task when_delete_than_not_key_async()
		{
			Assert.IsFalse( await this._writer.TryDeleteAsync( Guid.NewGuid() ) );
		}

		[ Test ]
		public async Task when_delete_than_exist_key_async()
		{
			this._writer.InitializeIfNeeded();
			var id = Guid.NewGuid();
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );

			Assert.IsTrue( await this._writer.TryDeleteAsync( id ) );
		}

		[ Test ]
		public void when_write_read()
		{
			var id = Guid.NewGuid();
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );

			TestView entity;
			var result = this._reader.TryGet( id, out entity );

			Assert.IsTrue( result );
			Assert.AreEqual( id, entity.Id );
			Assert.AreEqual( 10, entity.Data );
		}

		[ Test ]
		public void when_write_read_async()
		{
			var id = Guid.NewGuid();
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );

			var getTask = this._reader.GetAsync( id );
			getTask.Wait( 5000 );

			Assert.IsTrue( getTask.Result.HasValue );
			var entity = getTask.Result.Value;

			Assert.AreEqual( id, entity.Id );
			Assert.AreEqual( 10, entity.Data );
		}

		[ Test ]
		public void when_read_nothing_key()
		{
			var id = Guid.NewGuid();

			TestView entity;
			var result = this._reader.TryGet( id, out entity );

			Assert.IsFalse( result );
		}

		[ Test ]
		public void when_read_nothing_key_async()
		{
			var id = Guid.NewGuid();

			var getTask = this._reader.GetAsync( id );
			getTask.Wait( 5000 );

			Assert.IsFalse( getTask.Result.HasValue );
		}

		[ Test ]
		public void when_write_exist_key_and_read()
		{
			var id = Guid.NewGuid();
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );

			TestView entity;
			var result = this._reader.TryGet( id, out entity );

			Assert.IsTrue( result );
			Assert.AreEqual( id, entity.Id );
			Assert.AreEqual( 11, entity.Data );
		}

		[ Test ]
		public void when_write_exist_key_and_read_async()
		{
			var id = Guid.NewGuid();
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );
			this._writer.AddOrUpdate( id, () => new TestView( id )
				, old =>
				{
					old.Data++;
					return old;
				}
				, AddOrUpdateHint.ProbablyExists );

			CheckViewCorrect( id, 11 );
		}

		[ Test ]
		public void SaveNew()
		{
			//------------ Arrange
			var id = Guid.NewGuid();

			//------------ Act
			this._writer.Save( id, new TestView( id ) );

			//------------ Assert
			CheckViewCorrect( id );
		}

		[ Test ]
		public void SaveOverExisting()
		{
			//------------ Arrange
			var id = Guid.NewGuid();
			this._writer.Save( id, new TestView( id, 145 ) );

			//------------ Act
			this._writer.Save( id, new TestView( id ) );

			//------------ Assert
			CheckViewCorrect( id );
		}
		
		[ Test ]
		public async Task SaveNewAsync()
		{
			//------------ Arrange
			var id = Guid.NewGuid();

			//------------ Act
			await this._writer.SaveAsync( id, new TestView( id ) );

			//------------ Assert
			CheckViewCorrect( id );
		}

		[ Test ]
		public async Task SaveOverExistingAsync()
		{
			//------------ Arrange
			var id = Guid.NewGuid();
			this._writer.Save( id, new TestView( id, 145 ) );

			//------------ Act
			await this._writer.SaveAsync( id, new TestView( id ) );

			//------------ Assert
			CheckViewCorrect( id );
		}

		private void CheckViewCorrect( Guid id, int data = 10 )
		{
			var view = LoadView( id );
			Assert.AreEqual( id, view.Id );
			Assert.AreEqual( data, view.Data );
		}

		private TestView LoadView( Guid id )
		{
			var getTask = this._reader.GetAsync( id );
			getTask.Wait( 5000 );

			Assert.IsTrue( getTask.Result.HasValue );

			var entity = getTask.Result.Value;
			return entity;
		}
	}

	[ DataContract( Name = "test-view" ) ]
	public class TestView
	{
		[ DataMember( Order = 1 ) ]
		public Guid Id { get; set; }

		[ DataMember( Order = 2 ) ]
		public int Data { get; set; }

		public TestView()
		{
		}

		public TestView( Guid id )
		{
			this.Id = id;
			this.Data = 10;
		}

		public TestView( Guid id, int data )
		{
			this.Id = id;
			this.Data = data;
		}
	}

	public sealed class DocumentStrategy : IDocumentStrategy
	{
		private string _uniqName;

		public DocumentStrategy( string uniqName )
		{
			this._uniqName = uniqName;
		}

		public void Serialize< TEntity >( TEntity entity, Stream stream )
		{
			// ProtoBuf must have non-zero files
			stream.WriteByte( 42 );
			Serializer.Serialize( stream, entity );
		}

		public TEntity Deserialize< TEntity >( Stream stream )
		{
			var signature = stream.ReadByte();

			if( signature != 42 )
				throw new InvalidOperationException( "Unknown view format" );

			return Serializer.Deserialize< TEntity >( stream );
		}

		public string GetEntityBucket< TEntity >()
		{
			return this._uniqName + "/" + NameCache< TEntity >.Name;
		}

		public string GetEntityLocation< TEntity >( object key )
		{
			if( key is unit )
				return NameCache< TEntity >.Name + ".pb";

			return key.ToString().ToLowerInvariant() + ".pb";
		}
	}

	internal static class NameCache< T >
	{
		// ReSharper disable StaticFieldInGenericType
		public static readonly string Name;
		public static readonly string Namespace;
		// ReSharper restore StaticFieldInGenericType
		static NameCache()
		{
			var type = typeof( T );

			Name = new string( Splice( type.Name ).ToArray() ).TrimStart( '-' );
			var dcs = type.GetCustomAttributes( false ).OfType< DataContractAttribute >().ToArray();

			if( dcs.Length <= 0 )
				return;
			var attribute = dcs.First();

			if( !string.IsNullOrEmpty( attribute.Name ) )
				Name = attribute.Name;

			if( !string.IsNullOrEmpty( attribute.Namespace ) )
				Namespace = attribute.Namespace;
		}

		private static IEnumerable< char > Splice( string source )
		{
			foreach( var c in source )
			{
				if( char.IsUpper( c ) )
					yield return '-';
				yield return char.ToLower( c );
			}
		}
	}
}