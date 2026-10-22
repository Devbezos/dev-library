using DevClient.Clients;
using DevClient.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevClient;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevClientClients(this IServiceCollection services)
    {
        services.AddHttpClient();
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

