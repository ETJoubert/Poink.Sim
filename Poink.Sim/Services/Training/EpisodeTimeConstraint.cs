using System;
using System.Collections.Generic;

namespace Poink.Sim.Training;

// Confines which days/times of day RlTrainingService.RandomEpisodeWindow may
// pick an episode's start from. Enabled=false (None, the default) preserves
// the original fully-random-across-range behavior -- no day/session
// filtering, an episode may start anywhere and cross midnight freely.
//
// AllowedWeekdays and the SessionStart..SessionEnd time-of-day window are
// independent, combinable filters rather than mutually exclusive modes: a
// pure "Days" restriction is just every weekday allowed with the session left
// at its full 00:00-24:00 default (episodes simply never cross midnight); a
// "Weeks" restriction narrows AllowedWeekdays (e.g. Mon-Fri); a "Sessions"
// restriction narrows SessionStart/SessionEnd (e.g. 13:00-17:00); any
// combination of the two applies together. Once Enabled, an episode's whole
// fetchSpan must land inside a single calendar day's session window -- see
// EpisodeWindowSampler.EligibleDays.
//
// Public (not internal like most of this namespace) because it crosses
// TrainingPreview's [Parameter] boundary -- Blazor's generated component
// partial class is public, so a parameter can't be typed with a less-
// accessible type (same reason BrainRecipe/RlEpisodeWindow are public).
public sealed record EpisodeTimeConstraint(
    bool Enabled, IReadOnlySet<DayOfWeek> AllowedWeekdays, TimeSpan SessionStart, TimeSpan SessionEnd)
{
    // The switched-off default -- every weekday, full 00:00-24:00 session, so
    // callers that never opt in keep the pre-existing unrestricted sampling.
    public static readonly EpisodeTimeConstraint None = new(
        false, new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>()), TimeSpan.Zero, TimeSpan.FromHours(24));

    // What an episode's fetchSpan must fit inside, once Enabled.
    public TimeSpan SessionLength => SessionEnd - SessionStart;

    // "HH:mm" clock-time display for a session bound -- TimeSpan's own "hh\:mm"
    // custom format wraps a 24:00:00 value to Days=1/Hours=0 (renders "00:00",
    // indistinguishable from midnight-start), so 24:00 is special-cased here
    // instead. Shared by RlTrainingService's log line and the Train page's
    // session inputs so both display the same value the same way.
    public static string FormatClock(TimeSpan t) =>
        t >= TimeSpan.FromHours(24) ? "24:00" : t.ToString(@"hh\:mm");
}
