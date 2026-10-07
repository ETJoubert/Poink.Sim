namespace Poink.CTrader
{
    /// <summary>
    /// Thrown when cTrader responds with a ProtoOAErrorRes (or, for a trading request, a ProtoOAOrderErrorEvent) instead of the expected message.
    /// </summary>
    public class CTraderApiException : Exception
    {
        public CTraderApiException(string errorCode, string description) : base($"{errorCode}: {description}")
        {
            ErrorCode = errorCode;
            Description = description;
        }

        public string ErrorCode { get; }
        public string Description { get; }
    }
}
