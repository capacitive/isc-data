using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
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
            newSheet.Sheets = new List<Sheet>();

            var pilotSheetProperties = new Google.Apis.Sheets.v4.Data.SheetProperties
            {
                Title = "Pilots"
            };
            newSheet.Sheets.Add(new Sheet() { Properties = pilotSheetProperties });

            var seedingSheetProperties = new Google.Apis.Sheets.v4.Data.SheetProperties
            {
                Title = "Seeding"
            };
            newSheet.Sheets.Add(new Sheet() { Properties = seedingSheetProperties });

            var createdSheet = service.Spreadsheets.Create(newSheet).Execute();
            Console.WriteLine($"Created sheet: {createdSheet.SpreadsheetUrl}");

            var spreadsheetId = createdSheet.SpreadsheetId;

            #region create spreadsheet and insert header
            var header_range = $"Pilots!A1:M1";
            ValueRange header_valueRange = new ValueRange();
            header_valueRange.MajorDimension = "ROWS";
            var header_objectList = new List<object>() { "Team Name", "Pilot", "Unit Tag", "Percentile", "SurvivalRate", "TotalKills",
                "KillsPerMatch", "AverageMatchScore", "FirstSeason", "LightPercent", "MediumPercent", "HeavyPercent", "AssaultPercent" };
            header_valueRange.Values = new List<IList<object>> { header_objectList };

            SpreadsheetsResource.ValuesResource.UpdateRequest header_update = service.Spreadsheets.Values.Update(header_valueRange, spreadsheetId, header_range);
            header_update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            header_update.Execute();
            #endregion

            var teams = PilotInfo.GetTeamStats();
            List<Pilot> pilots = new List<Pilot>();

            var waitInterval = int.Parse(ConfigurationManager.AppSettings["throttle"]);

            #region add to Seed tab
            var seed_header_range = $"Seeding!A1:C1";
            ValueRange seed_header_valueRange = new ValueRange();
            seed_header_valueRange.MajorDimension = "ROWS";
            var seed_header_objectList = new List<object>() { "Team Name", "Seed Ranking", "Top 10 Pilots Used" };
            seed_header_valueRange.Values = new List<IList<object>> { seed_header_objectList };

            SpreadsheetsResource.ValuesResource.UpdateRequest seed_header_update = service.Spreadsheets.Values.Update(seed_header_valueRange, spreadsheetId, seed_header_range);
            seed_header_update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            seed_header_update.Execute();

            for (var i = 0; i < teams.Count; i++)
            {
                var range = $"Seeding!A{i + 2}:C{i + 2}";
                ValueRange valueRange = new ValueRange();
                valueRange.MajorDimension = "ROWS";

                var objectList = new List<object>() { teams[i].Name, teams[i].SeedRanking, teams[i].PilotsUsedForSeeding };
                valueRange.Values = new List<IList<object>> { objectList };

                SpreadsheetsResource.ValuesResource.UpdateRequest update = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, range);
                update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                UpdateValuesResponse response = update.Execute();

                Thread.Sleep(waitInterval);
                Console.WriteLine(JsonConvert.SerializeObject(response));
            }
            #endregion

            foreach (var t in teams)
            {
                pilots.AddRange(t.Pilots);
            };

            for (var i = 0; i < pilots.Count; i++)
            {
                var range = $"Pilots!A{i + 2}:M{i + 2}";
                ValueRange valueRange = new ValueRange();
                valueRange.MajorDimension = "ROWS";

                var objectList = new List<object>() { pilots[i].TeamName, pilots[i].PilotName, pilots[i].UnitTag, pilots[i].Percentile, pilots[i].SurvivalRate, pilots[i].TotalKills, pilots[i].KillsPerMatch,
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

            Console.WriteLine("Done!  Press ENTER to exit.");
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
}
