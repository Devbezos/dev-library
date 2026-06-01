using dev_library.Clients;
using dev_library.Data;
using dev_library.Data.Discord;
using dev_library.Data.Fitness;
using dev_refined;
using dev_refined.Clients;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using System.Security.Authentication;

namespace dev_library
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDevLibraryRepositories(this IServiceCollection services, string connectionString)
        {
            services.AddSingleton<IAppChannelRepository>(_ => new SqlClient(connectionString));
            services.AddSingleton<IGuildRepository>(_ => new GuildRepository(connectionString));
            services.AddSingleton<IFitnessRepository>(_ => new FitnessRepository(connectionString));
            services.AddSingleton<IJobRepository>(_ => new JobRepository(connectionString));
            services.AddSingleton<ITcgRepository>(_ => new TcgRepository(connectionString));
            services.AddSingleton<ITcgSourceUrlRepository>(_ => new TcgSourceUrlRepository(connectionString));
            services.AddSingleton<ITcgHiddenItemRepository>(_ => new TcgHiddenItemRepository(connectionString));
            services.AddSingleton<ITcgChannelSettingsRepository>(_ => new TcgChannelSettingsRepository(connectionString));
            services.AddSingleton<ITcgFilterSettingsRepository>(_ => new TcgFilterSettingsRepository(connectionString));
            return services;
        }

        public static IServiceCollection AddDevLibraryClients(this IServiceCollection services)
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
            return services;
        }
    }
}
