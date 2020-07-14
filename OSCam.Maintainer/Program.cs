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
using System.Text;
using System.Threading.Tasks;

namespace OSCam.Maintainer
{
    partial class Program
    {
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
            try
            {
                var maintainerOptions = serviceProvider.GetRequiredService<MaintainerOptions>();
                
                var task_A = GetListofNewCLinesFromWeb(maintainerOptions.URLToScrap);
                var task_B = GetListWithCurrentServerStatusFromOsCam(maintainerOptions.OsCamStatusPageURL);
                var task_C = GetListWithCurrentReadersOnOscamServerFile(maintainerOptions.OscamServerPath);

                await Task.WhenAll(task_A, task_B, task_C);

                var dailyListOfCLines = await task_A;
                var currentServerStatusList = await task_B;
                var currentListOfCCCamReadersFromFile = await task_C;

                await UpdateServersDescription(ref currentListOfCCCamReadersFromFile, currentServerStatusList);

                await DeleteStaleReaders(ref currentListOfCCCamReadersFromFile);

                var currentListOfCCCamReadersFromFileNEW = AddNewScrapedReaders(currentListOfCCCamReadersFromFile, dailyListOfCLines);

                WriteOsCamReadersToFile(currentListOfCCCamReadersFromFileNEW, maintainerOptions.OscamServerPath); // + DateTime.Now.ToShortTimeString().Replace(":","") + ".txt");
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
                if (reader.description.IndexOf("6;") >= 0)
                {
                    readersToRemove.Add(reader);
                    Log.Debug(reader.label + " is stale with: " + reader.description + " and is flagged to be deleted");
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

                        var asdf = linha.Split("=");

                        switch (asdf[0].Trim().ToLower())
                        {
                            case "[reader]":
                                {
                                    if (!string.IsNullOrEmpty(reader.label))
                                    {
                                        lista.Add(reader);
                                        counter++;
                                    }

                                    reader = new OsCamReader();
                                    continue;
                                }
                            case "label":
                                reader.label = string.IsNullOrEmpty(asdf[1]) ? reader.label : asdf[1].Trim();
                                continue;
                            case "description":
                                reader.description = string.IsNullOrEmpty(asdf[1]) ? reader.description : asdf[1].Trim();
                                continue;
                            case "enable":
                                reader.enable = string.IsNullOrEmpty(asdf[1]) ? reader.enable : asdf[1].Trim();
                                continue;
                            case "protocol":
                                reader.protocol = string.IsNullOrEmpty(asdf[1]) ? reader.protocol : asdf[1].Trim();
                                continue;
                            case "device":
                                {
                                    if (!string.IsNullOrEmpty(asdf[1]))
                                    {
                                        var device = asdf[1].Split(',');
                                        reader.device = string.IsNullOrEmpty(device[0]) ? reader.device : device[0].Trim();
                                        reader.port = string.IsNullOrEmpty(device[1]) ? reader.port : device[1].Trim();
                                    }
                                    continue;
                                }
                            case "key":
                                reader.key = string.IsNullOrEmpty(asdf[1]) ? reader.key : asdf[1].Trim();
                                continue;
                            case "user":
                                reader.user = string.IsNullOrEmpty(asdf[1]) ? reader.user : asdf[1].Trim();
                                continue;
                            case "password":
                                reader.password = string.IsNullOrEmpty(asdf[1]) ? reader.password : asdf[1].Trim();
                                continue;
                            case "inactivitytimeout":
                                reader.inactivitytimeout = string.IsNullOrEmpty(asdf[1]) ? reader.inactivitytimeout : asdf[1].Trim();
                                continue;
                            case "group":
                                reader.group = string.IsNullOrEmpty(asdf[1]) ? reader.group : asdf[1].Trim();
                                continue;
                            case "cccversion":
                                reader.cccversion = string.IsNullOrEmpty(asdf[1]) ? reader.cccversion : asdf[1].Trim();
                                continue;
                            case "ccckeepalive":
                                reader.ccckeepalive = string.IsNullOrEmpty(asdf[1]) ? reader.ccckeepalive : asdf[1].Trim();
                                continue;
                            case "reconnecttimeout":
                                reader.reconnecttimeout = string.IsNullOrEmpty(asdf[1]) ? reader.reconnecttimeout : asdf[1].Trim();
                                continue;
                            case "lb_weight":
                                reader.lb_weight = string.IsNullOrEmpty(asdf[1]) ? reader.lb_weight : asdf[1].Trim();
                                continue;
                            case "cccmaxhops":
                                reader.cccmaxhops = string.IsNullOrEmpty(asdf[1]) ? reader.cccmaxhops : asdf[1].Trim();
                                continue;
                            case "cccwantemu":
                                reader.cccwantemu = string.IsNullOrEmpty(asdf[1]) ? reader.cccwantemu : asdf[1].Trim();
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

        private static Task UpdateServersDescription(ref List<OsCamReader> currentListOfCCCamReadersFromFile, List<OscamUIStatusLine> currentServerStatusList)
        {
            foreach (var osCAMReader in currentListOfCCCamReadersFromFile)
            {
                var readerStatus = currentServerStatusList.Where(osCamSL => osCamSL.ReaderUser == osCAMReader.device &
                                                                    osCamSL.Port == osCAMReader.port &
                                                                    osCamSL.OsCamReaderDescription.Username == osCAMReader.user)
                                                          .Select(sl => sl.Status).FirstOrDefault();

                if (readerStatus != null)
                { 
                    osCAMReader.UpdateNewFoundStateOnDescription(readerStatus);
                    Log.Debug(osCAMReader.label + " reader was found with the stale state: " + readerStatus + " . Let's update its description.");
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
                    if (line.device == currentlines.device &&
                        line.port == currentlines.port &&
                        line.user == currentlines.user)
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
            using (StreamWriter sr = new StreamWriter(oscamServerFilepath, false, Encoding.ASCII))
            {
                foreach (var reader in currentServerStatusList)
                    sr.Write(reader.ToString());
            }

            Log.Information("Wrote a total of " + currentServerStatusList.Count + " readers to oscam.server");
        }

        private static async Task<List<OscamUIStatusLine>> GetListWithCurrentServerStatusFromOsCam(string osCamStatusPageURL)
        {
            var config = Configuration.Default.WithDefaultLoader();               // Create a new browsing context
            var context = BrowsingContext.New(config);                            // This is where the HTTP request happens, returns <IDocument> that // we can query later
            IDocument document = context.OpenAsync(osCamStatusPageURL).Result; // Log the data to the console
                                                                                                    //var asdf = document.DocumentElement.OuterHtml;
                                                                                                    // var docu = document.

            var rows = document.QuerySelectorAll("table.status tbody#tbodyp tr");

            var oscamUIStatusLine = new List<OscamUIStatusLine>();

            oscamUIStatusLine.AddRange(rows.Where(sl => sl != null)
                           .Select(sl => new OscamUIStatusLine()
                           {
                               Description = ((AngleSharp.Html.Dom.IHtmlTableDataCellElement)sl.QuerySelectorAll("td.statuscol4").FirstOrDefault())?.Title?.Substring(((AngleSharp.Html.Dom.IHtmlTableDataCellElement)sl.QuerySelectorAll("td.statuscol4").FirstOrDefault()).Title.LastIndexOf('\r') + 2)?.TrimEnd(')'),
                               ReaderUser = sl.QuerySelectorAll("td.statuscol4").Select(tg => tg.TextContent).FirstOrDefault()?.Trim(),
                               Port = sl.QuerySelectorAll("td.statuscol8").Select(tg => tg.TextContent).FirstOrDefault()?.Trim(),
                               Status = sl.QuerySelectorAll("td.statuscol16").Select(tg => tg.TextContent).FirstOrDefault()?.Trim()
                           }));

            oscamUIStatusLine.RemoveAll(line => line.ReaderUser == null || line.Port == null || line.Status == null);

            return oscamUIStatusLine;
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

            List<CCCamLine> cccamlines = new List<CCCamLine>();

            foreach (var cline in lines.ToList()[0])
                cccamlines.Add(ParseCLine(cline));
            

            List<OsCamReader> readers = new List<OsCamReader>();

            readers.AddRange(cccamlines.Where(cl => cl != null)
                                       .Select(cl => new OsCamReader()
                                       {
                                           device = cl.hostname,
                                           port = cl.port,
                                           user = cl.username,
                                           password = cl.password,
                                           label = cl.hostname,
                                           cccversion = cl.cccversion,
                                           cccwantemu = cl.wantemus,
                                           description = "0;0;0;"+ cl.username
                                       }));

            Log.Information("Retrieved " + readers.Count + " C lines from " + url);

            return readers;
        }

        private static CCCamLine ParseCLine(string cline)
        {
            if (!cline.ToString().StartsWith(@"C:"))
                return null;

            CCCamLine line = new CCCamLine();

            if (cline.LastIndexOf('#') != -1)
            {
                int lastIndexOfCardinal = cline.LastIndexOf('#');

                var c = cline.Substring(lastIndexOfCardinal + 1, cline.Length - lastIndexOfCardinal - 1).Trim().Replace("v", "");
                line.cccversion = c.Remove(c.IndexOf("-"), c.Length - c.IndexOf("-"));

                cline = cline.Substring(0, cline.IndexOf("#") - 1).Trim();
            }

            var s = cline.Replace("C:", "").Replace("c:", "").Trim().Split(" ");

            line.hostname = s[0];
            line.port = s[1];
            line.username = s[2];
            line.password = s[3];

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
    }
}
