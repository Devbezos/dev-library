namespace DevClient.Data.Fitness;

public class FitnessUser
{
    public string Username { get; set; } = string.Empty;
    public ulong ChannelId { get; set; }
    public bool Enabled { get; set; }
}
