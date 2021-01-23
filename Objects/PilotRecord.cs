using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace isc_data
{
    public class Team
    {
        public Team(string name = "", int year = 2021, string league = "MWO")
        {
            Name = name;
            Year = year;
            League = league;
            Pilots = new List<Pilot>();
        }

        public string Name { get; set; }
        public int Year { get; set; }
        public string League { get; set; }
        public decimal SeedRanking { get; set; }
        public List<Pilot> Pilots { get; set; }
        public string PilotsUsedForSeeding { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var p in Pilots)
            {
                if (p == null) continue;
                sb.Append(p.PilotName + "                   Percentile: ");
                sb.Append(p.Percentile + "\r\n");
            }
            return $"Team: {Name} | League {League} | Year: {Year} \r\nPilots: \r\n{sb}";
        }
    }

    public class Pilot
    {
        [JsonProperty("PilotName")]
        public string PilotName { get; set; }

        [JsonProperty("Rank")]
        public int Rank { get; set; }

        [JsonProperty("Percentile")]
        public decimal Percentile { get; set; }

        [JsonProperty("UnitTag")]
        public string UnitTag { get; set; }

        public string TeamName { get; set; }

        public decimal TeamSeedRanking { get; set; }

        [JsonProperty("TotalWins")]
        public int TotalWins { get; set; }

        [JsonProperty("TotalKills")]
        public int TotalKills { get; set; }

        [JsonProperty("WLRatio")]
        public decimal WLRatio { get; set; }

        [JsonProperty("TotalDeaths")]
        public int TotalDeaths { get; set; }

        [JsonProperty("KDRatio")]
        public decimal KDRatio { get; set; }

        [JsonProperty("SurvivalRate")]
        public decimal SurvivalRate { get; set; }

        [JsonProperty("GamesPlayed")]
        public int GamesPlayed { get; set; }

        [JsonProperty("KillsPerMatch")]
        public decimal KillsPerMatch { get; set; }

        [JsonProperty("AverageMatchScore")]
        public decimal AverageMatchScore { get; set; }

        [JsonProperty("AdjustedScore")]
        public decimal AdjustedScore { get; set; }

        [JsonProperty("Progress")]
        public int Progress { get; set; }

        [JsonProperty("FirstSeason")]
        public int FirstSeason { get; set; }

        [JsonProperty("LastSeason")]
        public int LastSeason { get; set; }

        [JsonProperty("LightPercent")]
        public int LightPercent { get; set; }

        [JsonProperty("MediumPercent")]
        public int MediumPercent { get; set; }

        [JsonProperty("HeavyPercent")]
        public int HeavyPercent { get; set; }

        [JsonProperty("AssaultPercent")]
        public int AssaultPercent { get; set; }

        [JsonProperty("ServerSeason")]
        public int ServerSeason { get; set; }
    }
}
