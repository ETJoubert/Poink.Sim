using System;
using System.Collections.Generic;

namespace Poink.Sim.Training;

public sealed record EpisodeTimeConstraint(
    bool Enabled, IReadOnlySet<DayOfWeek> AllowedWeekdays, TimeSpan SessionStart, TimeSpan SessionEnd)
{

    public static readonly EpisodeTimeConstraint None = new(
        false, new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>()), TimeSpan.Zero, TimeSpan.FromHours(24));


    public TimeSpan SessionLength => SessionEnd - SessionStart;

    public static string FormatClock(TimeSpan t) =>
        t >= TimeSpan.FromHours(24) ? "24:00" : t.ToString(@"hh\:mm");
}
