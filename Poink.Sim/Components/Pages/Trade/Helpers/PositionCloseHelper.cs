using System;
using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;

namespace Poink.Sim.Components.Pages.Trade.Helpers
{
	/// <summary>Decides which positions the close helpers (breakeven watch, auto-arm, time stop) should close now.</summary>
	public static class PositionCloseHelper
	{
		public const int MinSeconds = 1;

		public const int MaxSeconds = 86_400;

		public static readonly CloseAutomationSettings DefaultAutomation = new(false, 30, false, 60);

		public static int ClampSeconds(int seconds) => Math.Clamp(seconds, MinSeconds, MaxSeconds);

		/// <summary>
		/// A long closes against the bid and a short against the ask (as cTrader marks them), so each is at breakeven or
		/// better once that price is back to its entry.
		/// </summary>
		public static bool IsAtOrPastEntry(CTraderPositionInfo position, double bid, double ask) =>
			position.IsLong ? bid >= position.EntryPrice : ask <= position.EntryPrice;

		public static IReadOnlyList<CTraderPositionInfo> SidePositions(IEnumerable<CTraderPositionInfo> positions, BreakevenSide side) =>
			positions.Where(p => p.SymbolId == side.SymbolId && p.IsLong == side.IsLong).ToList();

		/// <summary>
		/// The time stop wins once a position is t + s old; otherwise it closes at breakeven or better when its side is armed
		/// or auto-arm has reached t. <paramref name="automation"/> is null for a symbol the automation does not manage, and a
		/// missing quote only skips the breakeven check.
		/// </summary>
		public static PositionCloseReason Decide(
			CTraderPositionInfo position,
			DateTime nowUtc,
			bool sideArmed,
			CloseAutomationSettings? automation,
			double? bid,
			double? ask
		)
		{
			var ageSeconds = (nowUtc - position.OpenTimeUtc).TotalSeconds;
			if (automation is { AutoClose: true } timed && ageSeconds >= timed.TimeStopSeconds)
				return PositionCloseReason.TimeStop;

			var watched = sideArmed || (automation is { AutoArm: true } armed && ageSeconds >= armed.ArmAfterSeconds);
			return watched && bid is { } b && ask is { } a && IsAtOrPastEntry(position, b, a)
				? PositionCloseReason.Breakeven
				: PositionCloseReason.None;
		}

		/// <summary>A position opened at <paramref name="openedUtc"/> on its way through a <paramref name="totalSeconds"/> delay.</summary>
		public static CloseCountdown Countdown(DateTime openedUtc, DateTime nowUtc, int totalSeconds) =>
			new(Math.Clamp((nowUtc - openedUtc).TotalSeconds, 0, totalSeconds), totalSeconds);

		public static IReadOnlyList<BreakevenArmedEntry> ToEntries(long accountId, IEnumerable<BreakevenSide> sides) =>
			sides.Select(s => new BreakevenArmedEntry(accountId, s.SymbolId, s.IsLong)).ToList();

		/// <summary>The stored sides to re-arm: only those armed on <paramref name="accountId"/>, so a switched account is never touched.</summary>
		public static IReadOnlyList<BreakevenSide> SidesToRearm(IEnumerable<BreakevenArmedEntry>? entries, long accountId) =>
			(entries ?? []).Where(e => e.AccountId == accountId).Select(e => new BreakevenSide(e.SymbolId, e.IsLong)).Distinct().ToList();
	}
}
