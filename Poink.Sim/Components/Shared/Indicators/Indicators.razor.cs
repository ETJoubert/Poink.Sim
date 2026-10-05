using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Poink.Sim.Components.Shared.Indicators
{
	/// <summary>
	/// Single control surface for the Lookback-Window-derived overlays (POC/VAH/VAL/VWAP/AVG/MED/REG, day and interval
	/// lines) plus the Lookback Window itself, so every chart a host passes <see cref="Settings"/> to shows the same levels.
	/// </summary>
	public partial class Indicators : ComponentBase
	{
		[Parameter]
		public IndicatorSettings Settings { get; set; } = new();

		[Parameter]
		public EventCallback<IndicatorSettings> SettingsChanged { get; set; }

		[Parameter]
		public bool LookbackEnabled { get; set; }

		[Parameter]
		public EventCallback<bool> LookbackEnabledChanged { get; set; }

		/// <summary>Number of bars the Lookback Window spans.</summary>
		[Parameter]
		public int LookbackWidth { get; set; }

		[Parameter]
		public EventCallback<int> LookbackWidthChanged { get; set; }

		/// <summary>Bars back from the most recently loaded bar to the end of the Lookback Window.</summary>
		[Parameter]
		public int LookbackOffset { get; set; }

		[Parameter]
		public EventCallback<int> LookbackOffsetChanged { get; set; }

		/// <summary>Replaces the built-in Lookback Window toggle and Width/Offset inputs with a host's own window control.</summary>
		[Parameter]
		public RenderFragment? LookbackContent { get; set; }

		private sealed record LevelToggle(
			string CssKey,
			string Label,
			string Tooltip,
			Func<IndicatorSettings, bool> IsOn,
			Func<IndicatorSettings, IndicatorSettings> Toggle
		);

		private static readonly IReadOnlyList<LevelToggle> LevelToggles =
		[
			new("poc", "POC", "Point of Control -- the price with the most tick activity in the Lookback Window", s => s.ShowPoc, s => s with { ShowPoc = !s.ShowPoc }),
			new("vah", "VAH", "Value Area High -- the upper edge of the band holding 70% of tick activity in the Lookback Window", s => s.ShowVah, s => s with { ShowVah = !s.ShowVah }),
			new("val", "VAL", "Value Area Low -- the lower edge of the band holding 70% of tick activity in the Lookback Window", s => s.ShowVal, s => s with { ShowVal = !s.ShowVal }),
			new("vwap", "VWAP", "Volume-weighted average price across the Lookback Window", s => s.ShowVwap, s => s with { ShowVwap = !s.ShowVwap }),
			new("avg", "AVG", "Mean price across the Lookback Window", s => s.ShowAverage, s => s with { ShowAverage = !s.ShowAverage }),
			new("med", "MED", "Median price across the Lookback Window", s => s.ShowMedian, s => s with { ShowMedian = !s.ShowMedian }),
			new("reg", "REG", "Linear regression of close prices fitted over the Lookback Window", s => s.ShowRegression, s => s with { ShowRegression = !s.ShowRegression }),
		];

		private Task SetSettings(IndicatorSettings value) => SettingsChanged.InvokeAsync(Settings = value);

		private Task ToggleLookback() => LookbackEnabledChanged.InvokeAsync(!LookbackEnabled);

		private Task SetLookbackWidth(int value) => LookbackWidthChanged.InvokeAsync(Math.Max(1, value));

		private Task SetLookbackOffset(int value) => LookbackOffsetChanged.InvokeAsync(Math.Max(0, value));

		private Task SetIntervalHours(int value) => SetSettings(Settings with { IntervalHours = Math.Clamp(value, 1, 24) });
	}
}
