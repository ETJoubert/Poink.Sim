using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAPI.Net.Auth;

namespace Poink.CTrader
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Binds <see cref="CTraderApiOptions"/> from configuration and registers an <see cref="App"/>
        /// built from those options. Callers must separately register an <see cref="ICTraderTokenStore"/>
        /// and an <see cref="ICTraderAuthUiService"/> for their platform.
        /// </summary>
        public static IServiceCollection AddCTraderOpenApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CTraderApiOptions>(configuration.GetSection(CTraderApiOptions.SectionName));

            services.AddTransient(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CTraderApiOptions>>().Value;
                return new App(options.ClientId, options.ClientSecret, options.RedirectUri);
            });

            return services;
        }
    }
}
