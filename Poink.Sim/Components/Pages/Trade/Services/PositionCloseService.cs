using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Components.Pages.Trade
{
	internal sealed class PositionCloseService : IPositionCloseService, IDisposable
	{
		/// <summary>How often the time stop is checked when no tick arrives to check it.</summary>
		private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(1);

		private readonly ITickStreamAccess _stream;
		private readonly ITradeExecutionAccess _execution;
		private readonly IAccountAccess _account;
		private readonly TimeProvider _time;
		private readonly ILogger<PositionCloseService> _logger;
		private readonly ITimer _sweepTimer;
		private readonly object _gate = new();
		private readonly HashSet<BreakevenSide> _armed = new();

		// A requested close stays here until cTrader drops the position, so later checks never request it twice.
		private readonly HashSet<long> _closingPositionIds = new();

		private readonly HashSet<long> _failedPositionIds = new();
		private CloseAutomationScope? _automation;
		private long? _accountId;

		public PositionCloseService(
			ITickStreamAccess stream,
			ITradeExecutionAccess execution,
			IAccountAccess account,
			TimeProvider time,
			ILogger<PositionCloseService> logger
		)
		{
			_stream = stream;
			_execution = execution;
			_account = account;
			_time = time;
			_logger = logger;
			_accountId = account.SelectedAccountId;
			_stream.TickReceived += OnTickReceived;
			_account.Changed += OnAccountChanged;
			_sweepTimer = time.CreateTimer(_ => Sweep(), null, SweepInterval, SweepInterval);
		}

		public event Action? Changed;

		public event Action<CTraderOrderResult>? CloseReported;

		public IReadOnlyList<BreakevenSide> ArmedSides
		{
			get
			{
				lock (_gate)
					return _armed.ToList();
			}
		}

		public bool IsArmed(BreakevenSide side)
		{
			lock (_gate)
				return _armed.Contains(side);
		}

		public void Arm(BreakevenSide side)
		{
			lock (_gate)
			{
				if (!_armed.Add(side))
					return;
			}

			_logger.LogInformation(
				"Breakeven close armed for {Side} positions on symbol {SymbolId}",
				side.Name,
				side.SymbolId
			);
			Changed?.Invoke();
			EvaluateWithLatestQuote(side.SymbolId);
		}

		public void Disarm(BreakevenSide side) => DisarmBecause(side, "turned off");

		public void SetAutomation(CloseAutomationScope? scope)
		{
			lock (_gate)
			{
				if (_automation == scope)
					return;
				_automation = scope;
			}

			if (scope is { Settings: var s })
				_logger.LogInformation(
					"Close automation for symbol {SymbolId}: auto-arm {AutoArm} after {ArmAfterSeconds}s, time stop {AutoClose} after {TimeStopSeconds}s",
					scope.SymbolId,
					s.AutoArm ? "on" : "off",
					s.ArmAfterSeconds,
					s.AutoClose ? "on" : "off",
					s.TimeStopSeconds
				);
			else
				_logger.LogInformation("Close automation turned off");

			Sweep();
		}

		/// <summary>Checks the automated symbol on the clock, so a time stop fires even while no ticks arrive.</summary>
		internal void Sweep()
		{
			long? symbolId;
			lock (_gate)
				symbolId = _automation?.SymbolId;
			if (symbolId is { } id)
				EvaluateWithLatestQuote(id);
		}

		private void EvaluateWithLatestQuote(long symbolId)
		{
			var quote = _stream.Quotes.FirstOrDefault(q => q.SymbolId == symbolId);
			Evaluate(symbolId, quote?.Bid, quote?.Ask);
		}

		private void OnTickReceived(MarketTick tick) => Evaluate(tick.SymbolId, tick.Bid, tick.Ask);

		private void Evaluate(long symbolId, double? bid, double? ask)
		{
			var positions = _stream.Positions;
			var nowUtc = _time.GetUtcNow().UtcDateTime;
			var due = new List<(CTraderPositionInfo Position, PositionCloseReason Reason)>();
			var emptied = new List<BreakevenSide>();
			lock (_gate)
			{
				var automation = _automation is { } scope && scope.SymbolId == symbolId ? scope.Settings : null;
				var armedHere = _armed.Where(s => s.SymbolId == symbolId).ToList();
				if (automation is null && armedHere.Count == 0)
					return;

				var openIds = positions.Select(p => p.PositionId).ToHashSet();
				_closingPositionIds.RemoveWhere(id => !openIds.Contains(id));
				_failedPositionIds.RemoveWhere(id => !openIds.Contains(id));
				emptied.AddRange(armedHere.Where(side => PositionCloseHelper.SidePositions(positions, side).Count == 0));

				foreach (var position in positions.Where(p => p.SymbolId == symbolId))
				{
					if (_closingPositionIds.Contains(position.PositionId) || _failedPositionIds.Contains(position.PositionId))
						continue;

					var sideArmed = armedHere.Contains(new BreakevenSide(symbolId, position.IsLong));
					var reason = PositionCloseHelper.Decide(position, nowUtc, sideArmed, automation, bid, ask);
					if (reason == PositionCloseReason.None)
						continue;

					_closingPositionIds.Add(position.PositionId);
					due.Add((position, reason));
				}
			}

			foreach (var side in emptied)
				DisarmBecause(side, "no positions left");
			foreach (var (position, reason) in due)
				_ = CloseAsync(position, reason, bid, ask);
		}

		private async Task CloseAsync(CTraderPositionInfo position, PositionCloseReason reason, double? bid, double? ask)
		{
			var side = new BreakevenSide(position.SymbolId, position.IsLong);
			_logger.LogInformation(
				"{Reason} close requested for {Side} position {PositionId} on {Symbol}: entry {Entry}, opened {OpenTime:u}, bid {Bid}, ask {Ask}",
				reason,
				side.Name,
				position.PositionId,
				position.SymbolName,
				position.EntryPrice,
				position.OpenTimeUtc,
				bid,
				ask
			);

			CTraderOrderResult result;
			try
			{
				result = await _execution.ClosePositionAsync(position.PositionId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "{Reason} close of {Side} position {PositionId} threw", reason, side.Name, position.PositionId);
				result = new CTraderOrderResult(false, ex.Message);
			}

			if (result.Success)
			{
				CloseReported?.Invoke(new CTraderOrderResult(true, SuccessMessage(side, position, reason)));
				return;
			}

			lock (_gate)
			{
				_closingPositionIds.Remove(position.PositionId);
				_failedPositionIds.Add(position.PositionId);
			}
			_logger.LogWarning(
				"{Reason} close of {Side} position {PositionId} failed: {Message}",
				reason,
				side.Name,
				position.PositionId,
				result.Message
			);
			DisarmBecause(side, "a close failed");
			CloseReported?.Invoke(
				new CTraderOrderResult(
					false,
					$"Close of {side.Name} position {position.PositionId} failed ({result.Message}); the close helpers leave it open."
				)
			);
		}

		private static string SuccessMessage(BreakevenSide side, CTraderPositionInfo position, PositionCloseReason reason) =>
			reason == PositionCloseReason.TimeStop
				? $"Closed {side.Name} position {position.PositionId} at its time stop."
				: $"Closed {side.Name} position {position.PositionId} at breakeven or better.";

		private void DisarmBecause(BreakevenSide side, string reason)
		{
			lock (_gate)
			{
				if (!_armed.Remove(side))
					return;
			}

			_logger.LogInformation(
				"Breakeven close disarmed for {Side} positions on symbol {SymbolId}: {Reason}",
				side.Name,
				side.SymbolId,
				reason
			);
			Changed?.Invoke();
		}

		private void OnAccountChanged()
		{
			var accountId = _account.SelectedAccountId;
			List<BreakevenSide> dropped;
			lock (_gate)
			{
				if (accountId == _accountId)
					return;
				_accountId = accountId;
				dropped = _armed.ToList();
				_armed.Clear();
				_closingPositionIds.Clear();
				_failedPositionIds.Clear();
			}

			if (dropped.Count == 0)
				return;

			_logger.LogInformation(
				"Breakeven close disarmed on {SideCount} side(s): the selected account changed to {AccountId}",
				dropped.Count,
				accountId
			);
			Changed?.Invoke();
		}

		public void Dispose()
		{
			_sweepTimer.Dispose();
			_stream.TickReceived -= OnTickReceived;
			_account.Changed -= OnAccountChanged;
			lock (_gate)
				_armed.Clear();
		}
	}
}
