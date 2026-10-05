using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Components.Charts;
using Poink.Sim.Components.Shared.Indicators;
using Poink.Sim.Helpers;

namespace Poink.Sim.Components.Simulation
{
	/// <summary>
	/// One watched symbol's full chart set on the Simulation page -- main chart, XGrid, volume/time profiles and TxFold --
	/// shown by the page's panel toggles and driven by its Lookback Window and Indicators, like a Profiler chart set.
	/// </summary>
	public partial class SimulationSymbolChartSet : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JS { get; set; } = default!;

		[Parameter, EditorRequired]
		public SimulationSymbolPanelData Data { get; set; } = default!;

		[Parameter, EditorRequired]
		public SimulationChartSetOptions Options { get; set; } = default!;

		/// <summary>Keeps the user's pan/zoom across updates; off right after a load so the first bars fit the view.</summary>
		[Parameter]
		public bool KeepView { get; set; }

		/// <summary>Kept per symbol by the page, since one pip spans a different price distance on every symbol.</summary>
		[Parameter]
		public int XGridPipCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> XGridPipCountChanged { get; set; }

		[Parameter]
		public int XGridColCount { get; set; } = 20;

		[Parameter]
		public EventCallback<int> XGridColCountChanged { get; set; }

		[Parameter]
		public HashSet<int> TxFoldSelectedSeconds { get; set; } = new();

		[Parameter]
		public EventCallback<HashSet<int>> TxFoldSelectedSecondsChanged { get; set; }

		private HashSet<TxFoldLevelKind> _txFoldLevels = new();
		private double? _height;
		private ElementReference _chartRowRef;
		private ElementReference _dividerRefXGrid;
		private ElementReference _dividerRefVolume;
		private ElementReference _dividerRefTime;
		private ElementReference _dividerRefTxFold;
		private ElementReference _resizeHandleRef;
		private IJSObjectReference? _dividerResizeInstance;
		private DotNetObjectReference<SimulationSymbolChartSet>? _selfReference;
		private HashSet<string> _lastVisiblePanels = new();
		private bool _disposed;

		private string SetStyle =>
			_height is { } height
				? FormattableString.Invariant($"--sscs-symbol-color: var({Data.ColorVar}); flex: 0 0 auto; height: {height}px")
				: $"--sscs-symbol-color: var({Data.ColorVar})";

		private bool ShowVolumeProfileLevels =>
			Options.Indicators.PocVisible || Options.Indicators.VahVisible || Options.Indicators.ValVisible;

		protected override void OnParametersSet() => _txFoldLevels = IndicatorsHelper.ToTxFoldLevels(Options.Indicators);

		// The shared chart-set divider script, so this and the Profiler chart sets size and drag their panel rows alike.
		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			try
			{
				if (firstRender)
				{
					var dividers = await JS.InvokeAsync<IJSObjectReference>("eval", ChartSetDividerScript.Source);
					if (_disposed)
					{
						await dividers.DisposeAsync();
						return;
					}
					_dividerResizeInstance = dividers;
					_selfReference = DotNetObjectReference.Create(this);
					await _dividerResizeInstance.InvokeVoidAsync("bindVertical", _resizeHandleRef, _selfReference);
				}

				if (_disposed || !Options.Aggregate || _dividerResizeInstance is null)
					return;

				var visiblePanels = CurrentVisiblePanelKeys();
				if (!visiblePanels.SetEquals(_lastVisiblePanels))
				{
					_lastVisiblePanels = visiblePanels;
					await _dividerResizeInstance.InvokeVoidAsync("resetEqual", _chartRowRef);
				}

				if (Options.ShowXGrid && Options.ShowMainChart)
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefXGrid);
				if (Options.ShowVolumeProfile && (Options.ShowMainChart || Options.ShowXGrid))
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefVolume);
				if (Options.ShowTimeProfile && (Options.ShowMainChart || Options.ShowXGrid || Options.ShowVolumeProfile))
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefTime);
				if (Options.ShowTxFold && (Options.ShowMainChart || Options.ShowXGrid || Options.ShowVolumeProfile || Options.ShowTimeProfile))
					await _dividerResizeInstance.InvokeVoidAsync("bind", _dividerRefTxFold);
			}
			catch (ObjectDisposedException) { }
			catch (JSDisconnectedException) { }
		}

		/// <summary>Called from the resize handle's JS when a height drag finishes, or with null when a double-click resets it.</summary>
		[JSInvokable]
		public Task OnHeightResized(double? height)
		{
			_height = height;
			StateHasChanged();
			return Task.CompletedTask;
		}

		public async ValueTask DisposeAsync()
		{
			_disposed = true;
			_selfReference?.Dispose();
			if (_dividerResizeInstance is null)
				return;
			try
			{
				await _dividerResizeInstance.DisposeAsync();
			}
			catch (JSDisconnectedException) { }
		}

		private HashSet<string> CurrentVisiblePanelKeys()
		{
			var keys = new HashSet<string>();
			if (Options.ShowMainChart) keys.Add("main");
			if (Options.ShowXGrid) keys.Add("xgrid");
			if (Options.ShowVolumeProfile) keys.Add("volume");
			if (Options.ShowTimeProfile) keys.Add("time");
			if (Options.ShowTxFold) keys.Add("txfold");
			return keys;
		}
	}
}
