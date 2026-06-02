using Microsoft.Extensions.DependencyInjection;

namespace dev_client;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevApiClient(this IServiceCollection services, string baseAddress)
    {
        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new ArgumentException("A dev API base address is required.", nameof(baseAddress));

        return services.AddDevApiClient(new Uri(baseAddress.TrimEnd('/') + "/"));
    }

    public static IServiceCollection AddDevApiClient(this IServiceCollection services, Uri baseAddress)
    {
        services.AddHttpClient<IDevApiClient, DevApiClient>(client =>
        {
            client.BaseAddress = baseAddress;
        });

        return services;
    }
}
