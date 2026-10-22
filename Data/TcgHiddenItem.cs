
namespace DevClient.Data
{
    public class TcgHiddenItem
    {
        public int Id { get; set; }
        public string Game { get; set; } = string.Empty;
        public string Store { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
    }

    public interface ITcgHiddenItemRepository
    {
        List<TcgHiddenItem> GetAll(string? game = null);
        void Hide(string game, string store, string productName);
        void Unhide(string game, string store, string productName);
    }
}



