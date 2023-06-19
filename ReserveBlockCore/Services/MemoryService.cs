using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace ReserveBlockCore.Services
{
    public class MemoryService
    {
        static SemaphoreSlim MemoryServiceLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim GlobalMemoryServiceLock = new SemaphoreSlim(1, 1);
        private static bool GetMemoryInfoError = false;
        private static bool GetGlobalMemoryInfoError = false;
        private static long LogNameAttribute = TimeUtil.GetTime();
        public static ConcurrentDictionary<string, decimal> GlobalMemoryDict = new ConcurrentDictionary<string, decimal>();

        public static async Task Run()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0,0,5));

                await MemoryServiceLock.WaitAsync();
                try
                {
                    await GetMemoryInfo();
                }
                finally
                {
                    MemoryServiceLock.Release();
                }

                await delay;
            }
        }

        public static async Task RunGlobals()
        {
            if(Globals.LogMemory)
            {
                while (true)
                {
                    var delay = Task.Delay(new TimeSpan(0, 0, 20));

                    await GlobalMemoryServiceLock.WaitAsync();
                    try
                    {
                        await GetGlobalMemoryInfo();
                    }
                    finally
                    {
                        GlobalMemoryServiceLock.Release();
                    }

                    await delay;
                }
            }
        }

        private static async Task GetMemoryInfo()
        {
            try
            {
                Process proc = Process.GetCurrentProcess();
                var workingSetMem = proc.WorkingSet64;

                Globals.CurrentMemory = Math.Round((decimal)workingSetMem / 1024 / 1024, 2);

                if (Globals.CurrentMemory >= 800M){ Globals.MemoryOverload = true; }
                else{ Globals.MemoryOverload = false; }
            }
            catch(Exception ex) 
            {
                if(!GetMemoryInfoError)
                {
                    GetMemoryInfoError = true;
                    ErrorLogUtility.LogError($"Error Logging. Error: {ex.ToString()}", "MemoryService.GetMemoryInfo()");
                }
                
            }
            
        }

        private static async Task GetGlobalMemoryInfo()
        {
            try
            {
                BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public;
                List<string> IgnoreList = new List<string> { "BlocksDownloadSlim", "CancelledToken" };

                foreach (FieldInfo field in typeof(Globals).GetFields(bindingFlags))
                {
                    try
                    {
                        var fieldName = field.Name;
                        if (!IgnoreList.Contains(fieldName))
                        {
                            var fieldValue = field.GetValue(null);
                            if (fieldValue != null)
                            {
                                var itemByte = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(fieldValue);
                                var memoryInMB = Math.Round((decimal)itemByte.Count() / 1024 / 1024, 8); ;

                                var result = GlobalMemoryDict.TryAdd(fieldName, memoryInMB);
                                if (!result)
                                    GlobalMemoryDict[fieldName] = memoryInMB;
                            }
                            else
                            {
                                var result = GlobalMemoryDict.TryAdd(fieldName, 0M);
                                if (!result)
                                    GlobalMemoryDict[fieldName] = 0M;
                            }
                        }
                    }
                    catch { }
                }

                var orderedDict = GlobalMemoryDict.OrderBy(x => x.Value < 1M).ThenBy(x => x.Key).ToList();
                StringBuilder strBld = new StringBuilder();
                var gcMemInfo = GC.GetGCMemoryInfo();
                strBld.AppendLine("------------------------App Memory Usage-----------------------------");
                strBld.AppendLine($"Start Memory: {Globals.StartMemory} | Current Memory: {Globals.CurrentMemory}");
                strBld.AppendLine($"Last Time Logged: {DateTime.UtcNow}");
                strBld.AppendLine($"GC Generation: {gcMemInfo.Generation}");
                strBld.AppendLine("---------------------------------------------------------------------");
                foreach (var globalMemItem in orderedDict)
                {
                    strBld.AppendLine("---------------------------------------------------------------------");
                    var memoryText = globalMemItem.Value > 1M ? $"!*^*^*!Size(Mb): {globalMemItem.Value}" : $"Size(Mb): {globalMemItem.Value}";
                    
                    strBld.AppendLine($"Name: {globalMemItem.Key} | {memoryText}");
                }

                if(Globals.AdjudicateAccount == null)
                {
                    
                    MemoryLogUtility.WriteToMemLog($"memorylog_{LogNameAttribute}.txt", strBld.ToString());
                }
                else
                {
                    MemoryLogUtility.WriteToMemLog($"memorylog.txt", strBld.ToString());
                }
               
            }
            catch (Exception ex)
            {
                if (!GetGlobalMemoryInfoError)
                {
                    GetGlobalMemoryInfoError = true;
                    ErrorLogUtility.LogError($"Error Logging. Error: {ex.ToString()}", "MemoryService.GetGlobalMemoryInfo()");
                }
            }
        }

        public static long GetTotalMemory()
        {
            if (OperatingSystem.IsWindows())
            {
                return GetTotalMemoryWindows();
            }
            else if (OperatingSystem.IsMacOS())
            {
                return GetTotalMemoryMacOS();
            }
            else if (OperatingSystem.IsLinux())
            {
                return GetTotalMemoryLinux();
            }
            else
            {
                throw new NotSupportedException("Unsupported operating system.");
            }
        }

        private static long GetTotalMemoryWindows()
        {
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            long installedMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
            // it will give the size of memory in MB
            var physicalMemory = (long)Math.Round((double)installedMemory / 1024000.0 / 1024);

            return physicalMemory;
        }

        private static long GetTotalMemoryMacOS()
        {
            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/sbin/sysctl",
                        Arguments = "hw.memsize",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (long.TryParse(output.Split(':')[1].Trim(), out long memory))
                {
                    return memory;
                }
            }
            catch
            {

            }

            return 1;
        }

        private static long GetTotalMemoryLinux()
        {
            try
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "free",
                        Arguments = "-b",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split('\n');
                var memoryLine = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (long.TryParse(memoryLine[1], out long memory))
                {
                    return memory;
                }
            }
            catch { }
            return 1;
        }

    }
}
