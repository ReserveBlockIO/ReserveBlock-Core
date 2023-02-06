using System.Diagnostics;
using System.Net.Sockets;
using System.Net;

namespace ReserveBlockCore.Services
{
    public class NetworkTimeService
    {
        static SemaphoreSlim NetworkTimeServiceLock = new SemaphoreSlim(1, 1);
        private static List<string> NtpServerList = new List<string> { "pool.ntp.org", "time.windows.com", "time.google.com", "time.apple.com" };

        public static async Task Run()
        {
            while (true)
            {
                //run once every 4 hours to check time
                var delay = Task.Delay(new TimeSpan(4,0,0));

                await NetworkTimeServiceLock.WaitAsync();
                try
                {
                    bool timeSynced = false;
                    foreach(var server in NtpServerList)
                    {
                        var diff = await CheckNetworkTime(server);
                        if(diff.HasValue)
                        {
                            timeSynced = true;
                            if (diff.Value > 1100)
                            {
                                Globals.TimeInSync = false;
                                Globals.TimeSyncError = false;
                                Globals.TimeSyncDiff = diff.Value;
                            }
                            else
                            {
                                Globals.TimeInSync = true;
                                Globals.TimeSyncError = false;
                                Globals.TimeSyncDiff = diff.Value;
                            }

                            break;
                        }
                    }

                    if (!timeSynced)
                        Globals.TimeSyncError = true;
                }
                finally
                {
                    NetworkTimeServiceLock.Release();
                }

                await delay;
            }
        }

        private static async Task<int?> CheckNetworkTime(string ntpServer)
        {
            int? timeDiff = null;

            var networkTime = await GetNetworkTime(ntpServer);
            var localNow = DateTime.Now;
            if (networkTime != null)
            {                    
                timeDiff = (networkTime.Value - localNow).Milliseconds > 0 ? (networkTime.Value - localNow).Milliseconds : 
                    ((networkTime.Value - localNow).Milliseconds * -1);

                Globals.TimeSyncLastDate = localNow;
            }

            return timeDiff;
        }

        private static async Task<DateTime?> GetNetworkTime(string ntpServer = "pool.ntp.org")
        {
            try
            {
                const int daysTo1900 = 1900 * 365 + 95; // 95 = offset for leap-years etc.
                const long ticksPerSecond = 10000000L;
                const long ticksPerDay = 24 * 60 * 60 * ticksPerSecond;
                const long ticksTo1900 = daysTo1900 * ticksPerDay;

                // NTP message size - 16 bytes of the digest (RFC 2030)
                var ntpData = new byte[48];
                ntpData[0] = 0x1B; // LeapIndicator = 0 (no warning), VersionNum = 3 (IPv4 only), Mode = 3 (Client Mode)

                var addresses = Dns.GetHostEntry(ntpServer).AddressList;
                var ipEndPoint = new IPEndPoint(addresses[0], 123);

                var pingDuration = Stopwatch.GetTimestamp(); 

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    await socket.ConnectAsync(ipEndPoint);
                    socket.ReceiveTimeout = 5000;
                    socket.Send(ntpData);
                    pingDuration = Stopwatch.GetTimestamp(); // after Send-Method to reduce WinSocket API-Call time

                    socket.Receive(ntpData);
                    pingDuration = Stopwatch.GetTimestamp() - pingDuration;
                }

                var pingTicks = pingDuration * ticksPerSecond / Stopwatch.Frequency;

                var intPart = (long)ntpData[40] << 24 | (long)ntpData[41] << 16 | (long)ntpData[42] << 8 | ntpData[43];
                var fractPart = (long)ntpData[44] << 24 | (long)ntpData[45] << 16 | (long)ntpData[46] << 8 | ntpData[47];
                var netTicks = intPart * ticksPerSecond + (fractPart * ticksPerSecond >> 32);

                var networkDateTime = new DateTime(ticksTo1900 + netTicks + pingTicks / 2);

                return networkDateTime.ToLocalTime(); // without ToLocalTime() = faster
            }
            catch
            {
                // fail
                return null;
            }
        }
    }
}
