using System;
using Poink.Sim.Services.TrainingEngine;

namespace Poink.Sim.Tests.TestSupport;

internal static class TestNetworkHelper
{
	public static MlpNetwork TwoTwoOne() =>
		new(
			2,
			[2],
			1,
			[new float[,] { { 1, -1 }, { -1, 1 } }, new float[,] { { 1, 1 } }],
			[new float[] { 0, 0 }, new float[] { 0.5f }]
		);

	public static MlpNetwork ZeroedLinear(int inputSize, int outputSize) =>
		new(inputSize, Array.Empty<int>(), outputSize, [new float[outputSize, inputSize]], [new float[outputSize]]);
}
