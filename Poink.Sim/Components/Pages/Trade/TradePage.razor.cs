using Microsoft.AspNetCore.Components;
using Poink.Sim.Services.CTrader;
using System;
using System.Threading.Tasks;

namespace Poink.Sim.Components.Pages.Trade
{
    public partial class TradePage : ComponentBase, IDisposable
    {
        [Inject]
        private ICTraderSessionService Session { get; set; } = null!;

        [Inject]
        private NavigationManager Navigation { get; set; } = null!;

        private string StateLabel => Session.State switch
        {
            CTraderConnectionState.Connecting => "CONNECTING",
            CTraderConnectionState.Connected => "CONNECTED",
            CTraderConnectionState.Failed => "FAILED",
            _ => "DISCONNECTED"
        };

        private string StateClass => Session.State switch
        {
            CTraderConnectionState.Connecting => "trade-state-connecting",
            CTraderConnectionState.Connected => "trade-state-connected",
            CTraderConnectionState.Failed => "trade-state-failed",
            _ => "trade-state-disconnected"
        };

        protected override void OnInitialized() => Session.Changed += OnSessionChanged;

        private void LogIn()
        {
            if (Session.BuildAuthorizationUri() is not { } authUri) return;

            Navigation.NavigateTo(authUri.ToString(), forceLoad: true);
        }

        private Task LogOutAsync() => Session.LogoutAsync();

        private void OnSessionChanged() => InvokeAsync(StateHasChanged);

        public void Dispose() => Session.Changed -= OnSessionChanged;
    }
}
