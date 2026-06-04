using System.Text.RegularExpressions;

namespace DevClient.Data
{
    public class TcgProductGroup
    {
        public string Game { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public decimal? ResaleMarketPrice { get; set; }
    }

    public interface ITcgProductGroupRepository
    {
        void EnsureTable();
        List<TcgProductGroup> GetAll(string? game = null);
        void SetMarketPrices(string game, string groupKey, string displayName, decimal? resaleMarketPrice);
    }
}



