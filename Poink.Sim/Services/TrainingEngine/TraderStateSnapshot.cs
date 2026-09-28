using System.Collections.Generic;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Raw trader/account values for one step, before TraderStateOptionsModel decides which of them
/// are actually included in the feature vector. History lists are newest-first and may be shorter
/// than the configured list length early in an episode -- FeatureAssembler pads them with zeros.
/// </summary>
internal sealed record TraderStateSnapshot(
	double Position,
	double PositionPrice,
	double PositionTicks,
	double PositionDistance,
	double CurrentPrice,
	double Cash,
	double Realized,
	double Unrealized,
	double Equity,
	double Margin,
	double MarginLevel,
	double PositionCount,
	IReadOnlyList<double> RecentOutcomes,
	IReadOnlyList<double> RecentPositionLevels,
	IReadOnlyList<double> RecentDirections
);
