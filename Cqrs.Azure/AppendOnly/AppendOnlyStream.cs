using System;
using System.IO;
using System.Threading.Tasks;
using Netco.ActionPolicyServices;
using Netco.Logging;

namespace Lokad.Cqrs.AppendOnly
{
	/// <summary>
	/// Helps to write data to the underlying store, which accepts only
	/// pages with specific size
	/// </summary>
	public sealed class AppendOnlyStream : IDisposable
	{
		private readonly int _pageSizeInBytes;
		private readonly AppendWriterDelegate _writer;
		private readonly int _maxByteCount;
		private MemoryStream _pending;

		private int _bytesWritten;
		private int _bytesPending;
		private int _fullPagesFlushed;
		private int _persistedPosition;

		public AppendOnlyStream( int pageSizeInBytes, AppendWriterDelegate writer, int maxByteCount )
		{
			this._writer = writer;
			this._maxByteCount = maxByteCount;
			this._pageSizeInBytes = pageSizeInBytes;
			this._pending = new MemoryStream();
		}

		public bool Fits( int byteCount )
		{
			return ( this._bytesWritten + byteCount <= this._maxByteCount );
		}

		public void Write( byte[] buffer )
		{
			this._pending.Write( buffer, 0, buffer.Length );
			this._bytesWritten += buffer.Length;
			this._bytesPending += buffer.Length;
		}

		public void Flush()
		{
			if( this._bytesPending == 0 )
				return;

			var flushBuffer = this._pending.ToArray();
			var size = flushBuffer.Length;
			var padSize = ( this._pageSizeInBytes - size % this._pageSizeInBytes ) % this._pageSizeInBytes;

			_policy.Do( () =>
			{
				using( var stream = new MemoryStream( size + padSize ) )
				{
					stream.Write( flushBuffer, 0, flushBuffer.Length );
					if( padSize > 0 )
						stream.Write( new byte[ padSize ], 0, padSize );

					stream.Position = 0;
					this._writer( this._fullPagesFlushed * this._pageSizeInBytes, stream );
				}
			} );

			var fullPagesFlushed = size / this._pageSizeInBytes;

			if( fullPagesFlushed > 0 )
			{
				// Copy remainder to the new stream and dispose the old stream
				var newStream = new MemoryStream();
				this._pending.Position = fullPagesFlushed * this._pageSizeInBytes;
				this._pending.CopyTo( newStream );
				this._pending.Dispose();
				this._pending = newStream;
				this._bytesPending = 0;
			}

			this._fullPagesFlushed += fullPagesFlushed;
			this._persistedPosition = this._fullPagesFlushed * this._pageSizeInBytes + ( int )this._pending.Length;
		}

		public int PersistedPosition
		{
			get { return this._persistedPosition; }
		}

		public void Dispose()
		{
			this.Flush();
			this._pending.Dispose();
		}

		private static readonly ActionPolicy _policy = ActionPolicy.Handle< Exception >().Retry( 2000, ( x, i ) =>
		{
			NetcoLogger.GetLogger( typeof( AppendOnlyStream ) ).Log().Error( x, "Error encountered while working with Azure. Retry count: {0}", i );
			Task.Delay( 500 ).Wait();
		} );
	}

	/// <summary>
	/// Delegate that writes pages to the underlying paged store.
	/// </summary>
	/// <param name="offset">The offset.</param>
	/// <param name="source">The source.</param>
	public delegate void AppendWriterDelegate( int offset, Stream source );
}