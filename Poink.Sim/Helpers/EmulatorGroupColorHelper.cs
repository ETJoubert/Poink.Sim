namespace Poink.Sim.Helpers;

/// <summary>
/// Colours bot groups from the page's validated categorical palette (<c>--emu-group-1</c> to <c>--emu-group-8</c>), in
/// fixed order by the group's position. Past the palette the colours repeat, so a group's name is always shown beside its
/// colour and identity never rests on colour alone.
/// </summary>
internal static class EmulatorGroupColorHelper
{
	public const int PaletteSize = 8;

	public static string ColorVar(int groupIndex) => $"var(--emu-group-{groupIndex % PaletteSize + 1})";
}
