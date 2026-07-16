using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Poink.Sim.Components.Training
{
    // Stylized, ambient visualization of the policy network "at work" -- not a
    // literal introspection of MlpPolicyBrain's real activations (nothing pipes
    // per-tick internals out of the brain today), just a canvas animation that
    // visibly changes state between idle and running so training feels alive.
    // Same JS ES-module import/dispose shape as OhlcvChart (this is nontrivial
    // enough to earn its own file rather than an inline eval'd string).
    public partial class NetworkEmulator : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

        [Parameter] public bool IsRunning { get; set; }

        private ElementReference _canvasRef;
        private IJSObjectReference? _instance;
        private bool _lastRunning;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                var module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/networkEmulator.js");
                _instance = await module.InvokeAsync<IJSObjectReference>("create", _canvasRef);
                _lastRunning = IsRunning;
                await _instance.InvokeVoidAsync("setRunning", IsRunning);
            }
            else if (_instance is not null && _lastRunning != IsRunning)
            {
                _lastRunning = IsRunning;
                await _instance.InvokeVoidAsync("setRunning", IsRunning);
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
