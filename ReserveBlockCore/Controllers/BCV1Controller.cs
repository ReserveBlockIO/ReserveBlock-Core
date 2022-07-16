using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("bcapi/[controller]")]
    [Route("bcapi/[controller]/{somePassword?}")]
    [ApiController]
    public class BCV1Controller : ControllerBase
    {
        /// <summary>
        /// Creates a beacon on the local host
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet("CreateBeacon/{name}")]
        public async Task<string> CreateBeacon(string name)
        {
            var output = "";

            var ip = P2PClient.ReportedIPs.Count() != 0 ? 
                P2PClient.ReportedIPs.GroupBy(x => x).OrderByDescending(y => y.Count()).Select(y => y.Key).First().ToString() : 
                "NA";

            if(ip == "NA")
            {
                return "Could not get external IP. Please ensure you are connected to peers and that you are not blocking ports.";
            }

            var bUID = Guid.NewGuid().ToString().Substring(0, 12).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

            BeaconInfo bInfo = new BeaconInfo();
            bInfo.Name = name;
            bInfo.IsBeaconActive = true;
            bInfo.BeaconUID = bUID;

            BeaconInfo.BeaconInfoJson beaconLoc = new BeaconInfo.BeaconInfoJson
            {
                IPAddress = ip,
                Port = Program.IsTestNet != true ? Program.Port + 10000 : Program.Port + 20000,
                Name = name,
                BeaconUID = bUID
                
            };

            var beaconLocJson = JsonConvert.SerializeObject(beaconLoc);
            bInfo.BeaconLocator = beaconLocJson.ToBase64();

            output = BeaconInfo.SaveBeaconInfo(bInfo);

            return output;
        }
        [HttpGet("DecodeBeaconLocator/{locator}")]
        public async Task<string> DecodeBeaconLocator(string locator)
        {
            var output = "";
            try
            {
                var beaconString = locator.ToStringFromBase64();
                var beaconDataJsonDes = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                output = JsonConvert.SerializeObject(new { Result = "Success", BeaconInfo = beaconDataJsonDes });
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Failed", ResultMessage = "Failed to retrieve beacon info from DB. Possible Corruption."});
            }

            return output;
        }

        [HttpGet("GetBeaconInfo")]
        public async Task<string> GetBeaconInfo()
        {
            var output = "";

            var beaconInfo = BeaconInfo.GetBeaconInfo();
            if(beaconInfo != null)
            {
                BeaconInfo.BeaconInfoJson beaconInfoJsonDes = new BeaconInfo.BeaconInfoJson();
                try
                {
                    var beaconString = beaconInfo.BeaconLocator.ToStringFromBase64();
                    beaconInfoJsonDes = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                    output = JsonConvert.SerializeObject(new { Result = "Success", BeaconInfo = beaconInfo, BeaconLocatorData = beaconInfoJsonDes });
                }
                catch(Exception ex)
                {
                    beaconInfoJsonDes = null;
                    output = JsonConvert.SerializeObject(new { Result = "Failed", ResultMessage = "Failed to retrieve beacon info from DB. Possible Corruption." });
                }
                
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Failed", ResultMessage = "No Beacon info found." });
            }

            return output;
        }

        [HttpGet("SetBeaconState")]
        public async Task<string> SetBeaconState()
        {
            var output = "";

            var result = BeaconInfo.SetBeaconActiveState();

            if(result == null)
            {
                output = JsonConvert.SerializeObject(new { Result = "Failed", ResultMessage = "Error turning beacon on/off" });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", ResultMessage = result.Value });
            }

            return output;
        }
    }
}
