using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KillProofModule.Persistance
{
    /// <summary>
    ///     JSON class for replies from https://killproof.me/api/
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum Mode
    {
        [EnumMember(Value = "none")]
        None,
        [EnumMember(Value = "fractal")]
        Fractal,
        [EnumMember(Value = "raid")]
        Raid
    }
    internal class Title
    {
        [JsonConverter(typeof(StringEnumConverter)), JsonProperty("mode")] public string Mode { get; set; }
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
    }
    internal class KillProof
    {
        [JsonProperty("titles")] public IList<Title> Titles { get; set; }
        [JsonProperty("proof_url")] public string ProofUrl { get; set; }
        [JsonProperty("tokens")] public IList<Token> Tokens { get; set; }
        [JsonProperty("killproofs")] public IList<Token> Killproofs { get; set; }
        [JsonProperty("kpid")] public string KpId { get; set; }
        [JsonProperty("last_refresh")] public DateTime LastRefresh { get; set; }
        [JsonProperty("account_name")] public string AccountName { get; set; }
        [JsonProperty("error")] public string Error { get; set; }

        public int GetTokenAmount(int id)
        {
            return Tokens.Concat(Killproofs).FirstOrDefault(x => x.Id == id)?.Amount ?? 0;
        }
        public int GetTokenAmount(string name)
        {
            name = name.Split('|').Reverse().ToList()[0].Trim();
            return Tokens.Concat(Killproofs).FirstOrDefault(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))?.Amount ?? 0;
        }
        public Token GetToken(string name)
        {
            name = name.Split('|').Reverse().ToList()[0].Trim();
            return Tokens.Concat(Killproofs).FirstOrDefault(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}