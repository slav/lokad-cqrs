#region (c) 2010-2011 Lokad CQRS - New BSD License 
// Copyright (c) Lokad SAS 2010-2011 (http://www.lokad.com)
// This code is released as Open Source under the terms of the New BSD Licence
// Homepage: http://lokad.github.com/lokad-cqrs/
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class FileDocumentReaderWriter< TKey, TEntity > : IDocumentReader< TKey, TEntity >,
		IDocumentWriter< TKey, TEntity >
	{
		private readonly IDocumentStrategy _strategy;
		private readonly string _folder;

		public FileDocumentReaderWriter( string directoryPath, IDocumentStrategy strategy )
		{
			this._strategy = strategy;
			this._folder = Path.Combine( directoryPath, strategy.GetEntityBucket< TEntity >() );
		}

		public void InitIfNeeded()
		{
			Directory.CreateDirectory( this._folder );
		}

		public bool TryGet( TKey key, out TEntity view )
		{
			view = default( TEntity );
			try
			{
				var name = this.GetName( key );
				StorageProfiler.CountReadRequest( name );

				if( !File.Exists( name ) )
					return false;

				using( var stream = RetryPolicies.GetPolicy.Get( () => File.Open( name, FileMode.Open, FileAccess.Read, FileShare.Read ) ) )
				{
					if( stream.Length == 0 )
						return false;
					view = this._strategy.Deserialize< TEntity >( stream );
					return true;
				}
			}
			catch( FileNotFoundException )
			{
				// if file happened to be deleted between the moment of check and actual read.
				return false;
			}
			catch( DirectoryNotFoundException )
			{
				return false;
			}
			catch( IOException )
			{
				return false;
			}
		}

		public async Task< Maybe< TEntity > > GetAsync( TKey key )
		{
			try
			{
				var name = this.GetName( key );
				StorageProfiler.CountReadRequest( name );

				if( !File.Exists( name ) )
					return Maybe< TEntity >.Empty;

				using( var fs = new FileStream( name, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous ) )
				{
					if( fs.Length == 0 )
						return Maybe< TEntity >.Empty;

					var bytes = new byte[ fs.Length ];
					var readBytes = await fs.ReadAsync( bytes, 0, bytes.Length ).ConfigureAwait( false );
					if( readBytes != bytes.Length )
						throw new IOException( "Not all bytes were read async. Need to implement better async reader." );

					using( var ms = new MemoryStream( bytes ) )
						return this._strategy.Deserialize< TEntity >( ms );
				}
			}
			catch( FileNotFoundException )
			{
				// if file happened to be deleted between the moment of check and actual read.
				return Maybe< TEntity >.Empty;
			}
			catch( DirectoryNotFoundException )
			{
				return Maybe< TEntity >.Empty;
			}
		}

		private string GetName( TKey key )
		{
			return Path.Combine( this._folder, this._strategy.GetEntityLocation< TEntity >( key ) );
		}

		public TEntity AddOrUpdate( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint )
		{
			return this.AddOrUpdateAsync( key, addFactory, update, hint ).GetAwaiter().GetResult();
		}

		public async Task< TEntity > AddOrUpdateAsync( TKey key, Func< TEntity > addFactory, Func< TEntity, TEntity > update, AddOrUpdateHint hint = AddOrUpdateHint.ProbablyExists )
		{
			var name = this.GetName( key );

			try
			{
				// This is fast and allows to have git-style subfolders in atomic strategy
				// to avoid NTFS performance degradation (when there are more than 
				// 10000 files per folder). Kudos to Gabriel Schenker for pointing this out
				var subfolder = Path.GetDirectoryName( name );
				if( subfolder != null && !Directory.Exists( subfolder ) )
					Directory.CreateDirectory( subfolder );

				// we are locking this file.
				using( var file = File.Open( name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None ) )
				{
					var initial = new byte[ 0 ];
					TEntity result;
					if( file.Length == 0 )
						result = addFactory();
					else
					{
						using( var mem = new MemoryStream() )
						{
							StorageProfiler.CountReadRequest( name );
							await file.CopyToAsync( mem );
							mem.Seek( 0, SeekOrigin.Begin );
							var entity = this._strategy.Deserialize< TEntity >( mem );
							initial = mem.ToArray();
							result = update( entity );
						}
					}

					// some serializers have nasty habbit of closing the
					// underling stream
					using( var mem = new MemoryStream() )
					{
						this._strategy.Serialize( result, mem );
						var data = mem.ToArray();

						if( !data.SequenceEqual( initial ) )
						{
							StorageProfiler.CountWriteRequest( name );

							// upload only if we changed
							file.Seek( 0, SeekOrigin.Begin );
							await file.WriteAsync( data, 0, data.Length );
							// truncate this file
							file.SetLength( data.Length );
						}
					}

					return result;
				}
			}
			catch( DirectoryNotFoundException )
			{
				var s = string.Format(
					"Container '{0}' does not exist.",
					this._folder );
				throw new InvalidOperationException( s );
			}
		}

		public void Save( TKey key, TEntity entity )
		{
			this.SaveAsync( key, entity ).Wait();
		}

		public async Task SaveAsync( TKey key, TEntity entity )
		{
			var name = this.GetName( key );

			try
			{
				// This is fast and allows to have git-style subfolders in atomic strategy
				// to avoid NTFS performance degradation (when there are more than 
				// 10000 files per folder). Kudos to Gabriel Schenker for pointing this out
				var subfolder = Path.GetDirectoryName( name );
				if( subfolder != null && !Directory.Exists( subfolder ) )
					Directory.CreateDirectory( subfolder );

				// we are locking this file.
				using( var file = File.Open( name, FileMode.Create, FileAccess.ReadWrite, FileShare.None ) )
					// some serializers have nasty habbit of closing the
					// underling stream
				using( var mem = new MemoryStream() )
				{
					this._strategy.Serialize( entity, mem );
					var data = mem.ToArray();

					StorageProfiler.CountWriteRequest( name );
					// upload only if we changed
					await file.WriteAsync( data, 0, data.Length );
					// truncate this file
					file.SetLength( data.Length );
				}
			}
			catch( DirectoryNotFoundException )
			{
				var s = string.Format(
					"Container '{0}' does not exist.",
					this._folder );
				throw new InvalidOperationException( s );
			}
		}

		public bool TryDelete( TKey key )
		{
			return this.TryDeleteAsync( key ).Result;
		}

		public Task< bool > TryDeleteAsync( TKey key )
		{
			var name = this.GetName( key );
			if( File.Exists( name ) )
			{
				File.Delete( name );
				return Task.FromResult( true );
			}
			return Task.FromResult( false );
		}
	}

	public static class StorageProfiler
	{
		private static readonly Stopwatch _stopWatch = new Stopwatch();
		private static Dictionary< string, StorageAccessRecord > _readRequests;
		private static Dictionary< string, StorageAccessRecord > _writeRequests;
		private static object _lock = new object();

		static StorageProfiler()
		{
			_readRequests = new Dictionary< string, StorageAccessRecord >();
			_writeRequests = new Dictionary< string, StorageAccessRecord >();
		}

		public static void Reset()
		{
			lock( _lock )
			{
				_readRequests = new Dictionary< string, StorageAccessRecord >();
				_writeRequests = new Dictionary< string, StorageAccessRecord >();
				_stopWatch.Restart();
			}
		}

		public static void CountReadRequest( string path )
		{
			IncrementRecordCount( path, _readRequests );
		}

		public static void CountWriteRequest( string path )
		{
			IncrementRecordCount( path, _writeRequests );
		}

		private static void IncrementRecordCount( string path, Dictionary< string, StorageAccessRecord > records )
		{
			lock( _lock )
			{
				StorageAccessRecord record;
				if( !records.TryGetValue( path, out record ) )
				{
					record = new StorageAccessRecord( path );
					records[ path ] = record;
				}
				record.IncrementRequestCounter();
			}
		}

		public static string GetStatistics()
		{
			lock( _lock )
			{
				var sb = new StringBuilder();
				sb.AppendLine( "Time: " + _stopWatch.Elapsed.Seconds );
				sb.AppendLine();

				sb.AppendLine( "Reads:" );
				var readRecords = from r in _readRequests.Values
					group r.Counter by r.FullPath into g
					orderby g.Key
					select new { g.Key, Count = g.Sum() };
				foreach( var recordsSummary in readRecords )
				{
					sb.AppendLine( "\t" + recordsSummary.Key + "," + recordsSummary.Count );
				}

				sb.AppendLine();

				sb.AppendLine( "Write:" );
				var writeRecords = from r in _writeRequests.Values
					group r.Counter by r.Directory
					into g
					orderby g.Key
					select new { g.Key, Count = g.Sum() };
				foreach( var recordsSummary in writeRecords )
				{
					sb.AppendLine( "\t" + recordsSummary.Key + "," + recordsSummary.Count );
				}

				return sb.ToString();
			}
		}

		public class StorageAccessRecord
		{
			public string Directory { get; private set; }
			public string File { get; private set; }
			public string FullPath { get; private set; }
			public int Counter { get; private set; }

			public StorageAccessRecord( string fullPath )
			{
				this.Directory = Path.GetDirectoryName( fullPath );
				this.File = Path.GetFileName( fullPath );
				this.FullPath = fullPath;
				this.Counter = 0;
			}

			public void IncrementRequestCounter()
			{
				this.Counter++;
			}
		}
	}
}