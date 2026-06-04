using Newtonsoft.Json;
using System.Text.Json;

namespace DevClient.Data.WoW
{
    public class DiscordRequest
    {
        public DiscordRequest(string content)
        {
            Content = content;
        }

        [JsonProperty("content")]
        public string Content { get; set; }
    }
}





