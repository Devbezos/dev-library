namespace DevClient.Data.Fitness
{
    public interface IFitnessRepository
    {
        void EnsureTable();
        void EnsureUsersTable(GoogleHealthUserSettings[] users);
        void LogPost(string username, string postType);
        GoogleHealthUserSettings[] GetGoogleHealthSettings();
        GoogleHealthUserSettings[] GetGoogleHealthSettingsAll();
        void UpsertFitnessUser(string username, ulong channelId, bool enabled,
            string clientId, string clientSecret, string refreshToken, double? highestWeightLbs = null,
            string weightSheetId = "", string weightSheetName = "", string weightSheetDateColumn = "",
            string weightSheetWeightColumn = "");
        void DeleteUser(string username);
        void UpdateUser(string username, ulong channelId, bool enabled);
        void UpdateRefreshToken(string username, string refreshToken);
        List<FitnessUser> GetUsers();
        List<FitnessUser> GetAllUsers();
        List<FitnessPost> GetRecentPosts(int limit = 50);
        void DeletePost(int id);
    }
}





