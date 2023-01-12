using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("api/[controller]")]
    [Route("api/[controller]/{somePassword?}")]
    [ApiController]
    public class ADJV1Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-ADJ", "API" };
        }

        /// <summary>
        /// Returns entire duplicates dictionary
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDups")]
        public async Task<string> GetDups()
        {
            string output = "";
            var dups = Globals.DuplicatesBroadcastedDict.Values.Select(x => new
            {
                x.IPAddress,
                x.Address,
                x.StopNotify,
                x.Reason,
                x.LastNotified,
                x.LastDetection,
                x.NotifyCount

            }).ToList();

            output = JsonConvert.SerializeObject(dups);

            return output;
        }


        /// <summary>
        /// Shows the consensus broadcast list of txs
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetConsensusBroadcastTx")]
        public async Task<string> GetConsensusBroadcastTx()
        {
            var output = "";

            var txlist = Globals.ConsensusBroadcastedTrxDict.Values.ToList();

            if (txlist.Count > 0)
            {
                output = JsonConvert.SerializeObject(txlist);
            }

            return output;
        }

        /// <summary>
        /// Shows the fortis pool work broadcast list of txs
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetFortisBroadcastTx")]
        public async Task<string> GetFortisBroadcastTx()
        {
            var output = "";

            var txlist = Globals.BroadcastedTrxDict.Values.ToList();

            if (txlist.Count > 0)
            {
                output = JsonConvert.SerializeObject(txlist);
            }

            return output;
        }


        /// <summary>
        /// Returns entire fortis pool (Masternode List)
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMasternodes")]
        public async Task<string> GetMasternodes()
        {
            string output = "";
            var validators = Globals.FortisPool.Values.Select(x => new
            {
                x.Context.ConnectionId,
                x.ConnectDate,
                x.LastAnswerSendDate,
                x.IpAddress,
                x.Address,
                x.UniqueName,
                x.WalletVersion
            }).ToList();

            output = JsonConvert.SerializeObject(validators);

            return output;
        }

        /// <summary>
        /// Returns master node list that is sent
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMasternodesSent")]
        public async Task<string> GetMasternodesSent()
        {
            string output = "";
            var currentTime = DateTime.Now.AddMinutes(-15);
            var fortisPool = Globals.FortisPool.Values.Where(x => x.LastAnswerSendDate != null).Select(x => new
            {
                x.Context.ConnectionId,
                x.ConnectDate,
                x.LastAnswerSendDate,
                x.IpAddress,
                x.Address,
                x.UniqueName,
                x.WalletVersion
            }).ToList(); 

            output = JsonConvert.SerializeObject(fortisPool);

            return output;
        }

        /// <summary>
        /// Returns a task answer list
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetTaskAnswersList")]
        public async Task<string> GetTaskAnswersList()
        {
            string output = "";

            if (Globals.LastBlock.Height < Globals.BlockLock)
            {
                var taskAnswerList = Globals.TaskAnswerDict_New.Values.Select(x => new {
                    x.Address,
                    x.Answer,
                    x.NextBlockHeight,
                    x.SubmitTime

                });
                output = JsonConvert.SerializeObject(taskAnswerList);
            }
            else
            {
                var taskAnswerList = Globals.TaskAnswerDictV3.Values.Select(x => new {
                    Address = x.RBXAddress,
                    Answer = x.Answer,
                    IP = x.IPAddress,
                    Signature = x.Signature.Substring(0, 12)

                });
                output = JsonConvert.SerializeObject(taskAnswerList);
            }

            return output;
        }

        /// <summary>
        /// Returns ADJ info
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAdjInfo")]
        public async Task<string> GetAdjInfo()
        {
            var output = "";
            StringBuilder outputBuilder = new StringBuilder();

            var taskSelectedNumbersV3 = Globals.TaskSelectedNumbersV3.Values.ToList();

            var adjConsensusNodes = Globals.Nodes.Values.ToList();
            var Now = TimeUtil.GetMillisecondTime();
            if (adjConsensusNodes.Count() > 0)
            {
                outputBuilder.AppendLine("*******************************Consensus Nodes*******************************");
                foreach (var cNode in adjConsensusNodes)
                {
                    var line = $"IP: {cNode.NodeIP} | Address: {cNode.Address} | IsConnected? {cNode.IsConnected} ({Now - cNode.LastMethodCodeTime < 3000})";
                    outputBuilder.AppendLine(line);
                }
                outputBuilder.AppendLine("******************************************************************************");
            }

            if (taskSelectedNumbersV3.Count() > 0)
            {
                outputBuilder.AppendLine("*******************************Task Answers V3********************************");
                foreach (var taskNum in taskSelectedNumbersV3)
                {
                    var taskLine = $"Address: {taskNum.RBXAddress} |  IP Address: {taskNum.IPAddress} | Answer: {taskNum.Answer}";
                    outputBuilder.AppendLine(taskLine);
                }
                outputBuilder.AppendLine("******************************************************************************");
            }

            output = outputBuilder.ToString();  
            return output;
        }

        /// <summary>
        /// Returns Consensus Info
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetConsensusInfo")]
        public async Task<string> GetConsensusInfo()
        {
            var output = "";
            StringBuilder outputBuilder = new StringBuilder();

            var conState = ConsensusServer.GetState();
            outputBuilder.AppendLine("*******************************Consensus State********************************");

            var conStateLine = $"Next Height: {Globals.LastBlock.Height + 1} | Status: {conState.Status} | Answer: {conState.Answer} | Method Code: {conState.MethodCode}";
            outputBuilder.AppendLine(conStateLine);
            LogUtility.LogQueue(conStateLine, "", "cinfo.txt", true);

            outputBuilder.AppendLine("******************************************************************************");

            var conMessage = string.Join("\r\n", ConsensusServer.Messages.Select(x => x.Value.Select(y => x.Key.Height + " " + x.Key.MethodCode + " " + y.Key + " " + y.Value.Message + " " + y.Value.Signature))
                .SelectMany(x => x));
            LogUtility.LogQueue(conMessage, "", "cinfo.txt", true);

            outputBuilder.AppendLine("*****************************Consensus Messages*******************************");

            outputBuilder.AppendLine(conMessage);

            outputBuilder.AppendLine("******************************************************************************");

            var hashMessage = string.Join("\r\n", ConsensusServer.Hashes.Select(x => x.Value.Select(y => x.Key.Height + " " + x.Key.MethodCode + " " + y.Key + " " + y.Value.Hash + " " + y.Value.Signature))
                            .SelectMany(x => x));
            LogUtility.LogQueue(hashMessage, "", "cinfo.txt", true);

            outputBuilder.AppendLine("*****************************Consensus Hashes*******************************");

            outputBuilder.AppendLine(hashMessage);

            outputBuilder.AppendLine("******************************************************************************");

            var addressesToWaitFor = ConsensusClient.AddressesToWaitFor(Globals.LastBlock.Height + 1, conState.MethodCode, 3000).ToArray();

            LogUtility.LogQueue(JsonConvert.SerializeObject(addressesToWaitFor), "", "cinfo.txt", true);
            outputBuilder.AppendLine("*****************************Addresses To Wait For*******************************");

            outputBuilder.AppendLine(JsonConvert.SerializeObject(addressesToWaitFor));

            outputBuilder.AppendLine("******************************************************************************");

            outputBuilder.AppendLine("*****************************Consensus Dump*******************************");

            outputBuilder.AppendLine(JsonConvert.SerializeObject(JsonConvert.SerializeObject(Globals.ConsensusDump)));

            LogUtility.LogQueue(JsonConvert.SerializeObject(Globals.ConsensusDump), "", "cinfo.txt", true);
            outputBuilder.AppendLine("******************************************************************************");

            outputBuilder.AppendLine("*****************************Node Dump*******************************");

            outputBuilder.AppendLine("Now: " + TimeUtil.GetMillisecondTime() + "\r\n");

            LogUtility.LogQueue(JsonConvert.SerializeObject(JsonConvert.SerializeObject(Globals.Nodes.Values)), "", "cinfo.txt", true);
            outputBuilder.AppendLine(JsonConvert.SerializeObject(JsonConvert.SerializeObject(Globals.Nodes.Values)));

            outputBuilder.AppendLine("******************************************************************************");

            output = outputBuilder.ToString();

            return output;
        }

    }
}
