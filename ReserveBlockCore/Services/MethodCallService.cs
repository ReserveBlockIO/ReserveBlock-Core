using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Text;

namespace ReserveBlockCore.Services
{
    
    public class MethodCallService
    {
        static SemaphoreSlim MethodCallServiceLock = new SemaphoreSlim(1, 1);

        public static async Task Run()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 25));

                await MethodCallServiceLock.WaitAsync();
                try
                {
                    await GetMethodCallInfo();
                }
                finally
                {
                    MethodCallServiceLock.Release();
                }

                await delay;
            }
        }

        private static async Task GetMethodCallInfo()
        {
            try
            {
                var orderedDict = Globals.MethodDict.OrderBy(x => x.Key).ToList();
                StringBuilder strBld = new StringBuilder();
                foreach (var methodCall in orderedDict)
                {
                    strBld.AppendLine("---------------------------------------------------------------------");
                    strBld.AppendLine($"Name: {methodCall.Key} | Enters: {methodCall.Value.Enters}, Exits: {methodCall.Value.Exits}, Exceptions: {methodCall.Value.Exceptions}");
                }

                MemoryLogUtility.WriteToMemLog("methodcallerlog.txt", strBld.ToString());
            }
            catch { }

        }
    }
}
