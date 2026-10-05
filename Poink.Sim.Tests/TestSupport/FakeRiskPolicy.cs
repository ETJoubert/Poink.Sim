using System;
using System.Collections.Generic;
using Poink.Sim.Models;
using Poink.Sim.Trader.Risk;

namespace Poink.Sim.Tests.TestSupport;

internal sealed class FakeRiskPolicy : IRiskPolicy
{
	private readonly Func<SimulationDecision, RiskReview> _review;
	private readonly Func<RiskContext, IReadOnlyList<SimulationDecision>> _supervise;

	public FakeRiskPolicy(
		string name,
		Func<SimulationDecision, RiskReview>? review = null,
		Func<RiskContext, IReadOnlyList<SimulationDecision>>? supervise = null
	)
	{
		Name = name;
		_review = review ?? RiskReview.Permit;
		_supervise = supervise ?? (_ => Array.Empty<SimulationDecision>());
	}

	public string Name { get; }

	public string Description => Name;

	public string? Status { get; set; }

	public List<SimulationDecision> Reviewed { get; } = new();

	public Dictionary<string, double> AppliedParameters { get; } = new();

	public void SetParameter(string key, double value) => AppliedParameters[key] = value;

	public IRiskPolicy Fresh() => this;

	public RiskReview Review(SimulationDecision proposed, RiskContext context)
	{
		Reviewed.Add(proposed);
		return _review(proposed);
	}

	public IReadOnlyList<SimulationDecision> Supervise(RiskContext context) => _supervise(context);

	public static FakeRiskPolicy Denying(string name) => new(name, decision => RiskReview.Deny(decision, $"{name} says no", name));

	public static FakeRiskPolicy SettingFraction(string name, double fraction) =>
		new(name, decision => RiskReview.Modify(decision with { CapitalFraction = fraction }, $"{name} resized"));
}
