using ReserveBlockCore.Commands;
using ReserveBlockCore.Models;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class WindowsUtilities
    {
        static SemaphoreSlim AdjAutoRestartLock = new SemaphoreSlim(1, 1);
        public static class DisableConsoleQuickEdit
        {
            const uint ENABLE_QUICK_EDIT = 0x0040;

            // STD_INPUT_HANDLE (DWORD): -10 is the standard input device.
            const int STD_INPUT_HANDLE = -10;

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern IntPtr GetStdHandle(int nStdHandle);

            [DllImport("kernel32.dll")]
            static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

            [DllImport("kernel32.dll")]
            static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

            internal static bool Go()
            {
                try
                {
                    IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);

                    // get current console mode
                    uint consoleMode;
                    if (!GetConsoleMode(consoleHandle, out consoleMode))
                    {
                        // ERROR: Unable to get console mode.
                        return false;
                    }

                    // Clear the quick edit bit in the mode flags
                    consoleMode &= ~ENABLE_QUICK_EDIT;

                    // set the new mode
                    if (!SetConsoleMode(consoleHandle, consoleMode))
                    {
                        // ERROR: Unable to set console mode
                        return false;
                    }
                }
                catch { }
                
                return true;
            }
        }

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

            var path = exeLocation + Path.DirectorySeparatorChar + "ReserveBlockCore.exe";
            Process.Start(path);

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
                        var timeDiff = Globals.BlockTimeDiff;
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

                            var path = exeLocation + Path.DirectorySeparatorChar + "ReserveBlockCore.exe";
                            Process.Start(path);

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
