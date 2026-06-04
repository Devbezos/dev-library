using DevClient.Clients;
using DevClient.Data;
using DevClient.Data.Discord;
using DevClient.Data.Fitness;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using System.Security.Authentication;

namespace DevClient;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDevClientRepositories(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IAppChannelRepository>(_ => new SqlClient(connectionString));
        services.AddSingleton<IGuildRepository>(_ => new GuildRepository(connectionString));
        services.AddSingleton<IFitnessRepository>(_ => new FitnessRepository(connectionString));
        services.AddSingleton<IJobRepository>(_ => new JobRepository(connectionString));
        services.AddSingleton<ITcgRepository>(_ => new TcgRepository(connectionString));
        services.AddSingleton<ITcgSourceUrlRepository>(_ => new TcgSourceUrlRepository(connectionString));
        services.AddSingleton<ITcgHiddenItemRepository>(_ => new TcgHiddenItemRepository(connectionString));
        services.AddSingleton<ITcgBlacklistWordRepository>(_ => new TcgBlacklistWordRepository(connectionString));
        services.AddSingleton<ITcgChannelSettingsRepository>(_ => new TcgChannelSettingsRepository(connectionString));
        services.AddSingleton<ITcgFilterSettingsRepository>(_ => new TcgFilterSettingsRepository(connectionString));
        services.AddSingleton<ITcgProductGroupRepository>(_ => new TcgProductGroupRepository(connectionString));
        services.AddSingleton<ITcgMessageStateRepository>(_ => new TcgMessageStateRepository(connectionString));
        services.AddSingleton<IPokemonCenterSecurityStateRepository>(_ => new PokemonCenterSecurityStateRepository(connectionString));
        services.AddSingleton<ITcgResaleRepository>(_ => new TcgResaleRepository(connectionString));
        return services;
    }

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
        return services;
    }
}

