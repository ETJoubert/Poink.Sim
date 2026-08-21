
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

internal static class NeatNetwork
{
    public static float[] Forward(NeatGenome genome, float[] inputs)
    {
        var nodeValues = new Dictionary<int, double>(genome.Nodes.Count);

        var inputNodes = genome.Nodes.Where(n => n.Kind == NeatNodeKind.Input).OrderBy(n => n.Id).ToList();
        for (int i = 0; i < inputNodes.Count; i++)
            nodeValues[inputNodes[i].Id] = i < inputs.Length ? inputs[i] : 0.0;

        foreach (var bias in genome.Nodes.Where(n => n.Kind == NeatNodeKind.Bias))
            nodeValues[bias.Id] = 1.0;

        var enabledConnections = genome.Connections.Where(c => c.Enabled).ToList();
        var incomingByNode = enabledConnections
            .GroupBy(c => c.OutNodeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<NeatConnectionGene>)g.ToList());

        foreach (var node in TopologicalOrder(genome, enabledConnections))
        {
            double sum = incomingByNode.TryGetValue(node.Id, out var incoming)
                ? incoming.Sum(c => nodeValues.GetValueOrDefault(c.InNodeId) * c.Weight)
                : 0.0;

            nodeValues[node.Id] = node.Kind == NeatNodeKind.Output ? sum : Math.Tanh(sum);
        }

        return genome.Nodes
            .Where(n => n.Kind == NeatNodeKind.Output)
            .OrderBy(n => n.Id)
            .Select(n => (float)nodeValues.GetValueOrDefault(n.Id))
            .ToArray();
    }

    // Kahn's algorithm over enabled connections; any node left stranded by a cycle falls back
    // to declaration order rather than throwing, since AddConnection mutation should never
    // introduce one -- this is a defensive backstop, not the primary correctness mechanism.
    private static IEnumerable<NeatNodeGene> TopologicalOrder(NeatGenome genome, IReadOnlyList<NeatConnectionGene> enabledConnections)
    {
        var nonSourceNodes = genome.Nodes.Where(n => n.Kind is NeatNodeKind.Hidden or NeatNodeKind.Output).ToList();
        var inDegree = nonSourceNodes.ToDictionary(n => n.Id, _ => 0);
        var dependents = new Dictionary<int, List<int>>();

        foreach (var connection in enabledConnections)
        {
            if (!inDegree.ContainsKey(connection.OutNodeId) || !inDegree.ContainsKey(connection.InNodeId))
                continue;

            inDegree[connection.OutNodeId]++;
            if (!dependents.TryGetValue(connection.InNodeId, out var list))
                dependents[connection.InNodeId] = list = [];
            list.Add(connection.OutNodeId);
        }

        var ready = new Queue<int>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var visited = new HashSet<int>();
        var orderedIds = new List<int>();

        while (ready.Count > 0)
        {
            int nodeId = ready.Dequeue();
            if (!visited.Add(nodeId))
                continue;
            orderedIds.Add(nodeId);

            if (!dependents.TryGetValue(nodeId, out var nextNodes))
                continue;
            foreach (int next in nextNodes)
                if (--inDegree[next] == 0)
                    ready.Enqueue(next);
        }

        foreach (var node in nonSourceNodes)
            if (visited.Add(node.Id))
                orderedIds.Add(node.Id);

        var byId = nonSourceNodes.ToDictionary(n => n.Id);
        return orderedIds.Select(id => byId[id]);
    }
}
