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
	private double _fundCapital;
	private double _absorbedLosses;
	private double _lentAtStepStart;
	private double _committedThisStep;
	private long _tradeCount;
	private long _tradedVolume;
	private long _rejectedOrders;
	private int _nextBotId = 1;
	private double? _tradedHigh;
	private double? _tradedLow;

	public EmulatorBroker(EmulatorSettings settings)
	{
		_settings = settings;
		_lastPriceTicks = Math.Max(1, EmulatorTickHelper.ToTicks(settings.StartPrice, settings.TickSize));
		_fundBalance = _fundCapital = settings.BrokerFund;

		_accounts = new List<EmulatorBotAccount>();
		_accountsById = new Dictionary<int, EmulatorBotAccount>();
		foreach (var population in settings.Populations)
		{
			AddBots(population, population.BotCount);
		}
	}

	public IReadOnlyList<EmulatorBotAccount> Accounts => _accounts;

	public double LastPrice => EmulatorTickHelper.FromTicks(_lastPriceTicks, _settings.TickSize);

	/// <summary>
	/// Brings the bots in line with the groups in <paramref name="next"/>, matched to <paramref name="previous"/> by
	/// position: renamed groups carry their bots over and larger or new groups get fresh bots with their starting
	/// capital. Fewer groups or bots need a fresh market instead. Returns the bots added.
	/// </summary>
	public IReadOnlyList<EmulatorBotAccount> ApplyGroups(IReadOnlyList<PopulationSettings> previous, IReadOnlyList<PopulationSettings> next)
	{
		var added = new List<EmulatorBotAccount>();
		for (var i = 0; i < next.Count; i++)
		{
			var group = next[i];
			var existing = 0;
			if (i < previous.Count)
			{
				var members = _accounts.Where(bot => bot.Population == previous[i].Name).ToArray();
				foreach (var bot in members)
				{
					bot.MoveToGroup(group.Name);
				}
				existing = members.Length;
			}

			added.AddRange(AddBots(group, group.BotCount - existing));
		}

		return added;
	}

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
	public double Replenish() => _accounts.Sum(ReplenishAccount);

	/// <summary>Pays every bot in one group its starting capital on top of what it has. Returns the points paid in.</summary>
	public double ReplenishGroup(string group) => _accounts.Where(bot => bot.Population == group).Sum(ReplenishAccount);

	/// <summary>Pays one bot its population's starting capital on top of what it has. Returns the points paid in.</summary>
	public double Replenish(int botId) => _accountsById.TryGetValue(botId, out var bot) ? ReplenishAccount(bot) : 0;

	/// <summary>Pays the settings' broker fund into the fund on top of what it has. Returns the points paid in.</summary>
	public double ReplenishFund()
	{
		var amount = Math.Max(0, _settings.BrokerFund);
		_fundBalance += amount;
		_fundCapital += amount;
		return amount;
	}

	/// <summary>The lowest and highest traded prices since the last call, or null when nothing traded.</summary>
	public (double Low, double High)? TakeTradedRange()
	{
		var range = _tradedLow is { } low && _tradedHigh is { } high ? (low, high) : ((double, double)?)null;
		_tradedLow = _tradedHigh = null;
		return range;
	}

	public BotMarketView ViewFor(EmulatorBotAccount bot)
	{
		var group = GroupOf(bot);
		return new BotMarketView(
			LastPrice,
			_book.BestBidTicks is { } bid ? EmulatorTickHelper.FromTicks(bid, _settings.TickSize) : null,
			_book.BestAskTicks is { } ask ? EmulatorTickHelper.FromTicks(ask, _settings.TickSize) : null,
			_settings.TickSize,
			bot.Cash,
			bot.Position,
			bot.Equity(LastPrice),
			Capacity(bot, OrderSide.Buy, LastPrice),
			Capacity(bot, OrderSide.Sell, LastPrice),
			_book.OrderIdsOf(bot.Id).ToArray(),
			group?.BuyChance ?? 0,
			group?.SellChance ?? 0,
			group?.CloseChance ?? 0
		);
	}

	/// <summary>Closes the bot's whole position at market against the book; resting orders stay.</summary>
	public OrderOutcome ClosePosition(EmulatorBotAccount bot)
	{
		if (bot.IsBust || bot.Position == 0)
		{
			return OrderOutcome.RejectedInvalid;
		}

		CloseAtMarket(bot);
		return OrderOutcome.Accepted;
	}

	public OrderOutcome Execute(EmulatorBotAccount bot, BotDecision decision) =>
		decision switch
		{
			PlaceOrderDecision place => Place(bot, place.Side, place.Price, place.Volume),
			CancelOrderDecision cancel => Cancel(bot, cancel.OrderId) ? OrderOutcome.Accepted : OrderOutcome.RejectedInvalid,
			ClosePositionDecision => ClosePosition(bot),
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

	/// <summary>Bots holding a position, grouped by the tick nearest their average entry price.</summary>
	public IReadOnlyList<PositionLevel> PositionLevels() =>
		_accounts
			.Where(bot => !bot.IsBust && bot.Position != 0)
			.GroupBy(bot => EmulatorTickHelper.ToTicks(bot.AverageEntryPrice, _settings.TickSize))
			.Select(level => new PositionLevel(
				ToPrice(level.Key),
				level.Count(bot => bot.Position > 0),
				level.Count(bot => bot.Position < 0),
				level.Sum(bot => bot.Equity(LastPrice))
			))
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

	public IReadOnlyDictionary<string, double> EquityByGroup() =>
		_accounts.GroupBy(bot => bot.Population).ToDictionary(group => group.Key, group => group.Sum(bot => bot.Equity(LastPrice)));

	public IReadOnlyList<BotSummary> BotSummaries() =>
		_accounts
			.Select(bot => new BotSummary(
				bot.Id,
				bot.Population,
				bot.Capital,
				bot.Equity(LastPrice),
				bot.Position,
				bot.AverageEntryPrice,
				_book.OrderIdsOf(bot.Id).Count,
				bot.ActivityChance,
				bot.IsBust
			))
			.ToArray();

	public BrokerSummary Summary() =>
		new(
			_fundBalance,
			_fundCapital,
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
		CloseAtMarket(bot);
		bot.StopOuts++;
	}

	private void CloseAtMarket(EmulatorBotAccount bot)
	{
		var side = bot.Position > 0 ? OrderSide.Sell : OrderSide.Buy;
		var priceTicks = side == OrderSide.Sell ? MarketSellPriceTicks : MarketBuyPriceTicks;
		var (_, trades) = _book.Submit(new OrderSubmission(bot.Id, side, priceTicks, Math.Abs(bot.Position)), immediateOrCancel: true);
		Settle(trades);
	}

	private IReadOnlyList<EmulatorBotAccount> AddBots(PopulationSettings group, int count)
	{
		var bots = Enumerable
			.Range(0, Math.Max(0, count))
			.Select(_ => new EmulatorBotAccount(_nextBotId++, group.Name, group.StartingPoints))
			.ToArray();
		foreach (var bot in bots)
		{
			_accounts.Add(bot);
			_accountsById.Add(bot.Id, bot);
		}

		return bots;
	}

	private PopulationSettings? GroupOf(EmulatorBotAccount bot) =>
		_settings.Populations.FirstOrDefault(population => population.Name == bot.Population);

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
			_tradedLow = Math.Min(_tradedLow ?? price, price);
			_tradedHigh = Math.Max(_tradedHigh ?? price, price);
			_tradeCount++;
			_tradedVolume += trade.Volume;
		}
	}

	private double ReplenishAccount(EmulatorBotAccount bot)
	{
		var capital = _settings.Populations.FirstOrDefault(p => p.Name == bot.Population)?.StartingPoints ?? 0;
		if (capital <= 0)
		{
			return 0;
		}

		bot.Deposit(capital);
		return capital;
	}

	private double ToPrice(long priceTicks) => EmulatorTickHelper.FromTicks(priceTicks, _settings.TickSize);
}
