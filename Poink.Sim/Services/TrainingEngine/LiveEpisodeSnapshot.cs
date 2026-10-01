using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// One decision's worth of in-progress episode state, handed to RunEpisode's optional onDecision
/// callback for the Live tab -- ClosedTrades is a defensive copy of the trader's trade log so far,
/// since the caller may read it from a different thread than the one still appending to it. EpisodeStart
/// is where training begins; DataStart is the earlier start of the preloaded feature window, whose
/// closed bars arrive as HistoryBars.
/// FeatureWindow is the trailing span the features actually read at each decision.
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
	int TicksTotal = 0,
	DateTime EpisodeEnd = default,
	double EpisodeMinPrice = 0,
	double EpisodeMaxPrice = 0,
	DateTime DataStart = default,
	IReadOnlyList<CTraderBarRow>? HistoryBars = null,
	TimeSpan FeatureWindow = default
);
