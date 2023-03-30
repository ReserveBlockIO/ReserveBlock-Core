using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Utilities
{
    internal class TimeUtil
    {
        public static long GetTime(int addSeconds = 0)
        {
            return DateTimeOffset.UtcNow.AddSeconds(addSeconds).ToUnixTimeSeconds();
        }

        public static long GetMillisecondTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();            
        }

        public static long GetTimeFromDateTime(DateTime date)
        {
            return ((DateTimeOffset)date).ToUnixTimeSeconds();
        }

        public static long GetTimeForBeaconRelease()
        {
            var beacons = Beacons.GetBeacons();
            if(beacons != null)
            {
                var selfBeacon = Globals.SelfBeacon;
                if(selfBeacon != null)
                {
                    if(selfBeacon.FileCachePeriodDays > 0)
                    {
                        return DateTimeOffset.UtcNow.AddDays(selfBeacon.FileCachePeriodDays).ToUnixTimeSeconds();
                    }
                    else
                    {
                        return DateTimeOffset.UtcNow.AddYears(7777).ToUnixTimeSeconds();
                    }
                }
            }

            //default to 5 days
            return DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds();
        }
        public static long GetReserveTime(int addHours = 0)
        {
            return DateTimeOffset.UtcNow.AddHours(24 + addHours).ToUnixTimeSeconds();
        }
        public static DateTime ToDateTime(long unixTime)
        {
            DateTime frDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            frDateTime = frDateTime.AddSeconds(unixTime).ToLocalTime();
            return frDateTime;
        }
    }
}
