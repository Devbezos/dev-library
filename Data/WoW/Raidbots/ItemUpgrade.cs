namespace DevClient.Data.WoW.Raidbots
{
    public class ItemUpgrade
    {
        public ItemUpgrade(string player, string slot, string difficulty, string itemName, double dpsGain, DateTime lastUpdated)
        {
            PlayerName = player;
            Slot = slot;
            Difficulty = difficulty;
            ItemName = itemName;
            DpsGain = dpsGain;
            LastUpdated = lastUpdated;
        }

        public string PlayerName { get; set; }
        public string Slot { get; set; }
        public string Difficulty { get; set; }
        public string ItemName { get; set; }
        public double DpsGain { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}





