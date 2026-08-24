namespace Poink.Sim.Components.Arch;

// Areas are discovered from live DI registrations, not a fixed enum, so there is no static
// per-area CSS rule to write (unlike CodePage's concept colours). A colour is derived instead,
// deterministically, from the area's own name — the same area always lands on the same hue.
internal static class ArchAreaPalette
{
	public static string ColorFor(string area)
	{
		var hash = 0;
		unchecked
		{
			foreach (var character in area)
				hash = hash * 31 + character;
		}

		var hue = ((hash % 360) + 360) % 360;
		return $"hsl({hue}, 82%, 62%)";
	}
}
