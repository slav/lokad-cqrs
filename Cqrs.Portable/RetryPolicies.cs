using System;
using System.Threading.Tasks;
using Netco.ActionPolicyServices;
using Netco.Logging;

namespace Lokad.Cqrs
{
	public static class RetryPolicies
	{
		 public static ActionPolicy GetPolicy
		{
			get { return _getPolicy; }
		}

		private static readonly ActionPolicy _getPolicy = ActionPolicy.Handle< Exception >().Retry( 15, ( ex, i ) =>
			{
				_logger.Trace( ex, "Retrying get call for the {0} time", i );
				Task.Delay( TimeSpan.FromSeconds( 0.2 + i ) ).Wait();
			} );

		private static readonly ILogger _logger = NetcoLogger.GetLogger( typeof( RetryPolicies ) );
	}
}