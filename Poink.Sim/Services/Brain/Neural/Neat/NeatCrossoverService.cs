
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

internal static class NeatCrossoverService
{
    public static NeatGenome Crossover(
        NeatGenome fitterParent, NeatGenome otherParent, int childId, NeatHyperparameters hyperparameters, Random rng)
    {
        var otherByInnovation = otherParent.Connections.ToDictionary(c => c.InnovationId);
        var childConnections = new List<NeatConnectionGene>(fitterParent.Connections.Count);

        foreach (var gene in fitterParent.Connections)
        {
            bool eitherDisabled;
            NeatConnectionGene inherited;
            if (otherByInnovation.TryGetValue(gene.InnovationId, out var matchingGene))
            {
                inherited = (rng.NextDouble() < 0.5 ? gene : matchingGene).Clone();
                eitherDisabled = !gene.Enabled || !matchingGene.Enabled;
            }
            else
            {
                // Disjoint/excess gene -- inherited from the fitter parent only.
                inherited = gene.Clone();
                eitherDisabled = !gene.Enabled;
            }

            if (eitherDisabled)
                inherited.Enabled = rng.NextDouble() >= hyperparameters.DisableInheritedGeneChance;

            childConnections.Add(inherited);
        }

        var nodesById = new Dictionary<int, NeatNodeGene>();
        foreach (var node in fitterParent.Nodes.Concat(otherParent.Nodes))
            nodesById.TryAdd(node.Id, node);

        var neededNodeIds = new HashSet<int>();
        foreach (var connection in childConnections)
        { neededNodeIds.Add(connection.InNodeId); neededNodeIds.Add(connection.OutNodeId); }

        // Input/bias/output nodes always carry over, even if every connection touching one
        // happened to end up disjoint/excess-from-the-other-parent and got skipped above.
        foreach (var node in fitterParent.Nodes.Where(n => n.Kind is NeatNodeKind.Input or NeatNodeKind.Bias or NeatNodeKind.Output))
            neededNodeIds.Add(node.Id);

        var childNodes = neededNodeIds.Select(id => nodesById[id]).ToList();
        return new NeatGenome(childId, childNodes, childConnections);
    }
}
