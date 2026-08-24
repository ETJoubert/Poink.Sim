using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

internal static class NeatMutationService
{
	private const int MaxAddConnectionAttempts = 20;

	public static void Mutate(
		NeatGenome genome,
		NeatInnovationTracker innovationTracker,
		NeatHyperparameters hyperparameters,
		Random rng
	)
	{
		MutateWeights(genome, hyperparameters, rng);

		if (rng.NextDouble() < hyperparameters.AddConnectionMutationRate)
			MutateAddConnection(genome, innovationTracker, rng);

		if (rng.NextDouble() < hyperparameters.AddNodeMutationRate)
			MutateAddNode(genome, innovationTracker, rng);
	}

	private static void MutateWeights(NeatGenome genome, NeatHyperparameters hyperparameters, Random rng)
	{
		if (rng.NextDouble() >= hyperparameters.WeightMutationRate)
			return;

		foreach (var connection in genome.Connections)
		{
			if (rng.NextDouble() < hyperparameters.WeightResetChance)
				connection.Weight = rng.NextDouble() * 2 - 1;
			else
				connection.Weight += NextGaussian(rng) * hyperparameters.WeightPerturbStdDev;
		}
	}

	private static void MutateAddConnection(NeatGenome genome, NeatInnovationTracker innovationTracker, Random rng)
	{
		var targets = genome.Nodes.Where(n => n.Kind is NeatNodeKind.Hidden or NeatNodeKind.Output).ToList();
		if (targets.Count == 0)
			return;

		for (int attempt = 0; attempt < MaxAddConnectionAttempts; attempt++)
		{
			var source = genome.Nodes[rng.Next(genome.Nodes.Count)];
			var target = targets[rng.Next(targets.Count)];

			if (source.Id == target.Id)
				continue;
			if (genome.Connections.Any(c => c.InNodeId == source.Id && c.OutNodeId == target.Id))
				continue;
			if (CanReach(genome, target.Id, source.Id))
				continue; // target already reaches source -- adding source->target would close a cycle

			int innovation = innovationTracker.InnovationForConnection(source.Id, target.Id);
			genome.Connections.Add(
				new NeatConnectionGene(innovation, source.Id, target.Id, rng.NextDouble() * 2 - 1, enabled: true)
			);
			return;
		}
	}

	private static void MutateAddNode(NeatGenome genome, NeatInnovationTracker innovationTracker, Random rng)
	{
		var candidates = genome.Connections.Where(c => c.Enabled).ToList();
		if (candidates.Count == 0)
			return;

		var split = candidates[rng.Next(candidates.Count)];
		split.Enabled = false;

		var (newNodeId, inInnovation, outInnovation) = innovationTracker.InnovationForNodeSplit(
			split.InnovationId,
			split.InNodeId,
			split.OutNodeId
		);

		genome.Nodes.Add(new NeatNodeGene(newNodeId, NeatNodeKind.Hidden));
		genome.Connections.Add(new NeatConnectionGene(inInnovation, split.InNodeId, newNodeId, 1.0, enabled: true));
		genome.Connections.Add(
			new NeatConnectionGene(outInnovation, newNodeId, split.OutNodeId, split.Weight, enabled: true)
		);
	}

	private static bool CanReach(NeatGenome genome, int fromNodeId, int toNodeId)
	{
		var visited = new HashSet<int> { fromNodeId };
		var stack = new Stack<int>();
		stack.Push(fromNodeId);

		while (stack.Count > 0)
		{
			int current = stack.Pop();
			if (current == toNodeId)
				return true;

			foreach (var connection in genome.Connections.Where(c => c.Enabled && c.InNodeId == current))
				if (visited.Add(connection.OutNodeId))
					stack.Push(connection.OutNodeId);
		}
		return false;
	}

	private static double NextGaussian(Random rng)
	{
		double u1 = 1.0 - rng.NextDouble();
		double u2 = rng.NextDouble();
		return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
	}
}
