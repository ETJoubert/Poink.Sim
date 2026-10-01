using System;
using System.Collections.Generic;
using Poink.Sim.Models;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// The market and account state after one tick, handed to RunEpisode's optional onTick callback
/// for the Live tab. ClosedTrades is only a copy when a trade closed on this tick, otherwise null.
/// RawDirectionOutputs (after Action Masking) and PreMaskDirectionOutputs (before it) are the network's current preview of the direction logits, refreshed on a wall-clock
/// throttle rather than every tick, and null on ticks that skipped it.
/// </summary>
internal sealed record LiveTickSnapshot(
	DateTime Timestamp,
	double Bid,
	double Ask,
	CTraderBarRow CurrentBar,
	CTraderBarRow? ClosedBar,
	CapitalState Capital,
	Position? OpenPosition,
	IReadOnlyList<Trade>? ClosedTrades,
	IReadOnlyList<float>? RawDirectionOutputs = null,
	IReadOnlyList<float>? PreMaskDirectionOutputs = null
);
