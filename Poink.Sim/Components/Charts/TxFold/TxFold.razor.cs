using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Self-contained multi-timeframe comparison chart -- each toggled timeframe (see
	/// <see cref="SnapshotTimeframeOptions"/>, 1m/2m/4m/8m/15m/30m/1h/2h selected by default) renders
	/// as its own single bar over its own trailing window ending at <see cref="AnchorEnd"/>, side by
	/// side, via <see cref="TxFoldStats.Compute"/> over ticks fetched here.
	/// </summary>
	public partial class TxFold : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Inject]
		private IJSRuntime JSRuntime { get; set; } = default!;

		[Parameter, EditorRequired]
		public string Table { get; set; } = "";

		[Parameter]
		public DateTime AnchorEnd { get; set; } = DateTime.UtcNow;

		/// <summary>Earliest instant the caller considers "available" (e.g. a simulation's replay start, or a profiler range's start) -- a bar's coverage meter is only ever backed by data from here forward, never by the underlying table's full history.</summary>
		[Parameter, EditorRequired]
		public DateTime RangeStart { get; set; }

		/// <summary>Supplies the ticks before <see cref="HistoryBeforeUtc"/> when set; <see cref="Table"/> supplies the rest.</summary>
		[Parameter]
		public string? HistoryTable { get; set; }

		[Parameter]
		public DateTime? HistoryBeforeUtc { get; set; }

		/// <summary>Drawn as a dashed horizontal line across every bar -- null hides it.</summary>
		[Parameter]
		public double? LatestPrice { get; set; }

		/// <summary>Display-only position levels (e.g. the selected trader's open position) -- null hides all four.</summary>
		[Parameter]
		public double? EntryPrice { get; set; }

		[Parameter]
		public bool? PositionIsLong { get; set; }

		[Parameter]
		public double? StopLossPrice { get; set; }

		[Parameter]
		public double? TakeProfitPrice { get; set; }

		/// <summary>A not-yet-submitted order previewed alongside any open-position levels above -- e.g. TradeConsole's PREVIEW toggle.</summary>
		[Parameter]
		public double? DraftEntryPrice { get; set; }

		[Parameter]
		public bool? DraftIsLong { get; set; }

		[Parameter]
		public double? DraftStopLossPrice { get; set; }

		[Parameter]
		public double? DraftTakeProfitPrice { get; set; }

		/// <summary>Which trailing windows render, one bar per selection -- bindable so a host can read/drive the toolbar's own selection (e.g. to show a feature count elsewhere) without duplicating it.</summary>
		[Parameter]
		public HashSet<int> SelectedSeconds { get; set; } = SnapshotTimeframeOptions.DefaultSelectedSeconds.ToHashSet();

		[Parameter]
		public EventCallback<HashSet<int>> SelectedSecondsChanged { get; set; }

		[Parameter]
		public BarAggregationKind BarKind { get; set; } = BarAggregationKind.Ohlc;

		[Parameter]
		public EventCallback<BarAggregationKind> BarKindChanged { get; set; }

		/// <summary>Projects every bar's High/Low as dashed lines and a right-margin coverage histogram; bindable so TrainPageV2's Features tab can also drive FeatureOptionsModel.TxFold.IncludeHighLow from the same toggle.</summary>
		[Parameter]
		public bool ShowHighLowLines { get; set; }

		[Parameter]
		public EventCallback<bool> ShowHighLowLinesChanged { get; set; }

		/// <summary>Overlays the tick-volume profile in the right margin; bindable so TrainPageV2's Features tab can also drive FeatureOptionsModel.TxFold.IncludeVolumeProfile from the same toggle.</summary>
		[Parameter]
		public bool ShowVolumeProfile { get; set; }

		[Parameter]
		public EventCallback<bool> ShowVolumeProfileChanged { get; set; }

		/// <summary>Draws every bar as a percentage deviation from the largest selected timeframe's midpoint instead of its real price; bindable so TrainPageV2's Features tab can also drive FeatureOptionsModel.TxFold.Normalize from the same toggle.</summary>
		[Parameter]
		public bool Normalize { get; set; }

		[Parameter]
		public EventCallback<bool> NormalizeChanged { get; set; }

		/// <summary>Which POC/VAH/VAL/VWAP/AVE/MED/REG overlays render as reference lines across the whole
		/// chart, computed once over the ticks behind the displayed bars -- bindable so TrainPageV2's
		/// Features tab can also drive FeatureOptionsModel.TxFold.IncludedLevels from the same toggle.</summary>
		[Parameter]
		public HashSet<TxFoldLevelKind> ShownLevels { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<TxFoldLevelKind>> ShownLevelsChanged { get; set; }

		/// <summary>False when a host drives <see cref="ShownLevels"/> from elsewhere (the Indicators component), hiding TxFold's own level buttons.</summary>
		[Parameter]
		public bool ShowLevelToggles { get; set; } = true;

		private List<TxFoldBar> _bars = new();
		private IReadOnlyList<TxFoldVolumeBucket> _volumeProfile = Array.Empty<TxFoldVolumeBucket>();
		private RangeStatsResult? _levels;
		private (string Table, string? HistoryTable, DateTime? HistoryBeforeUtc, DateTime AnchorEnd, DateTime RangeStart, BarAggregationKind BarKind, string SelectedKey)? _loadedFor;
		private bool _loading;
		private string? _error;

		private ElementReference _canvasRef;
		private IJSObjectReference? _module;
		private List<TxFoldBar>? _renderedBars;
		private double? _renderedLatestPrice;
		private bool _renderedShowHighLowLines;
		private bool _renderedShowVolumeProfile;
		private double? _renderedEntryPrice;
		private bool? _renderedPositionIsLong;
		private double? _renderedStopLossPrice;
		private double? _renderedTakeProfitPrice;
		private double? _renderedDraftEntryPrice;
		private bool? _renderedDraftIsLong;
		private double? _renderedDraftStopLossPrice;
		private double? _renderedDraftTakeProfitPrice;
		private bool _renderedNormalize;
		private string _renderedShownLevelsKey = "";
		private bool _disposed;

		protected override async Task OnParametersSetAsync() => await RefreshIfNeeded();

		private async Task SetBarKind(BarAggregationKind kind)
		{
			if (BarKind == kind)
				return;
			BarKind = kind;
			await BarKindChanged.InvokeAsync(BarKind);
			await RefreshIfNeeded();
		}

		private Task ToggleHighLowLines()
		{
			ShowHighLowLines = !ShowHighLowLines;
			return ShowHighLowLinesChanged.InvokeAsync(ShowHighLowLines);
		}

		private Task ToggleVolumeProfile()
		{
			ShowVolumeProfile = !ShowVolumeProfile;
			return ShowVolumeProfileChanged.InvokeAsync(ShowVolumeProfile);
		}

		private Task ToggleNormalize()
		{
			Normalize = !Normalize;
			return NormalizeChanged.InvokeAsync(Normalize);
		}

		private Task ToggleLevel(TxFoldLevelKind kind)
		{
			if (!ShownLevels.Remove(kind))
				ShownLevels.Add(kind);
			return ShownLevelsChanged.InvokeAsync(ShownLevels);
		}

		private double? LevelPrice(TxFoldLevelKind kind) => TxFoldStats.LevelValue(_levels, kind);

		private async Task ToggleTimeframe(int seconds)
		{
			if (SelectedSeconds.Contains(seconds))
			{
				if (SelectedSeconds.Count == 1)
					return;
				SelectedSeconds.Remove(seconds);
			}
			else
			{
				SelectedSeconds.Add(seconds);
			}
			await SelectedSecondsChanged.InvokeAsync(SelectedSeconds);
			await RefreshIfNeeded();
		}

		private async Task RefreshIfNeeded()
		{
			if (string.IsNullOrWhiteSpace(Table) || _loading)
				return;

			var selectedKey = string.Join(",", SelectedSeconds.OrderBy(s => s));
			var key = (Table, HistoryTable, HistoryBeforeUtc, AnchorEnd, RangeStart, BarKind, selectedKey);
			if (_loadedFor == key)
				return;

			_loading = true;
			_error = null;
			StateHasChanged();

			try
			{
				var table = Table;
				var historyTable = HistoryTable;
				var historyBeforeUtc = HistoryBeforeUtc;
				var anchorEnd = AnchorEnd;
				var rangeStart = RangeStart;
				var selectedSeconds = SelectedSeconds;
				var barKind = BarKind;
				var result = await Task.Run(() =>
				{
					var largestSeconds = selectedSeconds.Max();
					var ticks = SplitTickReadHelper.Read(
						TickRepository,
						table,
						historyTable,
						historyBeforeUtc,
						anchorEnd.AddSeconds(-largestSeconds),
						anchorEnd
					);
					return TxFoldStats.Compute(ticks, anchorEnd, rangeStart, selectedSeconds, barKind);
				});
				_bars = result.Bars;
				_volumeProfile = result.VolumeProfile;
				_levels = result.Levels;
				_loadedFor = key;
			}
			catch (Exception ex)
			{
				_error = ex.Message;
			}
			finally
			{
				_loading = false;
			}
			StateHasChanged();
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (_bars.Count == 0)
				return;

			try
			{
				if (_module is null)
				{
					_module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/txFold.js");
					if (_disposed)
						return;
				}

				var shownLevelsKey = string.Join(",", ShownLevels.OrderBy(k => k));
				if (
					!ReferenceEquals(_renderedBars, _bars)
					|| _renderedLatestPrice != LatestPrice
					|| _renderedShowHighLowLines != ShowHighLowLines
					|| _renderedShowVolumeProfile != ShowVolumeProfile
					|| _renderedEntryPrice != EntryPrice
					|| _renderedPositionIsLong != PositionIsLong
					|| _renderedStopLossPrice != StopLossPrice
					|| _renderedTakeProfitPrice != TakeProfitPrice
					|| _renderedDraftEntryPrice != DraftEntryPrice
					|| _renderedDraftIsLong != DraftIsLong
					|| _renderedDraftStopLossPrice != DraftStopLossPrice
					|| _renderedDraftTakeProfitPrice != DraftTakeProfitPrice
					|| _renderedNormalize != Normalize
					|| _renderedShownLevelsKey != shownLevelsKey
				)
				{
					_renderedBars = _bars;
					_renderedLatestPrice = LatestPrice;
					_renderedShowHighLowLines = ShowHighLowLines;
					_renderedShowVolumeProfile = ShowVolumeProfile;
					_renderedEntryPrice = EntryPrice;
					_renderedPositionIsLong = PositionIsLong;
					_renderedStopLossPrice = StopLossPrice;
					_renderedTakeProfitPrice = TakeProfitPrice;
					_renderedDraftEntryPrice = DraftEntryPrice;
					_renderedDraftIsLong = DraftIsLong;
					_renderedDraftStopLossPrice = DraftStopLossPrice;
					_renderedDraftTakeProfitPrice = DraftTakeProfitPrice;
					_renderedNormalize = Normalize;
					_renderedShownLevelsKey = shownLevelsKey;

					// The largest selected timeframe's own midpoint anchors 0%, its half-range +-100%
					// -- so its own High/Low land exactly there; other bars can still fall outside it.
					var largestBar = _bars.OrderByDescending(b => b.LookbackSeconds).First().Bar;
					var referencePrice = (largestBar.High + largestBar.Low) / 2;
					var referenceRange = (largestBar.High - largestBar.Low) / 2;

					var options = new
					{
						bars = _bars.Select(pair => new
						{
							label = SnapshotTimeframeOptions.All.First(o => o.Seconds == pair.LookbackSeconds).Label,
							open = pair.Bar.Open,
							high = pair.Bar.High,
							low = pair.Bar.Low,
							close = pair.Bar.Close,
							volume = pair.Bar.Volume,
							hasFullCoverage = pair.HasFullCoverage,
						}),
						barKind = BarKind.ToString(),
						normalize = Normalize,
						referencePrice,
						referenceRange,
						latestPrice = LatestPrice,
						showHighLowLines = ShowHighLowLines,
						showVolumeProfile = ShowVolumeProfile,
						volumeProfile = _volumeProfile.Select(b => new { low = b.Low, high = b.High, count = b.Count }),
						levels = TxFoldLevelOptions.All
							.Where(o => ShownLevels.Contains(o.Kind))
							.Select(o => new { kind = o.Kind.ToString().ToLowerInvariant(), label = o.Label, price = LevelPrice(o.Kind) })
							.Where(l => l.price is not null),
						entryPrice = EntryPrice,
						positionIsLong = PositionIsLong,
						stopLossPrice = StopLossPrice,
						takeProfitPrice = TakeProfitPrice,
						draftEntryPrice = DraftEntryPrice,
						draftIsLong = DraftIsLong,
						draftStopLossPrice = DraftStopLossPrice,
						draftTakeProfitPrice = DraftTakeProfitPrice,
					};

					await _module.InvokeVoidAsync("render", _canvasRef, options);
				}
			}
			catch (JSDisconnectedException) { }
			catch (ObjectDisposedException) { }
		}

		public async ValueTask DisposeAsync()
		{
			_disposed = true;

			if (_module is not null)
			{
				try
				{
					await _module.InvokeVoidAsync("dispose", _canvasRef);
					await _module.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
				catch (ObjectDisposedException) { }
			}

			GC.SuppressFinalize(this);
		}
	}
}
