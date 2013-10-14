using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using CuttingEdge.Conditions;
using Mono.Cecil;

namespace Lokad.Cqrs.Projections
{
	[ DataContract ]
	public class ProjectionHash : IEquatable< ProjectionHash >
	{
		[ DataMember( Order = 1 ) ]
		public int HashVersion { get; private set; }

		[ DataMember( Order = 2 ) ]
		public string ProjectionTypeHash { get; private set; }

		[ DataMember( Order = 3 ) ]
		public string ViewTypesHash { get; private set; }

		[ DataMember( Order = 4 ) ]
		public string StrategyTypeHash { get; private set; }

		public ProjectionHash( Type projectionType, IEnumerable< Type > viewTypes, Type strategyType )
		{
			Condition.Requires( projectionType, "projectionType" ).IsNotNull();
			Condition.Requires( viewTypes, "viewType" ).IsNotNull();
			Condition.Requires( strategyType, "strategyType" ).IsNotNull();

			this.HashVersion = 1;
			this.ProjectionTypeHash = GetClassHash( projectionType );
			this.ViewTypesHash = string.Concat( viewTypes.Select( GetClassHash ));
			this.StrategyTypeHash = GetClassHash( strategyType );
		}

		private ProjectionHash()
		{
		}

		private static string GetClassHash( Type type1 )
		{
			var location = type1.Assembly.Location;
			var mod = ModuleDefinition.ReadModule( location );
			var builder = new StringBuilder();
			var type = type1;

			var typeDefinition = mod.GetType( type.FullName );
			builder.AppendLine( typeDefinition.Name );
			ProcessMembers( builder, typeDefinition );

			// we include nested types
			foreach( var nested in typeDefinition.NestedTypes )
			{
				ProcessMembers( builder, nested );
			}

			return builder.ToString();
		}

		private static void ProcessMembers( StringBuilder builder, TypeDefinition typeDefinition )
		{
			foreach( var md in typeDefinition.Methods.OrderBy( m => m.ToString() ) )
			{
				builder.AppendLine( "  " + md );

				foreach( var instruction in md.Body.Instructions )
				{
					// we don't care about offsets
					instruction.Offset = 0;
					builder.AppendLine( "    " + instruction );
				}
			}
			foreach( var field in typeDefinition.Fields.OrderBy( f => f.ToString() ) )
			{
				builder.AppendLine( "  " + field );
			}
		}

		public bool Equals( ProjectionHash other )
		{
			if( ReferenceEquals( null, other ) )
				return false;
			if( ReferenceEquals( this, other ) )
				return true;
			return this.HashVersion == other.HashVersion && string.Equals( this.ProjectionTypeHash, other.ProjectionTypeHash ) && string.Equals( this.ViewTypesHash, other.ViewTypesHash ) && string.Equals( this.StrategyTypeHash, other.StrategyTypeHash );
		}

		public override bool Equals( object obj )
		{
			if( ReferenceEquals( null, obj ) )
				return false;
			if( ReferenceEquals( this, obj ) )
				return true;
			if( obj.GetType() != this.GetType() )
				return false;
			return Equals( ( ProjectionHash )obj );
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = this.HashVersion;
				hashCode = ( hashCode * 397 ) ^ ( this.ProjectionTypeHash != null ? this.ProjectionTypeHash.GetHashCode() : 0 );
				hashCode = ( hashCode * 397 ) ^ ( this.ViewTypesHash != null ? this.ViewTypesHash.GetHashCode() : 0 );
				hashCode = ( hashCode * 397 ) ^ ( this.StrategyTypeHash != null ? this.StrategyTypeHash.GetHashCode() : 0 );
				return hashCode;
			}
		}

		public static bool operator ==( ProjectionHash left, ProjectionHash right )
		{
			return Equals( left, right );
		}

		public static bool operator !=( ProjectionHash left, ProjectionHash right )
		{
			return !Equals( left, right );
		}
	}
}