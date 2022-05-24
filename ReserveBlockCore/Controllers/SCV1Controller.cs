using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Text;

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
                var sc = SmartContractMain.SmartContractData.GetSmartContract(id);

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
                        TransactionType = TransactionType.NFT_MINT,
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

        [HttpGet("MintSmartContract/{id}")]
        public async Task<string> MintSmartContract(string id)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);

            if(sc.IsPublished == true)
            {
                output = "This NFT has already been published";
            }
            else
            {
                var scTx = await SmartContractService.MintSmartContractTx(sc);
                if(scTx == null)
                {
                    output = "Failed to publish smart contract: " + sc.Name + ". Id: " + id;
                }
                else
                {
                    output = "Smart contract has been published to mempool";
                }
            }
            

            return output;
        }

        [HttpGet("ChangeNFTPublicState/{id}")]
        public async Task<string> ChangeNFTPublicState(string id)
        {
            var output = "";

            //Get SmartContractMain.IsPublic and set to True.
            var scs = SmartContractMain.SmartContractData.GetSCs();
            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            sc.IsPublic ^= true;

            scs.Update(sc);

            return output;

        }

        [HttpGet("GetSmartContractData/{id}")]
        public async Task<string> GetSmartContractData(string id)
        {
            var output = "";

            //Get SmartContractMain.IsPublic and set to True.
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(id);
            if(scStateTrei != null)
            {
                var scMain = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                output = JsonConvert.SerializeObject(scMain);
            }

            return output;
        }

        [HttpGet("TestRemove/{id}/{toAddress}")]
        public async Task<string> TestRemove(string id, string toAddress)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            if (sc != null)
            {
                if (sc.IsPublished == true)
                {
                    var result = await SmartContractReaderService.ReadSmartContract(sc);

                    var scText = result.Item1;
                    var bytes = Encoding.Unicode.GetBytes(scText);
                    var compressBase64 = SmartContractUtility.Compress(bytes).ToBase64();

                    SmartContractMain.SmartContractData.CreateSmartContract(compressBase64);
                    
                }
                else
                {
                    output = "Smart Contract Found, but has not been minted.";
                }
            }
            else
            {
                output = "No Smart Contract Found Locally.";
            }

            return output;
        }

        [HttpGet("TransferNFT/{id}/{toAddress}")]
        public async Task<string> TransferNFT(string id, string toAddress)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            if(sc != null)
            {
                if (sc.IsPublished == true)
                {
                    var tx = await SmartContractService.TransferSmartContract(sc, toAddress);

                    var txJson = JsonConvert.SerializeObject(tx);
                    output = txJson;
                }
                else
                {
                    output = "Smart Contract Found, but has not been minted.";
                }
            }
            else
            {
                output = "No Smart Contract Found Locally.";
            }
            
            
            return output;
        }

        [HttpGet("Burn/{id}")]
        public async Task<string> Burn(string id)
        {
            var output = "";

            var sc = SmartContractMain.SmartContractData.GetSmartContract(id);
            if (sc != null)
            {
                if (sc.IsPublished == true)
                {
                    var tx = await SmartContractService.BurnSmartContract(sc);

                    var txJson = JsonConvert.SerializeObject(tx);
                    output = txJson;
                }
            }

            return output;
        }

        [HttpGet("Evolve/{id}/{toAddress}")]
        public async Task<string> Evolve(string id, string toAddress)
        {
            var output = "";

            var tx = await SmartContractService.EvolveSmartContract(id, toAddress);

            if (tx == null)
            {
                output = "Failed to Evolve - TX";
            }
            else
            {
                var txJson = JsonConvert.SerializeObject(tx);
                output = txJson;
            }

            return output;
        }

        [HttpGet("Devolve/{id}/{toAddress}")]
        public async Task<string> Devolve(string id, string toAddress)
        {
            string output;

            var tx = await SmartContractService.DevolveSmartContract(id, toAddress);

            if (tx == null)
            {
                output = "Failed to Devolve - TX";
            }
            else
            {
                var txJson = JsonConvert.SerializeObject(tx);
                output = txJson;
            }

            return output;
        }

        [HttpGet("EvolveSpecific/{id}/{toAddress}/{evolveState}")]
        public async Task<string> EvolveSpecific(string id, string toAddress, int evolveState)
        {
            string output;

            var tx = await SmartContractService.ChangeEvolveStateSpecific(id, toAddress, evolveState);

            if (tx == null)
            {
                output = "Failed to Change State - TX";
            }
            else
            {
                var txJson = JsonConvert.SerializeObject(tx);
                output = txJson;
            }

            return output;
        }
    }
}
