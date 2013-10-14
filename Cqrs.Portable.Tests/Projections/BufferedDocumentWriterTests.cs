using System;
using System.Collections.Concurrent;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Lokad.Cqrs.AtomicStorage;
using Lokad.Cqrs.Projections;
using NUnit.Framework;
using Ploeh.AutoFixture;

namespace Cqrs.Portable.Tests.Projections
{
	public class BufferedDocumentWriterTests
	{
		private Fixture _f;
		private TestEntity _originalView;
		private TestKey _key;
		private MemoryDocumentReaderWriter< TestKey, TestEntity > _memWriter;
		private BufferedDocumentWriter< TestKey, TestEntity > _writer;

		[ SetUp ]
		public void Init()
		{
			this._f = new Fixture();
			this._originalView = new TestEntity( this._f.Create< string >() );
			this._key = new TestKey( this._f.Create< byte >() );

			var strategy = new DocumentStrategy();
			var storage = new ConcurrentDictionary< string, byte[] >();
			this._memWriter = new MemoryDocumentReaderWriter< TestKey, TestEntity >( strategy, storage );
			this._writer = new BufferedDocumentWriter< TestKey, TestEntity >( this._memWriter, this._memWriter, strategy );
		}

		[ Test ]
		public async Task NewView()
		{
			//------------ Arrange
			//------------ Act
			this._writer.AddOrUpdate( this._key, () => new TestEntity( this._originalView.Message ), te =>
			{
				throw new InvalidOperationException( "Unexpected" );
			} );
			await this._writer.Flush();

			//------------ Assert
			var loadedView = await this._memWriter.GetAsync( this.NewKey() );
			loadedView.Value.Should().Be( this._originalView );
		}

		[ Test ]
		public async Task UpdateExistingView()
		{
			//------------ Arrange
			this._memWriter.AddOrUpdate( this.NewKey(), () => this._originalView,
				te =>
				{
					throw new InvalidOperationException( "Unexpected" );
				} );
			var newMessage = this._f.Create< string >();

			//------------ Act
			this._writer.AddOrUpdate( this._key, () =>
			{
				throw new InvalidOperationException( "Unexpected" );
			}, te => te.Message = newMessage );
			await this._writer.Flush();

			//------------ Assert
			var loadedView = await this._memWriter.GetAsync( this.NewKey() );
			loadedView.Value.Should().Be( new TestEntity( newMessage ) );
		}

		[ Test ]
		public async Task AddAndUpdateView()
		{
			//------------ Arrange
			var newMessage = this._f.Create< string >();

			//------------ Act
			this._writer.AddOrUpdate( this._key, () => new TestEntity( this._originalView.Message ), te =>
			{
				throw new InvalidOperationException( "Unexpected" );
			} );
			this._writer.AddOrUpdate( this._key, () =>
			{
				throw new InvalidOperationException( "Unexpected" );
			}, te => te.Message = newMessage );
			await this._writer.Flush();

			//------------ Assert
			var loadedView = await this._memWriter.GetAsync( this.NewKey() );
			loadedView.Value.Should().Be( new TestEntity( newMessage ) );
		}

		private TestKey NewKey()
		{
			return TestKey.From( this._key );
		}
	}

	[ DataContract ]
	public class TestKey
	{
		[ DataMember( Order = 1 ) ]
		public long Key { get; private set; }

		public TestKey( long key )
		{
			this.Key = key;
		}

		public static TestKey From( TestKey otherKey )
		{
			return new TestKey( otherKey.Key );
		}

		private TestKey()
		{
		}
	}

	[ DataContract ]
	public class TestEntity : IEquatable< TestEntity >
	{
		[ DataMember( Order = 1 ) ]
		public string Message { get; set; }

		public TestEntity( string message )
		{
			this.Message = message;
		}

		private TestEntity()
		{
		}

		public bool Equals( TestEntity other )
		{
			if( ReferenceEquals( null, other ) )
				return false;
			if( ReferenceEquals( this, other ) )
				return true;
			return string.Equals( this.Message, other.Message );
		}

		public override bool Equals( object obj )
		{
			if( ReferenceEquals( null, obj ) )
				return false;
			if( ReferenceEquals( this, obj ) )
				return true;
			if( obj.GetType() != this.GetType() )
				return false;
			return this.Equals( ( TestEntity )obj );
		}

		public override int GetHashCode()
		{
			return ( this.Message != null ? this.Message.GetHashCode() : 0 );
		}

		public static bool operator ==( TestEntity left, TestEntity right )
		{
			return Equals( left, right );
		}

		public static bool operator !=( TestEntity left, TestEntity right )
		{
			return !Equals( left, right );
		}
	}
}