using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	public sealed class FileDocumentStore : IDocumentStore
	{
		private readonly string _folderPath;
		private readonly IDocumentStrategy _strategy;

		public FileDocumentStore( string folderPath, IDocumentStrategy strategy )
		{
			this._folderPath = folderPath;
			this._strategy = strategy;
		}

		public override string ToString()
		{
			return new Uri( Path.GetFullPath( this._folderPath ) ).AbsolutePath;
		}

		private readonly HashSet< Tuple< Type, Type > > _initialized = new HashSet< Tuple< Type, Type > >();

		public IDocumentWriter< TKey, TEntity > GetWriter< TKey, TEntity >()
		{
			var container = new FileDocumentReaderWriter< TKey, TEntity >( this._folderPath, this._strategy );
			if( this._initialized.Add( Tuple.Create( typeof( TKey ), typeof( TEntity ) ) ) )
				container.InitIfNeeded();
			return container;
		}

		public IDocumentReader< TKey, TEntity > GetReader< TKey, TEntity >()
		{
			return new FileDocumentReaderWriter< TKey, TEntity >( this._folderPath, this._strategy );
		}

		public IDocumentStrategy Strategy
		{
			get { return this._strategy; }
		}

		public IEnumerable< DocumentRecord > EnumerateContents( string bucket )
		{
			var full = Path.Combine( this._folderPath, bucket );
			var dir = new DirectoryInfo( full );
			if( !dir.Exists )
				yield break;

			var fullFolder = dir.FullName;
			foreach( var info in dir.EnumerateFiles( "*", SearchOption.AllDirectories ) )
			{
				var fullName = info.FullName;
				var path = fullName.Remove( 0, fullFolder.Length + 1 ).Replace( Path.DirectorySeparatorChar, '/' );
				yield return new DocumentRecord( path, () => File.ReadAllBytes( fullName ) );
			}
		}

		public void WriteContents( string bucket, IEnumerable< DocumentRecord > records )
		{
			this.WriteContentsAsync( bucket, records, CancellationToken.None ).GetAwaiter().GetResult();
		}

		public async Task WriteContentsAsync( string bucket, IEnumerable< DocumentRecord > records, CancellationToken token )
		{
			var buck = Path.Combine( this._folderPath, bucket );
			if( !Directory.Exists( buck ) )
				Directory.CreateDirectory( buck );

			var tasks = new List< Task >();
			var fullSw = new Stopwatch();
			var fullCounter = 0;

			var counter = 0;
			var sw = new Stopwatch();
			fullSw.Start();
			sw.Start();
			foreach( var record in records )
			{
				var recordPath = Path.Combine( buck, record.Key );

				var path = Path.GetDirectoryName( recordPath ) ?? "";
				if( !Directory.Exists( path ) )
					Directory.CreateDirectory( path );
				var data = record.Read();
				tasks.Add( AP.LongAsync.Do( () => this.SaveData( recordPath, data ) ) );
				counter++;
				fullCounter++;
				if( tasks.Count == 1000 )
				{
					await WaitForViewsSave( bucket, tasks, counter, sw );
					tasks.Clear();
					sw.Restart();
				}
			}
			await WaitForViewsSave( bucket, tasks, counter, sw );

			fullSw.Stop();
			SystemObserver.Notify( "{0}: Saved total {1} records in {2}", bucket, fullCounter, fullSw.Elapsed );
		}

		private static async Task WaitForViewsSave( string bucket, List< Task > tasks, int counter, Stopwatch sw )
		{
			SystemObserver.Notify( "{0}: Added {1}({2}) records to save", bucket, tasks.Count, counter );
			await Task.WhenAll( tasks );
			sw.Stop();
			SystemObserver.Notify( "{0}: Total {1}({2}) records saved in {3}", bucket, tasks.Count, counter, sw.Elapsed );
		}

		private Task SaveData( string fileName, byte[] data )
		{
			using( var fs = new FileStream( fileName, FileMode.Create, FileSystemRights.CreateFiles, FileShare.None, 4096, FileOptions.Asynchronous ) )
				return fs.WriteAsync( data, 0, data.Length );
		}

		public void ResetAll()
		{
			if( Directory.Exists( this._folderPath ) )
				Directory.Delete( this._folderPath, true );
			Directory.CreateDirectory( this._folderPath );
		}

		public void Reset( string bucket )
		{
			var path = Path.Combine( this._folderPath, bucket );
			if( Directory.Exists( path ) )
				Directory.Delete( path, true );
			Directory.CreateDirectory( path );
		}

		public Task ResetAsync( string bucket )
		{
			var path = Path.Combine( this._folderPath, bucket );
			if( Directory.Exists( path ) )
				Directory.Delete( path, true );
			Directory.CreateDirectory( path );

			return Task.FromResult( true );
		}
	}
}