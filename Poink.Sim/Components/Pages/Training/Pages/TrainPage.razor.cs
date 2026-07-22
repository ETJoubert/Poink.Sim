using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Training;
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

        // Both tabs now stay mounted (toggled via CSS, not @if) so this ref stays
        // valid and Setup's own state survives switching to Live and back -- the
        // top bar's Start Training button needs to reach into Setup regardless of
        // which tab is currently showing.
        private TrainSetupTab? _setupTab;

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

        // Lets the top bar's buttons work no matter which tab is active. On
        // validation failure, switch to Setup so the error message (rendered
        // there) is actually visible.
        private async Task StartTraining()
        {
            if (_setupTab is null) return;

            await _setupTab.StartTraining();

            if (_setupTab.StartError is not null) _activeTab = TrainTab.Setup;
        }

        // Mirrors Simulation's Load Dummy Data button -- ignores whatever
        // symbol/range is picked in Setup, always synthetic random-walk ticks.
        private async Task StartTrainingSynthetic()
        {
            if (_setupTab is null) return;

            await _setupTab.StartTrainingSynthetic();

            if (_setupTab.StartError is not null) _activeTab = TrainTab.Setup;
        }

        // Drives the sticky header's progress bar -- shared across both tabs
        // since it now lives in the static top bar rather than inside Setup.
        private int ProgressPercent =>
            Service.TotalEpisodes <= 0 ? 0 : (int)Math.Round(Service.EpisodesCompleted * 100.0 / Service.TotalEpisodes);
    }
}
