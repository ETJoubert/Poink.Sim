
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

public enum NeatNodeKind { Input, Bias, Hidden, Output }

internal sealed record NeatNodeGene(int Id, NeatNodeKind Kind);

internal sealed class NeatConnectionGene(int innovationId, int inNodeId, int outNodeId, double weight, bool enabled)
{
    public int InnovationId { get; } = innovationId;
    public int InNodeId { get; } = inNodeId;
    public int OutNodeId { get; } = outNodeId;
    public double Weight { get; set; } = weight;
    public bool Enabled { get; set; } = enabled;

    public NeatConnectionGene Clone() => new(InnovationId, InNodeId, OutNodeId, Weight, Enabled);
}

internal sealed class NeatGenome(int id, List<NeatNodeGene> nodes, List<NeatConnectionGene> connections)
{
    public int Id { get; } = id;
    public List<NeatNodeGene> Nodes { get; } = nodes;
    public List<NeatConnectionGene> Connections { get; } = connections;
    public double Fitness { get; set; }
    public double AdjustedFitness { get; set; }
    public int? SpeciesId { get; set; }

    public NeatGenome Clone(int newId) => new(newId, [.. Nodes], [.. Connections.Select(c => c.Clone())]);
}

// Population size and rate defaults follow Stanley & Miikkulainen 2002 ("Evolving Neural
// Networks through Augmenting Topologies"); tune per brain once the algorithm is validated.
public sealed record NeatHyperparameters(
    int PopulationSize = 150,
    double CompatibilityThreshold = 3.0,
    double ExcessCoefficient = 1.0,
    double DisjointCoefficient = 1.0,
    double WeightDiffCoefficient = 0.4,
    double SurvivalThreshold = 0.2,
    double WeightMutationRate = 0.8,
    double WeightResetChance = 0.1,
    double WeightPerturbStdDev = 0.5,
    double AddConnectionMutationRate = 0.05,
    double AddNodeMutationRate = 0.03,
    double DisableInheritedGeneChance = 0.75,
    double InterspeciesMatingRate = 0.001,
    int StagnationGenerations = 15,
    int ElitismMinSpeciesSize = 5);

public sealed record NeatGenerationStats(int Generation, int PopulationSize, int SpeciesCount, double BestFitness, double AverageFitness);

internal sealed class NeatSpecies(int id, NeatGenome representative)
{
    public int Id { get; } = id;
    public NeatGenome Representative { get; set; } = representative;
    public List<NeatGenome> Members { get; } = [];
    public double BestFitness { get; set; } = double.NegativeInfinity;
    public int GenerationsSinceImprovement { get; set; }
}
