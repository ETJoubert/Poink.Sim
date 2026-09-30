namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// The rank/highlight overlay toggles for XGrid's Total/Up/Down/Delta+/Delta- spectrums, shared
	/// between MetricsControlPanel (editing) and XGrid (canvas highlight borders). Immutable for the
	/// same reason as XGridDisplaySettings.
	/// </summary>
	public sealed record XGridSpectrumSettings
	{
		/// <summary>Quick on/off for every Highlights control (including Low/High) -- disabling doesn't clear any of their settings.</summary>
		public bool HighlightsEnabled { get; init; } = true;

		public XGridHighlightMode HighlightMode { get; init; } = XGridHighlightMode.Rank;

		public bool TotalShow { get; init; }
		public double TotalPercent { get; init; } = 10;

		public bool UpShow { get; init; }
		public double UpPercent { get; init; } = 10;

		public bool DownShow { get; init; }
		public double DownPercent { get; init; } = 10;

		public bool DeltaPosShow { get; init; }
		public double DeltaPosPercent { get; init; } = 10;

		public bool DeltaNegShow { get; init; }
		public double DeltaNegPercent { get; init; } = 10;

		/// <summary>Weight Total/Up/Down/Delta+/Delta- by moved price distance instead of raw tick count.</summary>
		public bool DistanceWeighted { get; init; }
	}
}
