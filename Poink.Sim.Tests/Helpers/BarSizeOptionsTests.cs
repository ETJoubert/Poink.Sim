using Poink.Sim.Helpers;
using Xunit;

namespace Poink.Sim.Tests.Helpers;

public class BarSizeOptionsTests
{
	[Theory]
	[InlineData(1, "1s")]
	[InlineData(32, "32s")]
	[InlineData(60, "1m")]
	[InlineData(240, "4m")]
	[InlineData(3600, "60m (~1h)")]
	[InlineData(86400, "1440m (1d)")]
	public void Label_UsesSecondsMinutesAndHourHints(int seconds, string expected)
	{
		Assert.Equal(expected, BarSizeOptions.Label(seconds));
	}

	[Fact]
	public void Values_AreStrictlyAscending()
	{
		for (var i = 1; i < BarSizeOptions.Values.Count; i++)
			Assert.True(BarSizeOptions.Values[i] > BarSizeOptions.Values[i - 1]);
	}
}
