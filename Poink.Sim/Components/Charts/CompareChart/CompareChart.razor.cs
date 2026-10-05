using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Several series as lines on one shared time axis, for comparing symbols: a main pane scaled by
	/// <see cref="ScaleMode"/>, an optional lower pane for series with Pane 1, and a shaded window.
	/// Draws via its own compareChart.js module, not marketChart.js.
	/// </summary>
	public partial class CompareChart : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		/// <summary>Pushed to JS only when the list reference changes, so hosts rebuild it only when its data does.</summary>
		[Parameter, EditorRequired]
		public IReadOnlyList<CompareChartSeries> Series { get; set; } = [];

		[Parameter]
		public CompareChartScaleMode ScaleMode { get; set; }

		[Parameter]
		public DateTime? WindowStart { get; set; }

		[Parameter]
		public DateTime? WindowEnd { get; set; }

		/// <summary>While true, panning near the oldest point raises <see cref="OnNearStart"/>.</summary>
		[Parameter]
		public bool CanLoadMore { get; set; }

		[Parameter]
		public EventCallback OnNearStart { get; set; }

		[Parameter]
		public string Tooltip { get; set; } = "Every series on one shared time axis";

		private ElementReference _containerRef;
		private Task<IJSObjectReference>? _moduleTask;
		private DotNetObjectReference<CompareChart>? _dotNetRef;
		private IReadOnlyList<CompareChartSeries>? _renderedSeries;
		private CompareChartScaleMode? _renderedScaleMode;
		private (DateTime? Start, DateTime? End)? _renderedWindow;
		private bool? _renderedCanLoadMore;
		private bool _created;
		private bool _disposed;

		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			try
			{
				_moduleTask ??= JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/compareChart.js").AsTask();
				var module = await _moduleTask;
				if (_disposed)
					return;

				if (!_created)
				{
					_created = true;
					_dotNetRef = DotNetObjectReference.Create(this);
					await module.InvokeVoidAsync("create", _containerRef, _dotNetRef);
				}

				if (!ReferenceEquals(_renderedSeries, Series) || _renderedScaleMode != ScaleMode)
				{
					_renderedSeries = Series;
					_renderedScaleMode = ScaleMode;
					await module.InvokeVoidAsync("setSeries", _containerRef, SeriesPayload(), JsScaleMode());
				}

				if (_renderedWindow != (WindowStart, WindowEnd))
				{
					_renderedWindow = (WindowStart, WindowEnd);
					await module.InvokeVoidAsync("setWindow", _containerRef, ToUnixSeconds(WindowStart), ToUnixSeconds(WindowEnd));
				}

				if (_renderedCanLoadMore != CanLoadMore)
				{
					_renderedCanLoadMore = CanLoadMore;
					await module.InvokeVoidAsync("setCanLoadMore", _containerRef, CanLoadMore);
				}
			}
			catch (ObjectDisposedException) { }
			catch (JSDisconnectedException) { }
		}

		/// <summary>Called from compareChart.js when the view pans near the oldest loaded point.</summary>
		[JSInvokable]
		public Task OnNearStartAsync() => OnNearStart.HasDelegate ? OnNearStart.InvokeAsync() : Task.CompletedTask;

		private object SeriesPayload() =>
			Series.Select(s => new
			{
				key = $"{s.Pane}:{s.Label}",
				label = s.Label,
				colorVar = s.ColorVar,
				pane = s.Pane,
				points = s.Points.Select(p => new { time = ToUnixSeconds(p.TimeUtc), value = p.Price }),
			});

		private string JsScaleMode() => ScaleMode == CompareChartScaleMode.PercentValues ? "percentvalues" : "rebased";

		private static long? ToUnixSeconds(DateTime? time) =>
			time is { } t ? new DateTimeOffset(t, TimeSpan.Zero).ToUnixTimeSeconds() : null;

		public async ValueTask DisposeAsync()
		{
			_disposed = true;

			if (_moduleTask is not null)
			{
				try
				{
					var module = await _moduleTask;
					await module.InvokeVoidAsync("dispose", _containerRef);
					await module.DisposeAsync();
				}
				catch (JSDisconnectedException) { }
				catch (ObjectDisposedException) { }
			}

			_dotNetRef?.Dispose();

			GC.SuppressFinalize(this);
		}
	}
}
