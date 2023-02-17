using ReserveBlockCore.Commands;
using ReserveBlockCore.Models;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class LinuxUtilities
    {
        static SemaphoreSlim AdjAutoRestartLock = new SemaphoreSlim(1, 1);
        
        public static async Task ClientRestart()
        {
            var shutDownDelay = Task.Delay(2000);
            LogUtility.Log("Send exit has been called. Closing Wallet.", "WindowsUtilities.ClientRestart()");
            Globals.StopAllTimers = true;
            await shutDownDelay;
            while (Globals.TreisUpdating)
            {
                await Task.Delay(300);
                //waiting for treis to stop
            }
            await Settings.InitiateShutdownUpdate();
            await BaseCommandServices.ConsensusNodeInfo();

            Environment.SetEnvironmentVariable("RBX-Restart", "1", EnvironmentVariableTarget.User);
            var exeLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);   
            var path = exeLocation + @"ReserveBlockCore.exe";
            ProcessStartInfo info = new ProcessStartInfo(path);
            Process.Start(info);

            Environment.Exit(0);
        }
        
        public static async Task TestMethod()
        {
            var shutDownDelay = Task.Delay(2000);
            LogUtility.Log("Send exit has been called. Closing Wallet.", "WindowsUtilities.AdjAutoRestart()");
            Globals.StopAllTimers = true;
            await shutDownDelay;
            while (Globals.TreisUpdating)
            {
                await Task.Delay(300);
                //waiting for treis to stop
            }

            await Settings.InitiateShutdownUpdate();

            Environment.SetEnvironmentVariable("RBX-Restart", "1", EnvironmentVariableTarget.User);
            var exeLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            var path = exeLocation + @"/ReserveBlockCore.dll";
            ProcessStartInfo info = new ProcessStartInfo("dotnet");
            info.Arguments = path;
            Process.Start(info);

            Environment.Exit(0);
        }

        public static async Task AdjAutoRestart()
        {
            if(Globals.AdjudicateAccount != null)
            {
                while (true)
                {
                    var delay = Task.Delay(4000);
                    if (Globals.StopAllTimers && !Globals.IsChainSynced)
                    {
                        await delay;
                        continue;
                    }
                    await AdjAutoRestartLock.WaitAsync();
                    try
                    {
                        var currentTime = TimeUtil.GetTime();
                        var lastBlockTime = Globals.LastBlockAddedTimestamp;
                        var timeDiff = currentTime - lastBlockTime;
                        if (timeDiff > 80) 
                        {
                            var shutDownDelay = Task.Delay(2000);
                            LogUtility.Log("Send exit has been called. Closing Wallet.", "WindowsUtilities.AdjAutoRestart()");
                            Globals.StopAllTimers = true;
                            await shutDownDelay;
                            while (Globals.TreisUpdating)
                            {
                                await Task.Delay(300);
                                //waiting for treis to stop
                            }

                            await BaseCommandServices.ConsensusNodeInfo();
                            await Settings.InitiateShutdownUpdate();
                            
                            Environment.SetEnvironmentVariable("RBX-Restart", "1", EnvironmentVariableTarget.User);
                            var exeLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                            var path = exeLocation + @"/ReserveBlockCore.dll";
                            ProcessStartInfo info = new ProcessStartInfo("dotnet");
                            info.Arguments = path;
                            Process.Start(info);

                            Environment.Exit(0);
                        }
                    }
                    finally
                    {
                        AdjAutoRestartLock.Release();
                    }

                    await delay;
                }
            }
        }
    }
}
