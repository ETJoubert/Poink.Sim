using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Poink.Sim.Helpers;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Components.Pages
{
	/// <summary>
	/// The rule-driven robot, active only while switched on: when a trader is flat and the Open Long (or Open Short) conditions all match, it
	/// enters using the Positioning options; when a trader holds a position and the Close conditions all
	/// match, it closes. Every action goes through the same path as a manual order, so risk policies still gate it.
	/// </summary>
	public partial class SimulationPage
	{
		[Inject]
		private ITickRepository TickRepository { get; set; } = default!;

		private readonly TxFoldBarsCache _robotTxFoldBars = new();
		private int _robotLastCursor = -1;

		private bool RobotUsesTxFold =>
			RuleEngineHelper.UsesTxFold(_openLongRules.EnabledKeys)
			|| RuleEngineHelper.UsesTxFold(_openShortRules.EnabledKeys)
			|| RuleEngineHelper.UsesTxFold(_closeRules.EnabledKeys);

		private bool RobotHasRules =>
			_openLongRules.EnabledKeys.Count > 0
			|| _openShortRules.EnabledKeys.Count > 0
			|| _closeRules.EnabledKeys.Count > 0;

		// Runs once per simulation step; a long match wins when both open sets match at once.
		private void EvaluateRobot()
		{
			if (_snapshot is not { } s || s.CursorIndex == _robotLastCursor)
				return;
			_robotLastCursor = s.CursorIndex;

			if (!_robotEnabled || !RobotHasRules || s.CurrentPrice is null)
				return;

			if (RobotUsesTxFold)
				_ = RefreshRobotTxFoldAsync(s);

			var rowsByPercent = new Dictionary<double, IReadOnlyList<RuleSignalRow>>();
			bool Matches(RobotRuleSet rules) =>
				RuleEngineHelper.Matches(RobotRows(s, rules.XGridPercent, rowsByPercent), rules.EnabledKeys);

			foreach (var trader in s.Traders)
			{
				if (trader.Position is not null)
				{
					if (Matches(_closeRules))
						SubmitRobotClose(trader);
				}
				else if (trader.PendingOrder is null)
				{
					if (Matches(_openLongRules))
						SubmitRobotEntry(s, trader, isLong: true);
					else if (Matches(_openShortRules))
						SubmitRobotEntry(s, trader, isLong: false);
				}
			}
		}

		private IReadOnlyList<RuleSignalRow> RobotRows(
			SimulationSnapshot s,
			double xgridPercent,
			Dictionary<double, IReadOnlyList<RuleSignalRow>> cache
		)
		{
			if (cache.TryGetValue(xgridPercent, out var cached))
				return cached;

			var rows =
				ComputeRuleEngine(s, xgridPercent) is { } result
					? result.Rows.Concat(RuleEngineHelper.TxFoldRows(_robotTxFoldBars.Bars, result.CurrentPrice)).ToList()
					: new List<RuleSignalRow>();
			cache[xgridPercent] = rows;
			return rows;
		}

		private async Task RefreshRobotTxFoldAsync(SimulationSnapshot s)
		{
			try
			{
				await _robotTxFoldBars.RefreshIfNeededAsync(
					TickRepository,
					s.Config.Table,
					SnapshotAnchorBar(s)?.CloseTime.AddTicks(1),
					s.Config.LoadFrom,
					_txFoldSelectedSeconds
				);
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, "Robot failed to load TxFold bars for table {Table}", s.Config.Table);
			}
		}

		private void SubmitRobotEntry(SimulationSnapshot s, TraderSnapshot trader, bool isLong)
		{
			double? entry = (isLong ? s.CurrentAsk : s.CurrentBid) ?? s.CurrentPrice;
			if (entry is not { } entryPrice)
				return;

			var options = _robotPositionOptions ?? RobotPositionHelper.DefaultOptions;
			if (
				RobotPositionHelper.PlanEntry(options, isLong, entryPrice, trader.Capital.Cash, trader.Leverage, s.Config.PipSize)
				is not { } plan
			)
				return;

			var decision = new SimulationDecision(
				isLong ? SimulationAction.EnterLong : SimulationAction.EnterShort,
				plan.CapitalFraction,
				plan.StopLossPrice,
				plan.TakeProfitPrice,
				Reason: "robot"
			);
			if (SimulationService.SubmitManualDecision(trader.Id, decision, out _))
				Logger.LogInformation("Robot opened {Side} for {Trader}", isLong ? "long" : "short", trader.Label);
		}

		private void SubmitRobotClose(TraderSnapshot trader)
		{
			if (SimulationService.SubmitManualDecision(trader.Id, new SimulationDecision(SimulationAction.Close, Reason: "robot"), out _))
				Logger.LogInformation("Robot closed the position for {Trader}", trader.Label);
		}
	}
}
