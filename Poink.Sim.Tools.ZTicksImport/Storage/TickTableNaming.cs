using System.Linq;

namespace Poink.Sim.Tools.ZTicksImport.Storage;

internal static class TickTableNaming
{
	public const string LivePrefix = "live_ticks_";
	public const string DemoPrefix = "demo_ticks_";

	public static string ForSymbol(string symbolName, bool isLive)
	{
		var cleaned = new string(symbolName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
		return (isLive ? LivePrefix : DemoPrefix) + cleaned;
	}
}
