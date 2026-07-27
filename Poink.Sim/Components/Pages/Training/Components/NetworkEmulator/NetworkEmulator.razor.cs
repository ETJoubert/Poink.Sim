using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Poink.Sim.Brain.Neural;

namespace Poink.Sim.Components.Training
{
    // Stylized, ambient visualization of the policy network "at work" -- not a
    // literal introspection of a brain's real activations (nothing pipes
    // per-tick internals out mid-decision), just a canvas animation that
    // visibly changes state between idle and running so training feels alive.
    // LayerShapes IS the real selected brain's architecture though (see
    // IPolicyGradientBrain.DescribeNetworkShape) -- node-per-layer counts are
    // still stylized down for legibility on the JS side, but the layer count/
    // relative sizes/recurrent marker are whichever brain is actually picked,
    // not a hardcoded MLP shape.
    // Same JS ES-module import/dispose shape as DrawableOhlcvChart (this is nontrivial
    // enough to earn its own file rather than an inline eval'd string).
    public partial class NetworkEmulator : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public bool IsRunning { get; set; }
        [Parameter] public IReadOnlyList<NetworkLayerShape> LayerShapes { get; set; } = [];

        private ElementReference _canvasRef;
        private IJSObjectReference? _instance;
        private bool _lastRunning;
        private IReadOnlyList<NetworkLayerShape> _lastLayerShapes = [];

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/networkEmulator.js");
                _instance = await module.InvokeAsync<IJSObjectReference>("create", _canvasRef);
                _lastRunning = IsRunning;
                _lastLayerShapes = LayerShapes;
                await _instance.InvokeVoidAsync("setRunning", IsRunning);
                await _instance.InvokeVoidAsync("setLayerShapes", LayerShapes);
            }
            else if (_instance is not null)
            {
                if (_lastRunning != IsRunning)
                {
                    _lastRunning = IsRunning;
                    await _instance.InvokeVoidAsync("setRunning", IsRunning);
                }
                if (!_lastLayerShapes.SequenceEqual(LayerShapes))
                {
                    _lastLayerShapes = LayerShapes;
                    await _instance.InvokeVoidAsync("setLayerShapes", LayerShapes);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_instance is not null)
            {
                try
                {
                    await _instance.InvokeVoidAsync("dispose");
                    await _instance.DisposeAsync();
                }
                catch (JSDisconnectedException) { }
            }

            GC.SuppressFinalize(this);
        }
    }
}
