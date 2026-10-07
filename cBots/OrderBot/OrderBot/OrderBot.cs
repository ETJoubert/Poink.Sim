using System.Linq;

namespace cAlgo.Robots;

/// <summary>
/// Channel check for the live account: opens one market order on <c>TestSymbolName</c>, holds it for
/// <c>HoldSeconds</c>, then closes it and stops, printing each step so a broker channel block shows its error code.
/// </summary>
public partial class OrderBot : Robot
{
    private const string OrderLabel = "OrderBot.ChannelTest";

    private Position _position;

    protected override void OnStart()
    {
        Print(
            "Account {0} ({1}) at {2}, balance {3} {4}.",
            Account.Number,
            Account.IsLive ? "LIVE" : "DEMO",
            Account.BrokerName,
            Account.Balance,
            Account.Asset.Name
        );

        if (Account.IsLive && !AllowLive)
        {
            Print("Live account detected and AllowLive is false: no order placed. Set AllowLive to true to run the test.");
            Stop();
            return;
        }

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
        var result = ExecuteMarketOrder(side, symbol.Name, volume, OrderLabel);

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
    protected override void OnStop()
    {
        CloseTestPosition();
    }

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
