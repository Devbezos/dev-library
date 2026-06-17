namespace DevClient.Data.Discord
{
    public interface IAutoReactionRepository
    {
        void EnsureTable();
        AutoReactionRule[] GetAll();
        void ReplaceAll(IEnumerable<AutoReactionRule> rules);
    }
}
