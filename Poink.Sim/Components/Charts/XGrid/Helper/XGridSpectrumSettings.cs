namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// The rank/highlight overlay toggles for XGrid's Total/Up/Down/Delta+/Delta- spectrums, shared
	/// between MetricsControlPanel (editing), XGrid (canvas highlight borders), and XGridSignalsPanel
	/// (current-cell signal dots). Immutable for the same reason as XGridDisplaySettings.
	/// </summary>
	public sealed record XGridSpectrumSettings
	{
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
	}
}
