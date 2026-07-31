using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.Calendar;

// Pure month-grid date+time picker -- no popover/backdrop of its own, that's
// the caller's responsibility (see DateRangeSlider, which opens one of these
// in a dropdown per from/to label and only closes it on an outside click --
// picking a day or nudging the time here never closes the popover itself).
public partial class Calendar : ComponentBase
{
    [Parameter] public DateTime Value { get; set; }
    [Parameter] public EventCallback<DateTime> ValueChanged { get; set; }
    [Parameter] public DateTime? Min { get; set; }
    [Parameter] public DateTime? Max { get; set; }

    private static readonly string[] DayHeaders = { "S", "M", "T", "W", "T", "F", "S" };

    private enum CalendarView { Days, Months, Years }

    private const int YearsPerPage = 12;

    private DateTime _displayedMonth;
    private CalendarView _view = CalendarView.Days;
    private int _hour;
    private int _minute;

    // Set once per instance (not OnParametersSet) so browsing months isn't
    // reset by parent re-renders while the popover stays open -- DateRangeSlider
    // mounts a fresh instance each time it opens via @if, so this still starts
    // on Value's month every time the popover is opened.
    protected override void OnInitialized()
    {
        _displayedMonth = new DateTime(Value.Year, Value.Month, 1);
        _hour = Value.Hour;
        _minute = Value.Minute;
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
        if (CanGoPrevMonth) _displayedMonth = _displayedMonth.AddMonths(-1);
    }

    private void NextMonth()
    {
        if (CanGoNextMonth) _displayedMonth = _displayedMonth.AddMonths(1);
    }

    private void PrevYear()
    {
        if (CanGoPrevYear) _displayedMonth = _displayedMonth.AddYears(-1);
    }

    private void NextYear()
    {
        if (CanGoNextYear) _displayedMonth = _displayedMonth.AddYears(1);
    }

    private int YearRangeStart => (_displayedMonth.Year / YearsPerPage) * YearsPerPage;
    private string YearRangeLabel => $"{YearRangeStart} - {YearRangeStart + YearsPerPage - 1}";

    private bool CanGoPrevYearRange => Min is null || YearRangeStart - 1 >= Min.Value.Year;
    private bool CanGoNextYearRange => Max is null || YearRangeStart + YearsPerPage <= Max.Value.Year;

    private void PrevYearRange()
    {
        if (CanGoPrevYearRange) _displayedMonth = _displayedMonth.AddYears(-YearsPerPage);
    }

    private void NextYearRange()
    {
        if (CanGoNextYearRange) _displayedMonth = _displayedMonth.AddYears(YearsPerPage);
    }

    private sealed record YearCell(int Year, bool IsCurrent, bool IsSelected, bool IsDisabled);

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
                var disabled = (Min is not null && yearEnd < Min.Value.Date) || (Max is not null && yearStart > Max.Value.Date);
                cells.Add(new YearCell(year, year == today.Year, year == Value.Year, disabled));
            }
            return cells;
        }
    }

    private void SelectYear(YearCell cell)
    {
        if (cell.IsDisabled) return;
        _displayedMonth = new DateTime(cell.Year, _displayedMonth.Month, 1);
        _view = CalendarView.Months;
    }

    private sealed record MonthCell(int Month, string Label, bool IsCurrent, bool IsSelected, bool IsDisabled);

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
                var disabled = (Min is not null && monthEnd < Min.Value.Date) || (Max is not null && monthStart > Max.Value.Date);
                var label = monthStart.ToString("MMM", CultureInfo.InvariantCulture);
                cells.Add(new MonthCell(m, label, year == today.Year && m == today.Month, year == Value.Year && m == Value.Month, disabled));
            }
            return cells;
        }
    }

    private void SelectMonth(MonthCell cell)
    {
        if (cell.IsDisabled) return;
        _displayedMonth = new DateTime(_displayedMonth.Year, cell.Month, 1);
        _view = CalendarView.Days;
    }

    private sealed record DayCell(DateTime Date, bool InMonth, bool IsToday, bool IsSelected, bool IsDisabled);

    // Always 42 cells (6 full weeks) so the grid doesn't resize as months of
    // different lengths are navigated -- leading/trailing days from adjacent
    // months are shown dimmed and disabled rather than omitted.
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
            cells.Add(new DayCell(date, inMonth, date == today, date == Value.Date, disabled));
        }
        return cells;
    }

    private Task SelectDay(DayCell cell)
    {
        if (cell.IsDisabled) return Task.CompletedTask;
        return ValueChanged.InvokeAsync(ClampToBounds(cell.Date.Date.AddHours(_hour).AddMinutes(_minute)));
    }

    // Hour/minute inputs live alongside the day grid -- changing them retimes
    // the currently selected date immediately, independent of picking a new
    // day. Day selection above reads the same _hour/_minute so the two stay
    // in sync no matter which is edited first.
    private Task OnHourChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var hour)) return Task.CompletedTask;
        return ApplyTime(Math.Clamp(hour, 0, 23), _minute);
    }

    private Task OnMinuteChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), out var minute)) return Task.CompletedTask;
        return ApplyTime(_hour, Math.Clamp(minute, 0, 59));
    }

    // Mouse-wheel nudging, one hour/minute per notch, wrapping around at the
    // day boundary rather than clamping -- scrolling past 23 or 59 should keep
    // moving, not get stuck at the edge.
    private Task OnHourWheel(WheelEventArgs e) => ApplyTime(((_hour + (e.DeltaY < 0 ? 1 : -1)) % 24 + 24) % 24, _minute);

    private Task OnMinuteWheel(WheelEventArgs e) => ApplyTime(_hour, ((_minute + (e.DeltaY < 0 ? 1 : -1)) % 60 + 60) % 60);

    private Task ApplyTime(int hour, int minute)
    {
        _hour = hour;
        _minute = minute;
        return ValueChanged.InvokeAsync(ClampToBounds(Value.Date.AddHours(_hour).AddMinutes(_minute)));
    }

    // Day-level Min/Max only gate which dates are pickable; the exact minute
    // within a boundary day still needs clamping (e.g. Min is 14:00 on a day
    // that itself isn't disabled).
    private DateTime ClampToBounds(DateTime dt)
    {
        if (Min is not null && dt < Min.Value) return Min.Value;
        if (Max is not null && dt > Max.Value) return Max.Value;
        return dt;
    }

    // Full-year overlay: a 4x3 grid of mini month calendars for one year, with
    // its own year cursor separate from _displayedMonth (browsing the overlay
    // shouldn't move the Days view underneath it until a day is actually picked).
    private bool _showFullYear;
    private int _fullYearYear;

    private void ToggleFullYear()
    {
        _showFullYear = !_showFullYear;
        if (_showFullYear) _fullYearYear = _displayedMonth.Year;
    }

    private bool CanGoPrevFullYear => Min is null || _fullYearYear > Min.Value.Year;
    private bool CanGoNextFullYear => Max is null || _fullYearYear < Max.Value.Year;

    private void PrevFullYear()
    {
        if (CanGoPrevFullYear) _fullYearYear--;
    }

    private void NextFullYear()
    {
        if (CanGoNextFullYear) _fullYearYear++;
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
        if (cell.IsDisabled) return Task.CompletedTask;
        _displayedMonth = new DateTime(cell.Date.Year, cell.Date.Month, 1);
        _showFullYear = false;
        return SelectDay(cell);
    }
}
