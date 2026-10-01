using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Supplies one episode's worth of tick data for training. EpisodeSamplingMode picks how: the entire
/// selected range, an explicit Lookback Window, or (the default) a random window sized/positioned from
/// EpisodeTimingOptionsModel, honoring its weekday/session restriction. Each is preceded by a feature
/// window of preloaded history (VisibleBars) that is never trained on. Continuous/streaming feeding is
/// a separate mode, not implemented here yet.
/// </summary>
internal sealed class DataProvider
{
	private const int MaxRandomWindowAttempts = 5;

	private readonly ITickRepository _tickRepository;
	private readonly ILogger<DataProvider> _logger;

	public DataProvider(ITickRepository tickRepository, ILogger<DataProvider> logger)
	{
		_tickRepository = tickRepository;
		_logger = logger;
	}

	public EpisodeData GetEpisodeWindow(
		DataOptionsModel data,
		EpisodeTimingOptionsModel episodeTiming,
		int barWidthSeconds,
		Random random
	)
	{
		var featureSpan = TimeSpan.FromSeconds((double)episodeTiming.VisibleBars * barWidthSeconds);
		switch (episodeTiming.SamplingMode)
		{
			case EpisodeSamplingMode.EntireSet:
				return Load(data.Table, data.From - featureSpan, data.To, data.From);

			case EpisodeSamplingMode.WindowSelected:
				if (episodeTiming.WindowStart is not { } windowStart || episodeTiming.WindowEnd is not { } windowEnd)
				{
					_logger.LogWarning("DataProvider: no Lookback Window selected for {Table}, skipping episode", data.Table);
					return EpisodeData.Empty;
				}
				return Load(data.Table, windowStart - featureSpan, windowEnd, windowStart);

			default:
				var trainingSpan = TimeSpan.FromSeconds((double)TrainingBars(episodeTiming, barWidthSeconds) * barWidthSeconds);
				return episodeTiming.RestrictEpisodeTiming
					? RandomConstrainedWindow(data, episodeTiming, featureSpan, trainingSpan, random)
					: RandomUnrestrictedWindow(data, featureSpan, trainingSpan, random);
		}
	}

	private EpisodeData Load(string table, DateTime loadFrom, DateTime to, DateTime trainingStart)
	{
		var ticks = _tickRepository.ReadTicks(table, loadFrom, to).ToList();
		var trainingStartIndex = ticks.FindIndex(t => t.Timestamp >= trainingStart);
		return trainingStartIndex < 0 ? EpisodeData.Empty : new EpisodeData(ticks, trainingStartIndex);
	}

	// Mirrors TrainPageV2's own EffectiveTrainingBars calc, since this is the same question
	// phrased for data-fetching rather than display: how many bars does one episode train on.
	private static int TrainingBars(EpisodeTimingOptionsModel episodeTiming, int barWidthSeconds)
	{
		if (!episodeTiming.RestrictEpisodeTiming)
			return episodeTiming.TrainingBars;

		var sessionEnd = episodeTiming.SessionEnd ?? TimeSpan.FromHours(24);
		return (int)((sessionEnd - episodeTiming.SessionStart).TotalSeconds / barWidthSeconds);
	}

	private EpisodeData RandomUnrestrictedWindow(DataOptionsModel data, TimeSpan featureSpan, TimeSpan trainingSpan, Random random)
	{
		var latestStart = data.To - trainingSpan;
		for (var attempt = 0; attempt < MaxRandomWindowAttempts; attempt++)
		{
			var start =
				latestStart <= data.From
					? data.From
					: data.From.AddTicks((long)(random.NextDouble() * (latestStart - data.From).Ticks));
			var episode = Load(data.Table, start - featureSpan, start + trainingSpan, start);
			if (episode.TrainingTickCount > 0)
				return episode;
		}

		_logger.LogWarning(
			"DataProvider: {Attempts} random starts in {Table} landed in empty windows; falling back to range start {From}",
			MaxRandomWindowAttempts,
			data.Table,
			data.From
		);
		return Load(data.Table, data.From - featureSpan, data.From + trainingSpan, data.From);
	}

	private EpisodeData RandomConstrainedWindow(
		DataOptionsModel data,
		EpisodeTimingOptionsModel episodeTiming,
		TimeSpan featureSpan,
		TimeSpan trainingSpan,
		Random random
	)
	{
		var eligibleDays = EligibleDays(data.From, data.To, episodeTiming).ToList();
		if (eligibleDays.Count == 0)
		{
			_logger.LogWarning(
				"DataProvider: no eligible day in {Table} for the configured weekday/session filters",
				data.Table
			);
			return EpisodeData.Empty;
		}

		var sessionStart = episodeTiming.SessionStart;
		for (var attempt = 0; attempt < MaxRandomWindowAttempts; attempt++)
		{
			var start = eligibleDays[random.Next(eligibleDays.Count)] + sessionStart;
			var episode = Load(data.Table, start - featureSpan, start + trainingSpan, start);
			if (episode.TrainingTickCount > 0)
				return episode;
		}

		_logger.LogWarning(
			"DataProvider: {Attempts} random starts in {Table} landed in empty windows; falling back to the first eligible day's session",
			MaxRandomWindowAttempts,
			data.Table
		);
		var fallbackStart = eligibleDays[0] + sessionStart;
		return Load(data.Table, fallbackStart - featureSpan, fallbackStart + trainingSpan, fallbackStart);
	}

	private static IEnumerable<DateTime> EligibleDays(DateTime from, DateTime to, EpisodeTimingOptionsModel episodeTiming)
	{
		var allowedWeekdays = episodeTiming.AllowedWeekdays ?? Enum.GetValues<DayOfWeek>();
		var sessionEnd = episodeTiming.SessionEnd ?? TimeSpan.FromHours(24);

		for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
		{
			if (!allowedWeekdays.Contains(day.DayOfWeek))
				continue;

			var sessionStart = day + episodeTiming.SessionStart;
			var sessionEndAbsolute = day + sessionEnd;
			if (sessionStart >= from && sessionEndAbsolute <= to)
				yield return day;
		}
	}
}
