using DevClient.Data.WoW;
using Newtonsoft.Json.Linq;

namespace DevClient.Clients
{
    public interface IRaiderIoClient
    {
        Task<RaiderIoKeyResponse> GetWeeklyKeyHistory(WoWAuditCharacter guildy);

        /// <summary>A character's profile with the given <paramref name="fields"/>; null when Raider.IO doesn't know the character or realm.</summary>
        Task<JToken?> GetCharacterProfile(string region, string realm, string name, string fields);

        /// <summary>Mythic+ static data (seasons and their dungeons) for an expansion; null when Raider.IO doesn't know the expansion.</summary>
        Task<JToken?> GetMythicPlusStaticData(int expansionId);

        /// <summary>The score colour tiers ([{ score, rgbHex }], highest first) for a season, or the current one when null.</summary>
        Task<JToken?> GetMythicPlusScoreTiers(string? season);
    }
}





