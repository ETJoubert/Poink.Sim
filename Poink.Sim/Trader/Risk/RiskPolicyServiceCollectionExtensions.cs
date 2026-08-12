
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Poink.Sim.Trader.Risk;

internal static class RiskPolicyServiceCollectionExtensions
{
    // Every concrete IRiskPolicy in this assembly is discovered and registered, so a new rule needs only its class -- no wiring line.
    public static IServiceCollection AddRiskPolicies(this IServiceCollection services)
    {
        var policyTypes = typeof(IRiskPolicy).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && typeof(IRiskPolicy).IsAssignableFrom(type));

        foreach (var type in policyTypes)
            services.AddSingleton(typeof(IRiskPolicy), type);

        services.AddSingleton<IRiskPolicyRegistry, RiskPolicyRegistry>();
        services.AddSingleton<IRiskManagerFactory, RiskManagerFactory>();
        return services;
    }
}
