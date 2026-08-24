using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Poink.Sim.Components.Shared.DateRangeSlider;

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

	private int _spanMinutes;
	private int _fromMinutes;
	private int _toMinutes;

	protected override void OnParametersSet()
	{
		_spanMinutes = Math.Max(1, (int)(Max - Min).TotalMinutes);
		_fromMinutes = Math.Clamp((int)(From - Min).TotalMinutes, 0, _spanMinutes);
		_toMinutes = Math.Clamp((int)(To - Min).TotalMinutes, 0, _spanMinutes);
	}

	private double FromPercent => _fromMinutes * 100.0 / _spanMinutes;
	private double ToPercent => _toMinutes * 100.0 / _spanMinutes;

	private static string Pct(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

	private async Task OnFromChanged(ChangeEventArgs e)
	{
		if (!int.TryParse(e.Value?.ToString(), out var minutes))
			return;
		_fromMinutes = Math.Clamp(minutes, 0, Math.Max(0, _toMinutes - 1));
		await FromChanged.InvokeAsync(Min.AddMinutes(_fromMinutes));
	}

	private async Task OnToChanged(ChangeEventArgs e)
	{
		if (!int.TryParse(e.Value?.ToString(), out var minutes))
			return;
		_toMinutes = Math.Clamp(minutes, Math.Min(_spanMinutes, _fromMinutes + 1), _spanMinutes);
		await ToChanged.InvokeAsync(Min.AddMinutes(_toMinutes));
	}

	private bool _showFromCalendar;
	private bool _showToCalendar;

	private void ToggleFromCalendar()
	{
		_showFromCalendar = !_showFromCalendar;
		_showToCalendar = false;
	}

	private void ToggleToCalendar()
	{
		_showToCalendar = !_showToCalendar;
		_showFromCalendar = false;
	}

	private void CloseCalendars()
	{
		_showFromCalendar = false;
		_showToCalendar = false;
	}

	private async Task OnFromDateSelected(DateTime date)
	{
		_fromMinutes = Math.Clamp((int)(date - Min).TotalMinutes, 0, Math.Max(0, _toMinutes - 1));
		await FromChanged.InvokeAsync(Min.AddMinutes(_fromMinutes));
	}

	private async Task OnToDateSelected(DateTime date)
	{
		_toMinutes = Math.Clamp((int)(date - Min).TotalMinutes, Math.Min(_spanMinutes, _fromMinutes + 1), _spanMinutes);
		await ToChanged.InvokeAsync(Min.AddMinutes(_toMinutes));
	}

	private string SpanLabel
	{
		get
		{
			var span = To - From;
			if (span.TotalDays >= 2)
				return $"{span.TotalDays:0.#} days";
			if (span.TotalHours >= 2)
				return $"{span.TotalHours:0.#} hrs";
			return $"{Math.Max(0, span.TotalMinutes):0} min";
		}
	}

	private sealed record WeekTick(double Percent, string Title, string Label, bool ShowLabel);

	private List<WeekTick> WeekTicks
	{
		get
		{
			const int minutesPerWeek = 60 * 24 * 7;
			if (_spanMinutes < minutesPerWeek)
				return new List<WeekTick>();

			var boundaries = new List<DateTime>();
			for (var d = Min.AddDays(7); d < Max; d = d.AddDays(7))
				boundaries.Add(d);

			int labelStep = Math.Max(1, (int)Math.Ceiling(boundaries.Count / 8.0));
			return boundaries
				.Select(
					(d, i) =>
						new WeekTick(
							(d - Min).TotalMinutes * 100.0 / _spanMinutes,
							d.ToString("yyyy-MM-dd"),
							d.ToString("MMM d", CultureInfo.InvariantCulture),
							i % labelStep == 0
						)
				)
				.ToList();
		}
	}
}
