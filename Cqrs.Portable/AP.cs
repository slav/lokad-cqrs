using System;
using System.Threading.Tasks;
using Netco.ActionPolicyServices;
using Netco.Logging;

namespace Lokad.Cqrs
{
	public static class AP
	{
		private static readonly Random _random = new Random();

		public static readonly ActionPolicyAsync LongAsync = ActionPolicyAsync.Handle< Exception >().Retry( 2000, ( ex, i ) =>
		{
			typeof( AP ).Log().Trace( ex, "Retrying CQRS API call: {0}/2000", i );
			var secondsDelay = 0.5 + 0.3 * _random.Next( -1, 1 ); // randomize wait time
			Task.Delay( TimeSpan.FromSeconds( secondsDelay ) ).Wait();
		} );

		public static readonly ActionPolicyAsync Async = ActionPolicyAsync.Handle< Exception >().Retry( 200, ( ex, i ) =>
		{
			typeof( AP ).Log().Trace( ex, "Retrying CQRS API call: {0}/200", i );
			var secondsDelay = 0.5 + 0.3 * _random.Next( -1, 1 ); // randomize wait time
			Task.Delay( TimeSpan.FromSeconds( secondsDelay ) ).Wait();
		} );
	}
}