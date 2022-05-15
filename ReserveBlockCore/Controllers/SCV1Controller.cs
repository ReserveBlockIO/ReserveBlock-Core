using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Controllers
{
    [Route("scapi/[controller]")]
    [ApiController]
    public class SCV1Controller : ControllerBase
    {
        // GET: api/<V1>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "Smart", "Contracts", "API" };
        }

        // GET api/<V1>/getgenesisblock
        [HttpGet("{id}")]
        public string Get(string id)
        {
            //use Id to get specific commands
            var output = "Command not recognized."; // this will only display if command not recognized.
            var command = id.ToLower();
            switch (command)
            {
                //This is initial example. Returns Genesis block in JSON format.
                case "getSCData":
                    //Do something later
                    break;
            }

            return output;
        }

        [HttpPost("SCPassTest")]
        public object SCPassTest([FromBody] object jsonData)
        {
            var output = jsonData;

            return output;
        }

        [HttpPost("SCPassDesTest")]
        public string SCPassDesTest([FromBody] object jsonData)
        {
            var output = jsonData.ToString();
            try
            {
                var scMain = JsonConvert.DeserializeObject<SmartContractMain>(jsonData.ToString());

                var json = JsonConvert.SerializeObject(scMain);

                output = json;
            }
            catch (Exception ex)
            {
                output = $"Error - {ex.Message}. Please Try Again.";
            }

            return output;
        }

        [HttpGet("GetAllSmartContracts")]
        public async Task<string> GetAllSmartContracts()
        {
            var output = "";

            var scs = SmartContractMain.SmartContractData.GetSCs().FindAll().ToList();

            if (scs.Count() > 0)
            {
                var json = JsonConvert.SerializeObject(scs);
                output = json;
            }
            else
            {
                output = "null";
            }

            return output;
        }

        [HttpGet("GetMintedSmartContracts")]
        public async Task<string> GetMintedSmartContracts()
        {
            var output = "";

            var scs = SmartContractMain.SmartContractData.GetSCs().Find(x => x.IsMinter == true).ToList();

            if (scs.Count() > 0)
            {
                var json = JsonConvert.SerializeObject(scs);
                output = json;
            }
            else
            {
                output = "null";
            }

            return output;
        }

        [HttpGet("GetSingleSmartContract/{id}")]
        public async Task<string> GetSingleSmartContract(string id)
        {
            var output = "";
            try
            {
                Guid scUID = Guid.Parse(id);

                var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                var result = await SmartContractReaderService.ReadSmartContract(sc);

                var scMain = result.Item2;
                var scCode = result.Item1;

                var scInfo = new[]
                {
                new { SmartContract = scMain, SmartContractCode = scCode}
            };

                if (sc != null)
                {
                    var json = JsonConvert.SerializeObject(scInfo);
                    output = json;
                }
                else
                {
                    output = "null";
                }
            }
            catch(Exception ex)
            {
                output = ex.Message;
            }
            
            return output;
        }

        [HttpPost("CreateSmartContract")]
        public async Task<string> CreateSmartContract([FromBody] object jsonData)
        {
            var output = "";

            try
            {
                SmartContractReturnData scReturnData = new SmartContractReturnData();
                var scMain = JsonConvert.DeserializeObject<SmartContractMain>(jsonData.ToString());
                try
                {
                    var result = await SmartContractWriterService.WriteSmartContract(scMain);
                    scReturnData.Success = true;
                    scReturnData.SmartContractCode = result.Item1;
                    scReturnData.SmartContractMain = result.Item2;
                    SmartContractMain.SmartContractData.SaveSmartContract(result.Item2, result.Item1);

                    var nTx = new Transaction
                    {
                        Timestamp = TimeUtil.GetTime(),
                        FromAddress = scReturnData.SmartContractMain.Address,
                        ToAddress = scReturnData.SmartContractMain.Address,
                        Amount = 0.0M,
                        Fee = 0,
                        Nonce = AccountStateTrei.GetNextNonce(scMain.Address),
                        TransactionType = TransactionType.NFT,
                        Data = scReturnData.SmartContractCode
                    };

                    //Calculate fee for tx.
                    nTx.Fee = FeeCalcService.CalculateTXFee(nTx);

                    nTx.Build();

                    var scInfo = new[]
                    {
                    new {Success = true, SmartContract = result.Item2, SmartContractCode = result.Item1, Transaction = nTx}
                    };
                    var json = JsonConvert.SerializeObject(scInfo, Formatting.Indented);
                    output = json;
                }
                catch (Exception ex)
                {
                    scReturnData.Success = false;
                    scReturnData.SmartContractCode = "Failure";
                    scReturnData.SmartContractMain = scMain;

                    var scInfo = new[]
                    {
                    new {Success = false, SmartContract = scReturnData.SmartContractCode, SmartContractCode = scReturnData.SmartContractMain}
                    };
                    var json = JsonConvert.SerializeObject(scInfo, Formatting.Indented);
                    output = json;
                }

            }
            catch (Exception ex)
            {
                output = $"Error - {ex.Message}. Please Try Again...";
            }


            return output;
        }

        [HttpGet("PublishSmartContract/{id}")]
        public async Task<string> PublishSmartContract(string id)
        {
            var output = "";

            return output;
        }

        [HttpGet("ChangeNFTPublicState/{id}")]
        public async Task<string> ChangeNFTPublicState(string id)
        {
            var output = "";

            Guid scUID = Guid.Parse(id);
            //Get SmartContractMain.IsPublic and set to True.
            var scs = SmartContractMain.SmartContractData.GetSCs();
            var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
            sc.IsPublic ^= true;

            scs.Update(sc);

            return output;

        }
    }
}
