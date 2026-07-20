using System;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Training;

namespace Poink.Sim.Components.Pages
{
    public partial class TrainPage : ComponentBase, IDisposable
    {
        [Inject] private RlTrainingService Service { get; set; } = default!;

        // The one piece of Setup-tab state the Live tab also needs (to visualize
        // whichever brain is currently picked) -- everything else setup/live-
        // specific now lives inside TrainSetupTab/TrainLiveTab respectively.
        private string _brainName = "";

        private enum TrainTab { Setup, Live }
        private TrainTab _activeTab = TrainTab.Setup;

        protected override void OnInitialized()
        {
            Service.OnChanged += HandleServiceChanged;

            if (Service.BrainOptions.Count > 0) _brainName = Service.BrainOptions[0].Name;
        }

        private void HandleServiceChanged() => _ = InvokeAsync(StateHasChanged);

        public void Dispose() => Service.OnChanged -= HandleServiceChanged;

        // TrainSetupTab jumps the page straight to the live view once a run is
        // actually kicked off -- that's the whole point of the tab existing.
        private void HandleTrainingStarted() => _activeTab = TrainTab.Live;
    }
}
