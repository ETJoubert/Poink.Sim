using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Emulator.Bots;
using Poink.Sim.Emulator.Matching;
using Poink.Sim.Helpers;

namespace Poink.Sim.Emulator.Broker;

/// <summary>
/// The brokerage: holds every bot's account, checks each order against the bot's margin and the broker fund, routes it to
/// the order book, settles fills into the accounts and stops out bots whose margin level falls too low. The last fill
/// sets the price. Margin is worst-case: a bot's position plus all its resting orders on one side, at most
/// equity × leverage.
/// </summary>
internal sealed class EmulatorBroker
{
	private const long MarketBuyPriceTicks = long.MaxValue / 4;
	private const long MarketSellPriceTicks = 1;

	private EmulatorSettings _settings;
	private readonly OrderMatchingEngine _book = new();
	private readonly List<EmulatorBotAccount> _accounts;
	private readonly Dictionary<int, EmulatorBotAccount> _accountsById;

	private long _lastPriceTicks;
	private double _fundBalance;
	private double _absorbedLosses;
	private double _lentAtStepStart;
	private double _committedThisStep;
	private long _tradeCount;
	private long _tradedVolume;
	private long _rejectedOrders;

	public EmulatorBroker(EmulatorSettings settings)
	{
		_settings = settings;
		_lastPriceTicks = Math.Max(1, EmulatorTickHelper.ToTicks(settings.StartPrice, settings.TickSize));
		_fundBalance = settings.BrokerFund;

		var nextId = 1;
		_accounts = settings
			.Populations.SelectMany(population =>
				Enumerable
					.Range(0, Math.Max(0, population.BotCount))
					.Select(_ => new EmulatorBotAccount(nextId++, population.Name, population.StartingPoints))
			)
			.ToList();
		_accountsById = _accounts.ToDictionary(account => account.Id);
	}

	public IReadOnlyList<EmulatorBotAccount> Accounts => _accounts;

	public double LastPrice => EmulatorTickHelper.FromTicks(_lastPriceTicks, _settings.TickSize);

	/// <summary>Takes on new terms, such as leverage, for every order and stop-out check from now on.</summary>
	public void UpdateTerms(EmulatorSettings settings) => _settings = settings;

	public void BeginStep()
	{
		_lentAtStepStart = _accounts.Sum(account => account.Borrowed(LastPrice));
		_committedThisStep = 0;
	}

	/// <summary>
	/// Pays every bot its population's starting capital on top of what it has, reviving bust bots. Returns the points
	/// paid in.
	/// </summary>
	public double Replenish()
	{
		var capitalByPopulation = _settings.Populations.ToDictionary(p => p.Name, p => p.StartingPoints);
		var paidIn = 0.0;
		foreach (var bot in _accounts)
		{
			if (capitalByPopulation.TryGetValue(bot.Population, out var capital) && capital > 0)
			{
				bot.Deposit(capital);
				paidIn += capital;
			}
		}

		return paidIn;
	}

	public BotMarketView ViewFor(EmulatorBotAccount bot) =>
		new(
			LastPrice,
			_book.BestBidTicks is { } bid ? EmulatorTickHelper.FromTicks(bid, _settings.TickSize) : null,
			_book.BestAskTicks is { } ask ? EmulatorTickHelper.FromTicks(ask, _settings.TickSize) : null,
			_settings.TickSize,
			bot.Cash,
			bot.Position,
			bot.Equity(LastPrice),
			Capacity(bot, OrderSide.Buy, LastPrice),
			Capacity(bot, OrderSide.Sell, LastPrice),
			_book.OrderIdsOf(bot.Id).ToArray()
		);

	public OrderOutcome Execute(EmulatorBotAccount bot, BotDecision decision) =>
		decision switch
		{
			PlaceOrderDecision place => Place(bot, place.Side, place.Price, place.Volume),
			CancelOrderDecision cancel => Cancel(bot, cancel.OrderId) ? OrderOutcome.Accepted : OrderOutcome.RejectedInvalid,
			_ => OrderOutcome.Accepted,
		};

