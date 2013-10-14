using System;
using Lokad.Cqrs;
using NUnit.Framework;
using FluentAssertions;

namespace Cqrs.Azure.Tests
{
	public class AzureStorageUtilsTests
	{
		[ Test ]
		public void CutFolder_BothParts()
		{
			//--------- Arrange
			var folder = "container/folder/subfolder";

			//--------- Act
			var folderParts = AzureStorageUtils.CutFolder( folder, "/" );

			//--------- Assert
			folderParts.ContainerName.Should().Be( "container" );
			folderParts.Subfolder.Should().Be( "folder/subfolder" );
		}

		[ Test ]
		public void CutFolder_OnlyContainer()
		{
			//--------- Arrange
			var folder = "container";

			//--------- Act
			var folderParts = AzureStorageUtils.CutFolder( folder, "/" );

			//--------- Assert
			folderParts.ContainerName.Should().Be( "container" );
			folderParts.Subfolder.Should().BeNull();
		}

		// cut folder - null
		[ Test ]
		public void CutFolder_NoString()
		{
			//--------- Arrange
			var folder = "";

			//--------- Act
			Assert.Throws< ArgumentException >( () => AzureStorageUtils.CutFolder( folder, "/" ));
		}
	}
}