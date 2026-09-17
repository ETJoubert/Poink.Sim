using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Models;
using Poink.Sim.Profiler;
using Poink.Sim.Repository;
using Poink.Sim.Services.Charts;

namespace Poink.Sim.Components.Pages
{
	public partial class ProfilerPage : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IProfilerService Service { get; set; } = default!;

		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Inject]
		private ITRangeChartRepository TRangeChartRepository { get; set; } = default!;

		[Inject]
		private ITRangeReferenceChartRepository TRangeReferenceChartRepository { get; set; } = default!;

		[Inject]
		private IBarKindChartService BarKindChartService { get; set; } = default!;

		private const int BarsPerFetch = 300;

		private string _table = "";
		private DateTime _from = DateTime.UtcNow.AddDays(-1);
		private DateTime _to = DateTime.UtcNow;
		private int _barSizeSeconds = 60;
		private BarAggregationKind _barAggregationKind = BarAggregationKind.Ohlc;

		// Cleared after the first range settle so later manual range edits still require LOAD.
		private bool _autoLoadPending = true;

		private bool _isLoading;
		private bool _isLoadingMore;
		private string? _error;

		private PanelTab _activePanelTab;

		private enum PanelTab
		{
			Drawings,
			SavedTRangeCharts,
			SavedTRangeReferenceCharts,
		}

		private bool _keepView;

		private ProfilerConfig? _config;
		private List<HlocAvdtBar> _bars = new();

		/// <summary>
		/// Cached CTraderBarRow projection of <see cref="_bars"/>, recomputed only when
		/// _bars itself changes -- MarketChart.razor.cs skips its JS setData call (and the
		/// fitContent() reset that follows on anything but KeepView) via ReferenceEquals on
		/// this list, which a fresh `ToBarRows()` on every render would defeat.
		/// </summary>
		private List<CTraderBarRow> _barRows = new();

		private List<QuartileBar> _quartileBars = new();

		// Per-bar median overlay, only populated in Quartile bar-kind mode (see RefreshBarRows).
		private List<ChartLinePoint> _medianLine = new();

		private List<BarPriceHistogram> _barHistograms = new();
		private int _tickCount;
		private bool _reachedStart;

		private sealed record Drawing(
			List<ChartHorizontalLine> HorizontalLines,
			List<ChartVerticalLine> VerticalLines,
			List<ChartDiagonalLine> DiagonalLines,
			List<CTraderBarRow> ChartBars,
			List<ChartVerticalLine> ReferenceLines
		)
		{
			public int Id { get; init; }
			public int? RangeBarCount { get; init; }
			public TimeSpan? RangeSpan { get; init; }

			public int? ReferenceBarCount { get; init; }

			public string Kind =>
				this switch
				{
					{ ChartBars.Count: > 0, ReferenceLines.Count: > 0 } => "T-RANGE REFERENCE CHART",
					{ ChartBars.Count: > 0 } => "T-RANGE CHART",
					{ HorizontalLines.Count: 1, VerticalLines.Count: 0, DiagonalLines.Count: 0 } => "H-LINE",
					{ HorizontalLines.Count: 0, VerticalLines.Count: 1, DiagonalLines.Count: 0 } => "V-LINE",
					{ HorizontalLines.Count: 0, VerticalLines.Count: 0, DiagonalLines.Count: 1 } => "D-LINE",
					{ HorizontalLines.Count: 2, VerticalLines.Count: 0 } => "P-RANGE",
					{ HorizontalLines.Count: 0, VerticalLines.Count: 2 } => "T-RANGE",
					{ HorizontalLines.Count: 2, VerticalLines.Count: 2 } => "RECTANGLE",
					_ => "DRAWING",
				};

			public string? ColorGroup =>
				Kind switch
				{
					"H-LINE" or "V-LINE" or "D-LINE" => "lines",
					"T-RANGE" or "P-RANGE" => "ranges",
					"RECTANGLE" => "shapes",
					"T-RANGE CHART" or "T-RANGE REFERENCE CHART" => "charts",
					_ => null,
				};

			public bool IsRange =>
				VerticalLines.Count == 2 && Kind is "T-RANGE" or "T-RANGE CHART" or "T-RANGE REFERENCE CHART";
		}

		private List<Drawing> _drawings = new();
		private Drawing _pending = NewPending();
		private int _nextDrawingId = 1;

		private DateTime? _volumeRangeStart;
		private DateTime? _volumeRangeEnd;

		private bool _lookbackEnabled;
		private int _lookbackWidth = 50;
		private int _lookbackOffset;

		private bool _showProfilePanelLevels = false;
		private bool _showProfileChartLines = false;
		private bool _showVwap = false;
		private bool _showAverageLine = false;
		private bool _showMedianLine = false;

		private bool _showMainChart = true;
		private bool _showXGrid = true;
		private bool _showVolumeProfile = false;
		private bool _showTimeProfile = false;
		private bool _showSubchart = false;

		private int _subchartBarSizeSeconds = 10;
		private BarAggregationKind _subchartBarKind = BarAggregationKind.Ohlc;
		private List<CTraderBarRow> _subchartBars = new();
		private List<ChartLinePoint> _subchartMedianLine = new();
		private bool _subchartLoading;
		private string? _subchartError;
		private (string Table, DateTime Start, DateTime End, int BarWidthSeconds, BarAggregationKind BarKind)? _subchartLoadedFor;

		private HashSet<int> _selectedDrawingIds = new();
		private HashSet<int> _savedDrawingIds = new();
		private bool _showLabelInput;
		private string _labelText = "";

		private List<TRangeChart> _savedTRangeCharts = new();

		private List<TRangeReferenceChart> _savedTRangeReferenceCharts = new();

		private readonly Dictionary<long, List<CTraderBarRow>> _chartBarsCache = new();
		private readonly HashSet<long> _chartBarsLoading = new();

		private readonly Dictionary<long, List<CTraderBarRow>> _referenceChartBarsCache = new();
		private readonly HashSet<long> _referenceChartBarsLoading = new();

		private static Drawing NewPending() => new(new(), new(), new(), new(), new());

		private IEnumerable<Drawing> AllDrawings() => _drawings.Append(_pending);

		private ElementReference _chartRowRef;
		private ElementReference _chartDividerRefXGrid;
		private ElementReference _chartDividerRef;
		private ElementReference _chartDividerRef2;
		private ElementReference _chartDividerRefSubchart;
		private ElementReference _chartHDividerRef;
		private IJSObjectReference? _dividerResizeInstance;
		private HashSet<string> _lastVisiblePanels = new();

		private const string ChartHDividerScript = """
			(function() {
			    // Every .prof-chart-main/.prof-chart-side child gets an explicit flex-basis
			    // percent (not width px) so N panels always sum to 100% regardless of window
			    // size -- resetEqual splits it evenly, bind's drag only ever moves percent
			    // between the divider's own two immediate neighbours.
			    function resetEqual(container) {
			        if (!container) return;
			        var panels = container.querySelectorAll(':scope > .prof-chart-main, :scope > .prof-chart-side');
			        if (panels.length === 0) return;
			        var percent = 100 / panels.length;
			        panels.forEach(function(p) { p.style.flex = '0 0 ' + percent + '%'; });
			    }
			    function bind(divider) {
			        if (!divider || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minWidthPx = parseInt(divider.dataset.minWidth, 10) || 200;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var left = divider.previousElementSibling;
			            var right = divider.nextElementSibling;
			            if (!left || !right) return;
			            var container = divider.parentElement;
			            var containerWidth = container.getBoundingClientRect().width;
			            var minPercent = (minWidthPx / containerWidth) * 100;
			            var startX = e.clientX;
			            var startLeftPercent = (left.getBoundingClientRect().width / containerWidth) * 100;
			            var startRightPercent = (right.getBoundingClientRect().width / containerWidth) * 100;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('prof-chart-divider-active');

			            function onMove(ev) {
			                var deltaPercent = ((ev.clientX - startX) / containerWidth) * 100;
			                var newLeft = startLeftPercent + deltaPercent;
			                var newRight = startRightPercent - deltaPercent;
			                if (newLeft < minPercent) { newRight -= (minPercent - newLeft); newLeft = minPercent; }
			                if (newRight < minPercent) { newLeft -= (minPercent - newRight); newRight = minPercent; }
			                left.style.flex = '0 0 ' + newLeft + '%';
			                right.style.flex = '0 0 ' + newRight + '%';
			            }
			            function onUp(ev) {
			                divider.releasePointerCapture(e.pointerId);
			                divider.removeEventListener('pointermove', onMove);
			                divider.removeEventListener('pointerup', onUp);
			                divider.classList.remove('prof-chart-divider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    function bindHeight(divider) {
			        if (!divider || divider.dataset.bound) return;
			        divider.dataset.bound = '1';
			        var minHeight = parseInt(divider.dataset.minHeight, 10) || 160;
			        var maxHeightVh = parseInt(divider.dataset.maxHeightVh, 10) || 80;
			        divider.addEventListener('pointerdown', function(e) {
			            e.preventDefault();
			            var target = divider.previousElementSibling;
			            if (!target) return;
			            var startY = e.clientY;
			            var startHeight = target.getBoundingClientRect().height;
			            var maxHeight = (maxHeightVh / 100) * window.innerHeight;
			            divider.setPointerCapture(e.pointerId);
			            divider.classList.add('prof-chart-hdivider-active');

			            function clamp(ev) {
			                return Math.min(maxHeight, Math.max(minHeight, startHeight + (ev.clientY - startY)));
			            }
			            function onMove(ev) {
			                target.style.height = clamp(ev) + 'px';
			            }
			            function onUp(ev) {
			                divider.releasePointerCapture(e.pointerId);
			                divider.removeEventListener('pointermove', onMove);
			                divider.removeEventListener('pointerup', onUp);
			                divider.classList.remove('prof-chart-hdivider-active');
			            }
			            divider.addEventListener('pointermove', onMove);
			            divider.addEventListener('pointerup', onUp);
			        });
			    }
			    return { bind: bind, bindHeight: bindHeight, resetEqual: resetEqual };
			})()
			""";

		protected override void OnInitialized()
		{
			_savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
			_savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (firstRender)
				_dividerResizeInstance = await JS.InvokeAsync<IJSObjectReference>("eval", ChartHDividerScript);

			if (HasData && _dividerResizeInstance is not null)
			{
				var visiblePanels = CurrentVisiblePanelKeys();
				if (!visiblePanels.SetEquals(_lastVisiblePanels))
				{
					_lastVisiblePanels = visiblePanels;
					await _dividerResizeInstance.InvokeVoidAsync("resetEqual", _chartRowRef);
				}

				if (_showXGrid)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRefXGrid);
				if (_showVolumeProfile)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef);
				if (_showTimeProfile)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRef2);
				if (_showSubchart)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _chartDividerRefSubchart);
				await _dividerResizeInstance.InvokeVoidAsync("bindHeight", _chartHDividerRef);
			}

			await RefreshSubchartIfNeeded();
		}

		private HashSet<string> CurrentVisiblePanelKeys()
		{
			var keys = new HashSet<string>();
			if (_showMainChart) keys.Add("main");
			if (_showXGrid) keys.Add("xgrid");
			if (_showVolumeProfile) keys.Add("volume");
			if (_showTimeProfile) keys.Add("time");
			if (_showSubchart) keys.Add("subchart");
			return keys;
		}

		public async ValueTask DisposeAsync()
		{
			if (_dividerResizeInstance is not null)
			{
				try
				{
					await _dividerResizeInstance.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
			}
		}

		private bool HasData => _config is not null && _bars.Count > 0;

		private string StateClass() =>
			_isLoading ? "prof-state-loading"
			: HasData ? "prof-state-ready"
			: "prof-state-setup";

		private async Task Load()
		{
			_error = null;

			if (string.IsNullOrWhiteSpace(_table))
			{
				_error = "pick a symbol";
				return;
			}
			if (_to <= _from)
			{
				_error = "To must be after From";
				return;
			}

			_isLoading = true;
			_keepView = false;
			_config = null;
			_bars = new();
			_quartileBars = new();
			_barRows = new();
			_medianLine = new();
			_barHistograms = new();
			_tickCount = 0;
			_reachedStart = false;
			_drawings = new();
			_pending = NewPending();
			_nextDrawingId = 1;
			_selectedDrawingIds.Clear();
			_savedDrawingIds.Clear();
			_volumeRangeStart = null;
			_volumeRangeEnd = null;
			_subchartBars = new();
			_subchartMedianLine = new();
			_subchartLoadedFor = null;
			CancelLabelSelected();

			try
			{
				var config = new ProfilerConfig(_table, _from, _to, TimeSpan.FromSeconds(_barSizeSeconds));
				var chunk = await Service.LoadAsync(config, config.To, BarsPerFetch);
				if (chunk.Bars.Count == 0 && chunk.ReachedStart)
				{
					_error = "no ticks in the selected range";
				}
				else
				{
					_config = config;
					ApplyChunk(chunk);
				}
			}
			catch (Exception ex)
			{
				_error = ex.Message;
			}
			finally
			{
				_isLoading = false;
			}
		}

		private async Task LoadMore()
		{
			// Latched before the guard below since OnNearStartAsync re-renders ProfilerPage
			// even on an early return, and a stale false here re-fits the chart every time.
			_keepView = true;

			if (_config is null || _reachedStart || _isLoadingMore)
				return;
			_isLoadingMore = true;

			try
			{
				var cursor = _bars.Count > 0 ? _bars[0].OpenTime : _config.To;
				var chunk = await Service.LoadAsync(_config, cursor, BarsPerFetch);
				ApplyChunk(chunk);
			}
			catch (Exception ex)
			{
				_error = ex.Message;
			}
			finally
			{
				_isLoadingMore = false;
			}
		}

		private void ApplyChunk(ProfilerChunk chunk)
		{
			_bars = chunk.Bars.Concat(_bars).ToList();
			_quartileBars = chunk.QuartileBars.Concat(_quartileBars).ToList();
			RefreshBarRows();
			_barHistograms = chunk.BarHistograms.Concat(_barHistograms).ToList();
			_tickCount += chunk.TickCount;
			_reachedStart = chunk.ReachedStart;
		}

		// Volume/Time Profile, XGrid, and VWAP always stay wired to _bars/_barHistograms since those can only be computed from ticks.
		private void RefreshBarRows()
		{
			if (_barAggregationKind == BarAggregationKind.Quartile)
			{
				_barRows = ToQuartileBarRows(_quartileBars);
				_medianLine = _quartileBars.Select(b => new ChartLinePoint(b.OpenTime, b.Median)).ToList();
			}
			else
			{
				_barRows = ToBarRows(_bars);
				_medianLine = new();
			}
		}

		private async Task OnRangeSettled()
		{
			if (!_autoLoadPending || string.IsNullOrWhiteSpace(_table))
				return;

			_autoLoadPending = false;
			await Load();
		}

		// Re-aggregates the active Lookback Window at the subchart's own bar size/kind -- only when
		// the panel is open and the window/settings actually changed, since this runs every render.
		private async Task RefreshSubchartIfNeeded()
		{
			if (!_showSubchart || _config is null || _volumeRangeStart is not { } start || _volumeRangeEnd is not { } end || _subchartLoading)
				return;

			var key = (_config.Table, start, end, _subchartBarSizeSeconds, _subchartBarKind);
			if (_subchartLoadedFor == key)
				return;

			_subchartLoading = true;
			_subchartError = null;
			StateHasChanged();

			try
			{
				var (bars, medianLine) = await BarKindChartService.BuildBarsAsync(
					_config.Table, start, end, _subchartBarSizeSeconds, _subchartBarKind);
				_subchartBars = bars;
				_subchartMedianLine = medianLine;
				_subchartLoadedFor = key;
			}
			catch (Exception ex)
			{
				_subchartError = ex.Message;
			}
			finally
			{
				_subchartLoading = false;
			}
			StateHasChanged();
		}

		private void AddHorizontalLine(double price) =>
			_pending.HorizontalLines.Add(new ChartHorizontalLine(price, price.ToString("F5")));

		private void AddVerticalLine(DateTime timeUtc) =>
			_pending.VerticalLines.Add(new ChartVerticalLine(timeUtc, timeUtc.ToString("yyyy-MM-dd HH:mm")));

		private void AddDiagonalLine(ChartDiagonalLine line)
		{
			var label =
				$"{line.StartPrice:F5} @ {line.StartTimeUtc:HH:mm} -> {line.EndPrice:F5} @ {line.EndTimeUtc:HH:mm}";
			_pending.DiagonalLines.Add(line with { Label = label });
		}

		private void AddTRangeChart(ChartTimeRange range)
		{
			var start = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
			var end = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
			var sliceStart = _pending.ReferenceLines.Count > 0 ? _pending.ReferenceLines[0].TimeUtc : start;
			var slice = _bars.Where(b => b.OpenTime >= sliceStart && b.OpenTime <= end);
			_pending.ChartBars.AddRange(ToBarRows(slice));
		}

		private void AddReferenceLine(DateTime timeUtc) =>
			_pending.ReferenceLines.Add(new ChartVerticalLine(timeUtc, timeUtc.ToString("yyyy-MM-dd HH:mm")));

		private void SetVolumeRange(ChartTimeRange range)
		{
			_volumeRangeStart = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc1 : range.TimeUtc2;
			_volumeRangeEnd = range.TimeUtc1 <= range.TimeUtc2 ? range.TimeUtc2 : range.TimeUtc1;
			_lookbackEnabled = true;
			SyncLookbackFieldsFromRange();
		}

		private void ClearVolumeRange()
		{
			_volumeRangeStart = null;
			_volumeRangeEnd = null;
			_lookbackEnabled = false;
		}

		private void ToggleLookbackWindow()
		{
			_lookbackEnabled = !_lookbackEnabled;
			if (_lookbackEnabled)
				ApplyLookbackWindow();
			else
			{
				_volumeRangeStart = null;
				_volumeRangeEnd = null;
			}
		}

		// Width/offset only recompute the applied window while the toggle is on -- otherwise
		// they're just the values it'll use once turned on.
		private int LookbackWidth
		{
			get => _lookbackWidth;
			set
			{
				_lookbackWidth = Math.Max(1, value);
				if (_lookbackEnabled)
					ApplyLookbackWindow();
			}
		}

		private int LookbackOffset
		{
			get => _lookbackOffset;
			set
			{
				_lookbackOffset = Math.Max(0, value);
				if (_lookbackEnabled)
					ApplyLookbackWindow();
			}
		}

		// Resolves _lookbackWidth/_lookbackOffset (both in bars, counted back from the most
		// recently loaded bar) to the DateTime pair every Lookback Window consumer below reads.
		private void ApplyLookbackWindow()
		{
			if (_bars.Count == 0)
				return;

			var endIndex = Math.Clamp(_bars.Count - 1 - _lookbackOffset, 0, _bars.Count - 1);
			var startIndex = Math.Clamp(endIndex - _lookbackWidth + 1, 0, endIndex);
			_volumeRangeStart = _bars[startIndex].OpenTime;
			_volumeRangeEnd = _bars[endIndex].OpenTime;
		}

		// Keeps the toolbar's width/offset fields in step when the window is instead drawn on
		// the chart via the LOOKBACK WINDOW tool, so neither input method goes stale.
		private void SyncLookbackFieldsFromRange()
		{
			if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end || _bars.Count == 0)
				return;

			var startIndex = _bars.FindIndex(b => b.OpenTime >= start);
			var endIndex = _bars.FindLastIndex(b => b.OpenTime <= end);
			if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
				return;

			_lookbackWidth = endIndex - startIndex + 1;
			_lookbackOffset = _bars.Count - 1 - endIndex;
		}

		// Sparkline's drag callback -- fires continuously as the user drags the window's body
		// (pan, width unchanged) or either edge (resize), not just once on release.
		private void OnWindowChanged((int Width, int Offset) window)
		{
			_lookbackWidth = Math.Max(1, window.Width);
			_lookbackOffset = Math.Max(0, window.Offset);
			ApplyLookbackWindow();
		}

		private string? VolumeRangeLabel()
		{
			if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end)
				return null;
			var barCount = _bars.Count(b => b.OpenTime >= start && b.OpenTime <= end);
			return $"{barCount:N0} bars, {FormatSpan(end - start)}";
		}

		private void FinishDrawing()
		{
			if (
				_pending.HorizontalLines.Count > 0
				|| _pending.VerticalLines.Count > 0
				|| _pending.DiagonalLines.Count > 0
			)
			{
				var drawing = _pending with { Id = _nextDrawingId++ };

				if (drawing.VerticalLines.Count == 2)
				{
					var t1 = drawing.VerticalLines[0].TimeUtc;
					var t2 = drawing.VerticalLines[1].TimeUtc;
					var start = t1 <= t2 ? t1 : t2;
					var end = t1 <= t2 ? t2 : t1;
					drawing = drawing with
					{
						RangeBarCount = _bars.Count(b => b.OpenTime >= start && b.OpenTime <= end),
						RangeSpan = end - start,
					};

					if (drawing.ReferenceLines.Count > 0)
					{
						var referenceStart = drawing.ReferenceLines[0].TimeUtc;
						drawing = drawing with
						{
							ReferenceBarCount = _bars.Count(b => b.OpenTime >= referenceStart && b.OpenTime < start),
						};
					}
				}

				_drawings = _drawings.Append(drawing).ToList();
			}
			_pending = NewPending();
		}

		private void RemoveDrawing(int index)
		{
			_selectedDrawingIds.Remove(_drawings[index].Id);
			_drawings = _drawings.Where((_, i) => i != index).ToList();
		}

		private void ToggleDrawingSelected(int id)
		{
			if (!_selectedDrawingIds.Remove(id))
				_selectedDrawingIds.Add(id);
		}

		private void RemoveSelectedDrawings()
		{
			_drawings = _drawings.Where(d => !_selectedDrawingIds.Contains(d.Id)).ToList();
			_selectedDrawingIds.Clear();
		}

		private void ShowLabelSelectedInput() => _showLabelInput = true;

		private void CancelLabelSelected()
		{
			_showLabelInput = false;
			_labelText = "";
		}

		private void OnLabelKeyDown(KeyboardEventArgs e)
		{
			if (e.Key == "Enter")
				SaveSelectedDrawings();
			else if (e.Key == "Escape")
				CancelLabelSelected();
		}

		private void SaveSelectedDrawings()
		{
			if (string.IsNullOrWhiteSpace(_labelText) || _config is null || _selectedDrawingIds.Count == 0)
				return;

			var label = _labelText.Trim();
			foreach (
				var drawing in _drawings.Where(d =>
					_selectedDrawingIds.Contains(d.Id) && d.Kind is "T-RANGE CHART" or "T-RANGE REFERENCE CHART"
				)
			)
			{
				var t1 = drawing.VerticalLines[0].TimeUtc;
				var t2 = drawing.VerticalLines[1].TimeUtc;
				var start = t1 <= t2 ? t1 : t2;
				var end = t1 <= t2 ? t2 : t1;
				var barCount = drawing.RangeBarCount ?? drawing.ChartBars.Count - (drawing.ReferenceBarCount ?? 0);

				if (drawing.Kind == "T-RANGE REFERENCE CHART")
				{
					var referenceStart = drawing.ReferenceLines[0].TimeUtc;
					var referenceBarCount = drawing.ReferenceBarCount ?? 0;
					TRangeReferenceChartRepository.SaveTRangeReferenceChart(
						label,
						_config.Table,
						start,
						end,
						_config.BarSize.TotalMinutes,
						barCount,
						referenceStart,
						start,
						referenceBarCount
					);
				}
				else
				{
					TRangeChartRepository.SaveTRangeChart(
						label,
						_config.Table,
						start,
						end,
						_config.BarSize.TotalMinutes,
						barCount
					);
				}

				_savedDrawingIds.Add(drawing.Id);
			}

			_savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
			_savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
			_selectedDrawingIds.Clear();
			CancelLabelSelected();
		}

		private void HandleDeleteTRangeChart(TRangeChart chart)
		{
			TRangeChartRepository.DeleteTRangeChart(chart.Id);
			_savedTRangeCharts = TRangeChartRepository.ListTRangeCharts();
			_chartBarsCache.Remove(chart.Id);
		}

		private void HandleDeleteTRangeReferenceChart(TRangeReferenceChart chart)
		{
			TRangeReferenceChartRepository.DeleteTRangeReferenceChart(chart.Id);
			_savedTRangeReferenceCharts = TRangeReferenceChartRepository.ListTRangeReferenceCharts();
			_referenceChartBarsCache.Remove(chart.Id);
		}

		private List<CTraderBarRow>? GetChartBars(TRangeChart chart)
		{
			if (_chartBarsCache.TryGetValue(chart.Id, out var bars))
				return bars;
			if (_chartBarsLoading.Add(chart.Id))
				_ = LoadChartBarsAsync(chart);
			return null;
		}

		private async Task LoadChartBarsAsync(TRangeChart chart)
		{
			try
			{
				var config = new ProfilerConfig(
					chart.Symbol,
					chart.StartTimeUtc,
					chart.EndTimeUtc,
					TimeSpan.FromMinutes(chart.BarMinutes)
				);
				var loaded = await Service.LoadAsync(config, chart.EndTimeUtc, chart.BarCount);
				_chartBarsCache[chart.Id] = ToBarRows(loaded.Bars);
			}
			finally
			{
				_chartBarsLoading.Remove(chart.Id);
			}
			StateHasChanged();
		}

		private List<CTraderBarRow>? GetReferenceChartBars(TRangeReferenceChart chart)
		{
			if (_referenceChartBarsCache.TryGetValue(chart.Id, out var bars))
				return bars;
			if (_referenceChartBarsLoading.Add(chart.Id))
				_ = LoadReferenceChartBarsAsync(chart);
			return null;
		}

		private async Task LoadReferenceChartBarsAsync(TRangeReferenceChart chart)
		{
			try
			{
				var config = new ProfilerConfig(
					chart.Symbol,
					chart.ReferenceStartTimeUtc,
					chart.EndTimeUtc,
					TimeSpan.FromMinutes(chart.BarMinutes)
				);
				var loaded = await Service.LoadAsync(
					config,
					chart.EndTimeUtc,
					chart.BarCount + chart.ReferenceBarCount
				);
				_referenceChartBarsCache[chart.Id] = ToBarRows(loaded.Bars);
			}
			finally
			{
				_referenceChartBarsLoading.Remove(chart.Id);
			}
			StateHasChanged();
		}

		private List<ChartHorizontalLine> AllHorizontalLines() =>
			AllDrawings()
				.SelectMany(d => d.HorizontalLines.Select(l => l with { ColorGroup = d.ColorGroup }))
				.Concat(ProfileLevelLines())
				.Concat(AverageMedianLines())
				.ToList();

		private List<ChartHorizontalLine> AverageMedianLines()
		{
			var lines = new List<ChartHorizontalLine>();
			if (!_showAverageLine && !_showMedianLine)
				return lines;

			var samples = MergedSamples();
			if (_showAverageLine && VolumeProfileStats.ComputeAverage(samples) is { } average)
				lines.Add(new ChartHorizontalLine(average, "AVG", "average"));
			if (_showMedianLine && VolumeProfileStats.ComputeMedian(samples) is { } median)
				lines.Add(new ChartHorizontalLine(median, "MED", "median"));
			return lines;
		}

		private List<ChartHorizontalLine> ProfileLevelLines()
		{
			if (!_showProfileChartLines)
				return new();
			if (VolumeProfileStats.Compute(MergedSamples()) is not { } levels)
				return new();
			return new()
			{
				new ChartHorizontalLine(levels.Poc, "POC", "profile"),
				new ChartHorizontalLine(levels.ValueAreaHigh, "VAH", "profile"),
				new ChartHorizontalLine(levels.ValueAreaLow, "VAL", "profile"),
			};
		}

		private List<ChartLinePoint> VwapPoints()
		{
			if (!_showVwap)
				return new();

			var bars =
				_volumeRangeStart is { } start && _volumeRangeEnd is { } end
					? _bars.Where(b => b.OpenTime >= start && b.OpenTime <= end)
					: _bars;

			var points = new List<ChartLinePoint>();
			double cumulativePriceVolume = 0,
				cumulativeVolume = 0;
			foreach (var bar in bars)
			{
				cumulativePriceVolume += bar.Average * bar.Volume;
				cumulativeVolume += bar.Volume;
				if (cumulativeVolume > 0)
					points.Add(new ChartLinePoint(bar.OpenTime, cumulativePriceVolume / cumulativeVolume));
			}
			return points;
		}

		private List<ChartVerticalLine> AllVerticalLines() =>
			AllDrawings()
				.SelectMany(d =>
					d.VerticalLines.Select(l => l with { ColorGroup = d.ColorGroup })
						.Concat(d.ReferenceLines.Select(l => l with { ColorGroup = "reference" }))
				)
				.ToList();

		private List<ChartDiagonalLine> AllDiagonalLines() =>
			AllDrawings().SelectMany(d => d.DiagonalLines.Select(l => l with { ColorGroup = d.ColorGroup })).ToList();

		private List<ChartRangeFill> AllRangeFills() =>
			AllDrawings()
				.Where(d => d.IsRange)
				.Select(d => new ChartRangeFill(
					d.VerticalLines[0].TimeUtc,
					d.VerticalLines[1].TimeUtc,
					d.Id > 0 ? $"#{d.Id}" : null,
					d.ColorGroup
				))
				.Concat(
					AllDrawings()
						.Where(d => d.ReferenceLines.Count > 0 && d.VerticalLines.Count == 2)
						.Select(d =>
						{
							var earliest =
								d.VerticalLines[0].TimeUtc <= d.VerticalLines[1].TimeUtc
									? d.VerticalLines[0].TimeUtc
									: d.VerticalLines[1].TimeUtc;
							return new ChartRangeFill(
								d.ReferenceLines[0].TimeUtc,
								earliest,
								d.Id > 0 ? $"#{d.Id} REF" : null,
								"reference"
							);
						})
				)
				.Concat(VolumeRangeFill())
				.ToList();

		private IEnumerable<ChartRangeFill> VolumeRangeFill()
		{
			if (_volumeRangeStart is { } start && _volumeRangeEnd is { } end)
				yield return new ChartRangeFill(start, end, "LOOKBACK WINDOW", "volume");
		}

		private static string FormatSpan(TimeSpan span) =>
			span switch
			{
				{ TotalDays: >= 1 } => $"{(int)span.TotalDays}d {span.Hours}h",
				{ TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
				{ TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m",
				_ => $"{(int)span.TotalSeconds}s",
			};

		private static List<CTraderBarRow> ToBarRows(IEnumerable<HlocAvdtBar> bars) =>
			bars.Select(b => new CTraderBarRow(
					b.OpenTime,
					b.Open,
					b.High,
					b.Low,
					b.Close,
					b.Volume,
					b.UpVolume,
					b.DownVolume,
					b.Distance
				))
				.ToList();

		// Box plot mapping (Open/Close = Q1/Q3, High/Low = Min/Max), matching Analize's Price tab.
		private static List<CTraderBarRow> ToQuartileBarRows(IEnumerable<QuartileBar> bars) =>
			bars.Select(b => new CTraderBarRow(b.OpenTime, b.Q1, b.Max, b.Min, b.Q3, b.Count)).ToList();

		private List<WeightedPriceSample> MergedSamples()
		{
			var histograms = _barHistograms;
			if (_volumeRangeStart is { } start && _volumeRangeEnd is { } end)
			{
				histograms = _bars
					.Zip(_barHistograms, (bar, histogram) => (bar, histogram))
					.Where(t => t.bar.OpenTime >= start && t.bar.OpenTime <= end)
					.Select(t => t.histogram)
					.ToList();
			}

			return BarPriceHistogram
				.Merge(histograms, histograms.Count)
				.Select(t => new WeightedPriceSample(t.Price, t.Count, t.UpCount, t.DownCount))
				.ToList();
		}

		private List<WeightedTimeSample> MergedTimeSamples()
		{
			var bars = _bars;
			var histograms = _barHistograms;
			if (_volumeRangeStart is { } start && _volumeRangeEnd is { } end)
			{
				var filtered = _bars
					.Zip(_barHistograms, (bar, histogram) => (bar, histogram))
					.Where(t => t.bar.OpenTime >= start && t.bar.OpenTime <= end)
					.ToList();
				bars = filtered.Select(t => t.bar).ToList();
				histograms = filtered.Select(t => t.histogram).ToList();
			}

			return BarPriceHistogram
				.MergeTime(bars, histograms, histograms.Count)
				.Select(t => new WeightedTimeSample(t.Price, t.Count, t.Seconds, t.UpSeconds, t.DownSeconds))
				.ToList();
		}

		private double? LastClose => _bars.Count > 0 ? _bars[^1].Close : null;

		private int _xgridPipCount = 20;

		// Placeholder until pip size is looked up from the loaded symbol -- ProfilerPage has
		// no per-instrument settings today, unlike Simulation's Config.PipSize.
		private const double XGridPipSize = 0.01;

		private int _xgridColCount = 20;

		private XGridResult? _xgridCache;
		private (int Pips, int Cols, DateTime Start, DateTime End, int Count, double LastClose)? _xgridKey;

		private XGridResult? ComputeXGrid()
		{
			if (_volumeRangeStart is not { } start || _volumeRangeEnd is not { } end)
				return null;

			var key = (_xgridPipCount, _xgridColCount, start, end, _bars.Count, _bars.Count > 0 ? _bars[^1].Close : 0);
			if (_xgridKey == key && _xgridCache is not null)
				return _xgridCache;

			_xgridKey = key;
			_xgridCache = XGridStats.Compute(
				_bars,
				_barHistograms,
				MergedSamples(),
				start,
				end,
				_xgridPipCount * XGridPipSize,
				_xgridColCount
			);
			return _xgridCache;
		}
	}
}
