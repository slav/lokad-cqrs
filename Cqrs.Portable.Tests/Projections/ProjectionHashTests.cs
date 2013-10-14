using CuttingEdge.Conditions;
using FluentAssertions;
using Lokad.Cqrs.Projections;
using NUnit.Framework;

namespace Cqrs.Portable.Tests.Projections
{
	public class ProjectionHashTests
	{
		[ Test ]
		public void SameTypes_Equal()
		{
			//------------ Arrange
			var initialHash = CreateHash( 1 );

			//------------ Act
			var testedHash = CreateHash( 1 );

			//------------ Assert
			testedHash.Should().Be( initialHash );
		}

		[ Test ]
		public void DifferentTypes_NotEqual()
		{
			//------------ Arrange
			var initialHash = CreateHash( 1 );

			//------------ Act
			var testedHash = CreateHash( 2 );

			//------------ Assert
			testedHash.Should().NotBe( initialHash );
		}

		[ Test ]
		public void MultipleViewsInSameOrder_Equal()
		{
			//------------ Arrange
			var initialHash = CreateHash( 1, 3, 2 );

			//------------ Act
			var testedHash = CreateHash( 1, 3, 2 );

			//------------ Assert
			testedHash.Should().Be( initialHash );
		}

		[ Test ]
		public void MultipleViewsInDifferentOrder_NotEqual()
		{
			//------------ Arrange
			var initialHash = CreateHash( 1, 3, 2 );

			//------------ Act
			var testedHash = CreateHash( 1, 2, 3 );

			//------------ Assert
			testedHash.Should().NotBe( initialHash );
		}

		private static ProjectionHash CreateHash( params int[] viewNumbers )
		{
			Condition.Requires( viewNumbers, "viewNumbers" ).IsNotEmpty();
			var viewTypes = ProjectionTestsHelper.GetViews( viewNumbers );
			var projection = ProjectionTestsHelper.GetProjection( viewNumbers[ 0 ] );
			return new ProjectionHash( projection.GetType(), viewTypes, typeof( DocumentStrategy ) );
		}
	}
}