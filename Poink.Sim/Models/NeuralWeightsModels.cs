using System;

namespace Poink.Sim.Models
{
    internal sealed record NeuralWeightsModel(Guid Id, Guid NeuralNetworkId, string Name, byte[] Data, long WeightCount, DateTime CreatedAt);
}
