using System.Collections.Generic;
using System.Linq;
using Poink.Sim.Trader.Risk;
using Xunit;

namespace Poink.Sim.Tests.Trader.Risk;

public class RiskPolicyTogglesTests
{
	private static RiskPolicyToggles Toggles(params string[] names) =>
		new(
			names.Select(name =>
				(name, $"{name} description", (IReadOnlyList<RiskPolicyParameter>)new[] { new RiskPolicyParameter("P", "P", 5, 0, 10, 1) })
			)
		);

	[Fact]
	public void Constructor_EnablesEveryPolicyAndDropsDuplicates()
	{
		var toggles = Toggles("A", "B", "A");

		Assert.Equal(new[] { "A", "B" }, toggles.OrderedNames());
		Assert.True(toggles.IsEnabled("A"));
		Assert.True(toggles.IsEnabled("B"));
	}

	[Fact]
	public void IsEnabled_UnknownPolicy_DefaultsToTrue()
	{
		Assert.True(Toggles("A").IsEnabled("Unknown"));
	}

	[Fact]
	public void Set_KnownPolicy_ChangesStateAndBumpsVersion()
	{
		var toggles = Toggles("A");
		long before = toggles.Version;

		Assert.True(toggles.Set("A", false));

		Assert.False(toggles.IsEnabled("A"));
		Assert.True(toggles.Version > before);
	}

	[Fact]
	public void Set_UnknownPolicy_ReturnsFalseWithoutBumpingVersion()
	{
		var toggles = Toggles("A");
		long before = toggles.Version;

		Assert.False(toggles.Set("Unknown", false));
		Assert.Equal(before, toggles.Version);
	}

	[Fact]
	public void Move_SwapsWithNeighbour()
	{
		var toggles = Toggles("A", "B", "C");

		Assert.True(toggles.Move("C", -1));

		Assert.Equal(new[] { "A", "C", "B" }, toggles.OrderedNames());
	}

	[Theory]
	[InlineData("A", -1)]
	[InlineData("C", 1)]
	[InlineData("Unknown", 1)]
	public void Move_OutOfRangeOrUnknown_IsIgnored(string name, int delta)
	{
		var toggles = Toggles("A", "B", "C");

		Assert.False(toggles.Move(name, delta));

		Assert.Equal(new[] { "A", "B", "C" }, toggles.OrderedNames());
	}

	[Theory]
	[InlineData(7, 7)]
	[InlineData(50, 10)]
	[InlineData(-3, 0)]
	public void SetParameter_ClampsToParameterRange(double requested, double expected)
	{
		var toggles = Toggles("A");

		Assert.True(toggles.SetParameter("A", "P", requested));

		Assert.Equal(expected, Assert.Single(toggles.ParametersFor("A")).Value);
	}

	[Fact]
	public void SetParameter_UnknownKey_ReturnsFalse()
	{
		Assert.False(Toggles("A").SetParameter("A", "Missing", 1));
		Assert.False(Toggles("A").SetParameter("Unknown", "P", 1));
	}

	[Fact]
	public void RestoreFrom_CarriesStateAndPutsNewPoliciesLast()
	{
		var previous = Toggles("A", "B", "C");
		previous.Move("C", -2);
		previous.Set("B", false);
		previous.SetParameter("C", "P", 9);
		var fresh = Toggles("A", "B", "C", "D");

		fresh.RestoreFrom(previous.States().Where(state => state.Name != "A").ToList());

		Assert.Equal(new[] { "C", "B", "A", "D" }, fresh.OrderedNames());
		Assert.False(fresh.IsEnabled("B"));
		Assert.Equal(9, Assert.Single(fresh.ParametersFor("C")).Value);
		Assert.Equal(5, Assert.Single(fresh.ParametersFor("D")).Value);
	}
}
