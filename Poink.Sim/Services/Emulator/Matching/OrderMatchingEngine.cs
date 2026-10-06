using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Emulator.Matching;

/// <summary>
/// Price-time priority limit order book on a whole-tick grid. An incoming order fills against the best opposite levels
/// at their resting prices, oldest first, and any remainder rests. An incoming order never fills against its own bot:
/// the bot's resting order is cancelled instead, so the book can never cross.
/// </summary>
internal sealed class OrderMatchingEngine
{
	private readonly SortedDictionary<long, LinkedList<RestingOrder>> _bids = new(Comparer<long>.Create((a, b) => b.CompareTo(a)));
	private readonly SortedDictionary<long, LinkedList<RestingOrder>> _asks = new();
	private readonly Dictionary<long, LinkedListNode<RestingOrder>> _ordersById = new();
	private readonly Dictionary<int, HashSet<long>> _orderIdsByBot = new();
	private long _nextOrderId = 1;

	public int OrderCount => _ordersById.Count;

	public long? BestBidTicks => _bids.Count > 0 ? _bids.Keys.First() : null;

	public long? BestAskTicks => _asks.Count > 0 ? _asks.Keys.First() : null;

	/// <summary>Matches the order against the book; the remainder rests unless <paramref name="immediateOrCancel"/>.</summary>
	public (RestingOrder? Rested, IReadOnlyList<EmulatorTrade> Trades) Submit(OrderSubmission submission, bool immediateOrCancel = false)
	{
		var trades = new List<EmulatorTrade>();
		var remaining = Match(submission, trades);
		if (remaining == 0 || immediateOrCancel)
		{
			return (null, trades);
		}

		var order = new RestingOrder
		{
			Id = _nextOrderId++,
			BotId = submission.BotId,
			Side = submission.Side,
			PriceTicks = submission.PriceTicks,
			Remaining = remaining,
		};
		Rest(order);
		return (order, trades);
	}

	public bool Cancel(long orderId)
	{
		if (!_ordersById.TryGetValue(orderId, out var node))
		{
			return false;
		}

		Remove(node);
		return true;
	}

	public void CancelAll(int botId)
	{
		foreach (var orderId in OrderIdsOf(botId).ToArray())
		{
			Cancel(orderId);
		}
	}

	public RestingOrder? Find(long orderId) => _ordersById.TryGetValue(orderId, out var node) ? node.Value : null;

	public IReadOnlyCollection<long> OrderIdsOf(int botId) =>
		_orderIdsByBot.TryGetValue(botId, out var ids) ? ids : Array.Empty<long>();

	public IEnumerable<RestingOrder> OrdersOf(int botId) => OrderIdsOf(botId).Select(id => _ordersById[id].Value);

	/// <summary>The best <paramref name="depth"/> levels of one side, best first, with the orders queued at each.</summary>
	public IEnumerable<(long PriceTicks, IReadOnlyCollection<RestingOrder> Orders)> Levels(OrderSide side, int depth) =>
		SideOf(side).Take(depth).Select(level => (level.Key, (IReadOnlyCollection<RestingOrder>)level.Value));

	private long Match(OrderSubmission submission, List<EmulatorTrade> trades)
	{
		var remaining = submission.Volume;
		var opposite = SideOf(Opposite(submission.Side));
		while (remaining > 0 && opposite.Count > 0)
		{
			var (levelTicks, queue) = opposite.First();
			if (!Crosses(submission, levelTicks))
			{
				break;
			}

			var node = queue.First!;
			var resting = node.Value;
			if (resting.BotId == submission.BotId)
			{
				Remove(node);
				continue;
			}

			var fill = Math.Min(remaining, resting.Remaining);
			resting.Remaining -= fill;
			remaining -= fill;
			trades.Add(
				submission.Side == OrderSide.Buy
					? new EmulatorTrade(levelTicks, fill, submission.BotId, resting.BotId)
					: new EmulatorTrade(levelTicks, fill, resting.BotId, submission.BotId)
			);
			if (resting.Remaining == 0)
			{
				Remove(node);
			}
		}

		return remaining;
	}

	private static bool Crosses(OrderSubmission submission, long levelTicks) =>
		submission.Side == OrderSide.Buy ? levelTicks <= submission.PriceTicks : levelTicks >= submission.PriceTicks;

	private void Rest(RestingOrder order)
	{
		var side = SideOf(order.Side);
		if (!side.TryGetValue(order.PriceTicks, out var queue))
		{
			queue = new LinkedList<RestingOrder>();
			side.Add(order.PriceTicks, queue);
		}

		_ordersById[order.Id] = queue.AddLast(order);
		if (!_orderIdsByBot.TryGetValue(order.BotId, out var ids))
		{
			ids = new HashSet<long>();
			_orderIdsByBot[order.BotId] = ids;
		}
		ids.Add(order.Id);
	}

	private void Remove(LinkedListNode<RestingOrder> node)
	{
		var order = node.Value;
		var queue = node.List!;
		queue.Remove(node);
		if (queue.Count == 0)
		{
			SideOf(order.Side).Remove(order.PriceTicks);
		}

		_ordersById.Remove(order.Id);
		if (_orderIdsByBot.TryGetValue(order.BotId, out var ids))
		{
			ids.Remove(order.Id);
			if (ids.Count == 0)
			{
				_orderIdsByBot.Remove(order.BotId);
			}
		}
	}

	private SortedDictionary<long, LinkedList<RestingOrder>> SideOf(OrderSide side) => side == OrderSide.Buy ? _bids : _asks;

	private static OrderSide Opposite(OrderSide side) => side == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
}
