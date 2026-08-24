using System;
using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Brain.Neural;

// One "episode" (in the RL training loop's existing sense) evaluates one genome; TrainEpisode
// reports its fitness and advances the population, calling Evolve() once every genome in the
// current generation has been scored. See NeatTrainingStrategy.
internal sealed class NeatPopulation
{
	private readonly NeatHyperparameters _hyperparameters;
	private readonly NeatInnovationTracker _innovationTracker;
	private readonly Random _rng = new();
	private readonly List<NeatSpecies> _species = [];

	private List<NeatGenome> _genomes;
	private int _currentIndex;
	private int _nextGenomeId;
	private int _nextSpeciesId;

	public int InputCount { get; }
	public int OutputCount { get; }
	public int Generation { get; private set; }
	public NeatGenome? BestGenome { get; private set; }

	public NeatPopulation(int inputCount, int outputCount, NeatHyperparameters hyperparameters)
	{
		InputCount = inputCount;
		OutputCount = outputCount;
		_hyperparameters = hyperparameters;
		_innovationTracker = new NeatInnovationTracker(
			startingNodeId: inputCount + 1 + outputCount,
			startingInnovationId: 0
		);
		_genomes = CreateInitialGenomes();
	}

	public NeatGenome CurrentGenome => _genomes[_currentIndex];

	public void ReportFitness(double fitness) => CurrentGenome.Fitness = fitness;

	public bool TryAdvanceToNextGenome()
	{
		if (_currentIndex + 1 >= _genomes.Count)
			return false;
		_currentIndex++;
		return true;
	}

	private List<NeatGenome> CreateInitialGenomes()
	{
		var inputNodes = Enumerable
			.Range(0, InputCount)
			.Select(id => new NeatNodeGene(id, NeatNodeKind.Input))
			.ToList();
		var biasNode = new NeatNodeGene(InputCount, NeatNodeKind.Bias);
		var outputNodes = Enumerable
			.Range(InputCount + 1, OutputCount)
			.Select(id => new NeatNodeGene(id, NeatNodeKind.Output))
			.ToList();
		var sourceNodes = inputNodes.Append(biasNode).ToList();

		var genomes = new List<NeatGenome>(_hyperparameters.PopulationSize);
		for (int i = 0; i < _hyperparameters.PopulationSize; i++)
		{
			var nodes = new List<NeatNodeGene>(sourceNodes.Count + outputNodes.Count);
			nodes.AddRange(sourceNodes);
			nodes.AddRange(outputNodes);

			var connections = new List<NeatConnectionGene>(sourceNodes.Count * outputNodes.Count);
			foreach (var source in sourceNodes)
			foreach (var output in outputNodes)
			{
				int innovation = _innovationTracker.InnovationForConnection(source.Id, output.Id);
				connections.Add(
					new NeatConnectionGene(innovation, source.Id, output.Id, NextRandomWeight(), enabled: true)
				);
			}

			genomes.Add(new NeatGenome(_nextGenomeId++, nodes, connections));
		}
		return genomes;
	}

	private double NextRandomWeight() => _rng.NextDouble() * 2 - 1;

