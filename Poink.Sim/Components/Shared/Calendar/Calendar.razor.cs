using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.Calendar;

// Pure month-grid date picker -- no popover/backdrop of its own, that's the
// caller's responsibility (see DateRangeSlider, which opens one of these in a
// dropdown per from/to label). Selecting a day keeps Value's existing
// time-of-day and only changes the date part.
public partial class Calendar : ComponentBase
{
    [Parameter] public DateTime Value { get; set; }
    [Parameter] public EventCallback<DateTime> ValueChanged { get; set; }
    [Parameter] public DateTime? Min { get; set; }
    [Parameter] public DateTime? Max { get; set; }

    private static readonly string[] DayHeaders = { "S", "M", "T", "W", "T", "F", "S" };

    private DateTime _displayedMonth;

    // Set once per instance (not OnParametersSet) so browsing months isn't
    // reset by parent re-renders while the popover stays open -- DateRangeSlider
    // mounts a fresh instance each time it opens via @if, so this still starts
    // on Value's month every time the popover is opened.
    protected override void OnInitialized()
    {
        _displayedMonth = new DateTime(Value.Year, Value.Month, 1);
    }

    private string MonthLabel => _displayedMonth.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private bool CanGoPrev => Min is null || _displayedMonth > new DateTime(Min.Value.Year, Min.Value.Month, 1);
    private bool CanGoNext => Max is null || _displayedMonth < new DateTime(Max.Value.Year, Max.Value.Month, 1);

    private void PrevMonth()
    {
        if (CanGoPrev) _displayedMonth = _displayedMonth.AddMonths(-1);
    }

    private void NextMonth()
    {
        if (CanGoNext) _displayedMonth = _displayedMonth.AddMonths(1);
    }

    private sealed record DayCell(DateTime Date, bool InMonth, bool IsToday, bool IsSelected, bool IsDisabled);

    // Always 42 cells (6 full weeks) so the grid doesn't resize as months of
    // different lengths are navigated -- leading/trailing days from adjacent
    // months are shown dimmed and disabled rather than omitted.
    private List<DayCell> Days
    {
        get
        {
            var leadingDays = (int)_displayedMonth.DayOfWeek;
            var gridStart = _displayedMonth.AddDays(-leadingDays);
            var today = DateTime.Today;

            var cells = new List<DayCell>(42);
            for (var i = 0; i < 42; i++)
            {
                var date = gridStart.AddDays(i);
                var inMonth = date.Month == _displayedMonth.Month && date.Year == _displayedMonth.Year;
                var disabled = (Min is not null && date < Min.Value.Date) || (Max is not null && date > Max.Value.Date);
                cells.Add(new DayCell(date, inMonth, date == today, date == Value.Date, disabled));
            }
            return cells;
        }
    }

    private Task SelectDay(DayCell cell)
    {
        if (cell.IsDisabled) return Task.CompletedTask;
        return ValueChanged.InvokeAsync(cell.Date.Date + Value.TimeOfDay);
    }
}
