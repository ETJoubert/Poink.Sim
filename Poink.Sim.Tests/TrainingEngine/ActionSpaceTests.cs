using System;
using System.Linq;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Services.TrainingEngine;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class ActionSpaceTests
{
	private static readonly SimulationAction[] AllDirections =
		[SimulationAction.Hold, SimulationAction.EnterLong, SimulationAction.EnterShort, SimulationAction.Close];

	private static ActionSpace Space(DecisionDecodingKind decoding, double temperature = 1.0) =>
		new(AllDirections, decoding, temperature);

	[Fact]
	public void HardMax_PicksHighestOutput()
	{
		var action = Space(DecisionDecodingKind.HardMax).DecodeDirection([0.1f, -2f, 3f, 2.9f], new Random(1));

		Assert.Equal(SimulationAction.EnterShort, action);
	}

	[Fact]
	public void Softmax_OverwhelminglyPreferredAction_IsAlwaysSampled()
	{
		var space = Space(DecisionDecodingKind.Softmax);
		var random = new Random(7);

		var actions = Enumerable.Range(0, 50).Select(_ => space.DecodeDirection([0f, 100f, 0f, 0f], random));

		Assert.All(actions, action => Assert.Equal(SimulationAction.EnterLong, action));
	}

	[Fact]
	public void Probabilities_EqualOutputs_AreUniform()
	{
		var probabilities = Space(DecisionDecodingKind.Softmax).Probabilities([1f, 1f, 1f, 1f]);

		Assert.All(probabilities, p => Assert.Equal(0.25, p, 9));
	}

	[Fact]
	public void Probabilities_AlwaysSumToOne_EvenForLargeOutputs()
	{
		var probabilities = Space(DecisionDecodingKind.Softmax).Probabilities([1000f, 999f, -1000f, 0f]);

		Assert.Equal(1, probabilities.Sum(), 9);
		Assert.DoesNotContain(probabilities, double.IsNaN);
	}

	[Fact]
	public void Probabilities_LowerTemperature_SharpensDistribution()
	{
		float[] outputs = [1f, 2f, 0f, 0f];

		double warm = Space(DecisionDecodingKind.Softmax, 2.0).Probabilities(outputs)[1];
		double cool = Space(DecisionDecodingKind.Softmax, 0.5).Probabilities(outputs)[1];

		Assert.True(cool > warm);
	}

	[Fact]
	public void DecodeDirection_WrongOutputCount_Throws()
	{
		Assert.Throws<ArgumentException>(() => Space(DecisionDecodingKind.HardMax).DecodeDirection([1f, 2f], new Random(1)));
	}

	[Fact]
	public void ActionAssembler_KeepsOnlyEnabledDirectionsInCanonicalOrder()
	{
		var options = new OutputOptionsModel(
			new ActionsOptionsModel(IncludeHold: false, IncludeEnterLong: true, IncludeEnterShort: true, IncludeClose: false),
			new ActionMaskingOptionsModel(),
			new ActionDecodingOptionsModel(DecisionDecodingKind.HardMax, 0.7)
		);

		var space = new ActionAssembler().Assemble(options);

		Assert.Equal(new[] { SimulationAction.EnterLong, SimulationAction.EnterShort }, space.Directions);
		Assert.Equal(2, space.TotalOutputWidth);
		Assert.Equal(DecisionDecodingKind.HardMax, space.DecisionDecoding);
		Assert.Equal(0.7, space.DecisionTemperature);
	}
}
