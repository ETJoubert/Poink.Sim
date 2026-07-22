// ── Categorized stat display shapes ──────────────────────────────────────
//  Generic label/value pairs (StatItem) grouped under a category title
//  (StatGroup) -- feeds StatisticsPanel, which renders one section per
//  group instead of a single flat table. Emphasis is a display hint only
//  (StatisticsPanel maps it to its own scoped CSS classes). Public (unlike
//  most of this internal Models folder) because they're bound directly to
//  StatisticsPanel's [Parameter], and Razor-generated component classes are
//  always public -- an internal type there would trip CS0053.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;

namespace Poink.Sim.Models;

public enum StatEmphasis { None, Positive, Negative }

public sealed record StatItem(string Label, string Value, StatEmphasis Emphasis = StatEmphasis.None);

public sealed record StatGroup(string Title, IReadOnlyList<StatItem> Items);
