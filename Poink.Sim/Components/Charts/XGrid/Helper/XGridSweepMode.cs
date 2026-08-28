namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Which precomputed sweep array (XGridResult.SweepLeftToRight/SweepRightToLeft/SweepSum) the
	/// grid shades rows by, or None to disable. Public since XGridDisplaySettings exposes it.
	/// </summary>
	public enum XGridSweepMode
	{
		None,
		LeftToRight,
		RightToLeft,
		Sum,
	}
}
