#region (c) 2010-2011 Lokad - CQRS for Windows Azure - New BSD License 
// Copyright (c) Lokad 2010-2011, http://www.lokad.com
// This code is released as Open Source under the terms of the New BSD Licence
#endregion

using System;
using System.Threading.Tasks;

namespace Lokad.Cqrs.AtomicStorage
{
	/// <summary>
	/// Basic usability wrapper for the atomic storage operations, that does not enforce concurrency handling. 
	/// If you want to work with advanced functionality, either request specific interfaces from the container 
	/// or go through the advanced members on this instance. 
	/// </summary>
	public sealed class NuclearStorage : HideObjectMembersFromIntelliSense
	{
		public readonly IDocumentStore Container;

		public NuclearStorage( IDocumentStore container )
		{
			this.Container = container;
		}

		public void CopyFrom( NuclearStorage source, params string[] buckets )
		{
			foreach( var bucket in buckets )
			{
				this.Container.WriteContents( bucket, source.Container.EnumerateContents( bucket ) );
			}
		}

		public bool TryDeleteEntity< TEntity >( object key )
		{
			return this.Container.GetWriter< object, TEntity >().TryDelete( key );
		}

		public Task< bool > TryDeleteEntityAsync< TEntity >( object key )
		{
			return this.Container.GetWriter< object, TEntity >().TryDeleteAsync( key );
		}

		public bool TryDeleteSingleton< TEntity >()
		{
			return this.Container.GetWriter< unit, TEntity >().TryDelete( unit.it );
		}

		public Task< bool > TryDeleteSingletonAsync< TEntity >()
		{
			return this.Container.GetWriter< unit, TEntity >().TryDeleteAsync( unit.it );
		}

		public TEntity UpdateEntity< TEntity >( object key, Action< TEntity > update )
		{
			return this.Container.GetWriter< object, TEntity >().UpdateOrThrow( key, update );
		}

		public TSingleton UpdateSingletonOrThrow< TSingleton >( Action< TSingleton > update )
		{
			return this.Container.GetWriter< unit, TSingleton >().UpdateOrThrow( unit.it, update );
		}

		public Maybe< TEntity > GetEntity< TEntity >( object key )
		{
			return this.Container.GetReader< object, TEntity >().Get( key );
		}

		public TEntity GetEntityOrThrow< TEntity >( object key )
		{
			var entityMaybe = this.Container.GetReader< object, TEntity >().Get( key );
			if( entityMaybe.HasValue )
				return entityMaybe.Value;
			throw new InvalidOperationException( string.Format( "Failed to locate entity {0} by key {1}", typeof( TEntity ), key ) );
		}

		public Task< Maybe< TEntity > > GetEntityAsync< TEntity >( object key )
		{
			return this.Container.GetReader< object, TEntity >().GetAsync( key );
		}

		public async Task< TEntity > GetEntityOrThrowAsync< TEntity >( object key )
		{
			var entityMaybe = await this.Container.GetReader< object, TEntity >().GetAsync( key ).ConfigureAwait( false );
			if( entityMaybe.HasValue )
				return entityMaybe.Value;
			throw new InvalidOperationException( string.Format( "Failed to locate entity {0} by key {1}", typeof( TEntity ), key ) );
		}

		public bool TryGetEntity< TEntity >( object key, out TEntity entity )
		{
			return this.Container.GetReader< object, TEntity >().TryGet( key, out entity );
		}

		public TEntity AddOrUpdateEntity< TEntity >( object key, TEntity entity )
		{
			return this.Container.GetWriter< object, TEntity >().AddOrUpdate( key, () => entity, source => entity );
		}

		public void SaveEntity< TEntity >( object key, TEntity entity )
		{
			this.Container.GetWriter< object, TEntity >().Save( key, entity );
		}

		public Task SaveEntityAsync< TEntity >( object key, TEntity entity )
		{
			return this.Container.GetWriter< object, TEntity >().SaveAsync( key, entity );
		}

		public TEntity AddOrUpdateEntity< TEntity >( object key, Func< TEntity > addFactory, Action< TEntity > update )
		{
			return this.Container.GetWriter< object, TEntity >().AddOrUpdate( key, addFactory, update );
		}

		public TEntity AddOrUpdateEntity< TEntity >( object key, Func< TEntity > addFactory, Func< TEntity, TEntity > update )
		{
			return this.Container.GetWriter< object, TEntity >().AddOrUpdate( key, addFactory, update );
		}

		public TEntity AddEntity< TEntity >( object key, TEntity newEntity )
		{
			return this.Container.GetWriter< object, TEntity >().Add( key, newEntity );
		}

		public TSingleton AddOrUpdateSingleton< TSingleton >( Func< TSingleton > addFactory, Action< TSingleton > update )
		{
			return this.Container.GetWriter< unit, TSingleton >().AddOrUpdate( unit.it, addFactory, update );
		}

		public TSingleton AddOrUpdateSingleton< TSingleton >( Func< TSingleton > addFactory,
			Func< TSingleton, TSingleton > update )
		{
			return this.Container.GetWriter< unit, TSingleton >().AddOrUpdate( unit.it, addFactory, update );
		}

		public TSingleton UpdateSingletonEnforcingNew< TSingleton >( Action< TSingleton > update ) where TSingleton : new()
		{
			return this.Container.GetWriter< unit, TSingleton >().UpdateEnforcingNew( unit.it, update );
		}

		public TSingleton GetSingletonOrNew< TSingleton >() where TSingleton : new()
		{
			return this.Container.GetReader< unit, TSingleton >().GetOrNew();
		}

		public Task< TSingleton > GetSingletonOrNewAsync< TSingleton >() where TSingleton : new()
		{
			return this.Container.GetReader< unit, TSingleton >().GetOrNewAsync();
		}

		public Maybe< TSingleton > GetSingleton< TSingleton >()
		{
			return this.Container.GetReader< unit, TSingleton >().Get();
		}

		public Task< Maybe< TSingleton > > GetSingletonAsync< TSingleton >()
		{
			return this.Container.GetReader< unit, TSingleton >().GetAsync();
		}
	}
}