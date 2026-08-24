using System.Linq;

namespace Poink.Sim.Services.MarketData
{
	/// <summary>
	/// Maps a cTrader symbol name to its Postgres tick table. Convention: "ticks_" + the symbol name
	/// lower-cased with every non-alphanumeric character removed (e.g. "EURUSD" -> "ticks_eurusd").
	/// The result is used as a SQL identifier, so it is restricted to [a-z0-9_] and never interpolated
	/// from raw user input.
	/// </summary>
	public static class TickTableNaming
	{
		public const string Prefix = "ticks_";

		public static string ForSymbol(string symbolName)
		{
			var cleaned = new string(symbolName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
			return Prefix + cleaned;
		}
	}
}
