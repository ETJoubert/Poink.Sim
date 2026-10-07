using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.OrderBot;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Connection status badge, account switcher and log in/out for the Trade page's header -- split out so TradePage doesn't own account/session chrome directly.</summary>
	public partial class TradeAccountMenu : ComponentBase, IDisposable
	{
		[Inject]
		private IAccountAccess Session { get; set; } = null!;

		[Inject]
		private NavigationManager Navigation { get; set; } = null!;

		[Inject]
		private IOrderBotStatus BotStatus { get; set; } = null!;

		private bool _accountMenuOpen;

		private bool IsConnected => Session.State == CTraderConnectionState.Connected;

		private CTraderAccountInfo? SelectedAccount => Session.Accounts.FirstOrDefault(a => a.AccountId == Session.SelectedAccountId);

		private string StateLabel =>
			Session.State switch
			{
				CTraderConnectionState.Connecting => "CONNECTING",
				CTraderConnectionState.Connected => "CONNECTED",
				CTraderConnectionState.Failed => "FAILED",
				_ => "DISCONNECTED",
			};

		private string StateClass =>
			Session.State switch
			{
				CTraderConnectionState.Connecting => "trade-state-connecting",
				CTraderConnectionState.Connected => "trade-state-connected",
				CTraderConnectionState.Failed => "trade-state-failed",
				_ => "trade-state-disconnected",
			};

		private string BotStateClass =>
			BotStatus.Status.State == OrderBotConnectionState.Online ? "trade-state-connected" : "trade-state-failed";

		protected override void OnInitialized()
		{
			Session.Changed += HandleSessionChanged;
			BotStatus.Changed += HandleSessionChanged;
		}

		private void HandleSessionChanged() => InvokeAsync(StateHasChanged);

		private bool _switchingAccount;

		private void ToggleAccountMenu() => _accountMenuOpen = !_accountMenuOpen;

		private void CloseAccountMenu() => _accountMenuOpen = false;

		private async Task SelectAccountAsync(long accountId)
		{
			if (accountId == Session.SelectedAccountId || _switchingAccount)
				return;

			_switchingAccount = true;
			_accountMenuOpen = false;
			try
			{
				await Session.SwitchAccountAsync(accountId);
			}
			finally
			{
				_switchingAccount = false;
			}
		}

		private void LogIn()
		{
			if (Session.BuildAuthorizationUri() is not { } authUri)
				return;

			Navigation.NavigateTo(authUri.ToString(), forceLoad: true);
		}

		private async Task LogOutAsync()
		{
			_accountMenuOpen = false;
			await Session.LogoutAsync();
		}

		public void Dispose()
		{
			Session.Changed -= HandleSessionChanged;
			BotStatus.Changed -= HandleSessionChanged;
		}
	}
}
