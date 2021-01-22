using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using Google.Apis.Sheets.v4;

namespace isc_data
{
    public class Program
    {
        static string[] Scopes = { SheetsService.Scope.Spreadsheets, DriveService.Scope.Drive, DriveService.Scope.DriveFile };
        static string ApplicationName = "ISCCompTeamStats";

        static void Main(string[] args)
        {
            var service = AuthorizeGoogleApp();
            string sheetName = "ISCCompTeamReconData_" + DateTime.Now.Year.ToString() + "-" + DateTime.Now.Month.ToString() + "_" + 
                DateTime.Now.Day.ToString() + "_" + 
                DateTime.Now.Minute.ToString() + ":" +
                DateTime.Now.Second.ToString();
            var newSheet = new Google.Apis.Sheets.v4.Data.Spreadsheet();
            newSheet.Properties = new Google.Apis.Sheets.v4.Data.SpreadsheetProperties();
            newSheet.Properties.Title = sheetName;
            var createdSheet = service.Spreadsheets.Create(newSheet).Execute();
            Console.WriteLine($"Created sheet: {createdSheet.SpreadsheetUrl}");

            var spreadsheetId = createdSheet.SpreadsheetId;

            var teams = PilotInfo.GetTeamStats();
            List<Pilot> pilots = new List<Pilot>();

            Parallel.ForEach(teams, t =>
            {
                pilots.AddRange(t.Pilots);
            });
            
            var header_range = $"Sheet1!A1:N1";
            ValueRange header_valueRange = new ValueRange();
            header_valueRange.MajorDimension = "ROWS";
            var header_objectList = new List<object>() { "Team Name", "TeamSeedRanking", "Pilot", "Unit Tag", "Percentile", "SurvivalRate", "TotalKills", 
                "KillsPerMatch", "AverageMatchScore", "FirstSeason", "LightPercent", "MediumPercent", "HeavyPercent", "AssaultPercent" };
            header_valueRange.Values = new List<IList<object>> { header_objectList };

            SpreadsheetsResource.ValuesResource.UpdateRequest header_update = service.Spreadsheets.Values.Update(header_valueRange, spreadsheetId, header_range);
            header_update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            header_update.Execute();

            var waitInterval = int.Parse(ConfigurationManager.AppSettings["throttle"]);

            for (var i = 0; i < pilots.Count; i++)
            {
                var range = $"Sheet1!A{i + 2}:N{i + 2}";
                ValueRange valueRange = new ValueRange();
                valueRange.MajorDimension = "ROWS";

                var objectList = new List<object>() { pilots[i].TeamName, pilots[i].TeamSeedRanking, pilots[i].PilotName, pilots[i].UnitTag, pilots[i].Percentile, pilots[i].SurvivalRate, pilots[i].TotalKills, pilots[i].KillsPerMatch, 
                    pilots[i].AverageMatchScore, pilots[i].FirstSeason, pilots[i].LightPercent, pilots[i].MediumPercent, pilots[i].HeavyPercent, pilots[i].AssaultPercent };
                valueRange.Values = new List<IList<object>> { objectList };

                SpreadsheetsResource.ValuesResource.UpdateRequest update = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
                update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                UpdateValuesResponse response = update.Execute();

                Thread.Sleep(waitInterval);
                Console.WriteLine(JsonConvert.SerializeObject(response));         
            }

            ////example of batch update:
            //data.Add(new ValueRange() { Values = (IList<IList<object>>)teams });

            //BatchUpdateValuesRequest requestBody = new BatchUpdateValuesRequest();
            //requestBody.ValueInputOption = valueInputOption;
            //requestBody.Data = data;

            //SpreadsheetsResource.ValuesResource.BatchUpdateRequest request = service.Spreadsheets.Values.BatchUpdate(requestBody, spreadsheetId: sheetName);
            //BatchUpdateValuesResponse response = request.Execute();
            //Console.WriteLine(JsonConvert.SerializeObject(response));
            
            Console.Read();
        }

        private static SheetsService AuthorizeGoogleApp()
        {
            UserCredential credential;

            using (var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/sheets.googleapis.com-dotnet-IscCompTeamStats.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine($"Credential file saved to: {credPath}");
            }

            var service = new SheetsService(new BaseClientService.Initializer() 
            { 
                HttpClientInitializer = credential, 
                ApplicationName = ApplicationName, 
            });

            return service;
        }
    }

    public static class APICall
    {
        private static HttpClient GetHttpClient(string url)
        {
            var client = new HttpClient { BaseAddress = new Uri(url) };
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static async Task<T> GetAsync<T>(string url, string urlParameters)
        {
            try
            {
                using (var client = GetHttpClient(url))
                {
                    HttpResponseMessage response = await client.GetAsync(urlParameters);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var json = await response.Content.ReadAsStringAsync();

                        var settings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };
                        var result = JsonConvert.DeserializeObject<T>(json, settings);
                        return result;
                    }

                    return default(T);
                }             
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                return default(T);
            }
        }

        public static async Task<T> RunAsync<T>(string url, string urlParameters)
        {
            return await GetAsync<T>(url, urlParameters);
        }
    }

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

                    if (response != null)
                    {
                        Pilot pilot = response;
                        pilot.TeamName = t.Name;
                        team.Pilots.Add(pilot);
                    }
                    else
                    {
                        team.Pilots.Add(new Pilot() { PilotName = p.PilotName, Percentile = -1, TeamName = t.Name });
                    }               
                }

                List<Pilot> sortedPilots = team.Pilots.OrderBy(o => o.Percentile).ToList();
                team.Pilots = new List<Pilot>();
                team.Pilots.AddRange(sortedPilots);

                decimal percentileTotal = 0;
                for (var i = 0; i < 10; i++)
                {
                    if (i > team.Pilots.Count - 1) break;
                    percentileTotal += team.Pilots[i].Percentile;
                }
                team.SeedRanking = percentileTotal / 10;

                teamswithStats.Add(team);
            }

            return teamswithStats;
        }
    }
}
