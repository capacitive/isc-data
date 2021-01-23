using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Configuration;
using System.Text;

namespace isc_data
{
    public static class PilotInfo
    {
        const string url = "https://leaderboard.isengrim.org/api/usernames/";
        const string apiKey = "";

        public static List<Team> CreateTeams()
        {
            Team team;
            var teams = new List<Team>();

            var path = ConfigurationManager.AppSettings["csvpath"];
            var year = int.Parse(ConfigurationManager.AppSettings["year"]);
            var league = ConfigurationManager.AppSettings["league"];

            using (var reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    team = new Team(name: values[0], year: year, league: league);
                    for(int i = 1; i < values.Length; i++)
                    {
                        team.Pilots.Add(new Pilot() { PilotName = values[i] });
                    }

                    teams.Add(team);
                }         
            }

            return teams;
        }

        public static List<Team> GetTeamStats()
        {
            var teamstoGET = PilotInfo.CreateTeams();

            var teamswithStats = new List<Team>();
            Team team;

            foreach (var t in teamstoGET)
            {
                team = new Team(t.Name, t.Year, t.League);

                foreach (var p in t.Pilots)
                {
                    string urlParameters = p.PilotName; //.Replace("0", "O");
                    var response = APICall.RunAsync<Pilot>(url, urlParameters).GetAwaiter().GetResult();

                    if (response != null && response.Percentile > 0)
                    {
                        Pilot pilot = response;
                        pilot.TeamName = t.Name;
                        team.Pilots.Add(pilot);
                    }
                    else
                    #region retry logic - last known season
                    {
                        //try one more times using latest season:
                        int season = 53;
                        bool hit = false;
                        while (hit == false && season > 44)
                        {
                            var seasonsUrl = url + p.PilotName + @"/seasons/";
                            urlParameters = season.ToString();
                            response = APICall.RunAsync<Pilot>(seasonsUrl, urlParameters).GetAwaiter().GetResult();
                            if (response != null)
                            {
                                Pilot pilot = response;
                                pilot.TeamName = t.Name;
                                team.Pilots.Add(pilot);
                                hit = true;
                            }
                            else
                            {
                                season--;
                            }
                        }
                    }
                    #endregion

                    if (response == null)
                    {
                        team.Pilots.Add(new Pilot() { PilotName = p.PilotName, Percentile = -1, TeamName = t.Name });
                    }
                }

                List<Pilot> sortedPilots = team.Pilots.OrderByDescending(d => d.Percentile).ToList();
                team.Pilots = new List<Pilot>();
                team.Pilots.AddRange(sortedPilots);

                #region seeding
                StringBuilder sb = new StringBuilder();
                decimal percentileTotal = 0;
                for (var i = 0; i < 8; i++)
                {
                    if (i > team.Pilots.Count - 1) 
                    {
                        break;
                    }
                    if (team.Pilots[i].LastSeason == 0)
                    {
                        continue;
                    }
                    sb.Append(team.Pilots[i].PilotName + ", ");
                    percentileTotal += team.Pilots[i].Percentile;
                }
                team.SeedRanking = percentileTotal / 8;
                team.PilotsUsedForSeeding = sb.ToString();
                #endregion

                teamswithStats.Add(team);
            }

            return teamswithStats.OrderByDescending(o => o.SeedRanking).ToList();
        }
    }
}
