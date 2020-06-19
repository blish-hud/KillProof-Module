using Newtonsoft.Json;
using System.Collections.Generic;
namespace KillProofModule.Persistance
{
    internal class Resources
    {
        [JsonProperty("raids")] public IList<Raid> Raids { get; set; }
        [JsonProperty("fractals")] public IList<Fractal> Fractals { get; set; }
    }
    internal class Raid
    {
        [JsonProperty("general_tokens")] public IList<Token> GeneralTokens { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("wings")] public IList<Wing> Wings { get; set; }
    }
    internal class Wing
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("mapid")] public int Mapid { get; set; }
        [JsonProperty("events")] public IList<Event> Events { get; set; }
    }
    internal class Event
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("miniatures")] public IList<Miniature> Miniature { get; set; }
        [JsonProperty("token")] public Token Token { get; set; }
    }
    internal class Miniature
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; }
    }
    internal class Token
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; }
    }
    internal class Fractal
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("token")] public Token Token { get; set; }
    }
}
