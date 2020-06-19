using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Blish_HUD;
namespace KillProofModule.Persistance
{
    internal class Resources
    {
        [JsonProperty("general_tokens")] public IList<Token> GeneralTokens { get; set; }
        [JsonProperty("raids")] public IList<Raid> Raids { get; set; }
        [JsonProperty("fractals")] public IList<Fractal> Fractals { get; set; }

        public Wing GetWing(int index)
        {
            var allWings = GetAllWings().ToArray();
            for (var i = 0; i < allWings.Count() - 1; i++)
            {
                if (i != index) continue;
                return allWings[i];
            }
            return null;
        }
        public Wing GetWing(string id)
        {
            if (Regex.IsMatch(id, @"^[Ww]\d+$"))
                return GetWing(int.Parse(string.Join(string.Empty,Regex.Matches(id, @"\d+$").OfType<Match>().Select(m => m.Value))) - 1);
            return GetAllWings().FirstOrDefault(wing => wing.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase));
        }
        public Wing GetWing(Token token)
        {
            return GetAllWings().FirstOrDefault(wing => wing.Events.Any(encounters => encounters.Token.Equals(token)));
        }
        public IEnumerable<Wing> GetAllWings()
        {
            return Raids.SelectMany(raid => raid.Wings).Where(wing => wing != null);
        }
        private IEnumerable<Event> GetAllEvents()
        {
            return GetAllWings().SelectMany(wing => wing.Events);
        }
        public IEnumerable<Token> GetAllTokens()
        {
            return GeneralTokens.Concat(GetAllEvents().Select(encounter => encounter.Token)
                                .Concat(Fractals.Select(fractal => fractal.Token)))
                                .Where(token => token != null)
                                .GroupBy(token => token.Id).Select(g => g.First());
        }
        public Token GetToken(int id)
        {
            return GetAllTokens().FirstOrDefault(token => token.Id == id);
        }
        public Token GetToken(string name)
        {
            name = name.Split('|').Reverse().ToList()[0].Trim();
            return GetAllTokens().FirstOrDefault(token => token.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
        private IEnumerable<Miniature> GetAllMiniatures()
        {
            return GetAllEvents().SelectMany(encounter => encounter.Miniatures)
                                 .GroupBy(miniature => miniature.Id).Select(g => g.First());
        }
        public Event GetEvent(string id)
        {
            return GetAllEvents().FirstOrDefault(encounter => encounter.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase));
        }
        public Dictionary<int, string> GetRenderUrlRepository()
        {
            return GetAllTokens().ToDictionary(token => token.Id, token => token.Icon)
                                 .MergeLeft(GetAllMiniatures().ToDictionary(miniature => miniature.Id, miniature => miniature.Icon));
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
        [JsonProperty("mapid")] public int Mapid { get; set; }
        [JsonProperty("events")] public IList<Event> Events { get; set; }
        public IList<Token> GetTokens()
        {
            return Events.Select(encounters => encounters.Token).ToList();
        }
    }

    internal class Event
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("miniatures")] public IList<Miniature> Miniatures { get; set; }
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
