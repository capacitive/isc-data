using System;
using Xunit;
using isc_data;
using System.Configuration;

namespace tests
{
    public class TeamData
    {
        [Fact]
        public void TestCSVDataGrab()
        {
            ConfigurationManager.AppSettings["csvpath"] = @"\\tsclient\E\_vidgames\mwo\comp\team-data.csv";
            ConfigurationManager.AppSettings["year"] = "2021";
            ConfigurationManager.AppSettings["league"] = "ISC";

            var teams = PilotInfo.CreateTeams();

            Assert.NotNull(teams);
            Assert.True(teams.Count > 0);
        }

        [Fact]
        public void TestTeamStatsGET()
        {
            ConfigurationManager.AppSettings["csvpath"] = @"\\tsclient\E\_vidgames\mwo\comp\team-data.csv";
            ConfigurationManager.AppSettings["year"] = "2021";
            ConfigurationManager.AppSettings["league"] = "ISC";

            var teams = PilotInfo.GetTeamStats();

            Assert.NotNull(teams);
            Assert.True(teams.Count > 0);
            Assert.True(teams[0].Pilots.Count > 0);
            Assert.True(teams[0].Pilots[1].Percentile > Decimal.MinValue);
        }

        [Fact]
        public void TestSeeding()
        {
            ConfigurationManager.AppSettings["csvpath"] = @"\\tsclient\E\_vidgames\mwo\comp\team-data.csv";
            ConfigurationManager.AppSettings["year"] = "2021";
            ConfigurationManager.AppSettings["league"] = "ISC";

            var teams = PilotInfo.GetTeamStats();

            Assert.NotNull(teams);
            Assert.True(teams[0].SeedRanking > 0);
        }
    }
}
