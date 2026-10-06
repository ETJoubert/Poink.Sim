using System;

namespace Poink.Sim.Helpers;

/// <summary>Converts between prices and whole ticks on the Emulator's price grid.</summary>
internal static class EmulatorTickHelper
{
	public static long ToTicks(double price, double tickSize) => (long)Math.Round(price / tickSize);

	public static double FromTicks(long ticks, double tickSize) => Math.Round(ticks * tickSize, 10);

	public static double RoundToTick(double price, double tickSize) => FromTicks(ToTicks(price, tickSize), tickSize);
}
