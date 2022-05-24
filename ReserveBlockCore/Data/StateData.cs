using LiteDB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Data
{
    public class StateData
    {
        public static void CreateGenesisWorldTrei(Block block)
        {
            var trxList = block.Transactions.ToList();
            var accStTrei = new List<AccountStateTrei>();

            trxList.ForEach(x => {

                var acctStateTreiTo = new AccountStateTrei
                {
                    Key = x.ToAddress,
                    Nonce = 0, 
                    Balance = (x.Amount), //subtract from the address
                    StateRoot = block.StateRoot
                };

                accStTrei.Add(acctStateTreiTo);

            });

            var worldTrei = new WorldTrei {
                StateRoot = block.StateRoot,
            };

            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            wTrei.Insert(worldTrei);
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            aTrei.InsertBulk(accStTrei);
        }

        public static void UpdateAccountNonce(string address, long ?nonce = null)
        {
            var account = GetSpecificAccountStateTrei(address);
            if(nonce == null)
            {
                account.Nonce += 1;
            }    
            else
            {
                account.Nonce = nonce.Value;
            }
            var accountTrei = GetAccountStateTrei();
            accountTrei.Update(account);
        }
        public static void UpdateTreis(Block block)
        {
            var txList = block.Transactions.ToList();
            var accStTrei = GetAccountStateTrei();

            txList.ForEach(x => {
                if (block.Height == 0)
                {
                    var acctStateTreiFrom = new AccountStateTrei
                    {
                        Key = x.FromAddress,
                        Nonce = x.Nonce + 1, //increase Nonce for next use
                        Balance = 0, //subtract from the address
                        StateRoot = block.StateRoot
                    };

                    accStTrei.Insert(acctStateTreiFrom);
                }
                else
                {
                    if (x.FromAddress != "Coinbase_TrxFees" && x.FromAddress != "Coinbase_BlkRwd")
                    {
                        var from = GetSpecificAccountStateTrei(x.FromAddress);

                        from.Nonce += 1;
                        from.StateRoot = block.StateRoot;
                        from.Balance -= (x.Amount + x.Fee);

                        accStTrei.Update(from);
                    }
                    else
                    {
                        //do nothing as its the coinbase fee
                    }
                    
                }

                var to = GetSpecificAccountStateTrei(x.ToAddress);

                if(to == null)
                {
                    var acctStateTreiTo = new AccountStateTrei
                    {
                        Key = x.ToAddress,
                        Nonce = 0, 
                        Balance = x.Amount, 
                        StateRoot = block.StateRoot
                    };

                    accStTrei.Insert(acctStateTreiTo);
                }
                else
                {
                    to.Balance += x.Amount;
                    to.StateRoot = block.StateRoot;

                    accStTrei.Update(to);
                }

                if (x.TransactionType != TransactionType.TX)
                {
                    if (x.TransactionType == TransactionType.NFT_TX || x.TransactionType == TransactionType.NFT_MINT
                        || x.TransactionType == TransactionType.NFT_BURN)
                    {
                        var scDataArray = JsonConvert.DeserializeObject<JArray>(x.Data);
                        var scData = scDataArray[0];
                        var function = (string?)scData["Function"];
                        var scUID = (string?)scData["ContractUID"];

                        if (function != "")
                        {
                            switch (function)
                            {
                                case "Mint()":
                                    AddNewlyMintedContract(x);
                                    break;
                                case "Transfer()":
                                    TransferSmartContract(x);
                                    break;
                                case "Burn()":
                                    BurnSmartContract(x);
                                    break;
                                case "Evolve()":
                                    EvolveSC(x);
                                    break;
                                case "Devolve()":
                                    DevolveSC(x);
                                    break;
                                case "ChangeEvolveStateSpecific()":
                                    EvolveDevolveSpecific(x);
                                    break;
                                default:
                                    break;
                            }
                        }

                    }
                }

            });

            WorldTrei.UpdateWorldTrei(block);

        }

        public static ILiteCollection<AccountStateTrei> GetAccountStateTrei()
        {
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            return aTrei;
            
        }

        public static AccountStateTrei GetSpecificAccountStateTrei(string address)
        {
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            var account = aTrei.FindOne(x => x.Key == address);
            if (account == null)
            {
                return null;
            }
            else
            {
                return account;
            }
        }

        public static SmartContractStateTrei GetSpecificSmartContractStateTrei(string scUID)
        {
            var scTrei = DbContext.DB_SmartContractStateTrei.GetCollection<SmartContractStateTrei>(DbContext.RSRV_SCSTATE_TREI);
            var account = scTrei.FindOne(x => x.SmartContractUID == scUID);
            if (account == null)
            {
                return null;
            }
            else
            {
                return account;
            }
        }

        public static void AddNewlyMintedContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            if (scData != null)
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
                //SmartContractMain.SmartContractData.SetSmartContractIsPublished(scUID);
            }

        }
        public static void TransferSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            var function = (string?)scData["Function"];
            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if(scStateTreiRec != null)
            {
                scStateTreiRec.OwnerAddress = tx.ToAddress;
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

        }
        public static void BurnSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            var function = (string?)scData["Function"];
            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                SmartContractStateTrei.DeleteSmartContract(scStateTreiRec);
            }

        }

        public static void EvolveSC(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }
        }

        public static void DevolveSC(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }
        }

        public static void EvolveDevolveSpecific(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }
        }

    }
}
