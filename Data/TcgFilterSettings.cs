
namespace DevClient.Data
{
    public interface ITcgFilterSettingsRepository
    {
        void EnsureTable();
        List<string> GetTerms(string game);
        void SetTerms(string game, IEnumerable<string> terms);
        void EnsureDefault(string game, IEnumerable<string> terms);
    }
}



