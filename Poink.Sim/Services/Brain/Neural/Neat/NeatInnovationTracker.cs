using System.Collections.Generic;

namespace Poink.Sim.Brain.Neural;

// Historical markings, reset each generation; genomes that add the same structural mutation
// within one generation get matching innovation numbers, enabling crossover gene-alignment.
internal sealed class NeatInnovationTracker(int startingNodeId, int startingInnovationId)
{
	private int _nextNodeId = startingNodeId;
	private int _nextInnovationId = startingInnovationId;

	private readonly Dictionary<(int InNodeId, int OutNodeId), int> _connectionInnovations = [];
	private readonly Dictionary<int, (int NewNodeId, int InInnovationId, int OutInnovationId)> _nodeSplitInnovations =
	[];

	public void StartNewGeneration()
	{
		_connectionInnovations.Clear();
		_nodeSplitInnovations.Clear();
	}

	public int InnovationForConnection(int inNodeId, int outNodeId)
	{
		var key = (inNodeId, outNodeId);
		if (_connectionInnovations.TryGetValue(key, out int existing))
			return existing;
		int innovation = _nextInnovationId++;
		_connectionInnovations[key] = innovation;
		return innovation;
	}

	public (int NewNodeId, int InInnovationId, int OutInnovationId) InnovationForNodeSplit(
		int splitConnectionInnovationId,
		int inNodeId,
		int outNodeId
	)
	{
		if (_nodeSplitInnovations.TryGetValue(splitConnectionInnovationId, out var existing))
			return existing;

		int newNodeId = _nextNodeId++;
		int inInnovation = InnovationForConnection(inNodeId, newNodeId);
		int outInnovation = InnovationForConnection(newNodeId, outNodeId);
		var result = (newNodeId, inInnovation, outInnovation);
		_nodeSplitInnovations[splitConnectionInnovationId] = result;
		return result;
	}
}
