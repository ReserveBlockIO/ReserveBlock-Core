using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Services;
using System.Collections.Concurrent;

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
            wTrei.InsertSafe(worldTrei);
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            aTrei.InsertBulkSafe(accStTrei);
        }

        public static async void UpdateTreis(Block block)
        {
            var txList = block.Transactions.ToList();
            var accStTrei = GetAccountStateTrei();
            ConcurrentDictionary<string, StateTreiAuditData> StateTreiAuditDict = new ConcurrentDictionary<string, StateTreiAuditData>();

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

                    accStTrei.InsertSafe(acctStateTreiFrom);
                }
                else
                {
                    if (x.FromAddress != "Coinbase_TrxFees" && x.FromAddress != "Coinbase_BlkRwd")
                    {
                        var from = GetSpecificAccountStateTrei(x.FromAddress);

                        var newRec = new StateTreiAuditData
                        {
                            NewValue = from.Balance -= (x.Amount + x.Fee),
                            OldValue = from.Balance,
                            NextNonce = from.Nonce += 1,
                            Nonce = from.Nonce,
                            Address = from.Key,
                            StateRoot = block.StateRoot,
                            StateRecordStatus = StateRecordStatus.Update
                        };

                        var stAD = StateTreiAuditDict.TryGet(from.Key);
                        if (stAD != null)
                        {
                            var newOldValue = stAD.NewValue;
                            var newOldNonce = stAD.NextNonce;
                            stAD.OldValue = newOldValue;
                            stAD.NewValue -= x.Amount;
                            stAD.Nonce = newOldNonce;
                            stAD.NextNonce = stAD.NextNonce + 1;
                            StateTreiAuditDict[from.Key] = stAD;
                        }
                        else
                        {
                            StateTreiAuditDict[from.Key] = newRec;
                        }

                        from.Nonce += 1;
                        from.StateRoot = block.StateRoot;
                        from.Balance -= (x.Amount + x.Fee);

                        accStTrei.UpdateSafe(from);
                    }
                    else
                    {
                        //do nothing as its the coinbase fee
                    }
                    
                }

                if(x.ToAddress != "Adnr_Base" && x.ToAddress != "DecShop_Base" && x.ToAddress != "Topic_Base" && x.ToAddress != "Vote_Base")
                {
                    var to = GetSpecificAccountStateTrei(x.ToAddress);
                    if(x.TransactionType == TransactionType.TX)
                    {
                        if (to == null)
                        {
                            var acctStateTreiTo = new AccountStateTrei
                            {
                                Key = x.ToAddress,
                                Nonce = 0,
                                Balance = x.Amount,
                                StateRoot = block.StateRoot
                            };

                            var newRec = new StateTreiAuditData
                            {
                                NewValue = x.Amount,
                                OldValue = x.Amount,
                                NextNonce = 0,
                                Nonce = 0,
                                Address = x.ToAddress,
                                StateRoot = block.StateRoot,
                                StateRecordStatus = StateRecordStatus.Insert
                            };

                            var stAD = StateTreiAuditDict.TryGet(x.ToAddress);
                            if (stAD != null)
                            {
                                var newOldValue = stAD.NewValue;
                                stAD.OldValue = newOldValue;
                                stAD.NewValue += x.Amount;
                                StateTreiAuditDict[x.ToAddress] = stAD;
                            }
                            else
                            {
                                StateTreiAuditDict[x.ToAddress] = newRec;
                            }

                            accStTrei.InsertSafe(acctStateTreiTo);
                        }
                        else
                        {
                            var newRec = new StateTreiAuditData
                            {
                                NewValue = to.Balance + x.Amount,
                                OldValue = to.Balance,
                                NextNonce = to.Nonce,
                                Nonce = to.Nonce,
                                Address = to.Key,
                                StateRoot = block.StateRoot,
                                StateRecordStatus = StateRecordStatus.Update
                            };

                            var stAD = StateTreiAuditDict.TryGet(to.Key);
                            if (stAD != null)
                            {
                                var newOldValue = stAD.NewValue;
                                stAD.OldValue = newOldValue;
                                stAD.NewValue += x.Amount;
                                StateTreiAuditDict[to.Key] = stAD;
                            }
                            else
                            {
                                StateTreiAuditDict[to.Key] = newRec;
                            }

                            to.Balance += x.Amount;
                            to.StateRoot = block.StateRoot;

                            accStTrei.UpdateSafe(to);
                        }

                    }
                    
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

                        if (!string.IsNullOrWhiteSpace(function))
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

                    if(x.TransactionType == TransactionType.ADNR)
                    {
                        var txData = x.Data;
                        if (!string.IsNullOrWhiteSpace(txData))
                        {
                            var jobj = JObject.Parse(txData);
                            var function = (string)jobj["Function"];
                            if (!string.IsNullOrWhiteSpace(function))
                            {
                                switch (function)
                                {
                                    case "AdnrCreate()":
                                        AddNewAdnr(x);
                                        break;
                                    case "AdnrTransfer()":
                                        TransferAdnr(x);
                                        break;
                                    case "AdnrDelete()":
                                        DeleteAdnr(x);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }

                    if(x.TransactionType == TransactionType.VOTE_TOPIC)
                    {
                        var txData = x.Data;
                        if (!string.IsNullOrWhiteSpace(txData))
                        {
                            var jobj = JObject.Parse(txData);
                            if (jobj != null)
                            {
                                var function = (string)jobj["Function"];
                                TopicTrei topic = jobj["Topic"].ToObject<TopicTrei>();
                                if(topic != null)
                                {
                                    topic.Id = 0;//save new
                                    topic.BlockHeight = x.Height;
                                    TopicTrei.SaveTopic(topic);
                                }
                            }
                        }
                    }

                    if (x.TransactionType == TransactionType.VOTE)
                    {
                        var txData = x.Data;
                        if (!string.IsNullOrWhiteSpace(txData))
                        {
                            var jobj = JObject.Parse(txData);
                            if (jobj != null)
                            {
                                var function = (string)jobj["Function"];
                                Vote vote = jobj["Vote"].ToObject<Vote>();
                                if(vote != null)
                                {
                                    vote.Id = 0;
                                    vote.TransactionHash = x.Hash;
                                    vote.BlockHeight = x.Height;
                                    var result = Vote.SaveVote(vote);
                                    if(result)
                                    {
                                        var topic = TopicTrei.GetSpecificTopic(vote.TopicUID);
                                        if(topic != null)
                                        {
                                            if (vote.VoteType == VoteType.Yes)
                                                topic.VoteYes += 1;
                                            if (vote.VoteType == VoteType.No)
                                                topic.VoteNo += 1;

                                            TopicTrei.UpdateTopic(topic);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (x.TransactionType == TransactionType.DSTR)
                    {
                        var txData = x.Data;
                        if (!string.IsNullOrWhiteSpace(txData))
                        {
                            var jobj = JObject.Parse(txData);
                            var function = (string)jobj["Function"];
                            if (!string.IsNullOrWhiteSpace(function))
                            {
                                switch (function)
                                {
                                    case "DecShopCreate()":
                                        //AddNewDecShop(x);
                                        break;
                                    case "DecShopDelete()":
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }

            });

            WorldTrei.UpdateWorldTrei(block);
            //await StateAuditUtility.AuditAccountStateTrei(StateTreiAuditDict);
        }

        public static LiteDB.ILiteCollection<AccountStateTrei> GetAccountStateTrei()
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

        private static void AddNewAdnr(Transaction tx)
        {
            try
            {
                var jobj = JObject.Parse(tx.Data);
                var name = (string)jobj["Name"];
                Adnr adnr = new Adnr();

                adnr.Address = tx.FromAddress;
                adnr.Timestamp = tx.Timestamp;
                adnr.Name = name + ".rbx";
                adnr.TxHash = tx.Hash;

                Adnr.SaveAdnr(adnr);
                
            }
            catch(Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
            }
        }
        private static void TransferAdnr(Transaction tx)
        {
            bool complete = false;
            while(!complete)
            {
                var adnrs = Adnr.GetAdnr();
                if (adnrs != null)
                {
                    var adnr = adnrs.FindOne(x => x.Address == tx.FromAddress);
                    if (adnr != null)
                    {
                        adnr.Address = tx.ToAddress;
                        adnr.TxHash = tx.Hash;
                        adnrs.UpdateSafe(adnr);
                        complete = true;
                    }
                }
            }
            
        }

        private static void DeleteAdnr(Transaction tx)
        {
            try
            {
                Adnr.DeleteAdnr(tx.FromAddress);
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
            }
        }

        private static void AddNewlyMintedContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            if (scData != null)
            {
                var function = (string?)scData["Function"];
                var data = (string?)scData["Data"];
                var scUID = (string?)scData["ContractUID"];
                var md5List = (string?)scData["MD5List"];


                scST.ContractData = data;
                scST.MinterAddress = tx.FromAddress;
                scST.OwnerAddress = tx.FromAddress;
                scST.SmartContractUID = scUID;
                scST.Nonce = 0;
                scST.MD5List = md5List;

                try
                {
                    var sc = SmartContractMain.GenerateSmartContractInMemory(data);
                    if (sc.Features != null)
                    {
                        var evoFeatures = sc.Features.Where(x => x.FeatureName == FeatureName.Evolving).Select(x => x.FeatureFeatures).FirstOrDefault();
                        var isDynamic = false;
                        if (evoFeatures != null)
                        {
                            var evoFeatureList = (List<EvolvingFeature>)evoFeatures;
                            foreach (var feature in evoFeatureList)
                            {
                                var evoFeature = (EvolvingFeature)feature;
                                if (evoFeature.IsDynamic == true)
                                    isDynamic = true;
                            }
                        }

                        if (!isDynamic)
                            scST.MinterManaged = true;
                    }
                }
                catch { }

                //Save to state trei
                SmartContractStateTrei.SaveSmartContract(scST);
            }

        }
        private static void TransferSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var function = (string?)scData["Function"];
            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];
            var locator = (string?)scData["Locators"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if(scStateTreiRec != null)
            {
                scStateTreiRec.OwnerAddress = tx.ToAddress;
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;
                scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

        }
        private static void BurnSmartContract(Transaction tx)
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

        private static void EvolveSC(Transaction tx)
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

        private static void DevolveSC(Transaction tx)
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

        private static void EvolveDevolveSpecific(Transaction tx)
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
