namespace DevClient.Data.Discord
{
    public interface IAutoReactionRepository
    {
        AutoReactionRule[] GetAll();
        void ReplaceAll(IEnumerable<AutoReactionRule> rules);
    }
}
