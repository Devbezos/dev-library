using System.Net.Http.Json;
using System.Text.Json;
using dev_library.Data;
using dev_library.Data.Discord;
using dev_library.Data.Fitness;

namespace dev_client;

public sealed class DevApiClient : IDevApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public DevApiClient(HttpClient http)
    {
        _http = http;
    }

    public static DevApiClient Create(string baseAddress, HttpMessageHandler? handler = null)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri(baseAddress.TrimEnd('/') + "/");
        return new DevApiClient(http);
    }

    public Task<IReadOnlyList<GuildSettingsDto>> GetGuilds(CancellationToken cancellationToken = default) =>
        GetList<GuildSettingsDto>("api/guilds", cancellationToken);

    public Task CreateGuild(GuildSettingsDto guild, CancellationToken cancellationToken = default) =>
        Post("api/guilds", guild, cancellationToken);

    public Task UpdateGuild(string name, GuildSettingsDto guild, CancellationToken cancellationToken = default) =>
        Put($"api/guilds/{Url(name)}", guild, cancellationToken);

    public Task DeleteGuild(string name, CancellationToken cancellationToken = default) =>
        Delete($"api/guilds/{Url(name)}", cancellationToken);

    public Task<IReadOnlyList<ApiChannel>> GetChannels(CancellationToken cancellationToken = default) =>
        GetList<ApiChannel>("api/channels", cancellationToken);

    public Task DeleteChannel(string channelId, CancellationToken cancellationToken = default) =>
        Delete($"api/channels/{Url(channelId)}", cancellationToken);

    public Task<IReadOnlyList<ScheduledJob>> GetJobs(CancellationToken cancellationToken = default) =>
        GetList<ScheduledJob>("api/jobs", cancellationToken);

    public Task UpdateJob(string name, ScheduledJob job, CancellationToken cancellationToken = default) =>
        Put($"api/jobs/{Url(name)}", new
        {
            job.Enabled,
            job.DayOfWeek,
            job.Hour,
            job.Minute,
            job.IntervalMinutes,
        }, cancellationToken);

    public Task ResetJobLastRun(string name, CancellationToken cancellationToken = default) =>
        Delete($"api/jobs/{Url(name)}/last-run", cancellationToken);

    public Task<IReadOnlyList<FitnessApiUser>> GetFitnessUsers(CancellationToken cancellationToken = default) =>
        GetList<FitnessApiUser>("api/fitness/users", cancellationToken);

    public Task<IReadOnlyList<FitnessApiUser>> GetAllFitnessUsers(CancellationToken cancellationToken = default) =>
        GetList<FitnessApiUser>("api/db/fitness/users", cancellationToken);

    public Task CreateFitnessUser(FitnessUserUpsert user, CancellationToken cancellationToken = default) =>
        Post("api/fitness/users", user, cancellationToken);

    public Task UpdateFitnessUser(string username, FitnessUserUpsert user, CancellationToken cancellationToken = default) =>
        Put($"api/fitness/users/{Url(username)}", user, cancellationToken);

    public Task DeleteFitnessUser(string username, CancellationToken cancellationToken = default) =>
        Delete($"api/fitness/users/{Url(username)}", cancellationToken);

    public Task<IReadOnlyList<FitnessPost>> GetFitnessPosts(CancellationToken cancellationToken = default) =>
        GetList<FitnessPost>("api/fitness/posts", cancellationToken);

    public Task DeleteFitnessPost(int id, CancellationToken cancellationToken = default) =>
        Delete($"api/fitness/posts/{id}", cancellationToken);

    public Task<OAuthInviteUrl> GetFitnessOAuthInviteUrl(CancellationToken cancellationToken = default) =>
        Get<OAuthInviteUrl>("api/fitness/oauth/invite-url", cancellationToken);

    public Task<DailyFitnessResult> GetDailyFitness(string? username = null, CancellationToken cancellationToken = default) =>
        Get<DailyFitnessResult>("api/fitness/daily" + Query(("username", username)), cancellationToken);

    public Task<WeeklyFitnessResult> GetWeeklyFitness(string? username = null, CancellationToken cancellationToken = default) =>
        Get<WeeklyFitnessResult>("api/fitness/weekly" + Query(("username", username)), cancellationToken);

    public Task<NutritionResult> GetNutrition(string? username = null, DateOnly? date = null, CancellationToken cancellationToken = default) =>
        Get<NutritionResult>("api/fitness/nutrition" + Query(
            ("username", username),
            ("date", date?.ToString("yyyy-MM-dd"))), cancellationToken);

    public Task<IReadOnlyList<TcgResult>> GetPokemonTcgResults(CancellationToken cancellationToken = default) =>
        GetList<TcgResult>("api/tcg/pokemon", cancellationToken);

    public Task<TcgRunResult> RunPokemonTcg(CancellationToken cancellationToken = default) =>
        Post<TcgRunResult>("api/tcg/pokemon/run", cancellationToken);

    public Task<IReadOnlyList<TcgResult>> GetGundamTcgResults(CancellationToken cancellationToken = default) =>
        GetList<TcgResult>("api/tcg/gundam", cancellationToken);

    public Task<TcgRunResult> RunGundamTcg(CancellationToken cancellationToken = default) =>
        Post<TcgRunResult>("api/tcg/gundam/run", cancellationToken);

    public Task<IReadOnlyList<TcgResult>> GetTcgPreorders(string game, CancellationToken cancellationToken = default) =>
        GetList<TcgResult>("api/tcg/preorders" + Query(("game", game)), cancellationToken);

    public Task<IReadOnlyList<TcgSourceUrl>> GetTcgSourceUrls(string? game = null, string? store = null, CancellationToken cancellationToken = default) =>
        GetList<TcgSourceUrl>("api/tcg/source-urls" + Query(("game", game), ("store", store)), cancellationToken);

    public async Task<int> CreateTcgSourceUrl(TcgSourceUrlCreate sourceUrl, CancellationToken cancellationToken = default)
    {
        var created = await Post<CreatedId, TcgSourceUrlCreate>("api/tcg/source-urls", sourceUrl, cancellationToken);
        return created.Id;
    }

    public Task UpdateTcgSourceUrl(int id, string url, CancellationToken cancellationToken = default) =>
        Put($"api/tcg/source-urls/{id}", new TcgSourceUrlUpdate(url), cancellationToken);

    public Task DeleteTcgSourceUrl(int id, CancellationToken cancellationToken = default) =>
        Delete($"api/tcg/source-urls/{id}", cancellationToken);

    public Task<IReadOnlyList<TcgHiddenItem>> GetTcgHiddenItems(string? game = null, CancellationToken cancellationToken = default) =>
        GetList<TcgHiddenItem>("api/tcg/hidden-items" + Query(("game", game)), cancellationToken);

    public Task HideTcgItem(TcgHiddenItemCreate item, CancellationToken cancellationToken = default) =>
        Post("api/tcg/hidden-items", item, cancellationToken);

    public Task UnhideTcgItem(string game, string store, string productName, CancellationToken cancellationToken = default) =>
        Delete("api/tcg/hidden-items" + Query(
            ("game", game),
            ("store", store),
            ("productName", productName)), cancellationToken);

    public Task<IReadOnlyList<TcgBlacklistWord>> GetTcgBlacklistWords(string? game = null, CancellationToken cancellationToken = default) =>
        GetList<TcgBlacklistWord>("api/tcg/blacklist-words" + Query(("game", game)), cancellationToken);

    public Task AddTcgBlacklistWord(TcgBlacklistWordCreate word, CancellationToken cancellationToken = default) =>
        Post("api/tcg/blacklist-words", word, cancellationToken);

    public Task DeleteTcgBlacklistWord(string game, string word, CancellationToken cancellationToken = default) =>
        Delete("api/tcg/blacklist-words" + Query(("game", game), ("word", word)), cancellationToken);

    public Task<TcgChannelSettings> GetTcgChannelSettings(CancellationToken cancellationToken = default) =>
        Get<TcgChannelSettings>("api/tcg/channels", cancellationToken);

    public Task UpdateTcgChannelSettings(TcgChannelSettings settings, CancellationToken cancellationToken = default) =>
        Put("api/tcg/channels", settings, cancellationToken);

    public Task<TcgChannel> GetTcgChannel(string game, CancellationToken cancellationToken = default) =>
        Get<TcgChannel>($"api/tcg/channels/{Url(game)}", cancellationToken);

    public Task UpdateTcgChannel(string game, string channelId, CancellationToken cancellationToken = default) =>
        PutEmpty($"api/tcg/channels/{Url(game)}/{Url(channelId)}", cancellationToken);

    public Task UpdateTcgNotificationUserIds(string game, IEnumerable<string> userIds, CancellationToken cancellationToken = default) =>
        Put($"api/tcg/notification-users/{Url(game)}", new { UserIds = userIds.ToArray() }, cancellationToken);

    public Task<IReadOnlyList<TcgProductGroupResult>> GetTcgProductGroups(string game, CancellationToken cancellationToken = default) =>
        GetList<TcgProductGroupResult>("api/tcg/product-groups" + Query(("game", game)), cancellationToken);

    public Task UpdateTcgProductGroupMarketPrices(string groupKey, TcgProductGroupMarketPrices prices, CancellationToken cancellationToken = default) =>
        Put($"api/tcg/product-groups/{Url(groupKey)}/market-prices", prices, cancellationToken);

    public Task<IReadOnlyList<GuildSettingsDto>> GetAllGuilds(CancellationToken cancellationToken = default) =>
        GetList<GuildSettingsDto>("api/db/guilds", cancellationToken);

    public Task<IReadOnlyList<ApiChannel>> GetAllChannels(CancellationToken cancellationToken = default) =>
        GetList<ApiChannel>("api/db/channels", cancellationToken);

    private async Task<T> Get<T>(string path, CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<T>(path, JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException($"API GET {path} returned an empty response.");
    }

    private async Task<IReadOnlyList<T>> GetList<T>(string path, CancellationToken cancellationToken)
    {
        var result = await _http.GetFromJsonAsync<List<T>>(path, JsonOptions, cancellationToken);
        return result ?? [];
    }

    private async Task Post<TBody>(string path, TBody body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
    }

    private async Task<TResult> Post<TResult>(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(path, null, cancellationToken);
        return await Read<TResult>(response, path);
    }

    private async Task<TResult> Post<TResult, TBody>(string path, TBody body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        return await Read<TResult>(response, path);
    }

    private async Task Put<TBody>(string path, TBody body, CancellationToken cancellationToken)
    {
        using var response = await _http.PutAsJsonAsync(path, body, JsonOptions, cancellationToken);
        await EnsureSuccess(response);
    }

    private async Task PutEmpty(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.PutAsync(path, null, cancellationToken);
        await EnsureSuccess(response);
    }

    private async Task Delete(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.DeleteAsync(path, cancellationToken);
        await EnsureSuccess(response);
    }

    private static async Task<TResult> Read<TResult>(HttpResponseMessage response, string path)
    {
        await EnsureSuccess(response);
        var result = await response.Content.ReadFromJsonAsync<TResult>(JsonOptions);
        return result ?? throw new InvalidOperationException($"API POST {path} returned an empty response.");
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"API request failed with {(int)response.StatusCode} {response.ReasonPhrase}. {body}",
            null,
            response.StatusCode);
    }

    private static string Query(params (string Name, string? Value)[] values)
    {
        var parts = values
            .Where(v => !string.IsNullOrWhiteSpace(v.Value))
            .Select(v => $"{Url(v.Name)}={Url(v.Value!)}")
            .ToArray();

        return parts.Length == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private static string Url(string value) => Uri.EscapeDataString(value);
}
