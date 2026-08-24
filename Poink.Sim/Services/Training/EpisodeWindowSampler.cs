using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Poink.Sim.Repository;

namespace Poink.Sim.Training;


internal static class EpisodeWindowSampler
{
    public static IEnumerable<DateTime> EligibleDays(DateTime from, DateTime to, EpisodeTimeConstraint constraint)
    {
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            if (!constraint.AllowedWeekdays.Contains(day.DayOfWeek))
                continue;

            var sessionStart = day + constraint.SessionStart;
            var sessionEnd = day + constraint.SessionEnd;
            if (sessionStart >= from && sessionEnd <= to)
                yield return day;
        }
    }

    public static IEnumerable<Tick> RandomWindow(
        ITickRepository tickRepository, ILogger logger, string table, DateTime from, DateTime to,
        TimeSpan fetchSpan, TimeSpan warmupSpan, Random rng, EpisodeTimeConstraint constraint) =>
        constraint.Enabled
            ? RandomConstrainedWindow(tickRepository, logger, table, from, to, fetchSpan, warmupSpan, rng, constraint)
            : RandomUnrestrictedWindow(tickRepository, logger, table, from, to, fetchSpan, rng);

    private static IEnumerable<Tick> RandomUnrestrictedWindow(
        ITickRepository tickRepository, ILogger logger, string table, DateTime from, DateTime to, TimeSpan fetchSpan, Random rng)
    {
        var latestStart = to - fetchSpan;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var start = latestStart <= from ? from : from.AddTicks((long)(rng.NextDouble() * (latestStart - from).Ticks));
            if (tickRepository.ReadTicks(table, start, start + fetchSpan).Any())
                return tickRepository.ReadTicks(table, start, start + fetchSpan);
        }
        logger.LogWarning(
            "RandomEpisodeWindow: 5 random starts in {Table} landed in empty windows; falling back to range start {From}",
            table, from);
        return tickRepository.ReadTicks(table, from, from + fetchSpan);
    }

    private static IEnumerable<Tick> RandomConstrainedWindow(
        ITickRepository tickRepository, ILogger logger, string table, DateTime from, DateTime to, TimeSpan fetchSpan,
        TimeSpan warmupSpan, Random rng, EpisodeTimeConstraint constraint)
    {
        var eligibleDays = EligibleDays(from, to, constraint).ToList();
        if (eligibleDays.Count == 0)
        {
            logger.LogWarning(
                "RandomConstrainedEpisodeWindow: no eligible day in {Table} for the configured weekday/session filters", table);
            return Array.Empty<Tick>();
        }

        for (int attempt = 0; attempt < 5; attempt++)
        {
            var day = eligibleDays[rng.Next(eligibleDays.Count)];
            var start = day + constraint.SessionStart - warmupSpan;
            if (tickRepository.ReadTicks(table, start, start + fetchSpan).Any())
                return tickRepository.ReadTicks(table, start, start + fetchSpan);
        }
        logger.LogWarning(
            "RandomConstrainedEpisodeWindow: 5 random starts in {Table} landed in empty windows; falling back to the first eligible day's session",
            table);
        var fallbackStart = eligibleDays[0] + constraint.SessionStart - warmupSpan;
        return tickRepository.ReadTicks(table, fallbackStart, fallbackStart + fetchSpan);
    }
}
