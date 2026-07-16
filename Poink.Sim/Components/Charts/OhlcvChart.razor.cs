using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Shared.Client.Components.Pages.CTrader;

namespace Poink.Sim.Components.Charts
{
    // Backed by the vendored TradingView Lightweight Charts library for pan/zoom, a hover
    // crosshair tooltip, and an expand-to-fullscreen toggle (see Poink.ML's copy of this
    // component for the twin -- kept separate so Poink.Sim carries no Poink.ML dependency).
    public partial class OhlcvChart : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public List<CTraderBarRow> Bars { get; set; } = new();
        [Parameter] public List<PredictedBar> Predicted { get; set; } = new();

        private ElementReference _containerRef;
        private IJSObjectReference? _module;
        private string? _chartId;
        private List<CTraderBarRow>? _renderedBars;
        private List<PredictedBar>? _renderedPredicted;
        private bool _expanded;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/ohlcvChart.js");
                _chartId = await _module.InvokeAsync<string>("create", _containerRef);
            }

            if (_module is not null && _chartId is not null &&
                (!ReferenceEquals(_renderedBars, Bars) || !ReferenceEquals(_renderedPredicted, Predicted)))
            {
                _renderedBars = Bars;
                _renderedPredicted = Predicted;

                var payload = Bars.Select(b => new
                {
                    time = new DateTimeOffset(b.TimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                    open = b.Open,
                    high = b.High,
                    low = b.Low,
                    close = b.Close,
                    volume = b.Volume,
                });

                var predictedPayload = Predicted.Select(p => new { high = p.High, low = p.Low, label = p.Label, color = p.Color });

                await _module.InvokeVoidAsync("setData", _chartId, payload, predictedPayload);
            }
        }

        private void ToggleExpanded() => _expanded = !_expanded;

        public async ValueTask DisposeAsync()
        {
            if (_module is not null)
            {
                try
                {
                    if (_chartId is not null) await _module.InvokeVoidAsync("dispose", _chartId);
                    await _module.DisposeAsync();
                }
                catch (JSDisconnectedException) { }
            }

            GC.SuppressFinalize(this);
        }
    }
}
