using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

internal static class NeatSpeciationService
{
	public static double CompatibilityDistance(NeatGenome a, NeatGenome b, NeatHyperparameters hyperparameters)
	{
		var aByInnovation = a.Connections.ToDictionary(c => c.InnovationId);
		var bByInnovation = b.Connections.ToDictionary(c => c.InnovationId);

		int maxInnovationA = a.Connections.Count > 0 ? a.Connections.Max(c => c.InnovationId) : 0;
		int maxInnovationB = b.Connections.Count > 0 ? b.Connections.Max(c => c.InnovationId) : 0;
		int lowerMaxInnovation = Math.Min(maxInnovationA, maxInnovationB);

		int matching = 0,
			disjoint = 0,
			excess = 0;
		double weightDiffSum = 0;

		foreach (var gene in a.Connections)
		{
			if (bByInnovation.TryGetValue(gene.InnovationId, out var other))
			{
				matching++;
				weightDiffSum += Math.Abs(gene.Weight - other.Weight);
			}
			else if (gene.InnovationId > lowerMaxInnovation)
				excess++;
			else
				disjoint++;
		}
		foreach (var gene in b.Connections)
		{
			if (aByInnovation.ContainsKey(gene.InnovationId))
				continue;
			if (gene.InnovationId > lowerMaxInnovation)
				excess++;
			else
				disjoint++;
		}

		int n = Math.Max(a.Connections.Count, b.Connections.Count);
		n = n < 20 ? 1 : n; // small-genome normalization, per the original NEAT paper

		double averageWeightDiff = matching > 0 ? weightDiffSum / matching : 0;

		return hyperparameters.ExcessCoefficient * excess / n
			+ hyperparameters.DisjointCoefficient * disjoint / n
			+ hyperparameters.WeightDiffCoefficient * averageWeightDiff;
	}

	public static void Speciate(
		IReadOnlyList<NeatGenome> genomes,
		List<NeatSpecies> species,
		NeatHyperparameters hyperparameters,
		ref int nextSpeciesId
	)
	{
		foreach (var s in species)
			s.Members.Clear();

		foreach (var genome in genomes)
		{
			var match = species.FirstOrDefault(s =>
				CompatibilityDistance(genome, s.Representative, hyperparameters)
				< hyperparameters.CompatibilityThreshold
			);
			if (match is null)
			{
				match = new NeatSpecies(nextSpeciesId++, genome);
				species.Add(match);
			}
			genome.SpeciesId = match.Id;
			match.Members.Add(genome);
		}

		species.RemoveAll(s => s.Members.Count == 0);
		foreach (var s in species)
			s.Representative = s.Members[Random.Shared.Next(s.Members.Count)];
	}
}
