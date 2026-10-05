using System;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// The page watching a <see cref="TrainingRunService"/> run. EpisodeStarting fires before every episode; IsLive is read once
/// per episode, and when false (or no observer is attached) the episode runs headless at full speed with no live callbacks.
/// </summary>
internal sealed record TrainingRunObserver(
	Func<bool> IsLive,
	Action EpisodeStarting,
	Action<LiveEpisodeSnapshot> OnDecision,
	Action<LiveTickSnapshot> OnTick,
	Func<TimeSpan> TickDelay,
	Func<bool> IsActionPreviewWanted
);
