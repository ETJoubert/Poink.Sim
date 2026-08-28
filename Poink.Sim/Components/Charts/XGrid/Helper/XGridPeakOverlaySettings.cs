namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Swing-low/-high overlay config for one direction, shared between MetricsControlPanel (editing)
	/// and XGrid/XGridSignalsPanel (ranking via <see cref="Settings"/>). Immutable for the same reason
	/// as XGridDisplaySettings.
	/// </summary>
	public sealed record XGridPeakOverlaySettings
	{
		public bool ShowHighlight { get; init; }
		public XGridPeakMode Mode { get; init; } = XGridPeakMode.Fixed;
		public double MinProminence { get; init; } = 2;
		public double AdaptivePercent { get; init; } = 20;
		public int MinDistance { get; init; } = 10;

		internal XGridPeakSettings Settings => new(Mode, MinProminence, AdaptivePercent, MinDistance);
	}
}
