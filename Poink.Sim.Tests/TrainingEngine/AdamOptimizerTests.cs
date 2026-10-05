using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class AdamOptimizerTests
{
	[Theory]
	[InlineData(2f, 0.99f)]
	[InlineData(-5f, 1.01f)]
	[InlineData(0.001f, 0.99f)]
	public void Apply_FirstStep_MovesByLearningRateAgainstGradientSign(float gradient, float expected)
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 1);
		network.Weights[0][0, 0] = 1;
		var optimizer = new AdamOptimizer(network);

		optimizer.Apply(network, [new float[,] { { gradient } }], [new float[] { gradient }], 0.01);

		Assert.Equal(expected, network.Weights[0][0, 0], 4);
		Assert.Equal(expected - 1, network.Biases[0][0], 4);
	}

	[Fact]
	public void Apply_ZeroGradient_LeavesWeightsUnchanged()
	{
		var network = TestNetworkHelper.ZeroedLinear(2, 1);
		network.Weights[0][0, 1] = 0.7f;
		var optimizer = new AdamOptimizer(network);

		optimizer.Apply(network, [new float[1, 2]], [new float[1]], 0.01);

		Assert.Equal(0.7f, network.Weights[0][0, 1]);
		Assert.Equal(0f, network.Biases[0][0]);
	}

	[Fact]
	public void Apply_RepeatedConsistentGradient_KeepsDescending()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 1);
		var optimizer = new AdamOptimizer(network);

		for (var step = 0; step < 10; step++)
			optimizer.Apply(network, [new float[,] { { 1f } }], [new float[] { 1f }], 0.01);

		Assert.Equal(-0.1f, network.Weights[0][0, 0], 3);
	}
}