	public OrderOutcome Place(EmulatorBotAccount bot, OrderSide side, double price, long volume)
	{
		var priceTicks = EmulatorTickHelper.ToTicks(price, _settings.TickSize);
		var outcome = Validate(bot, side, priceTicks, volume);
		if (outcome != OrderOutcome.Accepted)
		{
			_rejectedOrders++;
			return outcome;
		}

		var (_, trades) = _book.Submit(new OrderSubmission(bot.Id, side, priceTicks, volume));
		Settle(trades);
		return OrderOutcome.Accepted;
	}

	public bool Cancel(EmulatorBotAccount bot, long orderId) =>
		_book.Find(orderId) is { } order && order.BotId == bot.Id && _book.Cancel(orderId);

	public void RunStopOuts()
	{
		foreach (var bot in _accounts.Where(account => !account.IsBust))
		{
			if (bot.Position != 0 && MarginLevelPercent(bot) < _settings.StopOutLevelPercent)
			{
				StopOut(bot);
			}

			if (bot.Position == 0 && bot.Cash <= 0)
			{
				AbsorbDeficit(bot);
			}
		}
	}

	public OrderBook Book()
	{
		return new OrderBook(Levels(OrderSide.Buy), Levels(OrderSide.Sell));

		IReadOnlyList<OrderBookLevel> Levels(OrderSide side) =>
			_book
				.Levels(side, _settings.BookDepth)
				.Select(level => new OrderBookLevel(ToPrice(level.PriceTicks), level.Orders.Sum(order => order.Remaining)))
				.ToArray();
	}

	public IReadOnlyList<PopulationLevel> RestingPopulation() =>
		new[] { OrderSide.Buy, OrderSide.Sell }
			.SelectMany(side => _book.Levels(side, _settings.BookDepth))
			.Select(level =>
			{
				var bots = level.Orders.Select(order => order.BotId).Distinct().Select(id => _accountsById[id]).ToArray();
				return new PopulationLevel(ToPrice(level.PriceTicks), bots.Length, bots.Sum(bot => bot.Equity(LastPrice)));
			})
			.ToArray();

	public IReadOnlyList<PopulationSummary> PopulationSummaries() =>
		_accounts
			.GroupBy(account => account.Population)
			.Select(group => new PopulationSummary(
				group.Key,
				group.Count(),
				group.Count(bot => !bot.IsBust),
				group.Sum(bot => bot.Capital),
				group.Sum(bot => bot.Equity(LastPrice)),
				group.Sum(bot => bot.Cash),
				group.Sum(bot => Math.Max(0, bot.Position)),
				group.Sum(bot => Math.Max(0, -bot.Position)),
				group.Sum(bot => _book.OrderIdsOf(bot.Id).Count),
				group.Sum(bot => bot.Fills),
				group.Sum(bot => bot.StopOuts)
			))
			.ToArray();

	public IReadOnlyList<BotSummary> BotSummaries() =>
		_accounts
			.Select(bot => new BotSummary(
				bot.Id,
				bot.Population,
				bot.Capital,
				bot.Balance,
				bot.Equity(LastPrice),
				bot.Position,
				bot.AverageEntryPrice,
				_book.OrderIdsOf(bot.Id).Count,
				bot.IsBust
			))
			.ToArray();

	public BrokerSummary Summary() =>
		new(
			_fundBalance,
			_accounts.Sum(account => account.Borrowed(LastPrice)),
			_absorbedLosses,
			_settings.Leverage,
			_tradeCount,
			_tradedVolume,
			_rejectedOrders,
			_book.OrderCount
		);

