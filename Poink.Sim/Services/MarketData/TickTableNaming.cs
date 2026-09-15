using System.Linq;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>
	/// Maps a cTrader symbol name to its Postgres tick table. Convention: "live_ticks_" or "demo_ticks_"
	/// (depending on the account environment) + the symbol name lower-cased with every non-alphanumeric
	/// character removed (e.g. "EURUSD" on a live account -> "live_ticks_eurusd"). Live and demo data for
	/// the same symbol land in separate tables because their price series diverge.
	/// The result is used as a SQL identifier, so it is restricted to [a-z0-9_] and never interpolated
	/// from raw user input.
	/// </summary>
	public static class TickTableNaming
	{
		public const string LivePrefix = "live_ticks_";
		public const string DemoPrefix = "demo_ticks_";

		public static string ForSymbol(string symbolName, bool isLive)
		{
			var cleaned = new string(symbolName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
			return (isLive ? LivePrefix : DemoPrefix) + cleaned;
		}
	}
}
