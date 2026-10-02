using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Pages.Analize
{
	/// <summary>One barrier-map cell's figures and the distributions of its trade results and times to exit.</summary>
	public partial class BarrierCellDetail : ComponentBase
	{
		[Parameter, EditorRequired]
		public BarrierMapResult Result { get; set; } = default!;

		[Parameter, EditorRequired]
		public BarrierMapSelection Selection { get; set; } = default!;

		private BarrierCell? Cell =>
			Selection.CellIndex is { } index && Result.Grid(Selection.Side, Selection.TimeLimitMinutes) is { } grid && index < grid.Cells.Count
				? grid.Cells[index]
				: null;

		private string LimitLabel =>
			Selection.TimeLimitMinutes % 60 == 0 ? $"{Selection.TimeLimitMinutes / 60}h limit" : $"{Selection.TimeLimitMinutes}m limit";

		private IReadOnlyList<HistogramBar> PipsBins =>
			Cell is { } cell ? Bars(cell.PipsHistogram, cell.PipsHistogramLow, cell.PipsHistogramHigh) : Array.Empty<HistogramBar>();

		private IReadOnlyList<HistogramBar> MinutesBins =>
			Cell is { } cell ? Bars(cell.MinutesHistogram, 0, Selection.TimeLimitMinutes) : Array.Empty<HistogramBar>();

		private static IReadOnlyList<HistogramBar> Bars(IReadOnlyList<long> counts, double low, double high)
		{
			double width = counts.Count == 0 ? 0 : (high - low) / counts.Count;
			return counts.Select((count, i) => new HistogramBar(low + i * width, low + (i + 1) * width, count)).ToList();
		}

		private static string SignClass(double? value) =>
			value switch
			{
				> 0 => "barrier-pos",
				< 0 => "barrier-neg",
				_ => "",
			};
	}
}
