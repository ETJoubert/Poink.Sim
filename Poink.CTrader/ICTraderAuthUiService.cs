namespace Poink.CTrader
{
    /// <summary>
    /// Captures the OAuth authorization code from cTrader's login flow. The redirect URI never
    /// needs to be reachable - implementations intercept the browser's attempted navigation to it
    /// (e.g. a native WebView's navigating event) and read the "code" query parameter off that
    /// attempt before it is ever requested over the network.
    /// </summary>
    public interface ICTraderAuthUiService
    {
        Task<string> CaptureAuthorizationCodeAsync(Uri authorizationUri, string redirectUri, CancellationToken cancellationToken = default);
    }
}
