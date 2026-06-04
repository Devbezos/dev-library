using Newtonsoft.Json;

namespace DevClient.Data.WoW.Blizzard
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class ConnectedRealm
    {
        [JsonProperty("href")]
        private string href = string.Empty;

        public string Href { get => href[..href.LastIndexOf('?')]; set => href = value; }
    }

    public class Links
    {
        [JsonProperty("self")]
        public Self Self = new();
    }

    public class BlizzardRealmsResponse
    {
        [JsonProperty("_links")]
        public Links Links = new();

        [JsonProperty("connected_realms")]
        public List<ConnectedRealm> ConnectedRealms = new();
    }

    public class Self
    {
        [JsonProperty("href")]
        public string Href = string.Empty;
    }


}





