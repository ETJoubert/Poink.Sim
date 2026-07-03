namespace Poink.CTrader
{
    public record CTraderApiOptions
    {
        public const string SectionName = "CTrader";

        public string ClientId { get; init; } = string.Empty;
        public string ClientSecret { get; init; } = string.Empty;
        public string RedirectUri { get; init; } = string.Empty;
        public bool UseLiveEnvironment { get; init; } = false;
    }
}
