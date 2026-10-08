namespace Poink.Sim.Components.Pages.Trade.Models
{
	/// <summary>
	/// The price span of one XGrid column (its lowest occupied row's floor to its highest occupied row's ceiling)
	/// and the current price measured against it.
	/// </summary>
	public sealed record ColumnZoneRange(double Low, double High, double Price)
	{
		public double Span => High - Low;
	}

	/// <summary>
	/// The column zone sliders' per-browser settings: columns n to n-<see cref="ColumnsBack"/>, the LOW/HIGH share of each
	/// span, and whether the gate holds LONG/SHORT locked until <see cref="GateThreshold"/> of those columns support the side.
	/// </summary>
	public sealed record ColumnZoneSettings(int ColumnsBack, double ZonePercent, bool GateEnabled, int GateThreshold);

	/// <summary>
	/// How many of <see cref="ColumnCount"/> columns support each side; while the gate is enabled a side stays locked
	/// until its supporters reach <see cref="Threshold"/>.
	/// </summary>
	public sealed record ColumnZoneGate(bool Enabled, int LongSupporters, int ShortSupporters, int Threshold, int ColumnCount)
	{
		public bool LongLocked => Enabled && LongSupporters < Threshold;

		public bool ShortLocked => Enabled && ShortSupporters < Threshold;

		public bool IsLocked(bool isLong) => isLong ? LongLocked : ShortLocked;

		public int Supporters(bool isLong) => isLong ? LongSupporters : ShortSupporters;
	}

	/// <summary>Where the current price sits against a column's span, bottom to top; Below and Above fall outside it.</summary>
	public enum ColumnZone
	{
		Below,
		Low,
		Middle,
		High,
		Above,
	}
}
