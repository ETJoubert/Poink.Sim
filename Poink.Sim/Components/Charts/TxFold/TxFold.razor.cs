using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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

		private static readonly int[] DefaultSelectedSeconds = { 60, 120, 240, 480, 900, 1800, 3600, 7200 };

		private BarAggregationKind _barKind = BarAggregationKind.Ohlc;
		private bool _showHighLowLines;
		private bool _showVolumeProfile;
		private bool _normalize;
		private HashSet<int> _selectedSeconds = DefaultSelectedSeconds.ToHashSet();
		private List<TxFoldBar> _bars = new();
		private IReadOnlyList<TxFoldVolumeBucket> _volumeProfile = Array.Empty<TxFoldVolumeBucket>();
		private (string Table, DateTime AnchorEnd, DateTime RangeStart, BarAggregationKind BarKind, string SelectedKey)? _loadedFor;
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
		private bool _disposed;

		protected override async Task OnParametersSetAsync() => await RefreshIfNeeded();

		private async Task SetBarKind(BarAggregationKind kind)
		{
			if (_barKind == kind)
				return;
			_barKind = kind;
			await RefreshIfNeeded();
		}

		private void ToggleHighLowLines() => _showHighLowLines = !_showHighLowLines;

		private void ToggleVolumeProfile() => _showVolumeProfile = !_showVolumeProfile;

		private void ToggleNormalize() => _normalize = !_normalize;

		private async Task ToggleTimeframe(int seconds)
		{
			if (_selectedSeconds.Contains(seconds))
			{
				if (_selectedSeconds.Count == 1)
					return;
				_selectedSeconds.Remove(seconds);
			}
			else
			{
				_selectedSeconds.Add(seconds);
			}
			await RefreshIfNeeded();
		}

		private async Task RefreshIfNeeded()
		{
			if (string.IsNullOrWhiteSpace(Table) || _loading)
				return;

			var selectedKey = string.Join(",", _selectedSeconds.OrderBy(s => s));
			var key = (Table, AnchorEnd, RangeStart, _barKind, selectedKey);
			if (_loadedFor == key)
				return;

			_loading = true;
			_error = null;
			StateHasChanged();

			try
			{
				var table = Table;
				var anchorEnd = AnchorEnd;
				var rangeStart = RangeStart;
				var selectedSeconds = _selectedSeconds;
				var barKind = _barKind;
				var result = await Task.Run(() =>
				{
					var largestSeconds = selectedSeconds.Max();
					var ticks = TickRepository.ReadTicks(table, anchorEnd.AddSeconds(-largestSeconds), anchorEnd).ToList();
					return TxFoldStats.Compute(ticks, anchorEnd, rangeStart, selectedSeconds, barKind);
				});
				_bars = result.Bars;
				_volumeProfile = result.VolumeProfile;
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

				if (
					!ReferenceEquals(_renderedBars, _bars)
					|| _renderedLatestPrice != LatestPrice
					|| _renderedShowHighLowLines != _showHighLowLines
					|| _renderedShowVolumeProfile != _showVolumeProfile
					|| _renderedEntryPrice != EntryPrice
					|| _renderedPositionIsLong != PositionIsLong
					|| _renderedStopLossPrice != StopLossPrice
					|| _renderedTakeProfitPrice != TakeProfitPrice
					|| _renderedDraftEntryPrice != DraftEntryPrice
					|| _renderedDraftIsLong != DraftIsLong
					|| _renderedDraftStopLossPrice != DraftStopLossPrice
					|| _renderedDraftTakeProfitPrice != DraftTakeProfitPrice
					|| _renderedNormalize != _normalize
				)
				{
					_renderedBars = _bars;
					_renderedLatestPrice = LatestPrice;
					_renderedShowHighLowLines = _showHighLowLines;
					_renderedShowVolumeProfile = _showVolumeProfile;
					_renderedEntryPrice = EntryPrice;
					_renderedPositionIsLong = PositionIsLong;
					_renderedStopLossPrice = StopLossPrice;
					_renderedTakeProfitPrice = TakeProfitPrice;
					_renderedDraftEntryPrice = DraftEntryPrice;
					_renderedDraftIsLong = DraftIsLong;
					_renderedDraftStopLossPrice = DraftStopLossPrice;
					_renderedDraftTakeProfitPrice = DraftTakeProfitPrice;
					_renderedNormalize = _normalize;

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
						barKind = _barKind.ToString(),
						normalize = _normalize,
						referencePrice,
						referenceRange,
						latestPrice = LatestPrice,
						showHighLowLines = _showHighLowLines,
						showVolumeProfile = _showVolumeProfile,
						volumeProfile = _volumeProfile.Select(b => new { low = b.Low, high = b.High, count = b.Count }),
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
