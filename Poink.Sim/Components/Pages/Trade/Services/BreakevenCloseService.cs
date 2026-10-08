using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Services.MarketData;

namespace Poink.Sim.Components.Pages.Trade
{
	internal sealed class BreakevenCloseService : IBreakevenCloseService, IDisposable
	{
		private readonly ITickStreamAccess _stream;
		private readonly ITradeExecutionAccess _execution;
		private readonly IAccountAccess _account;
		private readonly ILogger<BreakevenCloseService> _logger;
		private long? _accountId;
		private readonly object _gate = new();
		private readonly HashSet<BreakevenSide> _armed = new();

		// A requested close stays here until cTrader drops the position, so later ticks never request it twice.
		private readonly HashSet<long> _closingPositionIds = new();

		public BreakevenCloseService(
			ITickStreamAccess stream,
			ITradeExecutionAccess execution,
			IAccountAccess account,
			ILogger<BreakevenCloseService> logger
		)
		{
			_stream = stream;
			_execution = execution;
			_account = account;
			_logger = logger;
			_accountId = account.SelectedAccountId;
			_stream.TickReceived += OnTickReceived;
			_account.Changed += OnAccountChanged;
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

			if (_stream.Quotes.FirstOrDefault(q => q.SymbolId == side.SymbolId) is { Bid: { } bid, Ask: { } ask })
				Evaluate(side, bid, ask);
		}

		public void Disarm(BreakevenSide side) => DisarmBecause(side, "turned off");

		private void OnTickReceived(MarketTick tick)
		{
			List<BreakevenSide> sides;
			lock (_gate)
			{
				if (_armed.Count == 0)
					return;
				sides = _armed.Where(s => s.SymbolId == tick.SymbolId).ToList();
			}

			foreach (var side in sides)
				Evaluate(side, tick.Bid, tick.Ask);
		}

		private void Evaluate(BreakevenSide side, double bid, double ask)
		{
			var positions = _stream.Positions;
			var sidePositions = BreakevenCloseHelper.SidePositions(positions, side);
			IReadOnlyList<CTraderPositionInfo> due;
			lock (_gate)
			{
				if (!_armed.Contains(side))
					return;

				var openIds = positions.Select(p => p.PositionId).ToHashSet();
				_closingPositionIds.RemoveWhere(id => !openIds.Contains(id));
				due = BreakevenCloseHelper.DuePositions(sidePositions, side, bid, ask, _closingPositionIds);
				foreach (var position in due)
					_closingPositionIds.Add(position.PositionId);
			}

			if (sidePositions.Count == 0)
			{
				DisarmBecause(side, "no positions left");
				return;
			}

			foreach (var position in due)
				_ = CloseAsync(side, position, bid, ask);
		}

		private async Task CloseAsync(BreakevenSide side, CTraderPositionInfo position, double bid, double ask)
		{
			_logger.LogInformation(
				"Breakeven close requested for {Side} position {PositionId} on {Symbol}: entry {Entry}, bid {Bid}, ask {Ask}",
				side.Name,
				position.PositionId,
				position.SymbolName,
				position.EntryPrice,
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
				_logger.LogError(ex, "Breakeven close of {Side} position {PositionId} threw", side.Name, position.PositionId);
				result = new CTraderOrderResult(false, ex.Message);
			}

			if (result.Success)
			{
				CloseReported?.Invoke(
					new CTraderOrderResult(true, $"Closed {side.Name} position {position.PositionId} at breakeven or better.")
				);
				return;
			}

			lock (_gate)
				_closingPositionIds.Remove(position.PositionId);
			_logger.LogWarning(
				"Breakeven close of {Side} position {PositionId} failed: {Message}",
				side.Name,
				position.PositionId,
				result.Message
			);
			DisarmBecause(side, "a close failed");
			CloseReported?.Invoke(
				new CTraderOrderResult(
					false,
					$"Breakeven close of {side.Name} position {position.PositionId} failed ({result.Message}); breakeven close is off for {side.Name}s."
				)
			);
		}

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
			_stream.TickReceived -= OnTickReceived;
			_account.Changed -= OnAccountChanged;
			lock (_gate)
				_armed.Clear();
		}
	}
}
