using Microsoft.AspNetCore.Components;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;

namespace Poink.Sim.Components.Pages.Trade
{
	/// <summary>Compact marker for whether a symbol has stored tick history: DB when up to date, DB in amber when behind, a dash when none.</summary>
	public partial class SymbolCoverageBadge : ComponentBase
	{
		[Parameter, EditorRequired]
		public SymbolTickCoverage Coverage { get; set; } = null!;

		private string Label => Coverage.HasData ? "DB" : "—";

		private string Tooltip => SymbolCoverageHelper.Describe(Coverage);

		private string CssClass =>
			Coverage.Status switch
			{
				SymbolCoverageStatus.Current => "symbol-coverage-current",
				SymbolCoverageStatus.Stale => "symbol-coverage-stale",
				_ => "symbol-coverage-none",
			};
	}
}
