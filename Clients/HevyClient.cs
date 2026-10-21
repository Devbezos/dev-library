using System.Net;
using DevClient.Data.Hevy;
using Newtonsoft.Json;

namespace DevClient.Clients;

/// <summary>A Hevy API call that failed, with a message fit to show the user.</summary>
public class HevyApiException(HttpStatusCode? status, string message) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>The Hevy calls the workout sync uses.</summary>
public interface IHevyApi
{
    Task<HevyUserInfo> GetUserInfo(CancellationToken ct = default);
    Task<HevyWorkoutsPage> GetWorkouts(int page, CancellationToken ct = default);
    Task<HevyWorkoutEventsPage> GetWorkoutEvents(DateTime sinceUtc, int page, CancellationToken ct = default);
}

/// <summary>
/// Reads workouts from the Hevy public API (https://api.hevyapp.com/docs). Requires Hevy Pro; the key comes from
/// hevy.com/settings?developer and is sent as the <c>api-key</c> header.
/// </summary>
public class HevyClient : IHevyApi
{
    public const string BaseUrl = "https://api.hevyapp.com/v1";
    /// <summary>The API's maximum page size for workouts and events.</summary>
    public const int PageSize = 10;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _apiKey;

    public HevyClient(string apiKey) => _apiKey = apiKey;

    public async Task<HevyUserInfo> GetUserInfo(CancellationToken ct = default) =>
        (await Get<HevyUserInfoResponse>("user/info", ct)).Data;

    public Task<HevyWorkoutsPage> GetWorkouts(int page, CancellationToken ct = default) =>
        Get<HevyWorkoutsPage>($"workouts?page={page}&pageSize={PageSize}", ct);

    public Task<HevyWorkout> GetWorkout(string workoutId, CancellationToken ct = default) =>
        Get<HevyWorkout>($"workouts/{Uri.EscapeDataString(workoutId)}", ct);

    /// <summary>Workouts updated or deleted since <paramref name="sinceUtc"/>, oldest page first.</summary>
    public Task<HevyWorkoutEventsPage> GetWorkoutEvents(DateTime sinceUtc, int page, CancellationToken ct = default) =>
        Get<HevyWorkoutEventsPage>($"workouts/events?page={page}&pageSize={PageSize}&since={Uri.EscapeDataString(sinceUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"))}", ct);

    private async Task<T> Get<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/{path}");
        request.Headers.Add("api-key", _apiKey);
        request.Headers.Add("accept", "application/json");

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new HevyApiException(null, $"Couldn't reach Hevy: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HevyApiException(response.StatusCode, response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Hevy rejected the API key. Check it at hevy.com/settings?developer.",
                    HttpStatusCode.Forbidden => "Hevy refused the request. The API needs an active Hevy Pro subscription.",
                    HttpStatusCode.TooManyRequests => "Hevy is rate limiting requests; it'll retry on the next sync.",
                    _ => $"Hevy returned {(int)response.StatusCode}: {(body.Length > 200 ? body[..200] : body)}",
                });
            }
            return JsonConvert.DeserializeObject<T>(body) ?? throw new HevyApiException(response.StatusCode, "Hevy returned an empty response.");
        }
    }
}
