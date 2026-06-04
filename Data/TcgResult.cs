namespace DevClient.Data
{
    public class TcgResult
    {
        public int Id { get; set; }
        public DateTime RunAt { get; set; }
        public string Game { get; set; } = "pokemon";
        public string Store { get; set; } = string.Empty;
        public string Keyword { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public interface ITcgRepository
    {
        void EnsureTable();
        void SaveResults(DateTime runAt, List<Search> results, string game = "pokemon");
        List<TcgResult> GetLatestRun(string game = "pokemon");
    }
}





