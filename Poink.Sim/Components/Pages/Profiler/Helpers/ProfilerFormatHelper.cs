using System;

namespace Poink.Sim.Components.Profiler
{
	internal static class ProfilerFormatHelper
	{
		public static string FormatSpan(TimeSpan span) =>
			span switch
			{
				{ TotalDays: >= 1 } => $"{(int)span.TotalDays}d {span.Hours}h",
				{ TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
				{ TotalMinutes: >= 1 } => $"{(int)span.TotalMinutes}m",
				_ => $"{(int)span.TotalSeconds}s",
			};
	}
}
