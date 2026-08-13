using Microsoft.AspNetCore.Components;
using Poink.Shared.Client.Components.Pages.CTrader;
using Poink.Sim.Services.CTrader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Poink.Sim.Components.Pages.Trade
{
    public partial class TradePage : ComponentBase, IDisposable
    {
        private const int MaxSymbolResults = 100;

        [Inject]
        private ICTraderSessionService Session { get; set; } = null!;

        [Inject]
        private NavigationManager Navigation { get; set; } = null!;

        private string _symbolFilter = string.Empty;

        private long? _chartSymbolId;
        private string? _chartSymbolName;
        private List<CTraderBarRow> _chartBars = new();
        private bool _chartLoading;
        private string? _chartError;

        private bool IsConnected => Session.State == CTraderConnectionState.Connected;

        private CTraderSymbolQuote? ChartQuote =>
            _chartSymbolId is { } id ? Session.Watchlist.FirstOrDefault(q => q.SymbolId == id) : null;

        private IReadOnlyList<CTraderSymbolInfo> FilteredSymbols
        {
            get
            {
                var matches = string.IsNullOrWhiteSpace(_symbolFilter)
                    ? Session.Symbols
                    : Session.Symbols.Where(s => s.SymbolName.Contains(_symbolFilter, StringComparison.OrdinalIgnoreCase));

                return matches.Take(MaxSymbolResults).ToList();
            }
        }

        private int TotalSymbolMatchCount =>
            string.IsNullOrWhiteSpace(_symbolFilter)
                ? Session.Symbols.Count
                : Session.Symbols.Count(s => s.SymbolName.Contains(_symbolFilter, StringComparison.OrdinalIgnoreCase));

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

        private Task WatchAsync(long symbolId) => Session.WatchSymbolAsync(symbolId);

        private Task UnwatchAsync(long symbolId) => Session.UnwatchSymbolAsync(symbolId);

        private async Task OpenChartAsync(long symbolId, string symbolName)
        {
            _chartSymbolId = symbolId;
            _chartSymbolName = symbolName;
            _chartError = null;
            _chartBars = new List<CTraderBarRow>();
            _chartLoading = true;

            try
            {
                var bars = await Session.GetHistoryAsync(symbolId);

                _chartBars = bars
                    .Select(b => new CTraderBarRow(b.TimeUtc, b.Open, b.High, b.Low, b.Close, b.Volume))
                    .ToList();

                if (_chartBars.Count == 0) _chartError = "No history returned for this symbol.";
            }
            catch (Exception ex)
            {
                _chartError = ex.Message;
            }
            finally
            {
                _chartLoading = false;
            }
        }

        private void CloseChart()
        {
            _chartSymbolId = null;
            _chartSymbolName = null;
            _chartBars = new List<CTraderBarRow>();
            _chartError = null;
        }

        private static string Price(double? value) => value?.ToString("N5") ?? "—";

        private void OnSessionChanged() => InvokeAsync(StateHasChanged);

        public void Dispose() => Session.Changed -= OnSessionChanged;
    }
}
