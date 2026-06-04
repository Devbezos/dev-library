namespace DevClient.Data
{
    public class TcgResaleValue
    {
        public int Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal? AvgPrice { get; set; }
        public int SampleCount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime LastUpdated { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
    }
}





