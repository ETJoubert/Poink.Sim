namespace Poink.Sim.Components.Arch;

internal static class ArchAreaPalette
{
	/// <summary>
	/// Areas are discovered from live DI registrations, not a fixed enum, so there's no static per-area
	/// CSS rule to write; the colour is derived deterministically from the area's own name instead.
	/// </summary>
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
