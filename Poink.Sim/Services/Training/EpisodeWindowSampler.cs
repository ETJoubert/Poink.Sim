using System;
using System.Collections.Generic;

namespace Poink.Sim.Training;

// Shared "which calendar days qualify" logic for EpisodeTimeConstraint --
// used both by RlTrainingService's actual training sampling
// (RandomConstrainedEpisodeWindow) and by TrainingPreview's illustrative
// draw, so a restricted preview always shows a day/session the real run
// could actually land on instead of drifting out of sync with a second,
// slightly different implementation.
internal static class EpisodeWindowSampler
{
    // Every calendar day between from.Date and to.Date (inclusive) whose
    // weekday is allowed and whose FULL session (day+SessionStart to
    // day+SessionEnd) fits entirely inside [from, to] -- a partially-clipped
    // session isn't good enough, since an episode is meant to span the
    // *whole* session, not a truncated piece of it. Pure in-memory date
    // arithmetic, no DB calls, so even a multi-year range is cheap to
    // enumerate fully upfront.
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
