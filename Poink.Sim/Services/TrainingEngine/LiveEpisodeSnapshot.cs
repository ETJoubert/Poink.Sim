using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// One decision's worth of in-progress episode state, handed to RunEpisode's optional onDecision
/// callback for the Live tab -- ClosedTrades is a defensive copy of the trader's trade log so far,
/// since the caller may read it from a different thread than the one still appending to it.
/// </summary>
internal sealed record LiveEpisodeSnapshot(
	DateTime EpisodeStart,
	DateTime Timestamp,
	double Bid,
	double Ask,
	CTraderBarRow CurrentBar,
	CTraderBarRow? ClosedBar,
	FeatureModel Features,
	SimulationAction Action,
	IReadOnlyList<float> RawDirectionOutputs,
	CapitalState Capital,
	IReadOnlyList<Trade> ClosedTrades,
	Position? OpenPosition = null,
	IReadOnlyList<float>? PreMaskDirectionOutputs = null,
	int TicksProcessed = 0,
	int TicksTotal = 0
);
