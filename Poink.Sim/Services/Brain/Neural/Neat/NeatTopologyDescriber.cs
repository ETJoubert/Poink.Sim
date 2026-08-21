
using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

public sealed record NeatTopologyNode(int Id, NeatNodeKind Kind, int Depth);

public sealed record NeatTopologyConnection(int FromNodeId, int ToNodeId, double Weight, bool Enabled);

public sealed record NeatTopologySnapshot(
    IReadOnlyList<NeatTopologyNode> Nodes, IReadOnlyList<NeatTopologyConnection> Connections, int Generation);

internal static class NeatTopologyDescriber
{
    public static NeatTopologySnapshot Describe(NeatGenome genome, int generation)
    {
        var depths = ComputeDepths(genome);
        var nodes = genome.Nodes.Select(n => new NeatTopologyNode(n.Id, n.Kind, depths.GetValueOrDefault(n.Id))).ToList();
        var connections = genome.Connections
            .Select(c => new NeatTopologyConnection(c.InNodeId, c.OutNodeId, c.Weight, c.Enabled))
            .ToList();
        return new NeatTopologySnapshot(nodes, connections, generation);
    }

    // Kahn's algorithm over enabled connections, tracking longest-path depth from the source
    // nodes rather than a flat execution order -- depth is what the topology graph lays out on.
    // A node stranded by a cycle (shouldn't happen; AddConnection mutation guards against
    // introducing one) simply keeps its default depth of 0 rather than throwing.
    private static Dictionary<int, int> ComputeDepths(NeatGenome genome)
    {
        var enabledConnections = genome.Connections.Where(c => c.Enabled).ToList();
        var depths = new Dictionary<int, int>();

        var nonSourceNodes = genome.Nodes.Where(n => n.Kind is NeatNodeKind.Hidden or NeatNodeKind.Output).ToList();
        var inDegree = nonSourceNodes.ToDictionary(n => n.Id, _ => 0);
        var dependents = new Dictionary<int, List<int>>();

        foreach (var connection in enabledConnections)
        {
            if (!inDegree.ContainsKey(connection.OutNodeId))
                continue;

            inDegree[connection.OutNodeId]++;
            if (!dependents.TryGetValue(connection.InNodeId, out var list))
                dependents[connection.InNodeId] = list = [];
            list.Add(connection.OutNodeId);
        }

        // Source nodes (depth 0 by definition) seed the traversal alongside any isolated
        // hidden/output node that happens to have no enabled incoming connection at all.
        var ready = new Queue<int>();
        foreach (var sourceNode in genome.Nodes.Where(n => n.Kind is NeatNodeKind.Input or NeatNodeKind.Bias))
        { depths[sourceNode.Id] = 0; ready.Enqueue(sourceNode.Id); }
        foreach (var isolatedNode in inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key))
        { depths.TryAdd(isolatedNode, 0); ready.Enqueue(isolatedNode); }

        var visited = new HashSet<int>(ready);
        while (ready.Count > 0)
        {
            int nodeId = ready.Dequeue();
            if (!dependents.TryGetValue(nodeId, out var nextNodes))
                continue;

            foreach (int next in nextNodes)
            {
                depths[next] = Math.Max(depths.GetValueOrDefault(next), depths.GetValueOrDefault(nodeId) + 1);
                if (--inDegree[next] == 0 && visited.Add(next))
                    ready.Enqueue(next);
            }
        }

        foreach (var node in nonSourceNodes)
            depths.TryAdd(node.Id, 0);

        return depths;
    }
}
