using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Profiler
{
	/// <summary>
	/// Every selected Profiler symbol on one <see cref="CompareChart"/>, as % change by <see cref="Mode"/>,
	/// with an optional primary ÷ <see cref="RatioTable"/> line in a lower pane.
	/// </summary>
	public partial class ProfilerCompareSet : ComponentBase
	{
		/// <summary>Primary first, matching the per-symbol chart sets this replaces.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<ProfilerSymbolData> Sets { get; set; } = [];

		[Parameter]
		public ProfilerCompareMode Mode { get; set; }

		[Parameter]
		public EventCallback<ProfilerCompareMode> ModeChanged { get; set; }

		[Parameter]
		public CompareChartSeriesStyle Style { get; set; }

		[Parameter]
		public EventCallback<CompareChartSeriesStyle> StyleChanged { get; set; }

		/// <summary>The comparison symbol the primary is divided by for the ratio line; null or unselected hides it.</summary>
		[Parameter]
		public string? RatioTable { get; set; }

		[Parameter]
		public EventCallback<string?> RatioTableChanged { get; set; }

		[Parameter]
		public DateTime? WindowStart { get; set; }

		[Parameter]
		public DateTime? WindowEnd { get; set; }

		[Parameter]
		public EventCallback OnNearStart { get; set; }

		private List<CompareChartSeries> _series = new();
		private (List<HlocAvdtBar>[] Bars, ProfilerCompareMode Mode, DateTime? Anchor, string? RatioTable, CompareChartSeriesStyle Style)? _builtFor;

		private ProfilerSymbolData? Primary => Sets.Count > 0 ? Sets[0] : null;

		private IEnumerable<ProfilerSymbolData> Comparisons => Sets.Skip(1);

		private DateTime? Anchor => Mode == ProfilerCompareMode.WindowAnchored ? WindowStart : null;

		private bool CanLoadMore => Sets.Any(s => s.HasBars && !s.ReachedStart);

		private bool HasRatio => Comparisons.Any(s => s.Table == RatioTable);

		private CompareChartScaleMode ScaleMode =>
			Mode == ProfilerCompareMode.WindowAnchored ? CompareChartScaleMode.PercentValues : CompareChartScaleMode.Rebased;

		protected override void OnParametersSet()
		{
			var bars = Sets.Select(s => s.Bars).ToArray();
			if (_builtFor is { } built
				&& built.Mode == Mode
				&& built.Anchor == Anchor
				&& built.RatioTable == RatioTable
				&& built.Style == Style
				&& built.Bars.SequenceEqual(bars, ReferenceEqualityComparer.Instance))
				return;

			_builtFor = (bars, Mode, Anchor, RatioTable, Style);
			_series = ProfilerCompareHelper.Build(Sets, Mode, Anchor, RatioTable, Style);
		}

		private string ModeNote() =>
			Mode switch
			{
				ProfilerCompareMode.WindowAnchored when WindowStart is { } start => $"% change from {start:yyyy-MM-dd HH:mm}",
				ProfilerCompareMode.WindowAnchored => "% change from each symbol's first loaded bar -- turn on the Lookback Window to anchor it",
				_ => "% change from the first bar in view",
			};

		private string ChartTooltip() =>
			HasRatio
				? $"Every symbol's {ModeNote()}, with {ProfilerCompareHelper.RatioLabel(Primary!.Table, RatioTable!)} in the lower pane -- pan left to backfill every symbol"
				: $"Every symbol's {ModeNote()} -- pan left to backfill every symbol";

		private Task OnModeSelected(ChangeEventArgs e) =>
			ModeChanged.InvokeAsync(Enum.TryParse<ProfilerCompareMode>(e.Value?.ToString(), out var mode) ? mode : ProfilerCompareMode.PercentChange);

		private Task OnStyleSelected(ChangeEventArgs e) =>
			StyleChanged.InvokeAsync(Enum.TryParse<CompareChartSeriesStyle>(e.Value?.ToString(), out var style) ? style : CompareChartSeriesStyle.Lines);

		private Task OnRatioSelected(ChangeEventArgs e)
		{
			var table = e.Value?.ToString();
			return RatioTableChanged.InvokeAsync(string.IsNullOrEmpty(table) ? null : table);
		}
	}
}
