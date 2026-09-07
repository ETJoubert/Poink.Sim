using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Charts
{
	/// <summary>
	/// Read-only equity-over-time chart: a solid line for equity, plus two fainter lines tracking
	/// the best and worst equity reached while the currently open trade (if any) was open -- gaps
	/// outside any open trade. Takes plain <see cref="EquityCurveSample"/> data (built via
	/// <see cref="EquityCurveHelper"/>) rather than RL- or simulation-specific types, so both
	/// Episode Playback and Simulation can feed it from their own equity series.
	/// </summary>
	public partial class EquityCurve : ComponentBase, IAsyncDisposable
	{
		[Inject]
		private IJSRuntime JSRuntime { get; set; } = null!;

		[Parameter]
		public IReadOnlyList<EquityCurveSample> Samples { get; set; } = Array.Empty<EquityCurveSample>();

		private ElementReference _containerRef;
		private Task<IJSObjectReference>? _moduleTask;
		private IReadOnlyList<EquityCurveSample>? _renderedSamples;
		private bool _created;
		private bool _disposed;

		// _moduleTask is memoized and awaited fresh on every call, not just started on firstRender --
		// Blazor can call OnAfterRenderAsync again before a still-pending earlier call's import resolves.
		protected override async Task OnAfterRenderAsync(bool firstRender)
		{
			try
			{
				_moduleTask ??= JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/equityCurve.js").AsTask();
				var module = await _moduleTask;
				if (_disposed)
					return;

				if (!_created)
				{
					_created = true;
					await module.InvokeVoidAsync("create", _containerRef);
					if (_disposed)
						return;
				}

				if (!ReferenceEquals(_renderedSamples, Samples))
				{
					_renderedSamples = Samples;
					var payload = Samples.Select(s => new
					{
						equity = s.Equity,
						tradeMax = s.TradeMaxEquity,
						tradeMin = s.TradeMinEquity,
						tradeNumber = s.TradeNumber,
					});
					await module.InvokeVoidAsync("setSamples", _containerRef, payload);
				}
			}
			catch (ObjectDisposedException) { }
			catch (JSDisconnectedException) { }
		}

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

			GC.SuppressFinalize(this);
		}
	}
}
