using System;

namespace Poink.Sim.Services.Activity;

/// <summary>
/// Adapts one long-running service (simulation, training, import, analysis) to the nav's activity indicators.
/// </summary>
public interface IActivitySource
{
	/// <summary>Display name used in toasts and tooltips.</summary>
	string Name { get; }

	/// <summary>The nav route whose tab shows this source's activity.</summary>
	string Route { get; }

	ActivityState Current { get; }

	ActivityResult LastResult { get; }

	/// <summary>Raised from any thread whenever <see cref="Current"/> or <see cref="LastResult"/> may have changed.</summary>
	event Action? Changed;
}
