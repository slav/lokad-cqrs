﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Lokad.Cqrs;
using Lokad.Cqrs.AtomicStorage;
using NUnit.Framework;

namespace Cqrs.Portable.Tests.AtomicStorage
{
	public class FileDocumentReaderWriterTest : DocumentReaderWriterTest
	{
		[ SetUp ]
		public void Setup()
		{
			var tmpPath = Path.GetTempPath();
			var documentStrategy = new DocumentStrategy();
			this._reader = new FileDocumentReaderWriter< Guid, int >( tmpPath, documentStrategy );
			this._writer = new FileDocumentReaderWriter< Guid, int >( tmpPath, documentStrategy );
			this._testClassReader = new FileDocumentReaderWriter< unit, Test1 >( tmpPath, documentStrategy );
			this._testClassWtiter = new FileDocumentReaderWriter< unit, Test1 >( tmpPath, documentStrategy );
			this._guidKeyClassReader = new FileDocumentReaderWriter< Guid, Test1 >( tmpPath, documentStrategy );
			this._guidKeyClassWriter = new FileDocumentReaderWriter< Guid, Test1 >( tmpPath, documentStrategy );
		}
	}

	public class MemoryDocumentReaderWriterTest : DocumentReaderWriterTest
	{
		[ SetUp ]
		public void Setup()
		{
			var documentStrategy = new DocumentStrategy();
			var concurrentDictionary = new ConcurrentDictionary< string, byte[] >();
			this._reader = new MemoryDocumentReaderWriter< Guid, int >( documentStrategy, concurrentDictionary );
			this._writer = new MemoryDocumentReaderWriter< Guid, int >( documentStrategy, concurrentDictionary );
			this._testClassReader = new MemoryDocumentReaderWriter< unit, Test1 >( documentStrategy, concurrentDictionary );
			this._testClassWtiter = new MemoryDocumentReaderWriter< unit, Test1 >( documentStrategy, concurrentDictionary );
			this._guidKeyClassReader = new MemoryDocumentReaderWriter< Guid, Test1 >( documentStrategy, concurrentDictionary );
			this._guidKeyClassWriter = new MemoryDocumentReaderWriter< Guid, Test1 >( documentStrategy, concurrentDictionary );
		}
	}

	public class NonserializingMemoryDocumentReaderWriterTest : DocumentReaderWriterTest
	{
		[ SetUp ]
		public void Setup()
		{
			var documentStrategy = new DocumentStrategy();
			var concurrentDictionary = new ConcurrentDictionary< string, object >();
			this._reader = new NonserializingMemoryDocumentReaderWriter< Guid, int >( documentStrategy, concurrentDictionary );
			this._writer = new NonserializingMemoryDocumentReaderWriter< Guid, int >( documentStrategy, concurrentDictionary );
			this._testClassReader = new NonserializingMemoryDocumentReaderWriter< unit, Test1 >( documentStrategy, concurrentDictionary );
			this._testClassWtiter = new NonserializingMemoryDocumentReaderWriter< unit, Test1 >( documentStrategy, concurrentDictionary );
			this._guidKeyClassReader = new NonserializingMemoryDocumentReaderWriter< Guid, Test1 >( documentStrategy, concurrentDictionary );
			this._guidKeyClassWriter = new NonserializingMemoryDocumentReaderWriter< Guid, Test1 >( documentStrategy, concurrentDictionary );
		}
	}

	public abstract class DocumentReaderWriterTest
	{
		public IDocumentReader< Guid, int > _reader;
		public IDocumentWriter< Guid, int > _writer;
		public IDocumentReader< unit, Test1 > _testClassReader;
		public IDocumentWriter< unit, Test1 > _testClassWtiter;
		public IDocumentReader< Guid, Test1 > _guidKeyClassReader;
		public IDocumentWriter< Guid, Test1 > _guidKeyClassWriter;

		[ Test ]
		public void get_not_created_entity()
		{
			var key = Guid.NewGuid();
			int entity;

			Assert.AreEqual( false, this._reader.TryGet( key, out entity ) );
		}

		[ Test ]
		public void deleted_not_created_entity()
		{
			var key = Guid.NewGuid();

			Assert.AreEqual( false, this._writer.TryDelete( key ) );
		}

		[ Test ]
		public void created_new_entity()
		{
			var key = Guid.NewGuid();

			Assert.AreEqual( 1, this._writer.AddOrUpdate( key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist ) );
		}

		[ Test ]
		public void update_exist_entity()
		{
			var key = Guid.NewGuid();
			this._writer.AddOrUpdate( key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist );

			Assert.AreEqual( 5, this._writer.AddOrUpdate( key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist ) );
		}

		[ Test ]
		public void get_new_created_entity()
		{
			var key = Guid.NewGuid();
			this._writer.AddOrUpdate( key, () => 1, i => 5, AddOrUpdateHint.ProbablyDoesNotExist );

			int result;
			Assert.AreEqual( true, this._reader.TryGet( key, out result ) );
			Assert.AreEqual( 1, result );
		}

		[ Test ]
		public void get_updated_entity()
		{
			var key = Guid.NewGuid();
			this._writer.AddOrUpdate( key, () => 1, i => 2, AddOrUpdateHint.ProbablyDoesNotExist );
			this._writer.AddOrUpdate( key, () => 3, i => 4, AddOrUpdateHint.ProbablyDoesNotExist );

			int result;
			Assert.AreEqual( true, this._reader.TryGet( key, out result ) );
			Assert.AreEqual( 4, result );
		}

