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
