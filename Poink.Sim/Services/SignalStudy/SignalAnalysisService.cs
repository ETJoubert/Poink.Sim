using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;

namespace Poink.Sim.SignalStudy;

internal sealed class SignalAnalysisService : ISignalAnalysisService
{
	private readonly ISignalDatasetRepository _repository;
	private readonly ILogger<SignalAnalysisService> _logger;
	private readonly object _cacheLock = new();
	private (long DatasetId, SignalPointSeries Series)? _cached;

	public SignalAnalysisService(ISignalDatasetRepository repository, ILogger<SignalAnalysisService> logger)
	{
		_repository = repository;
		_logger = logger;
	}

	public Task<SignalAnalysisResult> AnalyzeAsync(SignalDatasetInfo dataset, SignalAnalysisOptions options, CancellationToken token = default) =>
		Task.Run(() => Analyze(dataset, options, token), token);

	private SignalAnalysisResult Analyze(SignalDatasetInfo dataset, SignalAnalysisOptions options, CancellationToken token)
	{
		var stopwatch = Stopwatch.StartNew();
		_logger.LogInformation("Signal analysis started: dataset={Id} '{Name}', options={@Options}", dataset.Id, dataset.Name, options);

		var series = SeriesFor(dataset.Id);
		token.ThrowIfCancellationRequested();
		var labels = SignalLabels.Compute(series, options, token);
		var analyzer = new SignalStateAnalyzer(series, labels, options, dataset.PipSize);
		var baseline = analyzer.Baseline();

		var states = dataset
			.Catalog.SelectMany(entry => SlotsOf(entry).Select(slot => new SignalStateRef(entry, slot)))
			.ToList();
		var singles = Ranked(states.AsParallel().WithCancellation(token).Select(state => (state, stats: analyzer.Single(state))), options);

		var candidates = singles.Take(Math.Max(0, options.PairCandidates)).Select(single => single.state).ToList();
		var pairs = Ranked(
			candidates
				.SelectMany((first, i) => candidates.Skip(i + 1).Where(second => second.Entry.Index != first.Entry.Index).Select(second => (first, second)))
				.AsParallel()
				.WithCancellation(token)
				.Select(pair => (state: pair.first, stats: analyzer.Pair(pair.first, pair.second))),
			options
		);

		var result = new SignalAnalysisResult(
			dataset.Id,
			dataset.Name,
			options,
			series.Count,
			baseline,
			singles.Select(single => single.stats).ToList(),
			pairs.Select(pair => pair.stats).ToList(),
			stopwatch.Elapsed
		);
		_logger.LogInformation(
			"Signal analysis ended: dataset={Id}, points={Points}, rankedSingles={Singles}, rankedPairs={Pairs}, elapsed={Elapsed}",
			dataset.Id,
			series.Count,
			result.Singles.Count,
			result.Pairs.Count,
			stopwatch.Elapsed
		);
		return result;
	}

	// Ranked by the t-statistic of the better side's expectancy, so a small edge seen many times beats a big one seen rarely.
	private static List<(SignalStateRef state, SignalStateStats stats)> Ranked(
		IEnumerable<(SignalStateRef state, SignalStateStats stats)> analysed,
		SignalAnalysisOptions options
	) =>
		analysed
			.Where(item => item.stats.Entries >= options.MinEntries)
			.OrderByDescending(item => item.stats.BestTStat ?? double.MinValue)
			.ToList();

	private static IEnumerable<SignalStateSlot> SlotsOf(SignalCatalogEntry entry)
	{
		if (entry.AboveKey is not null)
			yield return SignalStateSlot.Above;
		yield return SignalStateSlot.In;
		if (entry.BelowKey is not null)
			yield return SignalStateSlot.Below;
	}

	// The last dataset's points stay in memory, so re-running with other options does not read them again.
	private SignalPointSeries SeriesFor(long datasetId)
	{
		lock (_cacheLock)
		{
			if (_cached is { } cached && cached.DatasetId == datasetId)
				return cached.Series;
		}

		var series = SignalPointSeries.From(_repository.ReadPoints(datasetId));
		lock (_cacheLock)
		{
			_cached = (datasetId, series);
		}
		return series;
	}
}
