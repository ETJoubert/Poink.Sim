using System;
using System.Linq;
using Poink.Sim.Brain.Neural;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Services.TrainingEngine;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class NetworkAssemblerTests
{
	[Fact]
	public void Assemble_BuildsLayerShapesFromInputHiddenAndOutputSizes()
	{
		var network = new NetworkAssembler().Assemble(new NetworkOptionsModel(MlpHiddenSizes: [8, 4]), 5, 3, new Random(1));

		Assert.Equal(3, network.Weights.Count);
		Assert.Equal((8, 5), (network.Weights[0].GetLength(0), network.Weights[0].GetLength(1)));
		Assert.Equal((4, 8), (network.Weights[1].GetLength(0), network.Weights[1].GetLength(1)));
		Assert.Equal((3, 4), (network.Weights[2].GetLength(0), network.Weights[2].GetLength(1)));
		Assert.All(network.Biases, biases => Assert.All(biases, bias => Assert.Equal(0f, bias)));
	}

	[Fact]
	public void Assemble_NoHiddenSizes_DefaultsToOneLayerOf64()
	{
		var network = new NetworkAssembler().Assemble(new NetworkOptionsModel(), 5, 3, new Random(1));

		Assert.Equal(new[] { 64 }, network.HiddenSizes);
	}

	[Fact]
	public void Assemble_SameSeed_IsReproducible()
	{
		var options = new NetworkOptionsModel(MlpHiddenSizes: [6]);

		var first = new NetworkAssembler().Assemble(options, 4, 2, new Random(99));
		var second = new NetworkAssembler().Assemble(options, 4, 2, new Random(99));

		Assert.Equal(first.Weights[0], second.Weights[0]);
		Assert.Equal(first.Weights[1], second.Weights[1]);
	}

	[Fact]
	public void Assemble_HeInitialisation_HasExpectedSpread()
	{
		var network = new NetworkAssembler().Assemble(new NetworkOptionsModel(MlpHiddenSizes: [200]), 50, 1, new Random(3));

		var weights = network.Weights[0].Cast<float>().Select(w => (double)w).ToList();
		double mean = weights.Average();
		double std = Math.Sqrt(weights.Average(w => (w - mean) * (w - mean)));

		Assert.Equal(0, mean, 1);
		Assert.Equal(Math.Sqrt(2.0 / 50), std, 1);
	}

	[Theory]
	[InlineData(ArchitectureKind.Lstm)]
	[InlineData(ArchitectureKind.Cnn)]
	public void Assemble_UnimplementedArchitecture_Throws(ArchitectureKind kind)
	{
		Assert.Throws<NotSupportedException>(() => new NetworkAssembler().Assemble(new NetworkOptionsModel(kind), 5, 3, new Random(1)));
	}
}
