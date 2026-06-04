using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevClient.Data.WoW.Blizzard
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Auction
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("item")]
        public Item? Item;

        [JsonProperty("buyout")]
        public long Buyout;

        [JsonProperty("quantity")]
        public uint Quantity;

        [JsonProperty("time_left")]
        public string TimeLeft = string.Empty;

        [JsonProperty("bid")]
        public long? Bid;
    }

    public class Modifier
    {
        [JsonProperty("type")]
        public int Type;

        [JsonProperty("value")]
        public int Value;

    }

    public class BlizzardAuctionResponse
    {
        [JsonProperty("_links")]
        public Links Links = new();

        [JsonProperty("connected_realm")]
        public ConnectedRealm ConnectedRealm = new();

        [JsonProperty("auctions")]
        public List<Auction> Auctions = new();
    }


}





