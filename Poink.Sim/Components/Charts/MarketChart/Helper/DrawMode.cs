namespace Poink.Sim.Components.Charts
{
	/// <summary>TRange/PRange/Rectangle are built from repeated H/V-line placement, not their own shape; VolumeRange is unrelated to line placement entirely.</summary>
	public enum DrawMode
	{
		None,
		Horizontal,
		Vertical,
		Diagonal,
		TRange,
		PRange,
		Rectangle,
		TRangeChart,
		TRangeReferenceChart,
		VolumeRange,
	}
}
