using System;
using Poink.Sim.Repository;

namespace Poink.Sim.Simulation;

// Fixed-width, absolute-origin price bucket (same scheme as BarPriceHistogram) -- stays
// stable across a run, unlike XGrid's own window-derived rows.
internal sealed class PriceCellTracker
{
	private readonly double _cellWidth;
	private long? _currentCell;

	public PriceCellTracker(double cellWidth) => _cellWidth = cellWidth;

	// Null on the tick that establishes the starting cell -- nothing to have left yet, same
	// shape as SimulationBarAggregator's first partial bar.
	public (long Cell, double Low, double High)? Feed(Tick tick)
	{
		double mid = (tick.Bid + tick.Ask) / 2.0;
		long cell = (long)Math.Floor(mid / _cellWidth);

		bool hadPrevious = _currentCell is not null;
		bool changed = _currentCell != cell;
		_currentCell = cell;

		return hadPrevious && changed ? (cell, cell * _cellWidth, (cell + 1) * _cellWidth) : null;
	}

	public void Reset() => _currentCell = null;
}