		[ Test ]
		public void get_by_key_does_not_exist()
		{
			var result = this._reader.Get( Guid.NewGuid() );

			Assert.IsFalse( result.HasValue );
		}

		[ Test ]
		public void get_by_exis_key()
		{
			var key = Guid.NewGuid();
			this._writer.AddOrUpdate( key, () => 1, i => 2, AddOrUpdateHint.ProbablyDoesNotExist );
			var result = this._reader.Get( key );

			Assert.IsTrue( result.HasValue );
			Assert.AreEqual( 1, result.Value );
		}

		[ Test ]
		[ ExpectedException( typeof( InvalidOperationException ) ) ]
		public void load_by_key_does_not_exist()
		{
			this._reader.Load( Guid.NewGuid() );
		}

		[ Test ]
		public void load_by_exis_key()
		{
			var key = Guid.NewGuid();
			this._writer.AddOrUpdate( key, () => 1, i => 2, AddOrUpdateHint.ProbablyDoesNotExist );
			var result = this._reader.Load( key );

			Assert.AreEqual( 1, result );
		}

		[ Test ]
		[ Ignore( "to be realized ExtendDocumentReader.GetOrNew" ) ]
		public void get_or_new()
		{
		}

		[ Test ]
		[ Ignore( "to be realized ExtendDocumentReader.Get<TSingleton>" ) ]
		public void get_singleton()
		{
		}

		[ Test ]
		public void when_not_found_key_get_new_view_and_not_call_action()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			var newValue = this._guidKeyClassWriter.AddOrUpdate( key, t, tv =>
			{
				tv.Value += 1;
			} );

			Assert.AreEqual( t, newValue );
			Assert.AreEqual( 555, newValue.Value );
		}

		[ Test ]
		public void when_key_exist_call_action_and_get_new_value()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			this._guidKeyClassWriter.AddOrUpdate( key, t, tv =>
			{
				tv.Value += 1;
			} );
			var newValue = this._guidKeyClassWriter.AddOrUpdate( key, t, tv =>
			{
				tv.Value += 1;
			} );

			Assert.AreEqual( typeof( Test1 ), newValue.GetType() );
			Assert.AreEqual( 556, newValue.Value );
		}

		[ Test ]
		public void when_not_found_key_get_new_view_func_and_not_call_action()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			var newValue = this._guidKeyClassWriter.AddOrUpdate( key, () => t, tv =>
			{
				tv.Value += 1;
			} );

			Assert.AreEqual( t, newValue );
			Assert.AreEqual( 555, newValue.Value );
		}

		[ Test ]
		public void when_key_exist_not_call_new_view_func_and_call_action_and_get_new_value()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			this._guidKeyClassWriter.AddOrUpdate( key, t, tv =>
			{
				tv.Value += 1;
			} );
			var newValue = this._guidKeyClassWriter.AddOrUpdate( key, () => t, tv =>
			{
				tv.Value += 1;
			} );

			Assert.AreEqual( typeof( Test1 ), newValue.GetType() );
			Assert.AreEqual( 556, newValue.Value );
		}

		[ Test ]
		public void add_new_value()
		{
			var t = new Test1 { Value = 555 };
			var newValue = this._guidKeyClassWriter.Add( Guid.NewGuid(), t );

			Assert.AreEqual( t, newValue );
			Assert.AreEqual( 555, newValue.Value );
		}

		[ Test ]
		public async Task add_value_when_key_exist()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			this._guidKeyClassWriter.Add( key, t );

			var t2 = new Test1 { Value = 333 };
			this._guidKeyClassWriter.Add( key, t2 );

			var actualT = await this._guidKeyClassReader.GetAsync( key );

			Assert.AreEqual( t2.Value, actualT.Value.Value );
		}

		[ Test ]
		[ ExpectedException( typeof( InvalidOperationException ) ) ]
		public void update_value_with_func_when_key_not_found()
		{
			this._guidKeyClassWriter.UpdateOrThrow( Guid.NewGuid(), tv =>
			{
				tv.Value += 1;
				return tv;
			} );
		}

		[ Test ]
		public void update_value_with_func_when_key_exist()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			this._guidKeyClassWriter.Add( key, t );
			var newValue = this._guidKeyClassWriter.UpdateOrThrow( key, tv =>
			{
				tv.Value += 1;
				return tv;
			} );

			Assert.AreEqual( 556, newValue.Value );
		}

		[ Test ]
		[ ExpectedException( typeof( InvalidOperationException ) ) ]
		public void update_value_with_action_when_key_not_found()
		{
			this._guidKeyClassWriter.UpdateOrThrow( Guid.NewGuid(), tv =>
			{
				tv.Value += 1;
			} );
		}

		[ Test ]
		public void update_value_with_action_when_key_exist()
		{
			var t = new Test1 { Value = 555 };
			var key = Guid.NewGuid();
			this._guidKeyClassWriter.Add( key, t );
			var newValue = this._guidKeyClassWriter.UpdateOrThrow( key, tv =>
			{
				tv.Value += 1;
			} );

			Assert.AreEqual( 556, newValue.Value );
		}

		[ Test ]
		public void created_new_instance_when_call_update_method_and_key_not_found()
		{
			var key = Guid.NewGuid();
			var newValue = this._guidKeyClassWriter.UpdateEnforcingNew( key, tv =>
			{
				tv.Value += 1;
			} );

			var defaultValue = default( int );
			Assert.AreEqual( defaultValue + 1, newValue.Value );
		}
	}

	[ DataContract ]
	public class Test1
	{
		[ DataMember( Order = 1 ) ]
		public int Value { get; set; }
	}
}