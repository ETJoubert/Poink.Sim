using System;
using System.Collections.Generic;

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
}
