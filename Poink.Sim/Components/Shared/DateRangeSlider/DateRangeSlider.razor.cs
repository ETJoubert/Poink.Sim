using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Shared.RangeCalendar;

namespace Poink.Sim.Components.Shared.DateRangeSlider;

/// <summary>
/// Date range picker: a week grid for coarse selection (snapped to whole weeks, at least <see cref="MinWeeks"/> long)
/// and one range calendar, opened from either date label, for picking start and end down to the minute.
/// </summary>
public partial class DateRangeSlider : ComponentBase
{
	[Parameter]
	public DateTime Min { get; set; }

	[Parameter]
	public DateTime Max { get; set; }

	[Parameter]
	public DateTime From { get; set; }

	[Parameter]
	public EventCallback<DateTime> FromChanged { get; set; }

	[Parameter]
	public DateTime To { get; set; }

	[Parameter]
	public EventCallback<DateTime> ToChanged { get; set; }

	[Parameter]
	public bool Disabled { get; set; }

	/// <summary>Smallest range the week grid selects; the calendar can still pick anything shorter.</summary>
	[Parameter]
	public int MinWeeks { get; set; } = 2;

	private bool _calendarOpen;
	private RangeEdge _calendarEdge;

	private void OpenCalendar(RangeEdge edge)
	{
		_calendarEdge = edge;
		_calendarOpen = true;
	}

	private void CloseCalendar() => _calendarOpen = false;

	// To is reported last so hosts reacting to ToChanged (e.g. @bind-To:after) see the final From as well.
	private async Task OnRangeChanged((DateTime From, DateTime To) range)
	{
		if (range.From != From)
			await FromChanged.InvokeAsync(range.From);
		if (range.To != To)
			await ToChanged.InvokeAsync(range.To);
	}

	private string SpanLabel
	{
		get
		{
			var span = To - From;
			if (span.TotalDays >= 14)
				return $"{span.TotalDays / 7:0.#} wks";
			if (span.TotalDays >= 2)
				return $"{span.TotalDays:0.#} days";
			if (span.TotalHours >= 2)
				return $"{span.TotalHours:0.#} hrs";
			return $"{Math.Max(0, span.TotalMinutes):0} min";
		}
	}
}
