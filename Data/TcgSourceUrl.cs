using MySqlConnector;
using System.Collections.Generic;

namespace DevClient.Data
{
    public class TcgSourceUrl
    {
        public int Id { get; set; }
        public string Store { get; set; } = string.Empty;
        public string Game { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }

    public interface ITcgSourceUrlRepository
    {
        void EnsureTable();
        List<TcgSourceUrl> GetAll(string? game = null, string? store = null, bool enabledOnly = false);
        int Add(TcgSourceUrl sourceUrl);
        void UpdateUrl(int id, string url);
        void UpdateEnabled(int id, bool enabled);
        void Delete(int id);
    }
}



