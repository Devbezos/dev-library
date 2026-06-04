namespace DevClient.Data.Fitness;

public class FitnessPost
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PostType { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
}
