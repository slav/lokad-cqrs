using Netco.Logging;
using NUnit.Framework;

namespace Cqrs.Portable.Tests
{
	[ SetUpFixture ]
	public class TestsSetup
	{
		[ SetUp ]
		public void Init()
		{
			NetcoLogger.LoggerFactory = new ConsoleLoggerFactory();
		}

		[ TearDown ]
		public void TearDown()
		{
		}
	}
}