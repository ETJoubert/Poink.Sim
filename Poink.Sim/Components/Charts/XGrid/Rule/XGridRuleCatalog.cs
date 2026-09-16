using System.Collections.Generic;
using System.Linq;

namespace Poink.Sim.Components.Charts;

internal readonly record struct XGridRuleRow(
	string Label,
	string ColorVar,
	string? AboveKey,
	string? InKey,
	string? BelowKey
);

internal static class XGridRuleCatalog
{
	public static readonly IReadOnlyList<XGridRuleRow> Rows = BuildRows(XGridSignalCatalog.All);

	// Exposed so XGridSignalOutputTable can derive rows from a filtered subset of conditions.
	public static IReadOnlyList<XGridRuleRow> BuildRows(IEnumerable<XGridSignalDef> defs) =>
		defs.GroupBy(d => d.RowKey).Select(ToRow).ToList();

	private static XGridRuleRow ToRow(IGrouping<string, XGridSignalDef> group)
	{
		string? KeyFor(XGridSignalDirection direction) => group.FirstOrDefault(d => d.Direction == direction)?.Key;

		var first = group.First();
		return new XGridRuleRow(
			first.RowLabel,
			first.ColorVar,
			KeyFor(XGridSignalDirection.Above),
			KeyFor(XGridSignalDirection.In),
			KeyFor(XGridSignalDirection.Below)
		);
	}
}