	public NeatGenerationStats Evolve()
	{
		int completedGeneration = Generation;
		int evaluatedPopulationSize = _genomes.Count;
		double averageFitness = _genomes.Average(g => g.Fitness);
		BestGenome = _genomes.OrderByDescending(g => g.Fitness).First();

		_innovationTracker.StartNewGeneration();
		NeatSpeciationService.Speciate(_genomes, _species, _hyperparameters, ref _nextSpeciesId);

		foreach (var species in _species)
		{
			double best = species.Members.Max(m => m.Fitness);
			if (best > species.BestFitness)
			{
				species.BestFitness = best;
				species.GenerationsSinceImprovement = 0;
			}
			else
				species.GenerationsSinceImprovement++;
		}

		var survivingSpecies =
			_species.Count <= 1
				? _species.ToList()
				: _species.Where(s => s.GenerationsSinceImprovement <= _hyperparameters.StagnationGenerations).ToList();
		if (survivingSpecies.Count == 0)
			survivingSpecies = _species.OrderByDescending(s => s.BestFitness).Take(1).ToList();

		foreach (var species in survivingSpecies)
		foreach (var member in species.Members)
			member.AdjustedFitness = member.Fitness / species.Members.Count;

		double totalAdjustedFitness = survivingSpecies.Sum(s => s.Members.Sum(m => m.AdjustedFitness));
		var offspringCounts = AllocateOffspring(survivingSpecies, totalAdjustedFitness);

		var nextGeneration = new List<NeatGenome>(_hyperparameters.PopulationSize);
		for (int i = 0; i < survivingSpecies.Count; i++)
		{
			var species = survivingSpecies[i];
			int offspringCount = offspringCounts[i];
			if (offspringCount == 0)
				continue;

			var breedingPool = species
				.Members.OrderByDescending(m => m.Fitness)
				.Take(Math.Max(1, (int)Math.Ceiling(species.Members.Count * _hyperparameters.SurvivalThreshold)))
				.ToList();

			int produced = 0;
			if (species.Members.Count >= _hyperparameters.ElitismMinSpeciesSize)
			{
				nextGeneration.Add(breedingPool[0].Clone(_nextGenomeId++));
				produced++;
			}

			while (produced < offspringCount)
			{
				var parentA = breedingPool[_rng.Next(breedingPool.Count)];
				var interspeciesPool =
					_rng.NextDouble() < _hyperparameters.InterspeciesMatingRate && survivingSpecies.Count > 1
						? survivingSpecies[_rng.Next(survivingSpecies.Count)].Members
						: breedingPool;
				var parentB = interspeciesPool[_rng.Next(interspeciesPool.Count)];

				NeatGenome child;
				if (ReferenceEquals(parentA, parentB))
					child = parentA.Clone(_nextGenomeId++);
				else
				{
					var fitterParent = parentA.Fitness >= parentB.Fitness ? parentA : parentB;
					var otherParent = ReferenceEquals(fitterParent, parentA) ? parentB : parentA;
					child = NeatCrossoverService.Crossover(
						fitterParent,
						otherParent,
						_nextGenomeId++,
						_hyperparameters,
						_rng
					);
				}

				NeatMutationService.Mutate(child, _innovationTracker, _hyperparameters, _rng);
				nextGeneration.Add(child);
				produced++;
			}
		}

		while (nextGeneration.Count < _hyperparameters.PopulationSize)
			nextGeneration.Add(BestGenome!.Clone(_nextGenomeId++));
		if (nextGeneration.Count > _hyperparameters.PopulationSize)
			nextGeneration.RemoveRange(
				_hyperparameters.PopulationSize,
				nextGeneration.Count - _hyperparameters.PopulationSize
			);

		_genomes = nextGeneration;
		_currentIndex = 0;
		Generation++;

		return new NeatGenerationStats(
			completedGeneration,
			evaluatedPopulationSize,
			survivingSpecies.Count,
			BestGenome.Fitness,
			averageFitness
		);
	}

	private List<int> AllocateOffspring(List<NeatSpecies> species, double totalAdjustedFitness)
	{
		if (totalAdjustedFitness <= 0)
		{
			int equalShare = _hyperparameters.PopulationSize / species.Count;
			var equalCounts = species.Select(_ => equalShare).ToList();
			equalCounts[0] += _hyperparameters.PopulationSize - equalCounts.Sum();
			return equalCounts;
		}

		var counts = species
			.Select(s =>
				(int)
					Math.Round(
						s.Members.Sum(m => m.AdjustedFitness) / totalAdjustedFitness * _hyperparameters.PopulationSize
					)
			)
			.ToList();

		int diff = _hyperparameters.PopulationSize - counts.Sum();
		if (diff != 0)
		{
			int largest = counts.IndexOf(counts.Max());
			counts[largest] += diff;
		}
		return counts;
	}
}
