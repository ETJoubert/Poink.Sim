namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// The row/column overlay toggles XGridController owns and passes into XGrid for both editing and
	/// rendering -- immutable so a change always flows back through XGrid's DisplayChanged callback
	/// rather than being mutated in place.
	/// </summary>
	public sealed record XGridDisplaySettings
	{
		/// <summary>Quick on/off for every overlay below -- disabling doesn't clear any of their settings.</summary>
		public bool OverlaysEnabled { get; init; } = true;

		public bool ShowPoc { get; init; }
		public bool ShowVah { get; init; }
		public bool ShowVal { get; init; }
		public bool ShowVwap { get; init; }
		public bool ShowAverage { get; init; }
		public bool ShowMedian { get; init; }
		public bool ShowRegressionLine { get; init; }
		public bool ShowDayLines { get; init; }
		public bool ShowIntervalLines { get; init; }
		public int IntervalHours { get; init; } = 4;
		public XGridSweepMode SweepMode { get; init; } = XGridSweepMode.None;
		public bool ShadeDelta { get; init; }
		public bool DistanceWeighted { get; init; }

		/// <summary>Shades each cell by how long price spent there; exclusive with ShadeDelta and DistanceWeighted.</summary>
		public bool TimeWeighted { get; init; }

		/// <summary>Shades each cell by its seconds times its tick count; exclusive with TimeWeighted, ShadeDelta and DistanceWeighted.</summary>
		public bool TimeCountWeighted { get; init; }

		/// <summary>Splits TimeCountWeighted into signed seconds-times-up versus seconds-times-down shading; only set alongside it.</summary>
		public bool TimeCountSplit { get; init; }

		/// <summary>Weights TimeCountWeighted (and its split) by each tick's moved distance -- time × ticks × average move; only set alongside it.</summary>
		public bool TimeCountDistance { get; init; }

		public bool ShowDwellHighlight { get; init; }
		public XGridDwellComparison DwellComparison { get; init; } = XGridDwellComparison.Above;
		public double DwellSeconds { get; init; } = 60;
	}
}
