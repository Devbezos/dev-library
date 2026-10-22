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
        // One cache of third-party answers per app (a test host gets its own).
        services.AddSingleton(_ => new ApiResponseCache());
        services.AddSingleton<IBattleNetClient>(sp => new BattleNetClient(
            settings: sp.GetService<IConfiguration>()?.GetSection("battleNet").Get<BattleNetSettings>(),
            cache: sp.GetRequiredService<ApiResponseCache>()));
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
            sp.GetService<IConfiguration>()?.GetSection("warcraftLogs").Get<WarcraftLogsSettings>(), sp.GetRequiredService<ApiResponseCache>()));
        return services;
    }
}

