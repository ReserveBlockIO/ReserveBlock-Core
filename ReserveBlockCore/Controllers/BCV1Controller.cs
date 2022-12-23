using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System.Diagnostics;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("bcapi/[controller]")]
    [Route("bcapi/[controller]/{somePassword?}")]
    [ApiController]
    public class BCV1Controller : ControllerBase
    {
        /// <summary>
        /// Creates a beacon on the local host and stores for later use to relay assets
        /// </summary>
        /// <param name="name"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        [HttpGet("CreateBeacon/{name}/{port}")]
        public async Task<string> CreateBeacon(string name, int port = 0)
        {
            var output = "";

            var ip = P2PClient.MostLikelyIP();

            if(ip == "NA")
            {
                return "Could not get external IP. Please ensure you are connected to peers and that you are not blocking ports.";
            }

            var bUID = Guid.NewGuid().ToString().Substring(0, 12).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();
            
            Beacons beacon = new Beacons
            {
                IPAddress = ip,
                Name = name,
                Port = port != 0 ? port : Globals.Port + 20000,
                BeaconUID = bUID,
                SelfBeacon = true,
                SelfBeaconActive = true,
            };

            var result = Beacons.SaveBeacon(beacon);

            if (!result)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Failed to add beacon." });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = "Beacon has been added." });
            }

            return output;
        }

        /// <summary>
        /// Gets the beacons local to this wallet.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBeacons")]
        public async Task<string> GetBeacons()
        {
            var output = "[]";

            var beacons = Beacons.GetBeacons();

            if (beacons != null)
            {
                var beaconList = beacons.FindAll().ToList();
                if (beaconList.Count > 0)
                    output = JsonConvert.SerializeObject(beaconList);
            }
            return output;
        }

        /// <summary>
        /// Adds a beacon on the local host and stores for later use
        /// </summary>
        /// <param name="name"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        /// <returns></returns>
        [HttpGet("AddBeacon/{name}/{port}/{**ip}")]
        public async Task<string> AddBeacon(string name,  string ip, int port = 0)
        {
            var output = "";

            var bUID = Guid.NewGuid().ToString().Substring(0, 12).Replace("-", "") + ":" + TimeUtil.GetTime().ToString();

            Beacons beacon = new Beacons {
                IPAddress = ip,
                Name = name,
                Port = port != 0 ? port : Globals.Port + 20000,
                BeaconUID = bUID,
                SelfBeacon = false,
                SelfBeaconActive = false
            };

            var result = Beacons.SaveBeacon(beacon);

            if (!result)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Failed to add beacon." });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = "Beacon has been added." });
            }
                
            return output;
        }

        /// <summary>
        /// Deletes a beacon on the local host
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("DeleteBeacon/{id}")]
        public async Task<string> DeleteBeacon(int id)
        {
            var output = "[]";

            var beacons = Beacons.GetBeacons();

            if (beacons != null)
            {
                var beaconExist = beacons.Query().Where(x => x.Id == id).FirstOrDefault();
                if(beaconExist != null)
                {
                    var result = Beacons.DeleteBeacon(beaconExist);
                    if(result)
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = "Beacon has been deleted." });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Failed to delete beacon." });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Beacon does not exist." });
                }
                
            }
            return output;
        }

        /// <summary>
        /// Decodes a beacon locator and sends the data
        /// </summary>
        /// <param name="locator"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets the local beacon information
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Changes your beacon state from active/inactive states.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SetBeaconState")]
        public async Task<string> SetBeaconState()
        {
            var output = "";

            var result = Beacons.SetBeaconActiveState();

            if(result == null)
            {
                output = JsonConvert.SerializeObject(new { Result = "Failed", Message = "Error turning beacon on/off" });
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Result = "Success", Message = result.Value });
            }

            return output;
        }

        /// <summary>
        /// Gets the assets based on the smart contract UID and the signature required.
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="locators"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        [HttpGet("GetBeaconAssets/{scUID}/{locators}/{**signature}/")]
        public async Task<string> GetBeaconAssets(string scUID, string locators, string signature)
        {
            //signature message = scUID
            string output = "";
            var result = await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, signature, "NA");
            return output;
        }

        /// <summary>
        /// Gets the assets queue this current beacon is responsible for
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAssetQueue")]
        public async Task<string> GetAssetQueue()
        {
            //signature message = scUID
            string output = "None";

            var aqDB = AssetQueue.GetAssetQueue();
            if(aqDB != null)
            {
                var aqList = aqDB.FindAll().ToList();
                if(aqList.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(aqList);
                }
            }

            return output;
        }

        /// <summary>
        /// Sets the asset queue to complete
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAssetQuestComplete")]
        public async Task<string> GetAssetQuestComplete()
        {
            //signature message = scUID
            string output = "Done";

            var aqDB = AssetQueue.GetAssetQueue();
            if (aqDB != null)
            {
                var aqList = aqDB.FindAll().ToList();
                if (aqList.Count() > 0)
                {
                    foreach(var item in aqList)
                    {
                        item.IsComplete = true;
                        aqDB.UpdateSafe(item);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Returns all current beacon request
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetBeaconRequest")]
        public async Task<string> GetBeaconRequest()
        {
            //signature message = scUID
            string output = "None";

            var beaconData = BeaconData.GetBeaconData();
            if (beaconData != null)
            {
                output = JsonConvert.SerializeObject(beaconData);
            }

            return output;
        }

        /// <summary>
        /// Deletes a specific beacon request with a provided scUID = id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("GetDeleteBeaconRequest/{id}")]
        public async Task<string> GetDeleteBeaconRequest(int id)
        {
            //signature message = scUID
            string output = "None";

            var result = await BeaconData.DeleteBeaconData(id);

            output = result.ToString();

            return output;
        }

        /// <summary>
        /// Deletes all beacon request tied to the current local beacon
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDeleteBeaconRequestAll")]
        public async Task<string> GetDeleteBeaconRequestAll()
        {
            //signature message = scUID
            string output = "None";

            var result = await BeaconData.DeleteAllBeaconData();

            output = result.ToString();

            return output;
        }
    }
}
