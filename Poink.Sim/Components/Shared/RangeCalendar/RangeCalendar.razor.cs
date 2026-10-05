using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.RangeCalendar;

/// <summary>Which end of the range the next day click or time edit applies to.</summary>
public enum RangeEdge
{
	Start,
	End,
}

/// <summary>
/// One calendar for a start/end datetime pair: the range is shaded across the day grid (and the full-year view),
/// day clicks set the active edge -- moving on to End after Start -- and each edge keeps its own time to the minute.
/// </summary>
public partial class RangeCalendar : ComponentBase
{
	[Parameter, EditorRequired]
	public DateTime Start { get; set; }

	[Parameter, EditorRequired]
	public DateTime End { get; set; }

	[Parameter]
	public EventCallback<(DateTime Start, DateTime End)> RangeChanged { get; set; }

	[Parameter]
	public DateTime? Min { get; set; }

	[Parameter]
	public DateTime? Max { get; set; }

	/// <summary>The edge active when the calendar first opens.</summary>
	[Parameter]
	public RangeEdge InitialEdge { get; set; }

	private static readonly string[] DayHeaders = { "S", "M", "T", "W", "T", "F", "S" };

	private static readonly TimeSpan MinimumSpan = TimeSpan.FromMinutes(1);

	private enum CalendarView
	{
		Days,
		Months,
		Years,
	}

	private const int YearsPerPage = 12;

	private DateTime _displayedMonth;
	private CalendarView _view = CalendarView.Days;
	private RangeEdge _activeEdge;

	protected override void OnInitialized()
	{
		_activeEdge = InitialEdge;
		var shown = ActiveValue;
		_displayedMonth = new DateTime(shown.Year, shown.Month, 1);
	}

	private DateTime ActiveValue => _activeEdge == RangeEdge.Start ? Start : End;

	private void SelectEdge(RangeEdge edge)
	{
		_activeEdge = edge;
		var shown = ActiveValue;
		_displayedMonth = new DateTime(shown.Year, shown.Month, 1);
		_view = CalendarView.Days;
	}

	private string MonthLabel => _displayedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

	private void ShowMonths() => _view = CalendarView.Months;

	private void ShowYears() => _view = CalendarView.Years;

	private bool CanGoPrevMonth => Min is null || _displayedMonth > new DateTime(Min.Value.Year, Min.Value.Month, 1);
	private bool CanGoNextMonth => Max is null || _displayedMonth < new DateTime(Max.Value.Year, Max.Value.Month, 1);

	private bool CanGoPrevYear => Min is null || _displayedMonth.Year > Min.Value.Year;
	private bool CanGoNextYear => Max is null || _displayedMonth.Year < Max.Value.Year;

	private void PrevMonth()
	{
		if (CanGoPrevMonth)
			_displayedMonth = _displayedMonth.AddMonths(-1);
	}

	private void NextMonth()
	{
		if (CanGoNextMonth)
			_displayedMonth = _displayedMonth.AddMonths(1);
	}

	private void PrevYear()
	{
		if (CanGoPrevYear)
			_displayedMonth = _displayedMonth.AddYears(-1);
	}

	private void NextYear()
	{
		if (CanGoNextYear)
			_displayedMonth = _displayedMonth.AddYears(1);
	}

	private int YearRangeStart => (_displayedMonth.Year / YearsPerPage) * YearsPerPage;
	private string YearRangeLabel => $"{YearRangeStart} - {YearRangeStart + YearsPerPage - 1}";

	private bool CanGoPrevYearRange => Min is null || YearRangeStart - 1 >= Min.Value.Year;
	private bool CanGoNextYearRange => Max is null || YearRangeStart + YearsPerPage <= Max.Value.Year;

	private void PrevYearRange()
	{
		if (CanGoPrevYearRange)
			_displayedMonth = _displayedMonth.AddYears(-YearsPerPage);
	}

