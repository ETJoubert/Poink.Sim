using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Components.Pages.Trade.Helpers;
using Poink.Sim.Components.Pages.Trade.Models;
using Poink.Sim.Models;
using Poink.Sim.Services.CTrader;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.Components.Pages.Trade.Helpers;

public class SymbolFilterHelperTests
{
	private static readonly CTraderSymbolInfo[] Symbols = [Symbol(1, "EURUSD"), Symbol(2, "GBPUSD"), Symbol(3, "USDJPY")];

	private static readonly IReadOnlyDictionary<long, SymbolCategoryModel> NoCategories = new Dictionary<long, SymbolCategoryModel>();

	private static readonly IReadOnlyDictionary<string, SymbolTickCoverage> Coverage = SymbolCoverageHelper.Build(
		[("live_ticks_eurusd", 50, TestTickHelper.Origin, TestTickHelper.Origin.AddHours(1)), ("live_ticks_usdjpy", 0, null, null)],
		TestTickHelper.Origin.AddDays(1)
	);

	private static CTraderSymbolInfo Symbol(long id, string name) => new(id, name, true, 0, 0, 0, name, id);

	private static SymbolFilterCriteria Criteria(bool onlyWithData, string name = "") =>
		new(name, new HashSet<SymbolCategoryBucket>(), false, new HashSet<string>(), onlyWithData);

	[Fact]
	public void Filter_OnlyWithData_KeepsSymbolsWithStoredTicks()
	{
		var result = SymbolFilterHelper.Filter(Symbols, Criteria(onlyWithData: true), NoCategories, Coverage);

		Assert.Equal(new[] { "EURUSD" }, result.Select(s => s.SymbolName));
	}

	[Fact]
	public void Filter_DataFilterOff_KeepsEverySymbol()
	{
		Assert.Equal(3, SymbolFilterHelper.TotalMatchCount(Symbols, Criteria(onlyWithData: false), NoCategories, Coverage));
	}

	[Fact]
	public void Filter_DataFilterCombinesWithNameFilter()
	{
		Assert.Equal(0, SymbolFilterHelper.TotalMatchCount(Symbols, Criteria(onlyWithData: true, name: "GBP"), NoCategories, Coverage));
	}
}
