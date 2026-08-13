namespace Poink.Sim.Services.CTrader
{
    public enum CTraderConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Failed
    }

    public sealed record CTraderAccountInfo(long AccountId, long Login, bool IsLive);
}
