using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Models;

namespace Poink.Sim.Components.Charts
{
    // Dependency-free canvas OHLC candlestick chart -- no pan/zoom/crosshair, no volume/
    // annotation layers (see MarketChart for the full lightweight-charts-backed view).
    // Used for the small T-Range Chart/T-Range Reference Chart previews on ProfilerPage.
    public partial class CandlestickChart : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public List<CTraderBarRow> Bars { get; set; } = new();

        // Shades the first ReferenceBarCount bars of Bars as a lookback window, with a
        // dashed boundary -- see ProfilerPage's T-Range Reference Chart drawing.
        [Parameter] public int ReferenceBarCount { get; set; }

        private ElementReference _canvasRef;
        private IJSObjectReference? _module;
        private List<CTraderBarRow>? _renderedBars;
        private int _renderedReferenceBarCount;
        private bool _disposed;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            try
            {
                if (firstRender)
                {
                    _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/candlestickChart.js");
                    if (_disposed)
                        return;
                }

                if (_module is not null &&
                    (!ReferenceEquals(_renderedBars, Bars) || _renderedReferenceBarCount != ReferenceBarCount))
                {
                    _renderedBars = Bars;
                    _renderedReferenceBarCount = ReferenceBarCount;

                    var payload = Bars.Select(b => new
                    {
                        time = new DateTimeOffset(b.TimeUtc, TimeSpan.Zero).ToUnixTimeSeconds(),
                        open = b.Open,
                        high = b.High,
                        low = b.Low,
                        close = b.Close,
                    });

                    await _module.InvokeVoidAsync("render", _canvasRef, payload, ReferenceBarCount);
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
