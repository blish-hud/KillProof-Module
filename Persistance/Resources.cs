using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KillProofModule.Persistance
{
    /// <summary>
    ///     JSON class for replies from https://killproof.me/api/resources
    /// </summary>
    internal enum RaidWingEventType
    {
        [EnumMember(Value = "Unknown")]
        Unknown,
        [EnumMember(Value = "Boss")]
        Boss,
        [EnumMember(Value = "Checkpoint")]
        Checkpoint
    }
    internal class Resources
    {
        [JsonProperty("general_tokens")] public IList<Token> GeneralTokens { get; set; }
        [JsonProperty("raids")] public IList<Raid> Raids { get; set; }
        [JsonProperty("fractals")] public IList<Fractal> Fractals { get; set; }

        public Wing GetWing(int index)
        {
            var allWings = GetAllWings().ToArray();
            for (var i = 0; i < allWings.Count(); i++)
            {
                if (i != index) continue;
                return allWings[i];
            }

            return null;
        }

        public Wing GetWing(string id)
        {
            if (Regex.IsMatch(id, @"^[Ww]\d+$"))
                return GetWing(int.Parse(string.Join(string.Empty,
                    Regex.Matches(id, @"\d+$").OfType<Match>().Select(m => m.Value))) - 1);
            return GetAllWings()
                .FirstOrDefault(wing => wing.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase));
        }

        public Wing GetWing(Token token)
        {
            return GetAllWings().FirstOrDefault(wing => wing.Events.Where(encounter => encounter?.Token != null)
                .Any(encounters => encounters.Token.Id.Equals(token.Id)));
        }

        public IEnumerable<Wing> GetAllWings()
        {
            return Raids.Where(raid => raid != null).SelectMany(raid => raid.Wings).Where(wing => wing != null);
        }

        private IEnumerable<Event> GetAllEvents()
        {
            return GetAllWings().Where(wing => wing.Events != null).SelectMany(wing => wing.Events);
        }

        public IEnumerable<Token> GetAllTokens()
        {
            return GeneralTokens
                .Concat(Fractals.Where(fractal => fractal.Token != null).Select(fractal => fractal.Token))
                .Concat(GetAllEvents().Where(encounter => encounter.Token != null)
                    .Select(encounter => encounter.Token));
        }

        public Token GetToken(int id)
        {
            return GetAllTokens().FirstOrDefault(token => token.Id == id);
        }

        public Token GetToken(string name)
        {
            name = name.Split('|').Reverse().ToList()[0].Trim();
            return GetAllTokens().Where(token => token.Name != null).FirstOrDefault(token =>
                token.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        private IEnumerable<Miniature> GetAllMiniatures()
        {
            return GetAllEvents().SelectMany(encounter => encounter.Miniatures);
        }

        public Event GetEvent(string id)
        {
            return GetAllEvents().FirstOrDefault(encounter =>
                encounter.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase));
        }
    }

    internal class Raid
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("wings")] public IList<Wing> Wings { get; set; }
    }

    internal class Wing
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("map_id")] public int MapId { get; set; }
        [JsonProperty("events")] public IList<Event> Events { get; set; }

        public IEnumerable<Token> GetTokens()
        {
            return Events.Where(encounter => encounter.Token != null).Select(encounters => encounters.Token);
        }
    }

    internal class Event
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("miniatures")] public IList<Miniature> Miniatures { get; set; }
        [JsonProperty("token")] public Token Token { get; set; }
        [JsonConverter(typeof(StringEnumConverter)), JsonProperty("type")] public RaidWingEventType Type { get; set; }
    }

    internal class Miniature
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }

    public class Token
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("icon")] public string Icon { get; set; }
        [JsonProperty("amount")] public int Amount { get; set; }
    }

    internal class Fractal
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("token")] public Token Token { get; set; }
    }
}