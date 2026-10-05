using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// TxFold for several symbols at once: one group per selected trailing timeframe ending at <see cref="AnchorEnd"/>,
	/// holding each symbol's bar as % change from that symbol's last price at the anchor, so differently priced symbols
	/// share one axis. <see cref="Layout"/> overlays the bars or sets them side by side. Draws via compareTxFold.js.
	/// </summary>
	public partial class CompareTxFold : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		[Inject]
		private IJSRuntime JSRuntime { get; set; } = default!;

		[Inject]
		private ILogger<CompareTxFold> Logger { get; set; } = default!;

		[Parameter, EditorRequired]
		public IReadOnlyList<CompareTxFoldSymbol> Symbols { get; set; } = [];

		/// <summary>Where every trailing window ends; null shows <see cref="NoAnchorMessage"/> instead of loading.</summary>
		[Parameter]
		public DateTime? AnchorEnd { get; set; }

		/// <summary>Earliest instant the host considers available -- a window reaching further back is flagged as partial.</summary>
		[Parameter, EditorRequired]
		public DateTime RangeStart { get; set; }

		[Parameter]
		public string NoAnchorMessage { get; set; } = "Pick an anchor time to view the snapshot.";

		[Parameter]
		public IReadOnlyCollection<int> SelectedSeconds { get; set; } = SnapshotTimeframeOptions.DefaultSelectedSeconds.ToHashSet();

		[Parameter]
		public EventCallback<IReadOnlyCollection<int>> SelectedSecondsChanged { get; set; }

		[Parameter]
		public BarAggregationKind BarKind { get; set; } = BarAggregationKind.Ohlc;

		[Parameter]
		public EventCallback<BarAggregationKind> BarKindChanged { get; set; }

		[Parameter]
		public CompareTxFoldLayout Layout { get; set; }

		[Parameter]
		public EventCallback<CompareTxFoldLayout> LayoutChanged { get; set; }

		/// <summary>Overlays every symbol's tick profile over the largest selected window in the right margin, on the same % axis.</summary>
		[Parameter]
		public bool ShowVolumeProfile { get; set; }

		[Parameter]
		public EventCallback<bool> ShowVolumeProfileChanged { get; set; }

		private sealed record SymbolSnapshot(string Table, double ReferencePrice, List<TxFoldBar> Bars, IReadOnlyList<TxFoldVolumeBucket> VolumeProfile);

		private List<SymbolSnapshot> _snapshots = new();
		private string? _loadedKey;
		private bool _loading;
		private string? _error;

		private ElementReference _canvasRef;
		private IJSObjectReference? _module;
		private string? _renderedKey;
		private bool _disposed;

		protected override Task OnParametersSetAsync() => RefreshIfNeeded();

		private string? CurrentLoadKey() =>
			AnchorEnd is { } anchorEnd && Symbols.Count > 0 && SelectedSeconds.Count > 0
				? $"{string.Join("|", Symbols.Select(s => s.Table))}#{anchorEnd.Ticks}#{RangeStart.Ticks}#{BarKind}#{string.Join(",", SelectedSeconds.Order())}"
				: null;

		// Loops so parameters that changed while a load was in flight are picked up once it finishes.
		private async Task RefreshIfNeeded()
		{
			if (_loading)
				return;

			while (CurrentLoadKey() is { } key && key != _loadedKey)
			{
				_loading = true;
				_error = null;
				StateHasChanged();

				try
				{
					_snapshots = await LoadAsync(Symbols.Select(s => s.Table).ToList(), AnchorEnd!.Value, RangeStart, SelectedSeconds.ToList(), BarKind);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "Failed to build the compare TxFold for [{Tables}]", string.Join(", ", Symbols.Select(s => s.Table)));
					_error = ex.Message;
					_snapshots = new();
				}
				finally
				{
					_loadedKey = key;
					_loading = false;
				}
				StateHasChanged();
			}
		}

		private async Task<List<SymbolSnapshot>> LoadAsync(
			IReadOnlyList<string> tables,
			DateTime anchorEnd,
			DateTime rangeStart,
			IReadOnlyList<int> seconds,
			BarAggregationKind barKind
		)
		{
			var snapshots = await Task.WhenAll(tables.Select(table => Task.Run(() => LoadSymbol(table, anchorEnd, rangeStart, seconds, barKind))));
			return snapshots.OfType<SymbolSnapshot>().ToList();
		}

		private SymbolSnapshot? LoadSymbol(string table, DateTime anchorEnd, DateTime rangeStart, IReadOnlyList<int> seconds, BarAggregationKind barKind)
		{
			var ticks = TickRepository.ReadTicks(table, anchorEnd.AddSeconds(-seconds.Max()), anchorEnd).ToList();
			if (ticks.Count == 0)
				return null;

			var referencePrice = (ticks[^1].Bid + ticks[^1].Ask) / 2;
			var snapshot = TxFoldStats.Compute(ticks, anchorEnd, rangeStart, seconds, barKind, includeLevels: false);
			return new SymbolSnapshot(table, referencePrice, snapshot.Bars, snapshot.VolumeProfile);
		}

		private async Task SetBarKind(BarAggregationKind kind)
		{
			if (BarKind == kind)
				return;
			BarKind = kind;
			await BarKindChanged.InvokeAsync(kind);
			await RefreshIfNeeded();
		}

		private Task SetLayout(CompareTxFoldLayout layout)
		{
			Layout = layout;
			return LayoutChanged.InvokeAsync(layout);
		}

		private Task ToggleVolumeProfile()
		{
			ShowVolumeProfile = !ShowVolumeProfile;
			return ShowVolumeProfileChanged.InvokeAsync(ShowVolumeProfile);
		}

		private async Task ToggleTimeframe(int seconds)
		{
			var selected = SelectedSeconds.ToHashSet();
			if (!selected.Remove(seconds))
				selected.Add(seconds);
			if (selected.Count == 0)
				return;

			SelectedSeconds = selected;
			await SelectedSecondsChanged.InvokeAsync(selected);
			await RefreshIfNeeded();
		}

		private string ColorVarFor(string table) => Symbols.FirstOrDefault(s => s.Table == table)?.ColorVar ?? "--color-neutral";

		private string MissingSymbolsNote()
		{
			var missing = Symbols.Select(s => s.Table).Except(_snapshots.Select(s => s.Table)).ToList();
			return missing.Count == 0 ? "" : $"No ticks before the anchor for {string.Join(", ", missing)}";
		}

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			if (_snapshots.Count == 0)
			{
				_renderedKey = null;
				return;
			}

			try
			{
				_module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/compareTxFold.js");
				if (_disposed)
					return;

				var renderKey = $"{_loadedKey}#{Layout}#{ShowVolumeProfile}#{string.Join("|", Symbols.Select(s => s.ColorVar))}";
				if (renderKey == _renderedKey)
					return;
				_renderedKey = renderKey;

				await _module.InvokeVoidAsync("render", _canvasRef, RenderOptions());
			}
			catch (JSDisconnectedException) { }
			catch (ObjectDisposedException) { }
		}

		private object RenderOptions() =>
			new
			{
				layout = Layout == CompareTxFoldLayout.SideBySide ? "sidebyside" : "overlaid",
				showVolumeProfile = ShowVolumeProfile,
				groups = SnapshotTimeframeOptions.All.Where(o => SelectedSeconds.Contains(o.Seconds)).Select(o => new { seconds = o.Seconds, label = o.Label }),
				series = _snapshots.Select(s => new
				{
					label = s.Table,
					colorVar = ColorVarFor(s.Table),
					bars = s.Bars.Select(b => new
					{
						seconds = b.LookbackSeconds,
						open = ToPercent(b.Bar.Open, s.ReferencePrice),
						high = ToPercent(b.Bar.High, s.ReferencePrice),
						low = ToPercent(b.Bar.Low, s.ReferencePrice),
						close = ToPercent(b.Bar.Close, s.ReferencePrice),
						hasFullCoverage = b.HasFullCoverage,
					}),
					profile = s.VolumeProfile.Select(v => new
					{
						low = ToPercent(v.Low, s.ReferencePrice),
						high = ToPercent(v.High, s.ReferencePrice),
						count = v.Count,
					}),
				}),
			};

		private static double ToPercent(double price, double reference) => reference == 0 ? 0 : (price / reference - 1) * 100;

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
