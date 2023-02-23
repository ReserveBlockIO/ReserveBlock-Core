using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Reflection;
using System.Text;

namespace ReserveBlockCore.Services
{
    public class MemoryService
    {
        static SemaphoreSlim MemoryServiceLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim GlobalMemoryServiceLock = new SemaphoreSlim(1, 1);
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
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 30));

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

        private static async Task GetMemoryInfo()
        {
            try
            {
                Process proc = Process.GetCurrentProcess();
                var workingSetMem = proc.WorkingSet64;

                Globals.CurrentMemory = Math.Round((decimal)workingSetMem / 1024 / 1024, 2);
            }
            catch { }
            
        }

        private static async Task GetGlobalMemoryInfo()
        {
            try
            {
                BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public;
                List<string> IgnoreList = new List<string> { "BlocksDownloadSlim", "CancelledToken" };

                foreach (FieldInfo field in typeof(Globals).GetFields(bindingFlags))
                {
                    var fieldName = field.Name;
                    if (!IgnoreList.Contains(fieldName))
                    {
                        var fieldValue = field.GetValue(null);
                        if(fieldValue != null)
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

                var orderedDict = GlobalMemoryDict.OrderBy(x => x.Key).ToList();
                StringBuilder strBld = new StringBuilder();
                strBld.AppendLine("------------------------App Memory Usage-----------------------------");
                strBld.AppendLine($"Start Memory: {Globals.StartMemory} | Current Memory: {Globals.CurrentMemory}");
                strBld.AppendLine("---------------------------------------------------------------------");
                foreach (var globalMemItem in orderedDict)
                {
                    strBld.AppendLine("---------------------------------------------------------------------");
                    var memoryText = globalMemItem.Value > 1M ? $"!*^*^*!Size(Mb): {globalMemItem.Value}" : $"Size(Mb): {globalMemItem.Value}";
                    
                    strBld.AppendLine($"Name: {globalMemItem.Key} | {memoryText}");
                }

                MemoryLogUtility.WriteToMemLog("memorylog.txt", strBld.ToString());
            }
            catch { }
            

        }

    }
}
