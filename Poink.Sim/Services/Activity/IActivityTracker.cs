using System;

namespace Poink.Sim.Services.Activity;

/// <summary>
/// Process-wide view of every background source, grouped by nav route, holding finished runs until their tab is visited.
/// </summary>
public interface IActivityTracker
{
	RouteActivity GetRoute(string route);

	/// <summary>Clears the route's unseen completion, if any.</summary>
	void Acknowledge(string route);

	/// <summary>Raised from any thread when a route's running state, progress or unseen completion changes.</summary>
	event Action? Changed;

	/// <summary>Raised from any thread once per run that finishes as completed or failed.</summary>
	event Action<ActivityCompletion>? Completed;
}
