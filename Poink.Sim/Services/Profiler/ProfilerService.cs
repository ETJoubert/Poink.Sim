using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models;
using Poink.Sim.Repository;
using Poink.Sim.Simulation;

namespace Poink.Sim.Profiler;

internal sealed class ProfilerService : IProfilerService
{
	private const int MaxSteps = 30;

	private readonly ITickRepository _tickRepository;
	private readonly ILogger<ProfilerService> _logger;

	public ProfilerService(ITickRepository tickRepository, ILogger<ProfilerService> logger)
	{
		_tickRepository = tickRepository;
		_logger = logger;
	}

	public Task<ProfilerChunk> LoadAsync(ProfilerConfig config, DateTime beforeUtc, int barCount) =>
		Task.Run(() =>
		{
			if (barCount < 1)
				barCount = 1;
			var stepSpan = TimeSpan.FromTicks(Math.Max(config.BarSize.Ticks, 1) * barCount);

			var bars = new List<HlocAvdtBar>();
			var quartileBars = new List<QuartileBar>();
			var histograms = new List<BarPriceHistogram>();
			var tickCount = 0;
			var to = beforeUtc;
			var reachedStart = to <= config.From;

			for (var step = 0; step < MaxSteps && bars.Count < barCount && !reachedStart; step++)
			{
				var rawFrom = to - stepSpan;
				DateTime from;
				if (rawFrom <= config.From)
				{
					from = config.From;
					reachedStart = true;
				}
				else
				{
					from = new DateTime(rawFrom.Ticks - (rawFrom.Ticks % config.BarSize.Ticks), rawFrom.Kind);
				}

				var aggregator = new SimulationBarAggregator(config.BarSize, config.PriceBucketWidth);
				var quartileAggregator = new QuartileBarAggregator(config.BarSize);
				var stepBars = new List<HlocAvdtBar>();
				var stepQuartileBars = new List<QuartileBar>();
				var stepHistograms = new List<BarPriceHistogram>();

				foreach (var tick in _tickRepository.ReadTicks(config.Table, from, to))
				{
					tickCount++;
					var completed = aggregator.Feed(tick);
					if (completed is not null)
					{
						stepBars.Add(completed.Value.Bar);
						stepHistograms.Add(completed.Value.Histogram);
					}

					if (quartileAggregator.Feed(tick) is { } completedQuartile)
						stepQuartileBars.Add(completedQuartile);
				}

				if (aggregator.CurrentPartialBar is { } partial)
				{
					stepBars.Add(partial);
					stepHistograms.Add(aggregator.CurrentPartialHistogram!);
				}

				if (quartileAggregator.CurrentPartialBar is { } partialQuartile)
					stepQuartileBars.Add(partialQuartile);

				bars.InsertRange(0, stepBars);
				quartileBars.InsertRange(0, stepQuartileBars);
				histograms.InsertRange(0, stepHistograms);
				to = from;
			}

			_logger.LogInformation(
				"Profiler chunk: table={Table}, before={Before}, earliest={Earliest}, ticks={TickCount}, bars={BarCount}, reachedStart={ReachedStart}",
				config.Table,
				beforeUtc,
				to,
				tickCount,
				bars.Count,
				reachedStart
			);

			return new ProfilerChunk(bars, quartileBars, histograms, to, tickCount, reachedStart);
		});
}
