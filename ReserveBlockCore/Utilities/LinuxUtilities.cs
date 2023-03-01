using ReserveBlockCore.Commands;
using ReserveBlockCore.Models;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class LinuxUtilities
    {
        static SemaphoreSlim AdjAutoRestartLock = new SemaphoreSlim(1, 1);
        
        public static async Task ClientRestart()
        {
            try
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

                var path = exeLocation + @$"{Path.DirectorySeparatorChar}ReserveBlockCore.dll";
                var command = $"clear; echo 'Hello, World!'; sleep 2; echo 'Goodbye, World!'; screen -S mainnet -p 0 -X stuff \"dotnet {path}^M\"";

                var escapedArgs = command.Replace("\"", "\\\"");

                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exeLocation),
                    Arguments = $"-c \"{escapedArgs}\"",
                };
                //info.Arguments = path;
                Process.Start(info);

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
            }
        }
        
        public static async Task TestMethod()
        {
            try
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

                var path = exeLocation + @$"{Path.DirectorySeparatorChar}ReserveBlockCore.dll";
                var command = $"clear; echo 'Hello, World!'; sleep 2; echo 'Goodbye, World!'; screen -S mainnet -p 0 -X stuff \"dotnet {path}^M\"";

                var escapedArgs = command.Replace("\"", "\\\"");

                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exeLocation),
                    Arguments = $"-c \"{escapedArgs}\"",
                };
                //info.Arguments = path;
                Process.Start(info);

                Environment.Exit(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
            }
            
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
                        if (timeDiff > 88) 
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

                            var path = exeLocation + @$"{Path.DirectorySeparatorChar}ReserveBlockCore.dll";
                            var command = $"clear; echo 'Hello, World!'; sleep 2; echo 'Goodbye, World!'; screen -S mainnet -p 0 -X stuff \"dotnet {path}^M\"";

                            var escapedArgs = command.Replace("\"", "\\\"");

                            ProcessStartInfo info = new ProcessStartInfo
                            {
                                FileName = "/bin/bash",
                                UseShellExecute = false,
                                WorkingDirectory = Path.GetDirectoryName(exeLocation),
                                Arguments = $"-c \"{escapedArgs}\"",
                            };
                            //info.Arguments = path;
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
