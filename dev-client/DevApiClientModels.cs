using dev_library.Data.Fitness;

namespace dev_client;

public sealed record ApiChannel(
    string GuildName,
    string ChannelId,
    string ChannelName,
    bool IsDeleted = false);

public sealed record FitnessApiUser(
    string Username,
    string ChannelId,
    bool Enabled,
    string ClientId,
    string ClientSecret,
    string RefreshToken,
    bool IsDeleted = false,
    double? HighestWeightLbs = null);

public sealed record FitnessUserUpsert(
    string Username,
    string ChannelId,
    bool Enabled,
    string ClientId,
    string ClientSecret,
    string RefreshToken,
    double? HighestWeightLbs = null);

public sealed record NutritionResult(
    string Username,
    string Date,
    double Calories,
    double ProteinG,
    int Entries);

public sealed record DailyFitnessResult(
    string Username,
    DailyFitnessSnapshot Snapshot);

public sealed record WeeklyFitnessResult(
    string Username,
    WeeklyFitnessSnapshot Snapshot,
    double? CurrentWeightLbs,
    double? TotalWeightLostLbs);

public sealed record OAuthInviteUrl(string Url);

public sealed record TcgRunResult(DateTime RanAt, int ResultsFound);

public sealed record TcgChannelSettings(
    string PokemonChannelId,
    string GundamChannelId,
    string PreorderChannelId,
    string[] PokemonNotificationUserIds,
    string[] GundamNotificationUserIds,
    string[] PreorderNotificationUserIds,
    string[] PokemonPreorderNotificationUserIds,
    string[] GundamPreorderNotificationUserIds,
    string[] PokemonCenterSecurityNotificationUserIds);

public sealed record TcgChannel(string Game, string ChannelId, string[] NotificationUserIds);

public sealed record TcgProductGroupResult(
    string Game,
    string GroupKey,
    string DisplayName,
    decimal? Msrp,
    decimal? ResaleMarketPrice,
    int ProductCount,
    string[] Stores,
    string[] Examples);

public sealed record TcgProductGroupMarketPrices(
    string Game,
    string DisplayName,
    decimal? Msrp,
    decimal? ResaleMarketPrice);

public sealed record TcgSourceUrlCreate(
    string Store,
    string Game,
    string Category,
    string Url,
    bool Enabled = true);

public sealed record TcgSourceUrlUpdate(string Url);

public sealed record TcgHiddenItemCreate(
    string Game,
    string Store,
    string ProductName);

public sealed record TcgBlacklistWordCreate(
    string Game,
    string Word);

public sealed record CreatedId(int Id);
