using Poink.Sim.Models;

namespace Poink.Sim.Tests.TestSupport;

internal static class TestBarHelper
{
	public static HlocAvdtBar Bar(double high, double low, double close, int upVolume = 0, int downVolume = 0) =>
		new(
			TestTickHelper.Origin,
			TestTickHelper.Origin,
			TestTickHelper.Origin,
			TestTickHelper.Origin,
			close,
			high,
			low,
			close,
			close,
			upVolume + downVolume,
			0,
			upVolume,
			downVolume
		);

	public static HlocAvdtBar Closing(double close) => Bar(close, close, close);
}
