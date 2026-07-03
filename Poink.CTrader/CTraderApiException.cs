namespace Poink.CTrader
{
    /// <summary>
    /// Thrown when cTrader responds with a ProtoOAErrorRes instead of the expected message.
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
