using System;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class MlpNetworkTests
{
	[Fact]
	public void Forward_AppliesReluToHiddenLayersOnly()
	{
		var output = TestNetworkHelper.TwoTwoOne().Forward([3f, 1f]);

		Assert.Equal(2.5f, Assert.Single(output));
	}

	[Fact]
	public void Forward_LinearOutputLayer_CanGoNegative()
	{
		var network = TestNetworkHelper.ZeroedLinear(1, 1);
		network.Weights[0][0, 0] = -2;

		Assert.Equal(-6f, Assert.Single(network.Forward([3f])));
	}

	[Fact]
	public void ForwardWithActivations_ReturnsInputThenEveryLayerOutput()
	{
		var (output, activations) = TestNetworkHelper.TwoTwoOne().ForwardWithActivations([3f, 1f]);

		Assert.Equal(3, activations.Count);
		Assert.Equal(new[] { 3f, 1f }, activations[0]);
		Assert.Equal(new[] { 2f, 0f }, activations[1]);
		Assert.Same(output, activations[2]);
	}

	[Fact]
	public void Forward_WrongInputLength_Throws()
	{
		Assert.Throws<ArgumentException>(() => TestNetworkHelper.TwoTwoOne().Forward([1f]));
	}
}
