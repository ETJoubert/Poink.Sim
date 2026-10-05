using System;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class PolicyGradientLearnerTests
{
	private static readonly float[] Input = [1f];

	private static readonly ActionSpace TwoActions = new([SimulationAction.Hold, SimulationAction.EnterLong], DecisionDecodingKind.Softmax, 1.0);

	private static readonly LearningOptionsModel Reinforce = new(TrainingStrategyCatalog.ReinforceName, 0.01, UseEntropyBonus: false);

	private static readonly LearningOptionsModel TradeOutcome = new(TrainingStrategyCatalog.TradeOutcomeName, 0.01, UseEntropyBonus: false);

	private static DecisionTrace Trace(MlpNetwork network, int actionIndex, double reward)
	{
		var (output, activations) = network.ForwardWithActivations(Input);
		return new DecisionTrace(activations, output, actionIndex, reward);
	}

	private static double Probability(MlpNetwork network, int actionIndex) => TwoActions.Probabilities(network.Forward(Input))[actionIndex];

	[Fact]
	public void EnsureSupportedStrategy_UnknownStrategy_Throws()
	{
		Assert.Throws<NotSupportedException>(() => PolicyGradientLearner.EnsureSupportedStrategy("Genetic"));
		PolicyGradientLearner.EnsureSupportedStrategy(TrainingStrategyCatalog.ReinforceName);
	}

	[Fact]
	public void LearnsOnline_OnlyForTradeOutcome()
	{
		Assert.True(PolicyGradientLearner.LearnsOnline(TrainingStrategyCatalog.TradeOutcomeName));
		Assert.False(PolicyGradientLearner.LearnsOnline(TrainingStrategyCatalog.ReinforceName));
	}

	[Fact]
	public void Learn_WithOnlineStrategy_Throws()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 2);

		Assert.Throws<InvalidOperationException>(
			() => new PolicyGradientLearner().Learn(network, TwoActions, [Trace(network, 0, 1)], TradeOutcome)
		);
	}

	[Fact]
	public void Learn_NoTraces_LeavesNetworkUnchanged()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 2);

		new PolicyGradientLearner().Learn(network, TwoActions, [], Reinforce);

		Assert.Equal(0.5, Probability(network, 0), 9);
	}

	[Fact]
	public void Learn_Reinforce_FavoursTheHigherReturnAction()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 2);
		var traces = new[] { Trace(network, 0, 1), Trace(network, 1, -1) };

		new PolicyGradientLearner().Learn(network, TwoActions, traces, Reinforce);

		Assert.True(Probability(network, 0) > 0.5);
	}

	[Fact]
	public void LearnOnline_WithoutBaselineHistory_MakesNoUpdate()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 2);
		var learner = new PolicyGradientLearner();

		learner.LearnOnline(network, TwoActions, Trace(network, 1, 5), 5, TradeOutcome);
		learner.LearnOnline(network, TwoActions, Trace(network, 1, 5), 5, TradeOutcome);

		Assert.Equal(0.5, Probability(network, 1), 9);
	}

	[Fact]
	public void LearnOnline_RewardAboveBaseline_ReinforcesChosenAction()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 2);
		var learner = new PolicyGradientLearner();
		learner.LearnOnline(network, TwoActions, Trace(network, 1, 0), 0, TradeOutcome);
		learner.LearnOnline(network, TwoActions, Trace(network, 1, 0), 0, TradeOutcome);

		learner.LearnOnline(network, TwoActions, Trace(network, 1, 1), 1, TradeOutcome);

		Assert.True(Probability(network, 1) > 0.5);
	}

	[Fact]
	public void LearnOnline_RewardBelowBaseline_DiscouragesChosenAction()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 2);
		var learner = new PolicyGradientLearner();
		learner.LearnOnline(network, TwoActions, Trace(network, 1, 0), 0, TradeOutcome);
		learner.LearnOnline(network, TwoActions, Trace(network, 1, 0), 0, TradeOutcome);

		learner.LearnOnline(network, TwoActions, Trace(network, 1, -1), -1, TradeOutcome);

		Assert.True(Probability(network, 1) < 0.5);
	}

	[Fact]
	public void LearnOnline_EntropyBonus_FlattensPeakedPolicyUntilFullyAnnealed()
	{
		var withEntropy = TradeOutcome with { UseEntropyBonus = true, LearningRate = 1 };
		var early = TestNetworkHelper.ZeroedLinear(1, 2);
		early.Biases[0][0] = 1;
		var late = TestNetworkHelper.ZeroedLinear(1, 2);
		late.Biases[0][0] = 1;
		double peaked = Probability(early, 0);

		new PolicyGradientLearner().LearnOnline(early, TwoActions, Trace(early, 0, 0), 0, withEntropy, progress: 0);
		new PolicyGradientLearner().LearnOnline(late, TwoActions, Trace(late, 0, 0), 0, withEntropy, progress: 1);

		Assert.True(Probability(early, 0) < peaked);
		Assert.Equal(peaked, Probability(late, 0), 12);
	}
}
