using DevClient.Clients;
using DevClient.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using System.Security.Authentication;

namespace DevClient;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevClientClients(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddHttpClient("raidbots")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            });
        services.AddSingleton<IBattleNetClient, BattleNetClient>();
        services.AddSingleton<IWoWAuditClient, WoWAuditClient>();
        services.AddSingleton<IRaiderIoClient, RaiderIoClient>();
        services.AddSingleton<IWoWUtilsClient, WoWUtilsClient>();
        services.AddSingleton<RaidBotsClient>();
        services.AddSingleton<IDiscordClient, DiscordClient>();
        services.AddSingleton<GoogleSheetsClient>();
        services.AddSingleton<RealmClient>();
        services.AddSingleton<RefinedClient>();
        services.AddSingleton<IEbayClient, EbayClient>();
        services.AddSingleton<IWarcraftLogsClient>(sp => new WarcraftLogsClient(
            sp.GetService<IConfiguration>()?.GetSection("warcraftLogs").Get<WarcraftLogsSettings>()));
        return services;
    }
}

