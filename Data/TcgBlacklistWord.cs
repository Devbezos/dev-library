using MySqlConnector;

namespace DevClient.Data
{
    public class TcgBlacklistWord
    {
        public int Id { get; set; }
        public string Game { get; set; } = string.Empty;
        public string Word { get; set; } = string.Empty;
    }

    public interface ITcgBlacklistWordRepository
    {
        void EnsureTable();
        List<TcgBlacklistWord> GetAll(string? game = null);
        void Add(string game, string word);
        void Remove(string game, string word);
    }
}



