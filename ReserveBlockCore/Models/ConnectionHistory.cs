using Newtonsoft.Json;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Models
{
    public class ConnectionHistory
    {
        public List<string> IPAddress { get; set; }
        public string Address { get; set; }
        public int LongestConnectionTime { get; set;  }
        public int ConnectionAttempts { get; set; }
        public DateTime FirstConnectionAttempt { get; set; }
        public DateTime LastConnectionAttempt { get; set; }
        public bool WasLastConnectionAttemptSuccessful { get; set; }

        public class ConnectionHistoryQueue
        {
            public string IPAddress { get; set; }
            public string Address { get; set; }
            public int ConnectionTime { get; set; }
            public bool WasSuccess { get; set; }
        }

        public void Process(string ipAddress, string address, int connectionTime, bool wasSuccess)
        {
            var recExist = Globals.ConnectionHistoryList.Any(x => x.Address == address);
            if(!recExist)
            {
                Globals.ConnectionHistoryList.Add(new ConnectionHistory {IPAddress = new List<string> { ipAddress }, Address = address, 
                    LongestConnectionTime = connectionTime, ConnectionAttempts = 1, FirstConnectionAttempt = DateTime.UtcNow, LastConnectionAttempt = DateTime.UtcNow,
                WasLastConnectionAttemptSuccessful = wasSuccess});
            }
            else
            {
                Update(ipAddress, address, connectionTime, wasSuccess);
            }
        }

        public void Update(string ipAddress, string address, int connectionTime, bool wasSuccess)
        {
            var rec = Globals.ConnectionHistoryList.Where(x => x.Address == address).FirstOrDefault();
            if(rec != null)
            {
                rec.Address = address;
                var ipExist = rec.IPAddress.Any(x => x == ipAddress);
                if (!ipExist)
                    rec.IPAddress.Add(ipAddress);
                rec.LongestConnectionTime = rec.LongestConnectionTime < connectionTime ? connectionTime : rec.LongestConnectionTime;
                rec.ConnectionAttempts += 1;
                rec.LastConnectionAttempt = DateTime.UtcNow;
                rec.WasLastConnectionAttemptSuccessful = wasSuccess;
            }
        }

        public static async Task<string> Read()
        {
            var conList = Globals.ConnectionHistoryList.Select(x => new {
                IPAddress = x.IPAddress,
                Address = x.Address,
                LongestConnectionTime = x.LongestConnectionTime,
                ConnectionAttempts = x.ConnectionAttempts,
                FirstConnectionAttempt = x.FirstConnectionAttempt,
                LastConnectionAttempt = x.LastConnectionAttempt,
                WasLastConnectionAttemptSuccessful = x.WasLastConnectionAttemptSuccessful
            }).ToList();

            var conListJson = JsonConvert.SerializeObject(conList);

            return conListJson;
        }

        public static async void WriteToConHistFile(string text)
        {
            try
            {
                var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
                var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

                string path = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                await File.WriteAllTextAsync(path + "connection_history.txt", text);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
