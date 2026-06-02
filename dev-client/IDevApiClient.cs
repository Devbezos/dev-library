using dev_library.Data;
using dev_library.Data.Discord;
using dev_library.Data.Fitness;

namespace dev_client;

public interface IDevApiClient
{
    Task<IReadOnlyList<GuildSettingsDto>> GetGuilds(CancellationToken cancellationToken = default);
    Task CreateGuild(GuildSettingsDto guild, CancellationToken cancellationToken = default);
    Task UpdateGuild(string name, GuildSettingsDto guild, CancellationToken cancellationToken = default);
    Task DeleteGuild(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiChannel>> GetChannels(CancellationToken cancellationToken = default);
    Task DeleteChannel(string channelId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledJob>> GetJobs(CancellationToken cancellationToken = default);
    Task UpdateJob(string name, ScheduledJob job, CancellationToken cancellationToken = default);
    Task ResetJobLastRun(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FitnessApiUser>> GetFitnessUsers(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FitnessApiUser>> GetAllFitnessUsers(CancellationToken cancellationToken = default);
    Task CreateFitnessUser(FitnessUserUpsert user, CancellationToken cancellationToken = default);
    Task UpdateFitnessUser(string username, FitnessUserUpsert user, CancellationToken cancellationToken = default);
    Task DeleteFitnessUser(string username, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FitnessPost>> GetFitnessPosts(CancellationToken cancellationToken = default);
    Task DeleteFitnessPost(int id, CancellationToken cancellationToken = default);
    Task<OAuthInviteUrl> GetFitnessOAuthInviteUrl(CancellationToken cancellationToken = default);
    Task<DailyFitnessResult> GetDailyFitness(string? username = null, CancellationToken cancellationToken = default);
    Task<WeeklyFitnessResult> GetWeeklyFitness(string? username = null, CancellationToken cancellationToken = default);
    Task<NutritionResult> GetNutrition(string? username = null, DateOnly? date = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TcgResult>> GetPokemonTcgResults(CancellationToken cancellationToken = default);
    Task<TcgRunResult> RunPokemonTcg(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TcgResult>> GetGundamTcgResults(CancellationToken cancellationToken = default);
    Task<TcgRunResult> RunGundamTcg(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TcgResult>> GetTcgPreorders(string game, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TcgSourceUrl>> GetTcgSourceUrls(string? game = null, string? store = null, CancellationToken cancellationToken = default);
    Task<int> CreateTcgSourceUrl(TcgSourceUrlCreate sourceUrl, CancellationToken cancellationToken = default);
    Task UpdateTcgSourceUrl(int id, string url, CancellationToken cancellationToken = default);
    Task DeleteTcgSourceUrl(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TcgHiddenItem>> GetTcgHiddenItems(string? game = null, CancellationToken cancellationToken = default);
    Task HideTcgItem(TcgHiddenItemCreate item, CancellationToken cancellationToken = default);
    Task UnhideTcgItem(string game, string store, string productName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TcgBlacklistWord>> GetTcgBlacklistWords(string? game = null, CancellationToken cancellationToken = default);
    Task AddTcgBlacklistWord(TcgBlacklistWordCreate word, CancellationToken cancellationToken = default);
    Task DeleteTcgBlacklistWord(string game, string word, CancellationToken cancellationToken = default);
    Task<TcgChannelSettings> GetTcgChannelSettings(CancellationToken cancellationToken = default);
    Task UpdateTcgChannelSettings(TcgChannelSettings settings, CancellationToken cancellationToken = default);
    Task<TcgChannel> GetTcgChannel(string game, CancellationToken cancellationToken = default);
    Task UpdateTcgChannel(string game, string channelId, CancellationToken cancellationToken = default);
    Task UpdateTcgNotificationUserIds(string game, IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TcgProductGroupResult>> GetTcgProductGroups(string game, CancellationToken cancellationToken = default);
    Task UpdateTcgProductGroupMarketPrices(string groupKey, TcgProductGroupMarketPrices prices, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GuildSettingsDto>> GetAllGuilds(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiChannel>> GetAllChannels(CancellationToken cancellationToken = default);
}
