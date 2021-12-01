using AngleSharp;
using AngleSharp.Dom;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OSCam.Maintainer.Configurations;
using OSCam.Maintainer.Models;
using Serilog;
using Serilog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OSCam.Maintainer
{
    partial class Program
    {
        static MaintainerOptions maintainerOptions;

        static LoggerProviderCollection Providers = new LoggerProviderCollection();

        static IServiceProvider serviceProvider;

        static void Main(string[] args)
        {
            try
            {
                serviceProvider = Initialize();

                Log.Information("Starting OSCam.Maintainer");

                MainAsync().Wait();
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "OSCam.Maintainer terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static async Task MainAsync()
        {

            maintainerOptions = serviceProvider.GetRequiredService<MaintainerOptions>();

            try
            {
                var task_A = GetListofNewCLinesFromWeb(maintainerOptions.URLToScrap);
                var task_B = GetListWithCurrentServerStatusFromOsCam(maintainerOptions.OsCamStatusPageURL);
                var task_C = GetListWithCurrentReadersOnOscamServerFile(maintainerOptions.OscamServerPath);

                await Task.WhenAll(task_A, task_B, task_C);

                var dailyListOfCLines = task_A.Result;
                var currentServerStatusList = task_B.Result;
                var currentListOfCcCamReadersFromFile = task_C.Result;

                currentListOfCcCamReadersFromFile = await RemoveReadersThatDontHaveTheCAID(currentListOfCcCamReadersFromFile, currentServerStatusList).ConfigureAwait(false);
                
                //await UpdateServersDescription(ref currentListOfCCCamReadersFromFile, currentServerStatusList);

                //await DeleteStaleReaders(ref currentListOfCCCamReadersFromFile);

                var currentListOfCcCamReadersFromFileNew = AddNewScrapedReaders(currentListOfCcCamReadersFromFile, dailyListOfCLines);

                WriteOsCamReadersToFile(currentListOfCcCamReadersFromFileNew, maintainerOptions.OscamServerPath); // + DateTime.Now.ToShortTimeString().Replace(":","") + ".txt");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error on the main thread! Execution aborted!", ex);
                throw;
            }
        }

        private static Task DeleteStaleReaders(ref List<OsCamReader> currentListOfCCCamReadersFromFile)
        {
            var readersToRemove = new List<OsCamReader>();

            foreach (var reader in currentListOfCCCamReadersFromFile)
            {
                if (reader.Description.IndexOf(maintainerOptions.NumberOfBackupsToKeep + ";") >= 0)
                {
                    readersToRemove.Add(reader);
                    Log.Debug(reader.Label + " is stale with: " + reader.Description + " and is flagged to be deleted");
                }
            }

            currentListOfCCCamReadersFromFile = currentListOfCCCamReadersFromFile.Except(readersToRemove).ToList();

            Log.Information("Deleted " + readersToRemove.Count + " stale readers from oscam.server");

            return Task.CompletedTask;
        }

        private static Task<List<OsCamReader>> GetListWithCurrentReadersOnOscamServerFile(string oscamServerFilepath = @"oscam.server")
        {
            var lista = new List<OsCamReader>();
            OsCamReader reader = new OsCamReader();
            int counter = 0;

            try
            {
                using (StreamReader sr = new StreamReader(oscamServerFilepath))
                {
                    foreach (string linha in File.ReadAllLines(oscamServerFilepath))
                    {
                        Debug.WriteLine(linha);

                        if (linha.StartsWith('#') || linha.StartsWith("/r/n") || string.IsNullOrEmpty(linha))
                            continue;

                        var arrayCCCAMLines = linha.Split("=");

                        switch (arrayCCCAMLines[0].Trim().ToLower())
                        {
                            case "[reader]":
                            {
                                if (!string.IsNullOrEmpty(reader.Label))
                                {
                                    lista.Add(reader);
                                    counter++;
                                }

                                reader = new OsCamReader();
                                continue;
                            }
                            case "label":
                                reader.Label = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Label : arrayCCCAMLines[1].Trim();
                                continue;
                            case "description":
                                reader.Description = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Description : arrayCCCAMLines[1].Trim();
                                continue;
                            case "enable":
                                reader.Enable = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Enable : arrayCCCAMLines[1].Trim();
                                continue;
                            case "protocol":
                                reader.Protocol = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Protocol : arrayCCCAMLines[1].Trim();
                                continue;
                            case "device":
                            {
                                if (!string.IsNullOrEmpty(arrayCCCAMLines[1]))
                                {
                                    var device = arrayCCCAMLines[1].Split(',');
                                    reader.Device = string.IsNullOrEmpty(device[0]) ? reader.Device : device[0].Trim();
                                    reader.Port = string.IsNullOrEmpty(device[1]) ? reader.Port : device[1].Trim();
                                }
                                continue;
                            }
                            case "key":
                                reader.Key = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Key : arrayCCCAMLines[1].Trim();
                                continue;
                            case "user":
                                reader.User = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.User : arrayCCCAMLines[1].Trim();
                                continue;
                            case "password":
                                reader.Password = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Password : arrayCCCAMLines[1].Trim();
                                continue;
                            case "inactivitytimeout":
                                reader.Inactivitytimeout = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Inactivitytimeout : arrayCCCAMLines[1].Trim();
                                continue;
                            case "group":
                                reader.Group = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Group : arrayCCCAMLines[1].Trim();
                                continue;
                            case "cccversion":
                                reader.Cccversion = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Cccversion : arrayCCCAMLines[1].Trim();
                                continue;
                            case "ccckeepalive":
                                reader.Ccckeepalive = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Ccckeepalive : arrayCCCAMLines[1].Trim();
                                continue;
                            case "reconnecttimeout":
                                reader.Reconnecttimeout = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Reconnecttimeout : arrayCCCAMLines[1].Trim();
                                continue;
                            case "lb_weight":
                                reader.LbWeight = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.LbWeight : arrayCCCAMLines[1].Trim();
                                continue;
                            case "cccmaxhops":
                                reader.Cccmaxhops = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Cccmaxhops : arrayCCCAMLines[1].Trim();
                                continue;
                            case "cccwantemu":
                                reader.Cccwantemu = string.IsNullOrEmpty(arrayCCCAMLines[1]) ? reader.Cccwantemu : arrayCCCAMLines[1].Trim();
                                continue;
                            default:
                                Console.WriteLine("Skiped " + linha);
                                continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error while reading the reader from oscam.server. ", ex);
            }

            Log.Information("Got " + lista.Count + " readers from oscam.server file");

            return Task.FromResult(lista);
        }

        private static async Task<List<OsCamReader>> RemoveReadersThatDontHaveTheCAID(List<OsCamReader> currentListOfCCCamReadersFromFile, List<OscamUiStatusLine> currentServerStatusList)
        {
            var readersToRemove = new List<OsCamReader>();

            //NEW VERSION, just deletes reader if CAID is not available
            foreach (var osCAMReader in currentListOfCCCamReadersFromFile)
            {
                ///Let's look for the CAID and if it's there we don't do anything
                if (await HasTheReaderAccessToTheCAID(maintainerOptions.OsCamReaderAPIURL + @"?part=entitlement&label=" + osCAMReader.Label,
                                                      maintainerOptions.CAIDs)
                        .ConfigureAwait(false)
                    //HasTheReaderAccessToTheCAIDScrapper(maintainerOptions.OsCamReaderPageURL + @"?label=" + osCAMReader.Label, maintainerOptions.CAIDs))
                    )
                    continue;

                readersToRemove.Add(osCAMReader);
                Log.Debug(osCAMReader.Label + " does not have a valid CAID and is flagged to be deleted");
            }

            return currentListOfCCCamReadersFromFile.Except(readersToRemove).ToList();
        }

        private static Task UpdateServersDescription(ref List<OsCamReader> currentListOfCCCamReadersFromFile, List<OscamUiStatusLine> currentServerStatusList)
        {
            foreach (var osCAMReader in currentListOfCCCamReadersFromFile)
            {
                var readerStatus = currentServerStatusList.Where(osCamSL => osCamSL.ReaderUser == osCAMReader.Device
                                                                           & osCamSL.Port == osCAMReader.Port
                                                                            //& osCamSL.OsCamReaderDescription.Username == osCAMReader.user
                                                                            )
                                                          .Select(sl => sl.Status).FirstOrDefault();

                if (readerStatus?.ToLowerInvariant() == "off" || readerStatus?.ToLowerInvariant() == "unknown" || readerStatus?.ToLowerInvariant() == "error")
                {
                    osCAMReader.UpdateNewFoundStateOnDescription(readerStatus);
                    Log.Debug(osCAMReader.Label + " reader was found with the stale state: " + readerStatus + " . Let's update its description.");
                }

                var readerLBValueReader = currentServerStatusList.Where(osCamSL => osCamSL.ReaderUser == osCAMReader.Device
                                                                           & osCamSL.Port == osCAMReader.Port
                                                                            //& osCamSL.OsCamReaderDescription.Username == osCAMReader.user
                                                                            )
                                                          .Select(sl => sl.LbValueReader).FirstOrDefault();

                if (readerLBValueReader == "no data")
                {
                    osCAMReader.UpdateNewFoundStateOnDescription("LBValueReader");
                    Log.Debug(osCAMReader.Label + " reader was found with the stale state: LBValueReader has not data. Let's update its description.");
                }
            }

            return Task.CompletedTask;
        }

        private static List<OsCamReader> AddNewScrapedReaders(List<OsCamReader> currentServerStatusList, List<OsCamReader> dailyListOfCLines)
        {
            var newReaders = new List<OsCamReader>();

            foreach (var line in dailyListOfCLines)
            {
                var OnFile = false;
                foreach (var currentlines in currentServerStatusList)
                {
                    if (line.Device == currentlines.Device &&
                        line.Port == currentlines.Port &&
                        line.User == currentlines.User)
                    {
                        OnFile = true;
                        break;
                    }
                }

                if (!OnFile)
                    newReaders.Add(line);
            }

            currentServerStatusList.AddRange(newReaders);

            Log.Information("Added " + newReaders.Count + " new readers to oscam.server");

            return currentServerStatusList;
        }

        private static void WriteOsCamReadersToFile(List<OsCamReader> currentServerStatusList, string oscamServerFilepath = @"oscam.server")
        {
            var readers = new List<OsCamReader>();

            foreach (var reader in currentServerStatusList)
            {
                if (readers.FirstOrDefault(camReader => camReader.Label.Contains(reader.Label)) != null)
                    reader.Label = reader.Label + Guid.NewGuid().ToString().Split('-')[0];

                readers.Add(reader);
            }

            using (StreamWriter sr = new StreamWriter(oscamServerFilepath, false, Encoding.ASCII))
            {
                foreach (var reader in readers)
                    sr.Write(reader.ToString());
            }

            Log.Information("Wrote a total of " + currentServerStatusList.Count + " readers to oscam.server");
        }

        private static async Task<List<OscamUiStatusLine>> GetListWithCurrentServerStatusFromOsCam(string osCamStatusPageURL)
        {
            var config = Configuration.Default.WithDefaultLoader();               // Create a new browsing context
            var context = BrowsingContext.New(config);                            // This is where the HTTP request happens, returns <IDocument> that // we can query later
            IDocument document = context.OpenAsync(osCamStatusPageURL).Result; // Log the data to the console
                                                                               //var asdf = document.DocumentElement.OuterHtml;
                                                                               // var docu = document.

            var rows = document.QuerySelectorAll("table.status tbody#tbodyp tr");

            var oscamUIStatusLine = new List<OscamUiStatusLine>();

            oscamUIStatusLine.AddRange(rows.Where(sl => sl != null)
                           .Select(sl => new OscamUiStatusLine()
                           {
                               Description = ((AngleSharp.Html.Dom.IHtmlTableDataCellElement)sl.QuerySelectorAll("td.statuscol4").FirstOrDefault())?.Title?.Substring(((AngleSharp.Html.Dom.IHtmlTableDataCellElement)sl.QuerySelectorAll("td.statuscol4").FirstOrDefault()).Title.LastIndexOf('\r') + 2)?.TrimEnd(')'),
                               ReaderUser = sl.QuerySelectorAll("td.statuscol4").Select(tg => tg.TextContent).FirstOrDefault()?.Trim(),
                               Port = sl.QuerySelectorAll("td.statuscol8").Select(tg => tg.TextContent).FirstOrDefault()?.Trim(),
                               LbValueReader = sl.QuerySelectorAll("td.statuscol14").Select(tg => tg.TextContent).FirstOrDefault()?.Trim(),
                               Status = sl.QuerySelectorAll("td.statuscol16").Select(tg => tg.TextContent).FirstOrDefault()?.Trim()
                           }));

            oscamUIStatusLine.RemoveAll(line => line.ReaderUser == null || line.Port == null || line.Status == null);

            return oscamUIStatusLine;
        }

        private static bool HasTheReaderAccessToTheCAIDScrapper(string osCamReaderPageURL, string[] CAIDs)
        {
            var config = Configuration.Default.WithDefaultLoader();               // Create a new browsing context
            var context = BrowsingContext.New(config);                            // This is where the HTTP request happens, returns <IDocument> that // we can query later
            IDocument document = context.OpenAsync(osCamReaderPageURL).Result; // Log the data to the console
                                                                               //var asdf = document.DocumentElement.OuterHtml;
                                                                               // var docu = document.

            foreach (string caid in CAIDs)
            {
                var result = document.QuerySelectorAll("table.stats")
                                    .FirstOrDefault(m => m.TextContent.Contains($"{caid}@"));

                if (result != null)
                    return true;
            }

            return false;
        }

        private static async Task<List<OsCamReader>> GetListofNewCLinesFromWeb(string url)
        {
            var date_yyyy_MM_dd = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Load default configuration
            var config = Configuration.Default.WithDefaultLoader();               // Create a new browsing context
            var context = BrowsingContext.New(config);                            // This is where the HTTP request happens, returns <IDocument> that // we can query later
            var document = await context.OpenAsync(url + date_yyyy_MM_dd + "/"); // Log the data to the console
                                                                                 //var asdf = document.DocumentElement.OuterHtml;
                                                                                 // var docu = document.
                                                                                 // workaround to get the previous day
            if (document.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                date_yyyy_MM_dd = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                document = await context.OpenAsync(url + date_yyyy_MM_dd + "/");
            }

            var lines = document.QuerySelectorAll("div.entry div")
                                                .Select(m => m.InnerHtml.Replace("<br>", "")
                                                .Trim().Split("\n"));

            if (lines.ToList().Count == 0)
                return new List<OsCamReader>();

            List<CcCamLine> cccamlines = new List<CcCamLine>();

            foreach (var cline in lines.ToList()[0])
                cccamlines.Add(ParseCLine(cline));


            List<OsCamReader> readers = new List<OsCamReader>();

            readers.AddRange(cccamlines.Where(cl => cl != null)
                                       .Select(cl => new OsCamReader()
                                       {
                                           Device = cl.Hostname,
                                           Port = cl.Port,
                                           User = cl.Username,
                                           Password = cl.Password,
                                           Label = cl.Hostname,
                                           Cccversion = cl.Cccversion,
                                           Cccwantemu = cl.Wantemus,
                                           Description = "0;0;0;0;" + cl.Username
                                       }));

            Log.Information("Retrieved " + readers.Count + " C lines from " + url);

            return readers;
        }

        private static CcCamLine ParseCLine(string cline)
        {
            if (!cline.ToString().StartsWith(@"C:"))
                return null;

            CcCamLine line = new CcCamLine();

            if (cline.LastIndexOf('#') != -1)
            {
                int lastIndexOfCardinal = cline.LastIndexOf('#');

                var c = cline.Substring(lastIndexOfCardinal + 1, cline.Length - lastIndexOfCardinal - 1).Trim().Replace("v", "");
                line.Cccversion = c.Remove(c.IndexOf("-"), c.Length - c.IndexOf("-"));

                cline = cline.Substring(0, cline.IndexOf("#") - 1).Trim();
            }

            var s = cline.Replace("C:", "").Replace("c:", "").Trim().Split(" ");

            line.Hostname = s[0];
            line.Port = s[1];
            line.Username = s[2];
            line.Password = s[3];

            //try
            //{
            //    line.wantemus = s[4] == "no" ? "no" : "yes";
            //}
            //catch { }

            return line;
        }

        private static IServiceProvider Initialize()
        {
            try
            {
                var services = new ServiceCollection();

                var configuration = new ConfigurationBuilder()
                                .AddJsonFile("appsettings.json")
                                .AddEnvironmentVariables()
                                .Build();

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .WriteTo.Providers(Providers)
                    .CreateLogger();

                services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(serviceProvider => configuration);

                services.AddSingleton(serviceProvider =>
                {
                    MaintainerOptions maintainerOptions = new MaintainerOptions();
                    configuration.GetSection("OsCam").Bind(maintainerOptions);

                    return maintainerOptions;
                });

                return services.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                Log.Error("Error while initializing OSCam.Maintainer", ex);
                throw;
            }
        }

        private static async void Adsfsd()
        {
            var httpClient = new HttpClient();
            //var someXmlString = "<SomeDto><SomeTag>somevalue</SomeTag></SomeDto>";
            //var stringContent = new StringContent(someXmlString, Encoding.UTF8, "application/xml");
            var response = await httpClient.GetAsync("http://192.168.1.244:8888/oscamapi.html?part=entitlement&label=ru256.cserver.tv");

            response.EnsureSuccessStatusCode();

            if (response.IsSuccessStatusCode)
            {
                XmlSerializer serializer = new XmlSerializer(typeof(oscam));
                using (StringReader reader = new StringReader(await response.Content.ReadAsStringAsync()))
                {
                    var test = (oscam)serializer.Deserialize(reader);

                    var totalCardCount = test.reader.Select(oscamReader => oscamReader)
                                        .FirstOrDefault()
                                        ?.cardlist.FirstOrDefault()
                                        ?.totalcards;
                    if (totalCardCount == null ||
                        int.Parse(totalCardCount) == 0)
                    {

                    }



                }

            }

        }

        private static async Task<bool> HasTheReaderAccessToTheCAID(string osCamReaderPageURL, string[] CAIDs)
        {
            try
            {

                XmlSerializer serializer = new XmlSerializer(typeof(oscam));
                using (var httpClient = new HttpClient())
                {
                    httpClient.BaseAddress = new Uri(osCamReaderPageURL);
                    httpClient.DefaultRequestHeaders.Accept.Clear();

                    //var someXmlString = "<SomeDto><SomeTag>somevalue</SomeTag></SomeDto>";
                    //var stringContent = new StringContent(someXmlString, Encoding.UTF8, "application/xml");
                    var response = await httpClient.GetAsync(osCamReaderPageURL).ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();

                    if (response.IsSuccessStatusCode)
                    {

                        using (StringReader reader =
                            new StringReader(await response.Content.ReadAsStringAsync().ConfigureAwait(false)))
                        {
                            var test = (oscam)serializer.Deserialize(reader);


                            var totalCardCount = test.reader?.Select(oscamReader => oscamReader)
                                                     .FirstOrDefault()
                                                     ?.cardlist.FirstOrDefault()
                                                     ?.totalcards;

                            if (totalCardCount == null ||
                                int.Parse(totalCardCount) == 0)
                            {
                                return false;
                            }

                            foreach (string caid in CAIDs)
                            {

                                var hasCaid = (test.reader.Select(oscamReader => oscamReader)
                                                   .FirstOrDefault()
                                                   ?.cardlist.FirstOrDefault().card)
                                    .FirstOrDefault(card => card.caid.Contains(caid));

                                if (hasCaid != null)
                                    return true;
                            }

                            return false;


                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }

            return false;


        }
    }
}
