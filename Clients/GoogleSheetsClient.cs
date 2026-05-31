using dev_library.Data;
using dev_library.Data.Discord;
using dev_library.Data.WoW.Raidbots;
using Google.Apis.Auth.OAuth2;
using Serilog;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace dev_library.Clients
{
    public class GoogleSheetsClient
    {


        public GoogleSheetsClient()
        {
            SheetsService = GetSheetsService();
        }

        readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
        readonly SheetsService SheetsService;
        readonly GoogleSheetsSettings _sheet = AppSettings.Guilds.First(g => g.GoogleSheet != null).GoogleSheet;

        private SheetsService GetSheetsService()
        {
            Log.Debug("GoogleSheetsClient.GetSheetsService: START");
            GoogleCredential credential;
            using (var stream = new FileStream(_sheet.CredentialsPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(Scopes);
            }

            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = _sheet.SheetName,
            });

            Log.Debug("GoogleSheetsClient.GetSheetsService: END");
            return service;
        }

        private async Task<List<ItemUpgrade>> ReadEntries()
        {
            Log.Information("GoogleSheetsClient.ReadEntries: START");
            var range = $"{_sheet.SheetName}!A:F"; // Assuming headers are in row 1
            var request = SheetsService.Spreadsheets.Values.Get(_sheet.Id, range);
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

                    var lastUpdated = string.IsNullOrWhiteSpace(row[5].ToString()) ? DateTime.MinValue : DateTime.Parse(row[5].ToString());

                    entries.Add(new ItemUpgrade(row[0].ToString(), row[1].ToString(), row[2].ToString(), row[3].ToString(),
                        double.Parse(row[4].ToString()), lastUpdated));
                }
            }
            Log.Information("GoogleSheetsClient.ReadEntries: END");
            return entries;
        }

        private async Task ClearSheet()
        {
            Log.Debug("GoogleSheetsClient.ClearSheet: START");
            var requestBody = new ClearValuesRequest();
            var request = SheetsService.Spreadsheets.Values.Clear(requestBody, _sheet.Id, $"{_sheet.SheetName}!A:F");
            await request.ExecuteAsync();
            Log.Debug("GoogleSheetsClient.ClearSheet: END");
        }

        private async Task WriteEntries(List<ItemUpgrade> entries)
        {
            Log.Information("GoogleSheetsClient.WriteEntries: START");
            var range = $"{_sheet.SheetName}!A:F";
            var values = new List<IList<object>>();

            foreach (var entry in entries)
            {
                values.Add(new List<object> { entry.PlayerName, entry.Slot, entry.Difficulty, entry.ItemName, entry.DpsGain, entry.LastUpdated });
            }

            var requestBody = new ValueRange { Values = values };
            var request = SheetsService.Spreadsheets.Values.Update(requestBody, _sheet.Id, range);
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
            var request = SheetsService.Spreadsheets.Values.Get(settings.Id, range);
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
            var request = SheetsService.Spreadsheets.Values.Update(requestBody, settings.Id, range);
            request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await request.ExecuteAsync();
        }
    }
}
