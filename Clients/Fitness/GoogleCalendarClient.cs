using DevClient.Data;
using Newtonsoft.Json;
using RestSharp;

namespace DevClient.Clients.Fitness
{
    /// <summary>An event on someone's primary Google Calendar. All-day events have <see cref="StartDate"/> instead of <see cref="StartUtc"/>.</summary>
    public sealed record GoogleCalendarEvent(
        string Id,
        string Summary,
        string? Location,
        string? HtmlLink,
        /// <summary>When the event was last changed, as Google reports it; changes whenever anything about the event does.</summary>
        string Updated,
        DateTime? StartUtc,
        /// <summary>"yyyy-MM-dd" for all-day events.</summary>
        string? StartDate,
        /// <summary>Recurring series and their individual instances.</summary>
        bool IsRecurring,
        bool Declined);

    public sealed class GoogleCalendarException(string message) : Exception(message);

    /// <summary>
    /// Reads the primary calendar of a fitness user's Google sign-in. Needs the calendar.readonly scope, which sign-ins made
    /// before it was added don't have; they get a 403 until the user reconnects Google on the Fitness tab.
    /// </summary>
    public class GoogleCalendarClient
    {
        private const int MaxPages = 20;
        private readonly GoogleHealthClient _auth;

        public GoogleCalendarClient(GoogleHealthUserSettings settings) => _auth = new GoogleHealthClient(settings);

        /// <summary>Events that end after <paramref name="fromUtc"/> and start before <paramref name="toUtc"/>, recurring series included (flagged).</summary>
        public async Task<List<GoogleCalendarEvent>> GetEvents(DateTime fromUtc, DateTime toUtc)
        {
            var token = await _auth.GetAccessToken();

            var events = new List<GoogleCalendarEvent>();
            string? pageToken = null;
            for (var page = 0; page < MaxPages; page++)
            {
                // singleEvents=false keeps a recurring series as one item with "recurrence", so it's easy to skip. Only
                // regular events and ones Gmail adds (flights, reservations); not working location, focus time or birthdays.
                var url = $"{Constants.GoogleHealth.CalendarBaseUrl}/calendars/primary/events" +
                    $"?timeMin={Uri.EscapeDataString(fromUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))}" +
                    $"&timeMax={Uri.EscapeDataString(toUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))}" +
                    "&singleEvents=false&maxResults=250&eventTypes=default&eventTypes=fromGmail" +
                    (pageToken is null ? "" : $"&pageToken={Uri.EscapeDataString(pageToken)}");

                var response = await SharedHttp.Client.SendAsync(SharedHttp.Bearer(Method.Get, url, token));
                var json = response.Content ?? string.Empty;
                if (!response.IsSuccessStatusCode)
                    throw new GoogleCalendarException((int)response.StatusCode == 403
                        ? "Google didn't grant calendar access. Reconnect Google on the Fitness tab."
                        : $"Google Calendar returned {(int)response.StatusCode}: {json}");

                var body = JsonConvert.DeserializeObject<EventsResponse>(json) ?? new EventsResponse();
                foreach (var item in body.Items ?? [])
                {
                    if (item.Id is null || item.Status == "cancelled")
                        continue;
                    events.Add(new GoogleCalendarEvent(
                        item.Id,
                        string.IsNullOrWhiteSpace(item.Summary) ? "(No title)" : item.Summary.Trim(),
                        string.IsNullOrWhiteSpace(item.Location) ? null : item.Location.Trim(),
                        item.HtmlLink,
                        item.Updated ?? "",
                        item.Start?.DateTime?.UtcDateTime,
                        item.Start?.Date,
                        item.Recurrence is { Count: > 0 } || item.RecurringEventId is not null,
                        item.Attendees?.Any(a => a.Self && a.ResponseStatus == "declined") == true));
                }

                pageToken = body.NextPageToken;
                if (pageToken is null)
                    return events;
            }
            throw new GoogleCalendarException($"Google Calendar returned more than {MaxPages} pages of events.");
        }

        private sealed class EventsResponse
        {
            [JsonProperty("items")] public List<EventItem>? Items { get; set; }
            [JsonProperty("nextPageToken")] public string? NextPageToken { get; set; }
        }

        private sealed class EventItem
        {
            [JsonProperty("id")] public string? Id { get; set; }
            [JsonProperty("status")] public string? Status { get; set; }
            [JsonProperty("summary")] public string? Summary { get; set; }
            [JsonProperty("location")] public string? Location { get; set; }
            [JsonProperty("htmlLink")] public string? HtmlLink { get; set; }
            [JsonProperty("updated")] public string? Updated { get; set; }
            [JsonProperty("start")] public EventTime? Start { get; set; }
            [JsonProperty("recurrence")] public List<string>? Recurrence { get; set; }
            [JsonProperty("recurringEventId")] public string? RecurringEventId { get; set; }
            [JsonProperty("attendees")] public List<Attendee>? Attendees { get; set; }
        }

        private sealed class EventTime
        {
            [JsonProperty("date")] public string? Date { get; set; }
            [JsonProperty("dateTime")] public DateTimeOffset? DateTime { get; set; }
        }

        private sealed class Attendee
        {
            [JsonProperty("self")] public bool Self { get; set; }
            [JsonProperty("responseStatus")] public string? ResponseStatus { get; set; }
        }
    }
}
