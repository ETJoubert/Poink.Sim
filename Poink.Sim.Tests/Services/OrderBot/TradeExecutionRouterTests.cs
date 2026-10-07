using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.OrderBot;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Services.OrderBot;

public class TradeExecutionRouterTests
{
	private const long LiveAccountId = 1;
	private const long LiveLogin = 19646985;
	private const long DemoAccountId = 2;
	private const long SymbolId = 1107;

	private readonly FakeTradePath _openApi = new();
	private readonly FakeTradePath _bot = new();
	private readonly FakeTradingSession _accounts = new()
	{
		Accounts =
		[
			new CTraderAccountInfo(LiveAccountId, LiveLogin, true),
			new CTraderAccountInfo(DemoAccountId, 48626334, false),
		],
		Symbols = [FakeTradingSession.Symbol(SymbolId, "#USNDAQ100")],
	};

	private TradeExecutionRouter CreateRouter(bool routeLiveOrders = true) =>
		new(
			_openApi,
			_bot,
			_accounts,
			_accounts,
			Options.Create(new OrderBotOptions { RouteLiveOrders = routeLiveOrders })
		);

	private static Task<CTraderOrderResult> PlaceMarketBuy(TradeExecutionRouter router, long symbolId = SymbolId) =>
		router.PlaceOrderAsync(symbolId, true, 0.01, CTraderOrderType.Market, null, null, null);

	[Fact]
	public async Task PlaceOrder_LiveAccount_GoesToTheBotWithLoginAndSymbolName()
	{
		_accounts.SelectedAccountId = LiveAccountId;

		await PlaceMarketBuy(CreateRouter());

		Assert.Equal($"bot:place:{LiveLogin}:#USNDAQ100", Assert.Single(_bot.Calls));
		Assert.Empty(_openApi.Calls);
	}

	[Fact]
	public async Task PlaceOrder_DemoAccount_StaysOnTheOpenApi()
	{
		_accounts.SelectedAccountId = DemoAccountId;

		await PlaceMarketBuy(CreateRouter());

		Assert.Equal($"api:place:{SymbolId}", Assert.Single(_openApi.Calls));
		Assert.Empty(_bot.Calls);
	}

	[Fact]
	public async Task PlaceOrder_LiveRoutingDisabled_StaysOnTheOpenApi()
	{
		_accounts.SelectedAccountId = LiveAccountId;

		await PlaceMarketBuy(CreateRouter(routeLiveOrders: false));

		Assert.Single(_openApi.Calls);
		Assert.Empty(_bot.Calls);
	}

	[Fact]
	public async Task PlaceOrder_LiveAccountUnknownSymbol_FailsWithoutTrading()
	{
		_accounts.SelectedAccountId = LiveAccountId;

		var result = await PlaceMarketBuy(CreateRouter(), symbolId: 9999);

		Assert.False(result.Success);
		Assert.Empty(_bot.Calls);
		Assert.Empty(_openApi.Calls);
	}

	[Fact]
	public async Task ClosePosition_LiveAccount_GoesToTheBot()
	{
		_accounts.SelectedAccountId = LiveAccountId;

		await CreateRouter().ClosePositionAsync(421447512);

		Assert.Equal($"bot:close-position:{LiveLogin}:421447512", Assert.Single(_bot.Calls));
	}

	[Fact]
	public async Task CloseSymbol_LiveAccount_GoesToTheBotBySymbolName()
	{
		_accounts.SelectedAccountId = LiveAccountId;

		await CreateRouter().CloseSymbolAsync(SymbolId);

		Assert.Equal($"bot:close-symbol:{LiveLogin}:#USNDAQ100", Assert.Single(_bot.Calls));
	}

	[Fact]
	public void UseLiveEnvironment_ReflectsTheOpenApiSession()
	{
		_openApi.UseLiveEnvironment = true;

		Assert.True(CreateRouter().UseLiveEnvironment);
	}
}
