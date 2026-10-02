using System;
using System.Collections.Generic;

namespace Poink.Sim.Bots;

/// <summary>
/// Holds each bot back from opening again right after a close: for a cooldown of simulated seconds from the
/// close, and, when re-arm is required, until that side's open conditions have stopped matching at least once.
/// A close is noticed from the account's trade log, so stop loss, take profit, stop-out and manual closes all count.
/// </summary>
internal sealed class BotReentryGuard
{
	private sealed class BotState
	{
		public int SeenTradeCount;
		public DateTime? LastExitTime;
		public bool LongArmed = true;
		public bool ShortArmed = true;
	}

	private readonly Dictionary<int, BotState> _states = new();

	public void Reset() => _states.Clear();

	public void Observe(Bot bot)
	{
		var state = StateFor(bot);
		var trades = bot.Account.TradeLog;
		if (trades.Count == state.SeenTradeCount)
			return;

		state.SeenTradeCount = trades.Count;
		state.LastExitTime = trades.Count > 0 ? trades[^1].ExitTime : null;
		state.LongArmed = false;
		state.ShortArmed = false;
	}

	public bool IsCoolingDown(Bot bot, DateTime now, double cooldownSeconds) =>
		cooldownSeconds > 0
		&& StateFor(bot).LastExitTime is { } exit
		&& now >= exit
		&& (now - exit).TotalSeconds < cooldownSeconds;

	public void Rearm(Bot bot, bool openLongMatches, bool openShortMatches)
	{
		var state = StateFor(bot);
		state.LongArmed |= !openLongMatches;
		state.ShortArmed |= !openShortMatches;
	}

	public bool IsArmed(Bot bot, bool isLong, bool requireRearm)
	{
		if (!requireRearm)
			return true;
		var state = StateFor(bot);
		return isLong ? state.LongArmed : state.ShortArmed;
	}

	private BotState StateFor(Bot bot)
	{
		if (!_states.TryGetValue(bot.Id, out var state))
		{
			state = new BotState();
			_states[bot.Id] = state;
		}
		return state;
	}
}
