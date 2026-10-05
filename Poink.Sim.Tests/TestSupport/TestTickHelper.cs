using System;
using Poink.Sim.Repository;

namespace Poink.Sim.Tests.TestSupport;

internal static class TestTickHelper
{
	public static readonly DateTime Origin = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

	public static Tick AtMid(double seconds, double mid) => new(Origin.AddSeconds(seconds), mid, mid);

	public static Tick AtQuote(double seconds, double bid, double ask) => new(Origin.AddSeconds(seconds), bid, ask);
}
