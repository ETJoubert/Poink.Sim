using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.WeekRangeGrid;

/// <summary>
/// A single row of week cells spanning Min..Max. The selected run's first and last cells are its start and end blocks:
/// drag either to resize, drag the middle to move the whole run, or click outside it to stretch the nearer edge there.
/// The run is previewed while dragging and only reported, snapped to week boundaries, when the pointer is released.
/// </summary>
public partial class WeekRangeGrid : ComponentBase
{
	[Parameter, EditorRequired]
	public DateTime Min { get; set; }

	[Parameter, EditorRequired]
	public DateTime Max { get; set; }

	[Parameter, EditorRequired]
	public DateTime From { get; set; }

	[Parameter, EditorRequired]
	public DateTime To { get; set; }

	[Parameter]
	public EventCallback<(DateTime From, DateTime To)> RangeChanged { get; set; }

	/// <summary>Smallest selectable run, in weeks; ranges holding fewer weeks select all of them.</summary>
	[Parameter]
	public int MinWeeks { get; set; } = 2;

	[Parameter]
	public bool Disabled { get; set; }

	private enum DragMode
	{
		None,
		Start,
		End,
		Move,
	}

	private List<WeekCell> _cells = new();
	private List<WeekLabel> _labels = new();
	private (DateTime Min, DateTime Max)? _builtFor;
	private int _start;
	private int _end;
	private DragMode _drag;
	private int _moveAnchor;
	private int _moveStart;

	private int LastIndex => _cells.Count - 1;

	private int MinCells => Math.Max(1, Math.Min(MinWeeks, _cells.Count));

	protected override void OnParametersSet()
	{
		if (_builtFor != (Min, Max))
		{
			_builtFor = (Min, Max);
			_cells = WeekRangeHelper.BuildCells(Min, Max);
			_labels = WeekRangeHelper.BuildLabels(_cells);
		}

		if (_drag == DragMode.None && _cells.Count > 0)
		{
			_start = WeekRangeHelper.IndexOf(_cells, From, isEnd: false);
			_end = Math.Max(_start, WeekRangeHelper.IndexOf(_cells, To, isEnd: true));
		}
	}

	private void OnCellPointerDown(int index, PointerEventArgs e)
	{
		if (Disabled || e.Button != 0)
			return;

		if (index < _start)
		{
			_start = Math.Clamp(index, 0, Math.Max(0, _end - (MinCells - 1)));
			_drag = DragMode.Start;
		}
		else if (index > _end)
		{
			_end = Math.Clamp(index, Math.Min(LastIndex, _start + MinCells - 1), LastIndex);
			_drag = DragMode.End;
		}
		else if (index == _start && _start != _end)
			_drag = DragMode.Start;
		else if (index == _end && _start != _end)
			_drag = DragMode.End;
		else
		{
			_drag = DragMode.Move;
			_moveAnchor = index;
			_moveStart = _start;
		}
	}

	private void OnCellPointerEnter(int index)
	{
		switch (_drag)
		{
			case DragMode.Start:
				_start = Math.Clamp(index, 0, Math.Max(0, _end - (MinCells - 1)));
				break;
			case DragMode.End:
				_end = Math.Clamp(index, Math.Min(LastIndex, _start + MinCells - 1), LastIndex);
				break;
			case DragMode.Move:
				var length = _end - _start;
				_start = Math.Clamp(_moveStart + index - _moveAnchor, 0, LastIndex - length);
				_end = _start + length;
				break;
		}
	}

	private Task EndDrag()
	{
		if (_drag == DragMode.None)
			return Task.CompletedTask;

		_drag = DragMode.None;
		EnforceMinimumRun();
		return RangeChanged.InvokeAsync((_cells[_start].Start, _cells[_end].End));
	}

	// A run picked finer than a week (e.g. on the calendar) is widened the first time the grid is dragged.
	private void EnforceMinimumRun()
	{
		var shortfall = MinCells - (_end - _start + 1);
		if (shortfall <= 0)
			return;

		var grow = Math.Min(shortfall, LastIndex - _end);
		_end += grow;
		_start = Math.Max(0, _start - (shortfall - grow));
	}

	private string CellClass(int index)
	{
		if (index < _start || index > _end)
			return "";
		if (index == _start && index == _end)
			return "wrg-cell-in wrg-cell-start wrg-cell-end";
		if (index == _start)
			return "wrg-cell-in wrg-cell-start";
		if (index == _end)
			return "wrg-cell-in wrg-cell-end";
		return "wrg-cell-in wrg-cell-mid";
	}

	private string CellTitle(int index)
	{
		var cell = _cells[index];
		var hint =
			index == _start ? "drag to move the start"
			: index == _end ? "drag to move the end"
			: index > _start && index < _end ? "drag to move the whole range"
			: "click to stretch the range to this week";
		return $"Week {cell.Start:yyyy-MM-dd} to {cell.End:yyyy-MM-dd} -- {hint}";
	}
}
