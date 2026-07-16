// High/Low/Open/Close/Average/Volume/Distance/Time bar: an alternative to plain OHLC that
// keeps the usual Open/Close price levels and per-edge timestamps (Open/Close/High/Low)
// alongside the typical (mean) price, tick count, and how far price actually traveled
// tick-to-tick within the bar (path length, not just High-Low range).
// UpVolume/DownVolume split the tick count by the classic tick rule (uptick vs downtick);
// they can undershoot Volume since zero ticks (unchanged mid) count toward neither.
//
// Poink.Sim's own copy of Poink.ML's Features/HlocAvdt/HlocAvdt.cs shape (kept duplicated,
// not shared, so this project carries no Poink.ML dependency) -- SimulationBarAggregator's
// per-tick math already mirrors HlocAvdtAggregator.Aggregate independently.
using System;

namespace Poink.Sim.Features.Simulation;

internal readonly record struct HlocAvdtBar(
    DateTime OpenTime, DateTime CloseTime, DateTime HighTime, DateTime LowTime,
    double Open, double High, double Low, double Close,
    double Average, int Volume, double Distance, int UpVolume, int DownVolume);
