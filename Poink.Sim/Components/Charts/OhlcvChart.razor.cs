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
        [Parameter] public double? CurrentBid { get; set; }
        [Parameter] public double? CurrentAsk { get; set; }
        [Parameter] public DateTime? WindowStartUtc { get; set; }

        private ElementReference _containerRef;
        private IJSObjectReference? _module;
        private string? _chartId;
        private List<CTraderBarRow>? _renderedBars;
        private List<PredictedBar>? _renderedPredicted;
        private double? _renderedBid;
        private double? _renderedAsk;
        private DateTime? _renderedWindowStart;
        private bool _expanded;
        private bool _disposed;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            // The sim's background timer (SimulationEngine.TimerCallback) drives StateHasChanged
            // independently of user input, so a render can be mid-await here right as the user
            // hides the chart / starts a new sim, which disposes _module out from under this
            // suspended continuation. _disposed is checked after every await -- since Blazor
            // Server serializes render/dispose on the same sync context, nothing can slip in
            // between a passing check and the JS call that immediately follows it -- and the
            // catches below are a backstop for disposal paths (e.g. circuit teardown) that
            // aren't gated by that flag.
            try
            {
                if (firstRender)
                {
                    _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/ohlcvChart.js");
                    if (_disposed) return;
                    _chartId = await _module.InvokeAsync<string>("create", _containerRef);
                }

                if (_disposed) return;

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

                if (_disposed) return;

                // Gated separately from Bars/Predicted above: CurrentBid/Ask moves every tick,
                // while Bars only advances once per completed bar interval, so piggybacking on
                // the Bars-changed check would leave the price lines stale between bar closes.
                if (_module is not null && _chartId is not null && (_renderedBid != CurrentBid || _renderedAsk != CurrentAsk))
                {
                    _renderedBid = CurrentBid;
                    _renderedAsk = CurrentAsk;
                    await _module.InvokeVoidAsync("setCurrentPrices", _chartId, CurrentBid, CurrentAsk);
                }

                if (_disposed) return;

                // Marks how far back the tick window (driving the volume-profile panel) reaches --
                // gated on its own, same reasoning as CurrentBid/Ask above.
                if (_module is not null && _chartId is not null && _renderedWindowStart != WindowStartUtc)
                {
                    _renderedWindowStart = WindowStartUtc;
                    long? unixSeconds = WindowStartUtc is { } w ? new DateTimeOffset(w, TimeSpan.Zero).ToUnixTimeSeconds() : null;
                    await _module.InvokeVoidAsync("setWindowStart", _chartId, unixSeconds);
                }
            }
            catch (ObjectDisposedException) { }
            catch (JSDisconnectedException) { }
        }

        private void ToggleExpanded() => _expanded = !_expanded;

        public async ValueTask DisposeAsync()
        {
            _disposed = true;

            if (_module is not null)
            {
                try
                {
                    if (_chartId is not null) await _module.InvokeVoidAsync("dispose", _chartId);
                    await _module.DisposeAsync();
                }
                catch (JSDisconnectedException) { }
                catch (ObjectDisposedException) { }
            }

            GC.SuppressFinalize(this);
        }
    }
}
