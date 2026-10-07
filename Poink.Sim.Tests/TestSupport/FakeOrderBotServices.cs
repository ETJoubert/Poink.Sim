using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.OrderBot;

namespace Poink.Sim.Tests.TestSupport;

internal sealed class FakeOrderBotClient : IOrderBotClient
{
	public List<OrderBotCommand> Sent { get; } = [];

	public OrderBotCommandResult NextResult { get; set; } = new(true, "Filled.");

	public OrderBotHealthResult Health { get; set; } = new(null, "Unreachable.");

	public Task<OrderBotCommandResult> SendAsync(OrderBotCommand command, CancellationToken cancellationToken = default)
	{
		Sent.Add(command);
		return Task.FromResult(NextResult);
	}

	public Task<OrderBotHealthResult> GetHealthAsync(CancellationToken cancellationToken = default) =>
		Task.FromResult(Health);
}

/// <summary>Records which trade path a router call took, returning a result tagged with that path.</summary>
internal sealed class FakeTradePath : ITradeExecutionAccess, IOrderBotTradeExecution
{
	public List<string> Calls { get; } = [];

	public bool UseLiveEnvironment { get; set; }

	public Task<CTraderOrderResult> PlaceOrderAsync(
		long symbolId,
		bool isLong,
		double volumeUnits,
		CTraderOrderType orderType,
		double? triggerPrice,
		double? stopLossPrice,
		double? takeProfitPrice,
		CancellationToken cancellationToken = default
	) => Record($"api:place:{symbolId}");

	public Task<CTraderOrderResult> CloseSymbolAsync(long symbolId, CancellationToken cancellationToken = default) =>
		Record($"api:close-symbol:{symbolId}");

	public Task<CTraderOrderResult> ClosePositionAsync(
		long positionId,
		CancellationToken cancellationToken = default
	) => Record($"api:close-position:{positionId}");

	public Task<CTraderOrderResult> PlaceOrderAsync(
		long accountNumber,
		string symbolName,
		bool isLong,
		double volumeUnits,
		CTraderOrderType orderType,
		double? triggerPrice,
		double? stopLossPrice,
		double? takeProfitPrice,
		CancellationToken cancellationToken = default
	) => Record($"bot:place:{accountNumber}:{symbolName}");

	public Task<CTraderOrderResult> ClosePositionAsync(
		long accountNumber,
		long positionId,
		CancellationToken cancellationToken = default
	) => Record($"bot:close-position:{accountNumber}:{positionId}");

	public Task<CTraderOrderResult> CloseSymbolAsync(
		long accountNumber,
		string symbolName,
		CancellationToken cancellationToken = default
	) => Record($"bot:close-symbol:{accountNumber}:{symbolName}");

	private Task<CTraderOrderResult> Record(string call)
	{
		Calls.Add(call);
		return Task.FromResult(new CTraderOrderResult(true, call));
	}
}

internal sealed class FakeTradingSession : IAccountAccess, IWatchlistAccess
{
	public CTraderConnectionState State { get; set; } = CTraderConnectionState.Connected;

	public string? StatusMessage => null;

	public IReadOnlyList<CTraderAccountInfo> Accounts { get; set; } = [];

	public long? SelectedAccountId { get; set; }

	public bool IsConfigured => true;

	public IReadOnlyList<CTraderSymbolInfo> Symbols { get; set; } = [];

	public IReadOnlyList<CTraderSymbolQuote> Watchlist => [];

	public event Action? Changed
	{
		add { }
		remove { }
	}

	public Uri? BuildAuthorizationUri() => null;

	public Task LogoutAsync() => Task.CompletedTask;

	public Task SwitchAccountAsync(long accountId, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public bool IsSymbolWatched(long symbolId) => false;

	public Task WatchSymbolAsync(long symbolId) => Task.CompletedTask;

	public Task UnwatchSymbolAsync(long symbolId) => Task.CompletedTask;

	public static CTraderSymbolInfo Symbol(long id, string name) => new(id, name, true, 0, 0, 0, name, 0);
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
	public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Answers every request through <see cref="Respond"/>, recording each one for assertions.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
	public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = [];

	private Func<HttpRequestMessage, HttpResponseMessage> Respond { get; } = respond;

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken
	)
	{
		var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
		Requests.Add((request, body));
		return Respond(request);
	}
}
