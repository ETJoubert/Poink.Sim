using System.Collections.Generic;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// One episode's ticks. Everything before TrainingStartIndex is the preloaded feature window: it feeds
/// features and bars but is never traded or learned from.
/// </summary>
internal sealed record EpisodeData(IReadOnlyList<Tick> Ticks, int TrainingStartIndex)
{
	public static EpisodeData Empty { get; } = new([], 0);

	public int TrainingTickCount => Ticks.Count - TrainingStartIndex;
}
