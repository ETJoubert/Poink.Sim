using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Converts an MlpNetwork to and from bytes for storage: a format version, the layer sizes, then each
/// layer's weights (row-major) followed by its biases, all little-endian.
/// </summary>
internal sealed class MlpWeightsSerializer
{
	private const int FormatVersion = 1;

	public byte[] Serialize(MlpNetwork network)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream);

		writer.Write(FormatVersion);
		writer.Write(network.InputSize);
		writer.Write(network.HiddenSizes.Count);
		foreach (var size in network.HiddenSizes)
			writer.Write(size);
		writer.Write(network.OutputSize);

		for (var layer = 0; layer < network.Weights.Count; layer++)
		{
			foreach (var weight in network.Weights[layer])
				writer.Write(weight);
			foreach (var bias in network.Biases[layer])
				writer.Write(bias);
		}

		writer.Flush();
		return stream.ToArray();
	}

	public MlpNetwork Deserialize(byte[] data)
	{
		using var reader = new BinaryReader(new MemoryStream(data));

		var version = reader.ReadInt32();
		if (version != FormatVersion)
			throw new InvalidDataException($"Saved weights use format version {version}; this build reads version {FormatVersion}.");

		var inputSize = reader.ReadInt32();
		var hiddenCount = reader.ReadInt32();
		var hiddenSizes = Enumerable.Range(0, hiddenCount).Select(_ => reader.ReadInt32()).ToList();
		var outputSize = reader.ReadInt32();

		var layerSizes = new List<int> { inputSize };
		layerSizes.AddRange(hiddenSizes);
		layerSizes.Add(outputSize);

		var weights = new List<float[,]>();
		var biases = new List<float[]>();
		for (var layer = 0; layer < layerSizes.Count - 1; layer++)
		{
			var layerInputs = layerSizes[layer];
			var layerOutputs = layerSizes[layer + 1];

			var layerWeights = new float[layerOutputs, layerInputs];
			for (var o = 0; o < layerOutputs; o++)
				for (var i = 0; i < layerInputs; i++)
					layerWeights[o, i] = reader.ReadSingle();
			weights.Add(layerWeights);

			var layerBiases = new float[layerOutputs];
			for (var o = 0; o < layerOutputs; o++)
				layerBiases[o] = reader.ReadSingle();
			biases.Add(layerBiases);
		}

		return new MlpNetwork(inputSize, hiddenSizes, outputSize, weights, biases);
	}
}
