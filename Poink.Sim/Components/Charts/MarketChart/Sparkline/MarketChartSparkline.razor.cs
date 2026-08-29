using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Standalone, read-only timeline of a bar list's close prices -- takes its own <see cref="Bars"/>
	/// directly (e.g. ProfilerPage's currently loaded/selected data) rather than syncing to a
	/// MarketChart instance, so it has no dependency on one being present, mounted, or interactive.
	/// Optionally highlights a Lookback Window (<see cref="WindowWidth"/>/<see cref="WindowOffset"/>,
	/// both in bars back from the most recent one) and lets the user drag its body to move it or
	/// either edge to resize it, live as they drag. Draws via its own marketChartSparkline.js
	/// module, not marketChart.js.
	/// </summary>
	public partial class MarketChartSparkline : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		[Parameter]
		public List<CTraderBarRow> Bars { get; set; } = new();

		/// <summary>Bars the highlighted window spans; null hides the highlight (and its drag handle) entirely.</summary>
		[Parameter]
		public int? WindowWidth { get; set; }

		/// <summary>Bars back from the most recently loaded bar to the window's end.</summary>
		[Parameter]
		public int? WindowOffset { get; set; }

		/// <summary>Fires continuously while the user drags the window's body (pan) or either edge (resize).</summary>
		[Parameter]
		public EventCallback<(int Width, int Offset)> WindowChanged { get; set; }

		private ElementReference _containerRef;
		private Task<IJSObjectReference>? _moduleTask;
		private DotNetObjectReference<MarketChartSparkline>? _dotNetRef;
		private List<CTraderBarRow>? _renderedBars;
		private int? _renderedWindowWidth;
		private int? _renderedWindowOffset;
		private bool _created;
		private bool _disposed;

		// _moduleTask is memoized and awaited fresh on every call, not just started on firstRender --
		// Blazor can call OnAfterRenderAsync again before a still-pending earlier call's import resolves.
		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			try
			{
				_moduleTask ??= JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/marketChartSparkline.js").AsTask();
				var module = await _moduleTask;
				if (_disposed)
					return;

				if (!_created)
				{
					_created = true;
					_dotNetRef = DotNetObjectReference.Create(this);
					await module.InvokeVoidAsync("create", _containerRef, _dotNetRef);
					if (_disposed)
						return;
				}

				if (!ReferenceEquals(_renderedBars, Bars))
				{
					_renderedBars = Bars;
					var payload = Bars.Select(b => new { close = b.Close });
					await module.InvokeVoidAsync("setBars", _containerRef, payload);
					if (_disposed)
						return;
				}

				if (_renderedWindowWidth != WindowWidth || _renderedWindowOffset != WindowOffset)
				{
					_renderedWindowWidth = WindowWidth;
					_renderedWindowOffset = WindowOffset;
					await module.InvokeVoidAsync("setWindow", _containerRef, WindowWidth, WindowOffset);
				}
			}
			catch (ObjectDisposedException) { }
			catch (JSDisconnectedException) { }
		}

		[JSInvokable]
		public Task OnWindowChangedAsync(double width, double offset) =>
			WindowChanged.HasDelegate ? WindowChanged.InvokeAsync(((int)width, (int)offset)) : Task.CompletedTask;

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