	private void NextYearRange()
	{
		if (CanGoNextYearRange)
			_displayedMonth = _displayedMonth.AddYears(YearsPerPage);
	}

	private sealed record YearCell(int Year, bool IsCurrent, bool IsInRange, bool IsDisabled);

	private List<YearCell> Years
	{
		get
		{
			var start = YearRangeStart;
			var today = DateTime.Today;

			var cells = new List<YearCell>(YearsPerPage);
			for (var i = 0; i < YearsPerPage; i++)
			{
				var year = start + i;
				var yearStart = new DateTime(year, 1, 1);
				var yearEnd = new DateTime(year, 12, 31);
				var disabled =
					(Min is not null && yearEnd < Min.Value.Date) || (Max is not null && yearStart > Max.Value.Date);
				cells.Add(new YearCell(year, year == today.Year, year >= Start.Year && year <= End.Year, disabled));
			}
			return cells;
		}
	}

	private void SelectYear(YearCell cell)
	{
		if (cell.IsDisabled)
			return;
		_displayedMonth = new DateTime(cell.Year, _displayedMonth.Month, 1);
		_view = CalendarView.Months;
	}

	private sealed record MonthCell(int Month, string Label, bool IsCurrent, bool IsInRange, bool IsDisabled);

	private List<MonthCell> Months
	{
		get
		{
			var year = _displayedMonth.Year;
			var today = DateTime.Today;

			var cells = new List<MonthCell>(12);
			for (var m = 1; m <= 12; m++)
			{
				var monthStart = new DateTime(year, m, 1);
				var monthEnd = monthStart.AddMonths(1).AddDays(-1);
				var disabled =
					(Min is not null && monthEnd < Min.Value.Date) || (Max is not null && monthStart > Max.Value.Date);
				var inRange = monthEnd >= Start.Date && monthStart <= End.Date;
				var label = monthStart.ToString("MMM", CultureInfo.InvariantCulture);
				cells.Add(new MonthCell(m, label, year == today.Year && m == today.Month, inRange, disabled));
			}
			return cells;
		}
	}

	private void SelectMonth(MonthCell cell)
	{
		if (cell.IsDisabled)
			return;
		_displayedMonth = new DateTime(_displayedMonth.Year, cell.Month, 1);
		_view = CalendarView.Days;
	}

	private sealed record DayCell(DateTime Date, bool InMonth, bool IsToday, bool IsDisabled);

	private List<DayCell> Days => BuildMonthDays(_displayedMonth);

	private List<DayCell> BuildMonthDays(DateTime monthStart)
	{
		var leadingDays = (int)monthStart.DayOfWeek;
		var gridStart = monthStart.AddDays(-leadingDays);
		var today = DateTime.Today;

		var cells = new List<DayCell>(42);
		for (var i = 0; i < 42; i++)
		{
			var date = gridStart.AddDays(i);
			var inMonth = date.Month == monthStart.Month && date.Year == monthStart.Year;
			var disabled = (Min is not null && date < Min.Value.Date) || (Max is not null && date > Max.Value.Date);
			cells.Add(new DayCell(date, inMonth, date == today, disabled));
		}
		return cells;
	}

	private string DayRangeClass(DateTime date)
	{
		var isStart = date == Start.Date;
		var isEnd = date == End.Date;
		if (isStart && isEnd)
			return "cal-day-range-start cal-day-range-end";
		if (isStart)
			return "cal-day-range-start";
		if (isEnd)
			return "cal-day-range-end";
		return date > Start.Date && date < End.Date ? "cal-day-in-range" : "";
	}

	private string DayTitle(DateTime date)
	{
		var target = _activeEdge == RangeEdge.Start ? "start" : "end";
		return $"{date:yyyy-MM-dd} -- click to set the range {target} to this day";
	}

