using System.Linq;

namespace cAlgo.Robots;

/// <summary>
/// Channel check: opens one market order on <c>TestSymbolName</c>, holds it for <c>HoldSeconds</c>, then closes it
/// and stops, printing each step so a broker channel block shows its error code.
/// </summary>
public partial class OrderBot
{
	private const string ChannelTestLabel = "OrderBot.ChannelTest";

	private Position _position;

	private void RunChannelTest()
	{
		var symbol = Symbols.GetSymbol(TestSymbolName);
		if (symbol is null)
		{
			Print("Symbol '{0}' was not found on this account.", TestSymbolName);
			Stop();
			return;
		}

		var volume = VolumeInUnits > 0 ? symbol.NormalizeVolumeInUnits(VolumeInUnits) : symbol.VolumeInUnitsMin;
		var side = IsBuy ? TradeType.Buy : TradeType.Sell;

		Print("Placing {0} {1} {2} unit(s) (symbol minimum {3}).", side, symbol.Name, volume, symbol.VolumeInUnitsMin);
		var result = ExecuteMarketOrder(side, symbol.Name, volume, ChannelTestLabel);

		if (!result.IsSuccessful)
		{
			Print("OPEN FAILED: {0}", result.Error);
			Stop();
			return;
		}

		_position = result.Position;
		Print("OPEN OK: position {0} at {1}. Closing in {2}s.", _position.Id, _position.EntryPrice, HoldSeconds);
		Timer.Start(TimeSpan.FromSeconds(HoldSeconds));
	}

	protected override void OnTimer()
	{
		Timer.Stop();
		CloseTestPosition();
		Stop();
	}

	// Stopping the bot early must not strand a live position opened by the test.
	private void CloseTestPosition()
	{
		if (_position is null || Positions.FirstOrDefault(p => p.Id == _position.Id) is null)
			return;

		var result = ClosePosition(_position);
		Print(
			result.IsSuccessful ? "CLOSE OK: position {0} closed, net profit {1}." : "CLOSE FAILED: position {0}: {2}",
			_position.Id,
			result.IsSuccessful ? _position.NetProfit : 0,
			result.Error
		);
		_position = null;
	}
}
