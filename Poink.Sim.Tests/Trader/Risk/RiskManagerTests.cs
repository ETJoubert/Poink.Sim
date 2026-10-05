using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Tests.TestSupport;
using Poink.Sim.Trader.Risk;
using Xunit;

namespace Poink.Sim.Tests.Trader.Risk;

using Poink.Sim.Trader;

public class RiskManagerTests
{
	private static readonly SimulationDecision Entry = new(SimulationAction.EnterLong, 0.8);

	private static RiskManager Manager(IReadOnlyList<IRiskPolicy> policies, RiskPolicyToggles? toggles = null) =>
		new(new PositionManager(new Trader(1000)), policies, toggles);

	private static RiskPolicyToggles TogglesFor(params FakeRiskPolicy[] policies)
	{
		var entries = new List<(string, string, IReadOnlyList<RiskPolicyParameter>)>();
		foreach (var policy in policies)
			entries.Add((policy.Name, policy.Description, [new RiskPolicyParameter("Knob", "Knob", 1, 0, 10, 1)]));
		return new RiskPolicyToggles(entries);
	}

	[Fact]
	public void Gate_NoPolicies_Permits()
	{
		var review = Manager([]).Gate(Entry, 100, 100, TestTickHelper.Origin);

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
		Assert.Equal(Entry, review.Decision);
	}

	[Fact]
	public void Gate_FirstDeny_StopsTheChain()
	{
		var denying = FakeRiskPolicy.Denying("A");
		var later = new FakeRiskPolicy("B");

		var review = Manager([denying, later]).Gate(Entry, 100, 100, TestTickHelper.Origin);

		Assert.Equal(RiskVerdict.Deny, review.Verdict);
		Assert.Equal("A", review.PolicyName);
		Assert.Empty(later.Reviewed);
	}

	[Fact]
	public void Gate_Modify_FeedsChangedDecisionToNextPolicy()
	{
		var resizing = FakeRiskPolicy.SettingFraction("A", 0.25);
		var later = new FakeRiskPolicy("B");

		var review = Manager([resizing, later]).Gate(Entry, 100, 100, TestTickHelper.Origin);

		Assert.Equal(RiskVerdict.Modify, review.Verdict);
		Assert.Equal(0.25, review.Decision.CapitalFraction);
		Assert.Equal(0.25, Assert.Single(later.Reviewed).CapitalFraction);
	}

	[Fact]
	public void Gate_DisabledToggle_SkipsPolicy()
	{
		var denying = FakeRiskPolicy.Denying("A");
		var toggles = TogglesFor(denying);
		var manager = Manager([denying], toggles);

		Assert.True(manager.SetPolicyEnabled("A", false));
		var review = manager.Gate(Entry, 100, 100, TestTickHelper.Origin);

		Assert.Equal(RiskVerdict.Permit, review.Verdict);
		Assert.Empty(denying.Reviewed);
	}

	[Fact]
	public void Gate_MovedToggle_ChangesExecutionOrder()
	{
		var resizing = FakeRiskPolicy.SettingFraction("A", 0.25);
		var denying = FakeRiskPolicy.Denying("B");
		var manager = Manager([resizing, denying], TogglesFor(resizing, denying));

		Assert.True(manager.MovePolicy("B", -1));
		var review = manager.Gate(Entry, 100, 100, TestTickHelper.Origin);

		Assert.Equal(RiskVerdict.Deny, review.Verdict);
		Assert.Empty(resizing.Reviewed);
	}

	[Fact]
	public void Gate_WithToggles_PushesParameterValuesIntoPolicies()
	{
		var policy = new FakeRiskPolicy("A");
		var manager = Manager([policy], TogglesFor(policy));

		manager.SetPolicyParameter("A", "Knob", 7);
		manager.Gate(Entry, 100, 100, TestTickHelper.Origin);

		Assert.Equal(7, policy.AppliedParameters["Knob"]);
	}

	[Fact]
	public void Supervise_CollectsInjectedDecisionsFromEveryPolicy()
	{
		var closer = new FakeRiskPolicy("A", supervise: _ => [new SimulationDecision(SimulationAction.Close)]);
		var quiet = new FakeRiskPolicy("B");

		var actions = Manager([closer, quiet]).Supervise(100, 100, TestTickHelper.Origin);

		Assert.Equal(SimulationAction.Close, Assert.Single(actions).Action);
	}

	[Fact]
	public void PolicyStatuses_ListOnlyPoliciesWithStatus()
	{
		var withStatus = new FakeRiskPolicy("A") { Status = "busy" };
		var silent = new FakeRiskPolicy("B");

		var statuses = Manager([withStatus, silent]).PolicyStatuses();

		Assert.Equal(new RiskPolicyStatus("A", "busy"), Assert.Single(statuses));
	}

	[Fact]
	public void ToggleMutations_WithoutToggles_ReturnFalse()
	{
		var manager = Manager([new FakeRiskPolicy("A")]);

		Assert.False(manager.SetPolicyEnabled("A", false));
		Assert.False(manager.MovePolicy("A", 1));
		Assert.False(manager.SetPolicyParameter("A", "Knob", 1));
		Assert.Empty(manager.PolicyStates());
	}
}
