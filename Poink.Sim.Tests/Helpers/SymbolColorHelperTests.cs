using System.Linq;
using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class SymbolColorHelperTests
{
	[Fact]
	public void Assign_GivesEachTableAColourBySelectionOrder()
	{
		var colors = SymbolColorHelper.Assign(["ticks_a", "ticks_b"]);

		Assert.Equal("--color-primary", colors["ticks_a"]);
		Assert.Equal("--color-badge-1", colors["ticks_b"]);
	}

	[Fact]
	public void Assign_IgnoresDuplicateTables()
	{
		var colors = SymbolColorHelper.Assign(["ticks_a", "ticks_a", "ticks_b"]);

		Assert.Equal(2, colors.Count);
		Assert.Equal("--color-badge-1", colors["ticks_b"]);
	}

	[Fact]
	public void Assign_WrapsAroundThePaletteForManyTables()
	{
		var tables = Enumerable.Range(0, 9).Select(i => $"ticks_{i}").ToList();

		var colors = SymbolColorHelper.Assign(tables);

		Assert.Equal(colors["ticks_0"], colors["ticks_8"]);
	}

	[Fact]
	public void ColorVarFor_UnselectedTable_UsesTheFallback()
	{
		var colors = SymbolColorHelper.Assign(["ticks_a"]);

		Assert.Equal(SymbolColorHelper.FallbackColorVar, SymbolColorHelper.ColorVarFor(colors, "ticks_z"));
	}
}
