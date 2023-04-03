using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Diagnostics;
using System.Formats.Asn1;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace ReserveBlockCore.Services
{
    
    public class VersionControlService
    {
        #region GitHub Classes
        private class Release
        {
            public string url { get; set; }
            public string html_url { get; set; }
            public string assets_url { get; set; }
            public string upload_url { get; set; }
            public int id { get; set; }
            public string node_id { get; set; }
            public string tag_name { get; set; }
            public string target_commitish { get; set; }
            public string name { get; set; }
            public string body { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public DateTimeOffset created_at { get; set; }
            public DateTimeOffset? published_at { get; set; }
            public Author author { get; set; }
            public string tarball_url { get; set; }
            public string zipball_url { get; set; }
            public IReadOnlyList<ReleaseAsset> assets { get; set; }
        }

        private class ReleaseAsset
        {
            public string url { get; set; }
            public int id { get; set; }
            public string node_id { get; set; }
            public string name { get; set; }
            public string label { get; set; }
            public string State { get; set; }
            public string content_type { get; set; }
            public int size { get; set; }
            public int download_count { get; set; }
            public DateTimeOffset created_at { get; set; }
            public DateTimeOffset updated_at { get; set; }
            public string browser_download_url { get; set; }
            public Author uploader { get; set; }

        }

        private class Author
        {
            public string login { get; set; }
            public int id { get; set; }
            public string node_id { get; set; }
            public string avatar_url { get; set; }
            public string url { get; set; }
            public string html_url { get; set; }
            public string followers_url { get; set; }
            public string following_url { get; set; }
            public string gists_url { get; set; }
            public string starred_url { get; set; }
            public string subscriptions_url { get; set; }
            public string organizations_url { get; set; }
            public string repos_url { get; set; }
            public string events_url { get; set; }
            public string received_events_url { get; set; }
            public string type { get; set; }
            public bool site_admin { get; set; }

        }

        #endregion

        static SemaphoreSlim VesrionControlServiceLock = new SemaphoreSlim(1, 1);
        public static async Task RunVersionControl()
        {
            while(true)
            {
                var delay = Task.Delay(new TimeSpan(12,0,0));
                await VesrionControlServiceLock.WaitAsync();
                try
                {
                    await CheckVersion();
                }
                finally
                {
                    VesrionControlServiceLock.Release();
                }

                await delay;
            }
        }

        public static async Task DownloadLatestRelease()
        {
            try
            {
                if (!Globals.UpToDate) //add the ! back to beginning
                {
                    var count = 1;
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    Console.WriteLine("Please select the appropriate download:");
                    Globals.GitHubLatestReleaseAssetsDict.Keys.ToList().ForEach(asset => {
                        Console.WriteLine($"{count}. {asset}");
                        dict.Add(count.ToString(), asset);
                        count += 1;
                    });

                    var assetChoice = Console.ReadLine();
                    if(!string.IsNullOrEmpty(assetChoice))
                    {
                        dict.TryGetValue(assetChoice, out var asset);
                        if(asset != null)
                        {
                            AnsiConsole.MarkupLine($"You have chosen to download: {asset}. Are you sure? ([green]'y'[/] for [green]yes[/] and [red]'n'[/] for [red]no[/])");
                            var confirm = Console.ReadLine();
                            if(!string.IsNullOrEmpty(confirm))
                            {
                                if(confirm.ToLower() == "y")
                                {
                                    Globals.GitHubLatestReleaseAssetsDict.TryGetValue(asset, out var downloadUrl);
                                    if(downloadUrl != null)
                                    {
                                        var path = DownloadPath();
                                        int progress = 0;
                                        bool isComplete = false;
                                        AnsiConsole.MarkupLine($"Beginning download of [purple]{asset}[/].");
                                        AnsiConsole.MarkupLine($"File Download coming from: [yellow]{downloadUrl}.[/]");
                                        AnsiConsole.MarkupLine($"File being downloaded to: [aqua]{path}.[/]");
                                        
                                        using (WebClient client = new WebClient())
                                        {
                                            client.DownloadProgressChanged += (s, e) =>
                                            {
                                                progress = e.ProgressPercentage;
                                            };
                                            client.DownloadFileCompleted += (s, e) =>
                                            {
                                                isComplete = true;
                                            };
                                            client.DownloadFileAsync(new Uri(downloadUrl), path + asset);
                                            while(!isComplete)
                                            {
                                                if(progress > 0 && progress <= 25)
                                                {
                                                    AnsiConsole.Markup($"\rDownloading File. Progress: [yellow]{progress}%[/]");
                                                }
                                                else if(progress > 25 && progress <= 50)
                                                {
                                                    AnsiConsole.Markup($"\rDownloading File. Progress: [purple]{progress}%[/]");
                                                }
                                                else if(progress > 50 && progress <= 99)
                                                {
                                                    AnsiConsole.Markup($"\rDownloading File. Progress: [blue]{progress}%[/]");
                                                }
                                                else if(progress == 100)
                                                {
                                                    AnsiConsole.Markup($"\rDownloading File. Progress: [green]{progress}%[/]");
                                                }
                                                else
                                                {
                                                    //do nothing
                                                }
                                            }
                                            Console.WriteLine($" ");
                                            Console.WriteLine($" ");
                                            Console.WriteLine($"Download is Complete. Download is located in: {path + asset}");

                                            AnsiConsole.MarkupLine($"Would you like to update files now? ([green]'y'[/] for [green]yes[/] and [red]'n'[/] for [red]no[/])");
                                            var confirmUpdate = Console.ReadLine();
                                            if(!string.IsNullOrEmpty(confirmUpdate))
                                            {
                                                if(confirmUpdate.ToLower() == "y")
                                                {
                                                    var assetPath = path + asset;
                                                    var unzippedPath = path + @"temp" + Path.DirectorySeparatorChar;
                                                    await PerformUpdate(assetPath, unzippedPath);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        StartupService.MainMenu();
                                        AnsiConsole.MarkupLine("[yellow]There was an issue with download URL. Please go to RBX GitHub for latest version. Returned to main menu.[/]");
                                    }
                                }
                                else
                                {
                                    StartupService.MainMenu();
                                    AnsiConsole.MarkupLine("[yellow]You did not selected 'Yes'. Returned to main menu.[/]");
                                }
                            }
                        }
                        else
                        {
                            StartupService.MainMenu();
                            AnsiConsole.MarkupLine("[yellow]Incorrect choice selected. Returned to main menu.[/]");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Client is already up to date.");
                    AnsiConsole.MarkupLine($"Client Version: [blue]{Globals.CLIVersion}[/] | GitHub Tag: [green]{Globals.GitHubVersion}[/]");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("There was an error updating client. Please download from GitHub.");
            }
            
        }

        public static async Task<bool> DownloadLatestAndUpdate(string fileName, bool update = false)
        {
            try
            {
                if (!Globals.UpToDate) //add the ! back to beginning
                {
                    Globals.GitHubLatestReleaseAssetsDict.TryGetValue(fileName, out var downloadUrl);
                    if (downloadUrl != null)
                    {
                        var path = DownloadPath();
                        bool isComplete = false;

                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFileCompleted += (s, e) =>
                            {
                                isComplete = true;
                            };

                            client.DownloadFileAsync(new Uri(downloadUrl), path + fileName);
                            while (!isComplete)
                            {

                            }
                            if (update)
                            {
                                var assetPath = path + fileName;
                                var unzippedPath = path + "temp" + Path.DirectorySeparatorChar;
                                var result = await PerformUpdate(assetPath, unzippedPath);
                                if (result)
                                {
                                    return true;
                                }
                                else
                                {
                                    return false;
                                }
                            }

                        }
                    }
                }
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Error Performing update. Error: {ex.ToString()}", "VersionControlService.DownloadLatestAndUpdate()");
            }
            return false;
        }

        public static async Task<List<string>?> GetLatestDownloadFiles()
        {
            await CheckVersion();
            var fileList = Globals.GitHubLatestReleaseAssetsDict.Keys.ToList();

            if(fileList.Count() > 0)
                return fileList;

            return null;
        }

        private static async Task<bool> PerformUpdate(string assetPath, string unzipPath)
        {
            try
            {
                if (Directory.Exists(unzipPath))
                    Directory.Delete(unzipPath, true);
                System.IO.Compression.ZipFile.ExtractToDirectory(assetPath, unzipPath);
                var directory = Directory.GetDirectories(unzipPath);
                var newFilesPath = directory.FirstOrDefault();
                var filePaths = Directory.GetFiles(newFilesPath);

                string strFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (strFilePath != null)
                {
                    string? strWorkPath = Path.GetDirectoryName(strFilePath);
                    if (strWorkPath != null)
                    {
                        foreach (var filePath in filePaths)
                        {
                            var fileName = Path.GetFileName(filePath);
                            var oldFilePath = strWorkPath + Path.DirectorySeparatorChar + fileName;
                            if (File.Exists(oldFilePath))
                            {
                                File.Move(oldFilePath, oldFilePath + "_outdated");
                                File.Move(filePath, oldFilePath);
                            }
                        }

                        Environment.SetEnvironmentVariable("RBX-Updated", "1", EnvironmentVariableTarget.User);
                        StartupService.MainMenu();
                        AnsiConsole.MarkupLine("[green]Update Has Finished. Please restart client to complete update.[/]");
                        AnsiConsole.MarkupLine("[yellow]Failure to restart may result in client glitches. Please restart now.[/]");

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Performing Updated. Error: {ex.ToString()}");
                ErrorLogUtility.LogError($"Error Performing Updated. Error: {ex.ToString()}", "VersionControlService.PerformUpdate()");
            }

            return false;
        }

        public static async Task DeleteOldFiles()
        {
            try
            {
                string strFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (strFilePath != null)
                {
                    string? strWorkPath = System.IO.Path.GetDirectoryName(strFilePath);
                    if (strWorkPath != null)
                    {
                        var files = Directory.GetFiles(strWorkPath);
                        var filesFiltered = files.Where(x => x.Contains("_outdated")).ToList();
                        foreach (var file in filesFiltered)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"DeleteOldFiles() Error: {ex.ToString()}");
                ErrorLogUtility.LogError($"DeleteOldFiles() Error: {ex.ToString()}", "VersionControlService.DeleteOldFiles()");
            }
            
        }

        private static string DownloadPath()
        {
            string MainFolder = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            var downloadLocation = Globals.IsTestNet != true ? "Download" : "DownloadTestNet";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + MainFolder.ToLower() + Path.DirectorySeparatorChar + downloadLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + downloadLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + MainFolder + Path.DirectorySeparatorChar + downloadLocation + Path.DirectorySeparatorChar;
                }
            }

            if (!string.IsNullOrEmpty(Globals.CustomPath))
            {
                path = Globals.CustomPath + MainFolder + Path.DirectorySeparatorChar + downloadLocation + Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        private static async Task CheckVersion()
        {
            try
            {
                if(Globals.NewUpdateLastChecked <= DateTime.UtcNow.AddHours(-1)) 
                {
                    var url = Globals.GitHubApiURL + Globals.GitHubRBXRepoURL;
                    using (var client = Globals.HttpClientFactory.CreateClient())
                    {
                        var productValue = new ProductInfoHeaderValue("RBX-Version-Check", "1.0");
                        client.DefaultRequestHeaders.UserAgent.Add(productValue);

                        using (var Response = await client.GetAsync(url, new CancellationTokenSource(5000).Token))
                        {
                            if (Response.StatusCode == HttpStatusCode.OK)
                            {
                                var responseString = await Response.Content.ReadAsStringAsync();
                                var release = JsonConvert.DeserializeObject<Release>(responseString.ToString());
                                if (release != null)
                                {
                                    var version = release.tag_name;
                                    if (version != Globals.GitHubVersion)
                                    {
                                        Globals.UpToDate = false;//out of date
                                        Globals.GitHubLatestReleaseVersion = version;
                                    }
                                    else
                                    {
                                        Globals.UpToDate = true;//up to date
                                        Globals.GitHubLatestReleaseVersion = version;
                                    }

                                    if (release.assets.Count > 0)
                                    {
                                        foreach (var asset in release.assets)
                                        {
                                            var added = Globals.GitHubLatestReleaseAssetsDict.TryAdd(asset.name, asset.browser_download_url);
                                            if (!added)
                                                Globals.GitHubLatestReleaseAssetsDict[asset.name] = asset.browser_download_url;
                                        }
                                    }

                                    Globals.NewUpdateLastChecked = DateTime.UtcNow;
                                }
                            }
                            else
                            {

                            }
                        }
                    }
                }
            }
            catch(Exception ex) 
            {
                //ConsoleWriterService.Output($"Error Checking GitHub for update. Error: {ex.ToString()}");
            }
        }

        
    }
}
