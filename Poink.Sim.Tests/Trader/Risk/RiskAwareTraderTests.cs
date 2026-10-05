using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Poink.Sim.Trader.Risk;
using Xunit;

namespace Poink.Sim.Tests.Trader.Risk;

using Poink.Sim.Trader;

public class RiskAwareTraderTests
{
	private static (RiskAwareTrader Wrapped, Trader Core) Wrap(params IRiskPolicy[] policies)
	{
		var core = new Trader(1000);
		var manager = new RiskManager(new PositionManager(core), policies, null);
		return (new RiskAwareTrader(core, manager), core);
	}

	[Fact]
	public void Apply_Denied_ReturnsBlockedExecutionAndLeavesCoreFlat()
	{
		var (trader, core) = Wrap(FakeRiskPolicy.Denying("Gatekeeper"));

		var execution = trader.Apply(new SimulationDecision(SimulationAction.EnterShort, 0.5), 100, 100, TestTickHelper.Origin);

		Assert.True(execution!.Value.Blocked);
		Assert.Equal("Gatekeeper", execution.Value.DeniedByPolicy);
		Assert.Equal(PositionDirection.Short, execution.Value.Direction);
		Assert.Null(core.CurrentPosition);
		Assert.Equal("Gatekeeper", trader.LastDenial!.Value.PolicyName);
	}

	[Fact]
	public void Apply_Modified_ExecutesTheModifiedDecision()
	{
		var (trader, core) = Wrap(FakeRiskPolicy.SettingFraction("Sizer", 0.1));

		trader.Apply(new SimulationDecision(SimulationAction.EnterLong, 1), 100, 100, TestTickHelper.Origin);

		Assert.Equal(1, core.CurrentPosition!.Value.Size, 9);
		Assert.Null(trader.LastDenial);
	}

	[Fact]
	public void Apply_PermitAfterDenial_ClearsLastDenial()
	{
		bool deny = true;
		var policy = new FakeRiskPolicy(
			"Flip",
			decision => deny ? RiskReview.Deny(decision, "no", "Flip") : RiskReview.Permit(decision)
		);
		var (trader, _) = Wrap(policy);
		trader.Apply(new SimulationDecision(SimulationAction.EnterLong, 1), 100, 100, TestTickHelper.Origin);

		deny = false;
		trader.Apply(new SimulationDecision(SimulationAction.EnterLong, 1), 100, 100, TestTickHelper.Origin);

		Assert.Null(trader.LastDenial);
	}

	[Fact]
	public void Advance_InjectedClose_BypassesGateAndClosesPosition()
	{
		var closer = new FakeRiskPolicy(
			"Closer",
			decision => decision.Action == SimulationAction.Close ? RiskReview.Deny(decision, "no closing", "Closer") : RiskReview.Permit(decision),
			context => context.Position is null ? [] : new List<SimulationDecision> { new(SimulationAction.Close) }
		);
		var (trader, core) = Wrap(closer);
		trader.Apply(new SimulationDecision(SimulationAction.EnterLong, 1), 100, 100, TestTickHelper.Origin);

		var result = trader.Advance(101, 101, TestTickHelper.Origin.AddSeconds(1));

		Assert.Equal(SimulationAction.Close, Assert.Single(result.AutomaticFills).Action);
		Assert.Null(core.CurrentPosition);
	}
}
