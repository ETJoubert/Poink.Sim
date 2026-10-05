using System;
using System.IO;
using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class MlpWeightsSerializerTests
{
	[Fact]
	public void Deserialize_RoundTripsShapeWeightsAndBiases()
	{
		var serializer = new MlpWeightsSerializer();
		var original = TestNetworkHelper.TwoTwoOne();

		var restored = serializer.Deserialize(serializer.Serialize(original));

		Assert.Equal(original.InputSize, restored.InputSize);
		Assert.Equal(original.HiddenSizes, restored.HiddenSizes);
		Assert.Equal(original.OutputSize, restored.OutputSize);
		for (var layer = 0; layer < original.Weights.Count; layer++)
		{
			Assert.Equal(original.Weights[layer], restored.Weights[layer]);
			Assert.Equal(original.Biases[layer], restored.Biases[layer]);
		}
		Assert.Equal(original.Forward([3f, 1f]), restored.Forward([3f, 1f]));
	}

	[Fact]
	public void Deserialize_RandomlyInitialisedNetwork_ProducesIdenticalOutputs()
	{
		var serializer = new MlpWeightsSerializer();
		var original = new NetworkAssembler().Assemble(new(MlpHiddenSizes: [8, 4]), 5, 3, new Random(42));
		float[] input = [0.1f, -0.4f, 2f, 0f, 1.5f];

		var restored = serializer.Deserialize(serializer.Serialize(original));

		Assert.Equal(original.Forward(input), restored.Forward(input));
	}

	[Fact]
	public void Deserialize_UnknownFormatVersion_Throws()
	{
		var serializer = new MlpWeightsSerializer();
		var bytes = serializer.Serialize(TestNetworkHelper.TwoTwoOne());
		BitConverter.GetBytes(99).CopyTo(bytes, 0);

		Assert.Throws<InvalidDataException>(() => serializer.Deserialize(bytes));
	}
}