	private OrderOutcome Validate(EmulatorBotAccount bot, OrderSide side, long priceTicks, long volume)
	{
		if (bot.IsBust || volume <= 0 || priceTicks <= 0)
		{
			return OrderOutcome.RejectedInvalid;
		}

		var referencePrice = side == OrderSide.Buy ? Math.Max(LastPrice, ToPrice(priceTicks)) : LastPrice;
		if (volume > Capacity(bot, side, referencePrice))
		{
			return OrderOutcome.RejectedMargin;
		}

		var borrowNeeded = BorrowNeeded(bot, side, volume, referencePrice);
		if (borrowNeeded > _fundBalance - _lentAtStepStart - _committedThisStep)
		{
			return OrderOutcome.RejectedFund;
		}

		_committedThisStep += borrowNeeded;
		return OrderOutcome.Accepted;
	}

	private long Capacity(EmulatorBotAccount bot, OrderSide side, double referencePrice)
	{
		var equity = bot.Equity(LastPrice);
		if (bot.IsBust || equity <= 0 || referencePrice <= 0)
		{
			return 0;
		}

		var maxUnits = equity * _settings.Leverage / referencePrice;
		var worstCaseUnits =
			side == OrderSide.Buy ? bot.Position + OpenVolume(bot, OrderSide.Buy) : OpenVolume(bot, OrderSide.Sell) - bot.Position;
		return Math.Max(0, (long)Math.Floor(maxUnits - worstCaseUnits));
	}

	private double BorrowNeeded(EmulatorBotAccount bot, OrderSide side, long volume, double referencePrice)
	{
		if (side == OrderSide.Buy)
		{
			var openBuyNotional = _book.OrdersOf(bot.Id).Where(o => o.Side == OrderSide.Buy).Sum(o => o.Remaining * ToPrice(o.PriceTicks));
			return Math.Max(0, volume * referencePrice - Math.Max(0, bot.Cash - openBuyNotional));
		}

		var freeUnits = Math.Max(0, bot.Position - OpenVolume(bot, OrderSide.Sell));
		return Math.Max(0, volume - freeUnits) * referencePrice;
	}

	private long OpenVolume(EmulatorBotAccount bot, OrderSide side) =>
		_book.OrdersOf(bot.Id).Where(order => order.Side == side).Sum(order => order.Remaining);

	private double MarginLevelPercent(EmulatorBotAccount bot)
	{
		var usedMargin = Math.Abs(bot.Position) * LastPrice / _settings.Leverage;
		return usedMargin <= 0 ? double.PositiveInfinity : bot.Equity(LastPrice) / usedMargin * 100;
	}

	private void StopOut(EmulatorBotAccount bot)
	{
		_book.CancelAll(bot.Id);
		var side = bot.Position > 0 ? OrderSide.Sell : OrderSide.Buy;
		var priceTicks = side == OrderSide.Sell ? MarketSellPriceTicks : MarketBuyPriceTicks;
		var (_, trades) = _book.Submit(new OrderSubmission(bot.Id, side, priceTicks, Math.Abs(bot.Position)), immediateOrCancel: true);
		Settle(trades);
		bot.StopOuts++;
	}

	private void AbsorbDeficit(EmulatorBotAccount bot)
	{
		_book.CancelAll(bot.Id);
		_fundBalance += bot.Cash;
		_absorbedLosses -= bot.Cash;
		bot.GoBust();
	}

	private void Settle(IReadOnlyList<EmulatorTrade> trades)
	{
		foreach (var trade in trades)
		{
			var price = ToPrice(trade.PriceTicks);
			_accountsById[trade.BuyBotId].ApplyFill(OrderSide.Buy, trade.Volume, price);
			_accountsById[trade.SellBotId].ApplyFill(OrderSide.Sell, trade.Volume, price);

			_lastPriceTicks = trade.PriceTicks;
			_tradeCount++;
			_tradedVolume += trade.Volume;
		}
	}

	private double ToPrice(long priceTicks) => EmulatorTickHelper.FromTicks(priceTicks, _settings.TickSize);
}
