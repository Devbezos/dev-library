using Newtonsoft.Json;

namespace DevClient.Data.Fitness
{
    public class GoogleHealthDataPointsResponse
    {
        [JsonProperty("dataPoints")]
        public List<GoogleHealthDataPoint> DataPoints { get; set; } = new();

        [JsonProperty("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GoogleHealthDataPoint
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("dataSource")]
        public GoogleHealthDataSource DataSource { get; set; } = new();

        [JsonProperty("exercise")]
        public GoogleHealthExercise Exercise { get; set; } = new();
    }

    public class GoogleHealthDataSource
    {
        [JsonProperty("recordingMethod")]
        public string RecordingMethod { get; set; } = string.Empty;

        [JsonProperty("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonProperty("device")]
        public GoogleHealthDevice Device { get; set; } = new();

        [JsonProperty("application")]
        public GoogleHealthApplication? Application { get; set; }
    }

    public class GoogleHealthDevice
    {
        [JsonProperty("displayName")]
        public string? DisplayName { get; set; }

        [JsonProperty("manufacturer")]
        public string? Manufacturer { get; set; }
    }

    public class GoogleHealthApplication
    {
        [JsonProperty("packageName")]
        public string? PackageName { get; set; }
    }

    public class GoogleHealthExercise
    {
        [JsonProperty("interval")]
        public GoogleHealthInterval Interval { get; set; } = new();

        [JsonProperty("exerciseType")]
        public string ExerciseType { get; set; } = string.Empty;

        [JsonProperty("metricsSummary")]
        public GoogleHealthMetrics MetricsSummary { get; set; } = new();

        [JsonProperty("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonProperty("activeDuration")]
        public string ActiveDuration { get; set; } = string.Empty;

        [JsonProperty("createTime")]
        public string? CreateTime { get; set; }

        [JsonProperty("updateTime")]
        public string? UpdateTime { get; set; }
    }

    public class GoogleHealthInterval
    {
        [JsonProperty("startTime")]
        public string StartTime { get; set; } = string.Empty;

        [JsonProperty("endTime")]
        public string EndTime { get; set; } = string.Empty;
    }

    public class GoogleHealthMetrics
    {
        [JsonProperty("caloriesKcal")]
        public double? CaloriesKcal { get; set; }

        [JsonProperty("distanceMillimiters")]
        public long? DistanceMillimiters { get; set; }

        [JsonProperty("steps")]
        public string? Steps { get; set; }

        [JsonProperty("averagePaceSecondsPerMeter")]
        public double? AveragePaceSecondsPerMeter { get; set; }

        [JsonProperty("averageHeartRateBeatsPerMinute")]
        public string? AverageHeartRateBeatsPerMinute { get; set; }

        [JsonProperty("activeZoneMinutes")]
        public string? ActiveZoneMinutes { get; set; }
    }

    public class GoogleHealthStepsResponse
    {
        [JsonProperty("dataPoints")]
        public List<GoogleHealthStepsDataPoint> DataPoints { get; set; } = new();

        [JsonProperty("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GoogleHealthStepsDataPoint
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("dataSource")]
        public GoogleHealthDataSource DataSource { get; set; } = new();

        [JsonProperty("steps")]
        public GoogleHealthStepsEntry Steps { get; set; } = new();
    }

    public class GoogleHealthStepsEntry
    {
        [JsonProperty("interval")]
        public GoogleHealthInterval Interval { get; set; } = new();

        // API returns count as a quoted string e.g. "4"
        [JsonProperty("count")]
        public string CountRaw { get; set; } = "0";

        public long GetCount() => long.TryParse(CountRaw, out var v) ? v : 0;
    }

    public class GoogleHealthSleepResponse
    {
        [JsonProperty("dataPoints")]
        public List<GoogleHealthSleepDataPoint> DataPoints { get; set; } = new();

        [JsonProperty("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GoogleHealthSleepDataPoint
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("dataSource")]
        public GoogleHealthDataSource DataSource { get; set; } = new();

        [JsonProperty("sleep")]
        public GoogleHealthSleepEntry Sleep { get; set; } = new();
    }

    public class GoogleHealthSleepEntry
    {
        [JsonProperty("interval")]
        public GoogleHealthInterval Interval { get; set; } = new();

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("stages")]
        public List<GoogleHealthSleepStage> Stages { get; set; } = new();

        [JsonProperty("summary")]
        public GoogleHealthSleepSummary Summary { get; set; } = new();

        [JsonProperty("metadata")]
        public GoogleHealthSleepMetadata Metadata { get; set; } = new();

        [JsonProperty("createTime")]
        public string? CreateTime { get; set; }

        [JsonProperty("updateTime")]
        public string? UpdateTime { get; set; }

        public double GetDurationHours()
        {
            if (Summary.MinutesAsleep > 0)
                return Summary.MinutesAsleep / 60.0;

            // fallback: compute from interval timestamps
            if (DateTime.TryParse(Interval.StartTime, out var start) &&
                DateTime.TryParse(Interval.EndTime, out var end) && end > start)
                return (end - start).TotalHours;

            return 0;
        }
    }

    public class GoogleHealthSleepSummary
    {
        // API returns numeric fields as quoted strings e.g. "560"
        [JsonProperty("minutesInSleepPeriod")]
        public string MinutesInSleepPeriodRaw { get; set; } = "0";

        public int MinutesInSleepPeriod => int.TryParse(MinutesInSleepPeriodRaw, out var v) ? v : 0;

        [JsonProperty("minutesAfterWakeUp")]
        public string MinutesAfterWakeUpRaw { get; set; } = "0";

        public int MinutesAfterWakeUp => int.TryParse(MinutesAfterWakeUpRaw, out var v) ? v : 0;

        [JsonProperty("minutesToFallAsleep")]
        public string MinutesToFallAsleepRaw { get; set; } = "0";

        public int MinutesToFallAsleep => int.TryParse(MinutesToFallAsleepRaw, out var v) ? v : 0;

        [JsonProperty("minutesAsleep")]
        public string MinutesAsleepRaw { get; set; } = "0";

        public int MinutesAsleep => int.TryParse(MinutesAsleepRaw, out var v) ? v : 0;

        [JsonProperty("minutesAwake")]
        public string MinutesAwakeRaw { get; set; } = "0";

        public int MinutesAwake => int.TryParse(MinutesAwakeRaw, out var v) ? v : 0;

        [JsonProperty("stagesSummary")]
        public List<GoogleHealthSleepStageSummary> StagesSummary { get; set; } = new();
    }

    public class GoogleHealthSleepStageSummary
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("minutes")]
        public string MinutesRaw { get; set; } = "0";

        public int Minutes => int.TryParse(MinutesRaw, out var v) ? v : 0;

        [JsonProperty("count")]
        public string CountRaw { get; set; } = "0";

        public int Count => int.TryParse(CountRaw, out var v) ? v : 0;
    }

    public class GoogleHealthSleepMetadata
    {
        [JsonProperty("stagesStatus")]
        public string? StagesStatus { get; set; }

        [JsonProperty("processed")]
        public bool Processed { get; set; }

        [JsonProperty("nap")]
        public bool Nap { get; set; }
    }

    public class GoogleHealthSleepStage
    {
        [JsonProperty("startTime")]
        public string StartTime { get; set; } = string.Empty;

        [JsonProperty("startUtcOffset")]
        public string? StartUtcOffset { get; set; }

        [JsonProperty("endTime")]
        public string EndTime { get; set; } = string.Empty;

        [JsonProperty("endUtcOffset")]
        public string? EndUtcOffset { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("createTime")]
        public string? CreateTime { get; set; }

        [JsonProperty("updateTime")]
        public string? UpdateTime { get; set; }
    }

    public class GoogleHealthWeightResponse
    {
        [JsonProperty("dataPoints")]
        public List<GoogleHealthWeightDataPoint> DataPoints { get; set; } = new();

        [JsonProperty("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GoogleHealthWeightDataPoint
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("dataSource")]
        public GoogleHealthDataSource DataSource { get; set; } = new();

        [JsonProperty("weight")]
        public GoogleHealthWeightEntry Weight { get; set; } = new();
    }

    public class GoogleHealthWeightEntry
    {
        [JsonProperty("sampleTime")]
        public GoogleHealthSampleTime SampleTime { get; set; } = new();

        [JsonProperty("weightGrams")]
        public double? WeightGrams { get; set; }

        public double? WeightKg => WeightGrams.HasValue ? WeightGrams.Value / 1000.0 : null;
    }

    public class GoogleHealthSampleTime
    {
        [JsonProperty("physicalTime")]
        public string PhysicalTime { get; set; } = string.Empty;
    }

    public class GoogleHealthRestingHrResponse
    {
        [JsonProperty("dataPoints")]
        public List<GoogleHealthRestingHrDataPoint> DataPoints { get; set; } = new();

        [JsonProperty("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GoogleHealthRestingHrDataPoint
    {
        [JsonProperty("dailyRestingHeartRate")]
        public GoogleHealthRestingHr DailyRestingHeartRate { get; set; } = new();
    }

    public class GoogleHealthRestingHr
    {
        [JsonProperty("date")]
        public GoogleHealthDate Date { get; set; } = new();

        [JsonProperty("beatsPerMinute")]
        public string BeatsPerMinuteRaw { get; set; } = string.Empty;

        public int BeatsPerMinute => int.TryParse(BeatsPerMinuteRaw, out var v) ? v : 0;
    }

    public class GoogleHealthDate
    {
        [JsonProperty("year")]
        public int Year { get; set; }

        [JsonProperty("month")]
        public int Month { get; set; }

        [JsonProperty("day")]
        public int Day { get; set; }

        public DateOnly ToDateOnly() => new DateOnly(Year, Month, Day);
    }

    public class GoogleHealthNutritionResponse
    {
        [JsonProperty("dataPoints")]
        public List<GoogleHealthNutritionDataPoint> DataPoints { get; set; } = new();

        [JsonProperty("nextPageToken")]
        public string? NextPageToken { get; set; }
    }

    public class GoogleHealthNutritionDataPoint
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("dataSource")]
        public GoogleHealthDataSource DataSource { get; set; } = new();

        [JsonProperty("nutritionLog")]
        public GoogleHealthNutritionEntry Nutrition { get; set; } = new();
    }

    public class GoogleHealthNutritionEntry
    {
        [JsonProperty("interval")]
        public GoogleHealthInterval Interval { get; set; } = new();

        [JsonProperty("mealType")]
        public string MealType { get; set; } = string.Empty;

        [JsonProperty("foodDisplayName")]
        public string? FoodDisplayName { get; set; }

        // Calories come from the energy field (kcal), not the nutrients array
        [JsonProperty("energy")]
        public GoogleHealthEnergyQuantity? Energy { get; set; }

        [JsonProperty("totalCarbohydrate")]
        public GoogleHealthWeightQuantity? TotalCarbohydrate { get; set; }

        [JsonProperty("totalFat")]
        public GoogleHealthWeightQuantity? TotalFat { get; set; }

        // API returns nutrients as an array of {nutrient, quantity.grams} objects
        [JsonProperty("nutrients")]
        public List<GoogleHealthNutrientQuantity> NutrientList { get; set; } = new();

        [JsonIgnore]
        public GoogleHealthNutrients Nutrients => new GoogleHealthNutrients(this);
    }

    public class GoogleHealthEnergyQuantity
    {
        [JsonProperty("kcal")]
        public double Kcal { get; set; }
    }

    public class GoogleHealthWeightQuantity
    {
        [JsonProperty("grams")]
        public double Grams { get; set; }
    }

    public class GoogleHealthNutrientQuantity
    {
        [JsonProperty("nutrient")]
        public string Nutrient { get; set; } = string.Empty;

        [JsonProperty("quantity")]
        public GoogleHealthWeightQuantity? Quantity { get; set; }
    }

    public class GoogleHealthNutrients
    {
        private readonly GoogleHealthNutritionEntry _entry;

        public GoogleHealthNutrients(GoogleHealthNutritionEntry entry) => _entry = entry;

        private double? FromList(string name) =>
            _entry.NutrientList.FirstOrDefault(n => n.Nutrient == name)?.Quantity?.Grams;

        public double? Calories           => _entry.Energy?.Kcal;
        public double? ProteinG           => FromList("PROTEIN");
        public double? TotalCarbohydrateG => _entry.TotalCarbohydrate?.Grams ?? FromList("CARBOHYDRATES");
        public double? TotalFatG          => _entry.TotalFat?.Grams ?? FromList("TOTAL_FAT");
        public double? DietaryFiberG      => FromList("DIETARY_FIBER");
        public double? SugarG             => FromList("SUGAR");
    }

    public class FitnessActivityEntry
    {
        public string Name { get; set; } = string.Empty;
        public int Minutes { get; set; }
    }

    public class DailyFitnessSnapshot
    {
        public double? SleepHours { get; set; }
        public long Steps { get; set; }
        public int? RestingHrBpm { get; set; }
        public double? CaloriesBurnt { get; set; }
        public double? CaloriesEaten { get; set; }
        public List<FitnessActivityEntry> Activities { get; set; } = new();
    }

    public class WeeklyFitnessSnapshot
    {
        public double? AvgSleepHours { get; set; }
        public long AvgStepsPerDay { get; set; }
        public double? AvgRestingHrBpm { get; set; }
        public double? WeightDeltaLbs { get; set; }
        public int TotalActivityMinutes { get; set; }
        public double? AvgDailyCalorieDeficit { get; set; }
        public int CalorieLoggedDays { get; set; }
        public List<FitnessActivityEntry> Activities { get; set; } = new();
    }

    public class GoogleHealthTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonProperty("scope")]
        public string Scope { get; set; } = string.Empty;
    }
}





