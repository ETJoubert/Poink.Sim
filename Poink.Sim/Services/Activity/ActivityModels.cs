using System;

namespace Poink.Sim.Services.Activity;

/// <summary>How a background run ended; only <see cref="Completed"/> and <see cref="Failed"/> call for attention.</summary>
public enum ActivityOutcome
{
	Completed,
	Failed,
	Stopped,
	Paused,
}

/// <summary>A background source's live state; <see cref="ProgressPercent"/> is null when its progress is unknown.</summary>
public sealed record ActivityState(bool IsRunning, double? ProgressPercent)
{
	public static readonly ActivityState Idle = new(false, null);
}

/// <summary>How a source's most recent run ended, read when it stops running.</summary>
public sealed record ActivityResult(ActivityOutcome Outcome, string? Detail = null);

/// <summary>A finished run that called for attention, raised once and held against its route until acknowledged.</summary>
public sealed record ActivityCompletion(string Route, string Name, ActivityOutcome Outcome, string? Detail, DateTime FinishedUtc)
{
	public bool IsFailure => Outcome == ActivityOutcome.Failed;
}

/// <summary>Combined state of every source on one nav route, as its tab shows it.</summary>
public sealed record RouteActivity(bool IsRunning, double? ProgressPercent, ActivityCompletion? Unseen)
{
	public static readonly RouteActivity Idle = new(false, null, null);

	public bool NeedsAttention => Unseen is not null;
}
