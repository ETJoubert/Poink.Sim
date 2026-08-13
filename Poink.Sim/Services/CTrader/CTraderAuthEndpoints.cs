using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Poink.CTrader;
using System;
using System.Threading.Tasks;

namespace Poink.Sim.Services.CTrader
{
    public static class CTraderAuthEndpoints
    {
        /// <summary>
        /// Maps the OAuth redirect handler at the path of the configured cTrader RedirectUri, so cTrader's
        /// post-login redirect lands here carrying a "code" query parameter. The code is exchanged for a
        /// token server-side, then the browser is sent back to the Trade page. No mapping happens when the
        /// RedirectUri is unset or not an absolute URI.
        /// </summary>
        public static IEndpointRouteBuilder MapCTraderAuthCallback(this IEndpointRouteBuilder endpoints)
        {
            var options = endpoints.ServiceProvider.GetRequiredService<IOptions<CTraderApiOptions>>().Value;

            if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out var redirectUri))
            {
                return endpoints;
            }

            endpoints.MapGet(redirectUri.AbsolutePath, async (HttpContext context, ICTraderSessionService session) =>
            {
                var code = context.Request.Query["code"].ToString();

                if (!string.IsNullOrWhiteSpace(code))
                {
                    await session.CompleteLoginAsync(code, context.RequestAborted);
                }

                return Results.Redirect("/trade");
            });

            return endpoints;
        }
    }
}
