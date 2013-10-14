using System.Runtime.Serialization;

namespace Lokad.Cqrs.Projections
{
	[ DataContract ]
	public class ProjectionCheckpoint
	{
		[ DataMember( Order = 1 ) ]
		public long LatestEventVersion { get; set; }

		public static ProjectionCheckpoint CreateNew()
		{
			return new ProjectionCheckpoint();
		}
	}
}