using System;

namespace Poink.Sim.Models
{
    internal sealed record NeuralNetworkModel(
        Guid Id,
        string Name,
        string StructureJson,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );
}

