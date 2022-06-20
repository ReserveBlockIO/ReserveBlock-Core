using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Controllers
{
    [Route("bcapi/[controller]")]
    [ApiController]
    public class BCV1Controller : ControllerBase
    {
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
                Port = Program.Port,
                Name = name,
                BeaconUID = bUID
                
            };

            var beaconLocJson = JsonConvert.SerializeObject(beaconLoc);
            bInfo.BeaconLocator = beaconLocJson.ToBase64();

            output = BeaconInfo.SaveBeaconInfo(bInfo);

            return output;
        }

        [HttpGet("GetBeaconInfo")]
        public async Task<string> GetBeaconInfo(string name)
        {
            var output = "";


            var beaconInfo = BeaconInfo.GetBeaconInfo();
            if(beaconInfo != null)
            {
                BeaconInfo.BeaconInfoJson beaconDataJsonDes = new BeaconInfo.BeaconInfoJson();
                try
                {
                    beaconDataJsonDes = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconInfo.BeaconLocator.ToStringFromBase64());
                }
                catch(Exception ex)
                {
                    beaconDataJsonDes = null;
                }
                var successResult = new[]
                {
                    new { Result = "Success", BeaconInfo = beaconInfo, BeaconIPPort = beaconDataJsonDes}
                };

                output = JsonConvert.SerializeObject(successResult);
            }
            else
            {
                var failedResult = new[]
                {
                    new { Result = "Failed", ResultMessage = "No Beacon info found."}
                };

                output = JsonConvert.SerializeObject(failedResult);
            }

            return output;
        }

        [HttpGet("SetBeaconState")]
        public async Task<string> SetBeaconState(string name)
        {
            var output = "";

            var result = BeaconInfo.SetBeaconActiveState();

            if(result == null)
            {
                var failedResult = new[]
                {
                    new { Result = "Failed", ResultMessage = "Error turning beacon on/off"}
                };

                output = JsonConvert.SerializeObject(failedResult);
            }
            else
            {
                var successResult = new[]
                {
                    new { Result = "Success", ResultMessage = result.Value}
                };

                output = JsonConvert.SerializeObject(successResult);
            }

            return output;
        }
    }
}
