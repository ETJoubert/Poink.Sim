using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Poink.Sim.Services.Activity;

/// <summary>
/// Watches every registered <see cref="IActivitySource"/>, turning running-to-idle transitions into completions and
/// holding the latest one per route until <see cref="Acknowledge"/> is called for it.
/// </summary>
internal sealed class ActivityTracker : IActivityTracker, IDisposable
{
	private readonly object _lock = new();
	private readonly IReadOnlyList<IActivitySource> _sources;
	private readonly Dictionary<IActivitySource, ActivityState> _states = new();
	private readonly Dictionary<IActivitySource, Action> _handlers = new();
	private readonly Dictionary<string, ActivityCompletion> _unseen = new(StringComparer.OrdinalIgnoreCase);
	private readonly ILogger<ActivityTracker> _logger;

	public ActivityTracker(IEnumerable<IActivitySource> sources, ILogger<ActivityTracker> logger)
	{
		_sources = sources.ToList();
		_logger = logger;

		foreach (var source in _sources)
		{
			_states[source] = Quantize(source.Current);
			Action handler = () => HandleSourceChanged(source);
			_handlers[source] = handler;
			source.Changed += handler;
		}

		_logger.LogInformation(
			"Activity tracker watching {Count} sources: {Sources}",
			_sources.Count,
			string.Join(", ", _sources.Select(s => $"{s.Name} ({s.Route})"))
		);
	}

	public event Action? Changed;
	public event Action<ActivityCompletion>? Completed;

	public RouteActivity GetRoute(string route)
	{
		lock (_lock)
		{
			var running = _sources
				.Where(s => string.Equals(s.Route, route, StringComparison.OrdinalIgnoreCase))
				.Select(s => _states[s])
				.Where(state => state.IsRunning)
				.ToList();
			var unseen = _unseen.GetValueOrDefault(route);
			if (running.Count == 0)
				return unseen is null ? RouteActivity.Idle : new RouteActivity(false, null, unseen);

			var known = running.Where(state => state.ProgressPercent.HasValue).Select(state => state.ProgressPercent!.Value).ToList();
			return new RouteActivity(true, known.Count == 0 ? null : known.Average(), unseen);
		}
	}

	public void Acknowledge(string route)
	{
		bool removed;
		lock (_lock)
			removed = _unseen.Remove(route);
		if (removed)
			Changed?.Invoke();
	}

	private void HandleSourceChanged(IActivitySource source)
	{
		var next = Quantize(source.Current);
		ActivityCompletion? completion = null;
		lock (_lock)
		{
			var previous = _states[source];
			if (previous == next)
				return;
			_states[source] = next;

			if (previous.IsRunning && !next.IsRunning)
				completion = ToCompletion(source);
			if (completion is not null)
				_unseen[source.Route] = completion;
		}

		Changed?.Invoke();
		if (completion is null)
			return;
		_logger.LogDebug("Activity {Source} finished: {Outcome} {Detail}", completion.Name, completion.Outcome, completion.Detail);
		Completed?.Invoke(completion);
	}

	private static ActivityCompletion? ToCompletion(IActivitySource source)
	{
		var result = source.LastResult;
		return result.Outcome is ActivityOutcome.Completed or ActivityOutcome.Failed
			? new ActivityCompletion(source.Route, source.Name, result.Outcome, result.Detail, DateTime.UtcNow)
			: null;
	}

	// Whole-percent resolution caps nav re-renders at what the dial can actually show.
	private static ActivityState Quantize(ActivityState state) =>
		state.IsRunning
			? new ActivityState(true, state.ProgressPercent is { } p ? Math.Clamp(Math.Floor(p), 0, 100) : null)
			: ActivityState.Idle;

	public void Dispose()
	{
		foreach (var (source, handler) in _handlers)
			source.Changed -= handler;
	}
}
