
using System.Collections.Generic;

namespace Poink.Sim.Models;

public enum StatEmphasis { None, Positive, Negative }

public sealed record StatItem(string Label, string Value, StatEmphasis Emphasis = StatEmphasis.None);

public sealed record StatGroup(string Title, IReadOnlyList<StatItem> Items);
