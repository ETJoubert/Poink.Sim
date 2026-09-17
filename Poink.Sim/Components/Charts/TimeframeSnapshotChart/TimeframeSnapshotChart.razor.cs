using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Models;
using Poink.Sim.Services.Charts;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Self-contained multi-timeframe comparison chart -- each toggled timeframe (see
	/// <see cref="SnapshotTimeframeOptions"/>, 1m/2m/4m/8m/15m/30m/1h/2h selected by default) renders
	/// as its own single bar over its own trailing window ending at <see cref="AnchorEnd"/>, side by
	/// side, via <see cref="ITimeframeSnapshotService"/>.
	/// </summary>
	public partial class TimeframeSnapshotChart : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private ITimeframeSnapshotService SnapshotService { get; set; } = default!;

		[Inject]
		private IJSRuntime JSRuntime { get; set; } = default!;

		[Parameter, EditorRequired]
		public string Table { get; set; } = "";

		[Parameter]
		public DateTime AnchorEnd { get; set; } = DateTime.UtcNow;

		/// <summary>Drawn as a dashed horizontal line across every bar -- null hides it.</summary>
		[Parameter]
		public double? LatestPrice { get; set; }

		private static readonly int[] DefaultSelectedSeconds = { 60, 120, 240, 480, 900, 1800, 3600, 7200 };

		private BarAggregationKind _barKind = BarAggregationKind.Ohlc;
		private HashSet<int> _selectedSeconds = DefaultSelectedSeconds.ToHashSet();
		private List<(int LookbackSeconds, CTraderBarRow Bar)> _bars = new();
		private (string Table, DateTime AnchorEnd, BarAggregationKind BarKind, string SelectedKey)? _loadedFor;
		private bool _loading;
		private string? _error;

		private ElementReference _canvasRef;
		private IJSObjectReference? _module;
		private List<(int LookbackSeconds, CTraderBarRow Bar)>? _renderedBars;
		private double? _renderedLatestPrice;
		private bool _disposed;

		protected override async Task OnParametersSetAsync() => await RefreshIfNeeded();

		private async Task SetBarKind(BarAggregationKind kind)
		{
			if (_barKind == kind)
				return;
			_barKind = kind;
			await RefreshIfNeeded();
		}

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
			var key = (Table, AnchorEnd, _barKind, selectedKey);
			if (_loadedFor == key)
				return;

			_loading = true;
			_error = null;
			StateHasChanged();

			try
			{
				_bars = await SnapshotService.BuildSnapshotBarsAsync(Table, AnchorEnd, _selectedSeconds, _barKind);
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
					_module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/timeframeSnapshotChart.js");
					if (_disposed)
						return;
				}

				if (!ReferenceEquals(_renderedBars, _bars) || _renderedLatestPrice != LatestPrice)
				{
					_renderedBars = _bars;
					_renderedLatestPrice = LatestPrice;

					var payload = _bars.Select(pair => new
					{
						label = SnapshotTimeframeOptions.All.First(o => o.Seconds == pair.LookbackSeconds).Label,
						open = pair.Bar.Open,
						high = pair.Bar.High,
						low = pair.Bar.Low,
						close = pair.Bar.Close,
					});

					await _module.InvokeVoidAsync("render", _canvasRef, payload, LatestPrice);
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
