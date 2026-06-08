using DevClient.Data;
using DevClient.Data.Discord;
using DevClient.Data.WoW.Raidbots;
using Google.Apis.Auth.OAuth2;
using Serilog;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using DevClient.Data.Fitness;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DevClient.Clients
{
    public class GoogleSheetsClient
    {
        public GoogleSheetsClient() { }

        readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };

        private SheetsService? _service;
        private SheetsService Service => _service ??= GetSheetsService();

        private GoogleSheetsSettings? _sheetSettings;
        private GoogleSheetsSettings Sheet => _sheetSettings ??= AppSettings.Guilds.First(g => g.GoogleSheet != null).GoogleSheet!;

        private SheetsService GetSheetsService()
        {
            return GetSheetsService(Sheet.CredentialsPath, Sheet.SheetName);
        }

        private static SheetsService GetSheetsService(string credentialsPath, string applicationName)
        {
            Log.Debug("GoogleSheetsClient.GetSheetsService: START");
            GoogleCredential credential;
            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = applicationName,
            });

            Log.Debug("GoogleSheetsClient.GetSheetsService: END");
            return service;
        }

        public async Task<bool> UpdateFitnessWeight(GoogleHealthUserSettings user, double weightLbs, DateTime postedAt)
        {
            if (string.IsNullOrWhiteSpace(user.WeightSheetId)
                || string.IsNullOrWhiteSpace(user.WeightSheetName)
                || string.IsNullOrWhiteSpace(user.WeightSheetDateColumn)
                || string.IsNullOrWhiteSpace(user.WeightSheetWeightColumn)
                || string.IsNullOrWhiteSpace(AppSettings.FitnessWeightSheet.CredentialsPath))
            {
                return false;
            }

            var dateColumn = NormalizeColumn(user.WeightSheetDateColumn);
            var weightColumn = NormalizeColumn(user.WeightSheetWeightColumn);
            var service = GetSheetsService(AppSettings.FitnessWeightSheet.CredentialsPath, "Fitness Weight");
            var dateRange = $"'{EscapeSheetName(user.WeightSheetName)}'!{dateColumn}:{dateColumn}";
            var dateRequest = service.Spreadsheets.Values.Get(user.WeightSheetId, dateRange);
            var dateResponse = await dateRequest.ExecuteAsync();
            var targetDate = DateOnly.FromDateTime(postedAt);
            var rowIndex = FindDateRow(dateResponse.Values, targetDate);

            if (rowIndex == null)
            {
                Log.Warning(
                    "GoogleSheetsClient.UpdateFitnessWeight: no matching date {Date} found for {Username} in {SheetName}!{DateColumn}",
                    targetDate, user.Username, user.WeightSheetName, dateColumn);
                return false;
            }

            var updateRange = $"'{EscapeSheetName(user.WeightSheetName)}'!{weightColumn}{rowIndex.Value}";
            var requestBody = new ValueRange
            {
                Values = new List<IList<object>>
                {
                    new List<object> { Math.Round(weightLbs, 1) }
                }
            };

            var updateRequest = service.Spreadsheets.Values.Update(requestBody, user.WeightSheetId, updateRange);
            updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await updateRequest.ExecuteAsync();
            return true;
        }

        private static string EscapeSheetName(string sheetName) => sheetName.Replace("'", "''", StringComparison.Ordinal);

        private static string NormalizeColumn(string column)
        {
            var normalized = Regex.Replace(column.Trim(), "[^A-Za-z]", "").ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("Sheet column must contain at least one letter.", nameof(column));
            return normalized;
        }

        private static int? FindDateRow(IList<IList<object>>? rows, DateOnly targetDate)
        {
            if (rows == null) return null;

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Count == 0) continue;
                if (TryParseSheetDate(rows[i][0]?.ToString(), out var cellDate) && cellDate == targetDate)
                    return i + 1;
            }

            return null;
        }

        private static bool TryParseSheetDate(string? value, out DateOnly date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var text = value.Trim();
            if (DateOnly.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)
                || DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }

            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var dateTime)
                || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dateTime))
            {
                date = DateOnly.FromDateTime(dateTime);
                return true;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 0)
            {
                date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
                return true;
            }

            return false;
        }

        private async Task<List<ItemUpgrade>> ReadEntries()
        {
            Log.Information("GoogleSheetsClient.ReadEntries: START");
            var range = $"{Sheet.SheetName}!A:F"; // Assuming headers are in row 1
            var request = Service.Spreadsheets.Values.Get(Sheet.Id, range);
            var response = await request.ExecuteAsync();

            var values = response.Values;
            var entries = new List<ItemUpgrade>();

            if (values != null)
            {
                foreach (var row in values)
                {
                    if (row.Count < 6 || string.IsNullOrWhiteSpace(row[0].ToString()) || string.IsNullOrWhiteSpace(row[1].ToString()) ||
                        string.IsNullOrWhiteSpace(row[2].ToString()) || string.IsNullOrWhiteSpace(row[3].ToString()) ||
                        string.IsNullOrWhiteSpace(row[4].ToString())) continue; // Skip incomplete rows

                    var lastUpdated = string.IsNullOrWhiteSpace(row[5].ToString()) ? DateTime.MinValue : DateTime.Parse(row[5].ToString()!);

                    entries.Add(new ItemUpgrade(row[0].ToString()!, row[1].ToString()!, row[2].ToString()!, row[3].ToString()!,
                        double.Parse(row[4].ToString()!), lastUpdated));
                }
            }
            Log.Information("GoogleSheetsClient.ReadEntries: END");
            return entries;
        }

        private async Task ClearSheet()
        {
            Log.Debug("GoogleSheetsClient.ClearSheet: START");
            var requestBody = new ClearValuesRequest();
            var request = Service.Spreadsheets.Values.Clear(requestBody, Sheet.Id, $"{Sheet.SheetName}!A:F");
            await request.ExecuteAsync();
            Log.Debug("GoogleSheetsClient.ClearSheet: END");
        }

        private async Task WriteEntries(List<ItemUpgrade> entries)
        {
            Log.Information("GoogleSheetsClient.WriteEntries: START");
            var range = $"{Sheet.SheetName}!A:F";
            var values = new List<IList<object>>();

            foreach (var entry in entries)
            {
                values.Add(new List<object> { entry.PlayerName, entry.Slot, entry.Difficulty, entry.ItemName, entry.DpsGain, entry.LastUpdated });
            }

            var requestBody = new ValueRange { Values = values };
            var request = Service.Spreadsheets.Values.Update(requestBody, Sheet.Id, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
            Log.Information("GoogleSheetsClient.WriteEntries: END");
        }

        public async Task<bool> UpdateSheet(List<ItemUpgrade> newEntries)
        {
            Log.Information("GoogleSheetsClient.UpdateSheet: START");

            // Step 1: Read current data from the sheet
            var sheetData = await ReadEntries();

            // Step 2: Remove existing entries that match "Player Name" and "Difficulty"
            sheetData.RemoveAll(existing =>
                newEntries.Any(newEntry =>
                    (existing.PlayerName.ToUpper() == newEntry.PlayerName.ToUpper() &&
                    existing.Difficulty.ToUpper() == newEntry.Difficulty.ToUpper()) ||
                    existing.LastUpdated < DateTime.Now.AddDays(-14)));

            // Step 3: Append new data
            sheetData.AddRange(newEntries);

            // Step 4: Clear & Update Sheet
            await ClearSheet();

            sheetData = sheetData.GroupBy(sd => new { sd.PlayerName, sd.Slot, sd.Difficulty, sd.ItemName }).Select(g => g.First()).ToList();

            await WriteEntries(sheetData.OrderByDescending(sd => sd.DpsGain).ToList());

            Log.Information("GoogleSheetsClient.UpdateSheet: END");
            return true;
        }

        public async Task<List<GuildApplication>> ReadApplications(ApplicationSheetSettings settings)
        {
            var range = $"'{settings.SheetName}'!A:L";
            var request = Service.Spreadsheets.Values.Get(settings.Id, range);
            var response = await request.ExecuteAsync();

            var applications = new List<GuildApplication>();
            var values = response.Values;

            if (values == null || values.Count <= 1)
                return applications;

            // Row 1 (index 0) is the header; data starts at index 1 = sheet row 2
            var header = values[0];
            string Header(int col) => header.Count > col ? header[col]?.ToString() ?? string.Empty : string.Empty;

            for (var i = 1; i < values.Count; i++)
            {
                var row = values[i];
                if (row.Count < 1 || string.IsNullOrWhiteSpace(row[0]?.ToString())) continue;

                string Cell(int col) => row.Count > col ? row[col]?.ToString() ?? string.Empty : string.Empty;

                if (!DateTime.TryParse(Cell(0), out var timestamp)) continue;

                applications.Add(new GuildApplication
                {
                    RowIndex = i + 1, // i is 0-based list index; sheet row = i + 1 (header is row 1)
                    IsPosted = Cell(11).Equals("TRUE", StringComparison.OrdinalIgnoreCase),
                    Timestamp = timestamp,
                    ContactInfoLabel = Header(1),
                    ContactInfo = Cell(1),
                    ClassSpecLabel = Header(2),
                    ClassSpec = Cell(2),
                    MulticlassingLabel = Header(3),
                    Multiclassing = Cell(3),
                    CanMakeRaidTimesLabel = Header(4),
                    CanMakeRaidTimes = Cell(4),
                    WarcraftLogsLabel = Header(5),
                    WarcraftLogs = Cell(5),
                    PrivateLogCredentials = Cell(6),
                    MythicExperienceLabel = Header(7),
                    MythicExperience = Cell(7),
                    ReasonForLeavingLabel = Header(8),
                    ReasonForLeaving = Cell(8),
                    WhyGuildLabel = Header(9),
                    WhyGuild = Cell(9),
                    AnythingElseLabel = Header(10),
                    AnythingElse = Cell(10)
                });
            }

            var newCount = applications.Count(a => !a.IsPosted);
            if (newCount > 0)
                Log.Information("GoogleSheetsClient.ReadApplications: found {Count} new application(s)", newCount);
            return applications;
        }

        public async Task MarkApplicationAsPosted(ApplicationSheetSettings settings, int sheetRowNumber)
        {
            Log.Information($"GoogleSheetsClient.MarkApplicationAsPosted: row {sheetRowNumber}");
            var range = $"'{settings.SheetName}'!L{sheetRowNumber}";
            var requestBody = new ValueRange { Values = new List<IList<object>> { new List<object> { "TRUE" } } };
            var request = Service.Spreadsheets.Values.Update(requestBody, settings.Id, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
        }
    }
}





