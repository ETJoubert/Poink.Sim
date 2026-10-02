using System;
using Poink.Sim.Models;

namespace Poink.Sim.SignalStudy;

/// <summary>
/// Replays a tick range headlessly and stores the rule engine's signals once per second as a signal dataset. One build
/// runs at a time and carries on in the background when the page is left.
/// </summary>
internal interface ISignalDatasetBuilder
{
	bool IsBuilding { get; }

	SignalDatasetBuildProgress? Progress { get; }

	/// <summary>Why the last build failed, or null.</summary>
	string? LastError { get; }

	/// <summary>Raised whenever the build state or its progress changes.</summary>
	event Action? OnChanged;

	/// <summary>Starts a build; false when one is already running.</summary>
	bool TryStart(SignalDatasetBuildRequest request);

	void Cancel();
}
