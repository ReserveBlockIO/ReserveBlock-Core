using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class TransactionValidatorService
    {
        public static async Task<bool> VerifyTX(Transaction txRequest, bool blockDownloads = false)
        {
            bool txResult = false;

            var accStTrei = StateData.GetAccountStateTrei();
            var from = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);

            //Balance Check
            if(from == null)
            {
                //They may also just need the block that contains this TX.
                //We might want to queue a block check and download.
                return txResult;
            }
            else
            {
                if(from.Balance < (txRequest.Amount + txRequest.Fee))
                {
                    return txResult;//balance was less than the amount they are trying to send.
                }
            }

            //Timestamp Check
            if(!blockDownloads)
            {
                var currentTime = TimeUtil.GetTime();
                var timeDiff = currentTime - txRequest.Timestamp;
                var minuteDiff = timeDiff / 60M;

                if (minuteDiff > 180.0M)
                {
                    return txResult;
                }
            }

            //Prev Tx in Block Check - this is to prevent someone sending a signed TX again
            var memBlocksTxs = Program.MemBlocks.SelectMany(x => x.Transactions).ToList();
            var txExist = memBlocksTxs.Exists(x => x.Hash == txRequest.Hash);
            if (txExist)
            {
                return txResult;
            }

            //REMOVE THIS ON NEW UPDATE
            
            //Hash Check
            var newTxn = new Transaction()
            {
                Timestamp = txRequest.Timestamp,
                FromAddress = txRequest.FromAddress,
                ToAddress = txRequest.ToAddress,
                Amount = txRequest.Amount,
                Fee = txRequest.Fee,
                Nonce = txRequest.Nonce,
                TransactionType = txRequest.TransactionType,
                Data = txRequest.Data,
            };

            newTxn.Build();

            if (!newTxn.Hash.Equals(txRequest.Hash))
            {
                var amountCheck = txRequest.Amount % 1 == 0;
                var amountFormat = 0M;
                if (amountCheck)
                {
                    var amountStr = txRequest.Amount.ToString("#");
                    amountFormat = decimal.Parse(amountStr);
                }

                var newTxnMod = new Transaction()
                {
                    Timestamp = txRequest.Timestamp,
                    FromAddress = txRequest.FromAddress,
                    ToAddress = txRequest.ToAddress,
                    Amount = amountFormat,
                    Fee = txRequest.Fee,
                    Nonce = txRequest.Nonce,
                    TransactionType = txRequest.TransactionType,
                    Data = txRequest.Data,
                };

                newTxnMod.Build();

                if (!newTxnMod.Hash.Equals(txRequest.Hash))
                {
                    return txResult;
                }
                
            }

            //Signature Check - Final Check to return true.
            var isTxValid = SignatureService.VerifySignature(txRequest.FromAddress, txRequest.Hash, txRequest.Signature);
            if (isTxValid)
            {
                txResult = true;
            }
            else
            {
                return txResult;
            }

            //Return verification result.
            return txResult;

        }

        public static async Task AddNewlyMintedContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scData = JsonConvert.DeserializeObject<JArray>(tx.Data);
            if(scData != null)
            {
                var function = (string?)scData["Function"];
                var data = (string?)scData["Data"];
                var scUID = (string?)scData["ContractUID"];

                scST.ContractData = data;
                scST.MinterAddress = tx.FromAddress;
                scST.OwnerAddress = tx.FromAddress;
                scST.SmartContractUID = scUID;
                scST.Nonce = 0;

                //Save to state trei
                SmartContractStateTrei.SaveSmartContract(scST);
                SmartContractMain.SmartContractData.SetSmartContractIsPublished(scUID);
            }
            
        }

        public static async Task TransferNFT(Transaction tx)
        {
            var scData = JsonConvert.DeserializeObject<JArray>(tx.Data);
            if( scData != null )
            {
                var function = (string?)scData["Function"];
                var data = (string?)scData["Data"];
                var scUID = (string?)scData["ContractUID"];

                var scMain = SmartContractStateTrei.GetSmartContractState(scUID);
                if(scMain != null)
                {
                    scMain.OwnerAddress = tx.ToAddress;
                    scMain.ContractData = data;
                    scMain.Nonce += 1;

                    SmartContractStateTrei.UpdateSmartContract(scMain);
                }


            }
        }

        public static async Task RemoveNFT(Transaction tx)
        {
            var scData = JsonConvert.DeserializeObject<JArray>(tx.Data);
            if (scData != null)
            {
                var function = (string?)scData["Function"];
                var data = (string?)scData["Data"];
                var scUID = (string?)scData["ContractUID"];

                var scMain = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                if (scMain != null)
                {
                    SmartContractMain.SmartContractData.DeleteSmartContract(scUID);
                }

            }
        }
    }
}