	// Clicking End before the start moves the start there instead, so the range never inverts.
	private Task SelectDay(DayCell cell)
	{
		if (cell.IsDisabled)
			return Task.CompletedTask;

		var date = cell.Date.Date;
		if (_activeEdge == RangeEdge.Start)
		{
			var start = OnDay(date, Start);
			var end = End > start ? End : OnDay(date, End);
			_activeEdge = RangeEdge.End;
			return Apply(start, end <= start ? start.AddDays(1) : end);
		}

		var newEnd = OnDay(date, End);
		return newEnd <= Start ? Apply(OnDay(date, Start), End) : Apply(Start, newEnd);
	}

	private static DateTime OnDay(DateTime date, DateTime timeSource) =>
		DateTime.SpecifyKind(date.Date + timeSource.TimeOfDay, timeSource.Kind);

	private int ActiveHour => ActiveValue.Hour;

	private int ActiveMinute => ActiveValue.Minute;

	private Task OnHourChanged(ChangeEventArgs e) =>
		int.TryParse(e.Value?.ToString(), out var hour) ? ApplyTime(Math.Clamp(hour, 0, 23), ActiveMinute) : Task.CompletedTask;

	private Task OnMinuteChanged(ChangeEventArgs e) =>
		int.TryParse(e.Value?.ToString(), out var minute) ? ApplyTime(ActiveHour, Math.Clamp(minute, 0, 59)) : Task.CompletedTask;

	private Task OnHourWheel(WheelEventArgs e) =>
		ApplyTime(((ActiveHour + (e.DeltaY < 0 ? 1 : -1)) % 24 + 24) % 24, ActiveMinute);

	private Task OnMinuteWheel(WheelEventArgs e) =>
		ApplyTime(ActiveHour, ((ActiveMinute + (e.DeltaY < 0 ? 1 : -1)) % 60 + 60) % 60);

	private Task ApplyTime(int hour, int minute)
	{
		var value = ActiveValue.Date.AddHours(hour).AddMinutes(minute);
		return _activeEdge == RangeEdge.Start
			? Apply(value < End ? value : End - MinimumSpan, End)
			: Apply(Start, value > Start ? value : Start + MinimumSpan);
	}

	private Task Apply(DateTime start, DateTime end)
	{
		start = ClampToBounds(start);
		end = ClampToBounds(end);
		if (end <= start)
			end = ClampToBounds(start + MinimumSpan);
		return RangeChanged.InvokeAsync((start, end));
	}

	private DateTime ClampToBounds(DateTime dt)
	{
		if (Min is not null && dt < Min.Value)
			return Min.Value;
		if (Max is not null && dt > Max.Value)
			return Max.Value;
		return dt;
	}

	private bool _showFullYear;
	private int _fullYearYear;

	private void ToggleFullYear()
	{
		_showFullYear = !_showFullYear;
		if (_showFullYear)
			_fullYearYear = _displayedMonth.Year;
	}

	private bool CanGoPrevFullYear => Min is null || _fullYearYear > Min.Value.Year;
	private bool CanGoNextFullYear => Max is null || _fullYearYear < Max.Value.Year;

	private void PrevFullYear()
	{
		if (CanGoPrevFullYear)
			_fullYearYear--;
	}

	private void NextFullYear()
	{
		if (CanGoNextFullYear)
			_fullYearYear++;
	}

	private sealed record MiniMonth(string Label, List<DayCell> Days);

	private List<MiniMonth> FullYearMonths
	{
		get
		{
			var cells = new List<MiniMonth>(12);
			for (var m = 1; m <= 12; m++)
			{
				var monthStart = new DateTime(_fullYearYear, m, 1);
				cells.Add(new MiniMonth(monthStart.ToString("MMM", CultureInfo.InvariantCulture), BuildMonthDays(monthStart)));
			}
			return cells;
		}
	}

	private Task SelectFullYearDay(DayCell cell)
	{
		if (cell.IsDisabled)
			return Task.CompletedTask;
		_displayedMonth = new DateTime(cell.Date.Year, cell.Date.Month, 1);
		_showFullYear = false;
		return SelectDay(cell);
	}
}
