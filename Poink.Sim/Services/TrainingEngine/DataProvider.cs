using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Poink.Sim.Models.TrainingModels;
using Poink.Sim.Repository;

namespace Poink.Sim.Services.TrainingEngine;

/// <summary>
/// Supplies one episode's worth of tick data for training -- resolves DataOptionsModel's range and
/// EpisodeTimingOptionsModel's weekday/session restriction into an actual window, then reads it from
/// ITickRepository. Continuous/streaming feeding is a separate mode, not implemented here yet.
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

	public IReadOnlyList<Tick> GetEpisodeWindow(
		DataOptionsModel data,
		EpisodeTimingOptionsModel episodeTiming,
		int barWidthSeconds,
		Random random
	)
	{
		var fetchSpan = TimeSpan.FromSeconds((double)EpisodeBars(episodeTiming, barWidthSeconds) * barWidthSeconds);

		return episodeTiming.RestrictEpisodeTiming
			? RandomConstrainedWindow(data, episodeTiming, fetchSpan, random)
			: RandomUnrestrictedWindow(data, fetchSpan, random);
	}

	// Mirrors TrainPageV2's own EffectiveTrainingBars/EpisodeBars calc, since this is the same
	// question phrased for data-fetching rather than display: how many bars does one episode span.
	private static int EpisodeBars(EpisodeTimingOptionsModel episodeTiming, int barWidthSeconds)
	{
		if (!episodeTiming.RestrictEpisodeTiming)
			return episodeTiming.VisibleBars + episodeTiming.TrainingBars;

		var sessionEnd = episodeTiming.SessionEnd ?? TimeSpan.FromHours(24);
		var trainingBars = (int)((sessionEnd - episodeTiming.SessionStart).TotalSeconds / barWidthSeconds);
		return episodeTiming.VisibleBars + trainingBars;
	}

	private IReadOnlyList<Tick> RandomUnrestrictedWindow(DataOptionsModel data, TimeSpan fetchSpan, Random random)
	{
		var latestStart = data.To - fetchSpan;
		for (var attempt = 0; attempt < MaxRandomWindowAttempts; attempt++)
		{
			var start =
				latestStart <= data.From
					? data.From
					: data.From.AddTicks((long)(random.NextDouble() * (latestStart - data.From).Ticks));
			var ticks = _tickRepository.ReadTicks(data.Table, start, start + fetchSpan).ToList();
			if (ticks.Count > 0)
				return ticks;
		}

		_logger.LogWarning(
			"DataProvider: {Attempts} random starts in {Table} landed in empty windows; falling back to range start {From}",
			MaxRandomWindowAttempts,
			data.Table,
			data.From
		);
		return _tickRepository.ReadTicks(data.Table, data.From, data.From + fetchSpan).ToList();
	}

	private IReadOnlyList<Tick> RandomConstrainedWindow(
		DataOptionsModel data,
		EpisodeTimingOptionsModel episodeTiming,
		TimeSpan fetchSpan,
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
			return [];
		}

		var sessionStart = episodeTiming.SessionStart;
		for (var attempt = 0; attempt < MaxRandomWindowAttempts; attempt++)
		{
			var day = eligibleDays[random.Next(eligibleDays.Count)];
			var start = day + sessionStart;
			var ticks = _tickRepository.ReadTicks(data.Table, start, start + fetchSpan).ToList();
			if (ticks.Count > 0)
				return ticks;
		}

		_logger.LogWarning(
			"DataProvider: {Attempts} random starts in {Table} landed in empty windows; falling back to the first eligible day's session",
			MaxRandomWindowAttempts,
			data.Table
		);
		var fallbackStart = eligibleDays[0] + sessionStart;
		return _tickRepository.ReadTicks(data.Table, fallbackStart, fallbackStart + fetchSpan).ToList();
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
