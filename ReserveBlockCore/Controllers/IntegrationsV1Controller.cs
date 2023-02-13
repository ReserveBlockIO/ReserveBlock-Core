using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("iapi/[controller]")]
    [ApiController]
    public class IntegrationsV1Controller : ControllerBase
    {
        [HttpGet("Network")]
        public async Task<string> Network()
        {
            string output = "";

            var currentBlockHeight = Globals.LastBlock.Height;
            var hash = Globals.LastBlock.Hash;
            var lastTimeUTC = Globals.LastBlockAddedTimestamp.ToUTCDateTimeFromUnix();
            var cliVersion = Globals.CLIVersion;
            var gitVersion = Globals.GitHubLatestReleaseVersion;
            var blockVersion = Globals.LastBlock.Version;
            DateTime originDate = new DateTime(2022, 1, 1);
            DateTime currentDate = DateTime.Now;
            var dateDiff = (int)Math.Round((currentDate - originDate).TotalDays);
            var totalSupply = 372000000;

            Integrations.Network network = new Integrations.Network { 
                BlockVersion= blockVersion,
                Hash= hash,
                CLIVersion= cliVersion,
                GitHubVersion= gitVersion,
                Height= currentBlockHeight,
                LastBlockAddedTimeUTC = lastTimeUTC,
                NetworkAgeInDays = dateDiff,
                TotalSupply = totalSupply,
            };

            output = JsonConvert.SerializeObject(network, Formatting.Indented);

            return output;
        }

        [HttpGet("Height")]
        public async Task<string> Height()
        {
            string output = "";

            var currentBlockHeight = Globals.LastBlock.Height;

            output = currentBlockHeight.ToString();

            return output;
        }

        [HttpGet("LastBlock")]
        public async Task<string> LastBlock()
        {
            string output = "";

            var lastBlock = Globals.LastBlock;

            output = JsonConvert.SerializeObject(lastBlock);

            return output;
        }
    }
}
