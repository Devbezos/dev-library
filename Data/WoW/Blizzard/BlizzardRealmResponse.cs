// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
using Newtonsoft.Json;

public class Auctions
{
    [JsonProperty("href")]
    public string Href { get; set; } = string.Empty;
}

public class ConnectedRealm
{
    [JsonProperty("href")]
    public string Href { get; set; } = string.Empty;
}

public class Key
{
    [JsonProperty("href")]
    public string Href { get; set; } = string.Empty;
}

public class Links
{
    [JsonProperty("self")]
    public Self Self { get; set; } = new();
}

public class MythicLeaderboards
{
    [JsonProperty("href")]
    public string Href { get; set; } = string.Empty;
}

public class Population
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

public class Realm
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("region")]
    public Region Region { get; set; } = new();

    [JsonProperty("connected_realm")]
    public ConnectedRealm ConnectedRealm { get; set; } = new();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("locale")]
    public string Locale { get; set; } = string.Empty;

    [JsonProperty("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [JsonProperty("type")]
    public Type Type { get; set; } = new();

    [JsonProperty("is_tournament")]
    public bool IsTournament { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; } = string.Empty;
}

public class Region
{
    [JsonProperty("key")]
    public Key Key { get; set; } = new();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("id")]
    public int Id { get; set; }
}

public class BlizzardRealmResponse
{
    [JsonProperty("_links")]
    public Links Links { get; set; } = new();

    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("has_queue")]
    public bool HasQueue { get; set; }

    [JsonProperty("status")]
    public Status Status { get; set; } = new();

    [JsonProperty("population")]
    public Population Population { get; set; } = new();

    [JsonProperty("realms")]
    public List<Realm> Realms { get; set; } = new();

    [JsonProperty("mythic_leaderboards")]
    public MythicLeaderboards MythicLeaderboards { get; set; } = new();

    [JsonProperty("auctions")]
    public Auctions Auctions { get; set; } = new();
}

public class Self
{
    [JsonProperty("href")]
    public string Href { get; set; } = string.Empty;
}

public class Status
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

public class Type
{
    [JsonProperty("type")]
    public string Types { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

