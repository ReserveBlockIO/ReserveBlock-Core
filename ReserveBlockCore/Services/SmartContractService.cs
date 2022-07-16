using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace ReserveBlockCore.Services
{
    public static class SmartContractService
    {
        #region MintSmartContractTx
        public static async Task<Transaction?> MintSmartContractTx(SmartContractMain scMain)
        {
            Transaction? scTx = null;

            var account = AccountData.GetSingleAccount(scMain.MinterAddress);
            if (account == null)
            {
                NFTLogUtility.Log($"Minter address is not found for : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null;//Minter address is not found
            }

            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);

            if(scStateTrei != null)
            {
                NFTLogUtility.Log($"This NFT has already be minted : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null;// record already exist
            }

            var scData = SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            if(scData.Result.Item1 != null)
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                var scBase64 = bytes.ToCompress().ToBase64();
                var newSCInfo = new[]
                {
                    new { Function = "Mint()", ContractUID = scMain.SmartContractUID, Data = scBase64}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
            }

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = scMain.MinterAddress,
                ToAddress = scMain.MinterAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(scMain.MinterAddress),
                TransactionType = TransactionType.NFT_MINT,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                NFTLogUtility.Log($"Balance insufficient to send NFT : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                NFTLogUtility.Log($"Signing NFT TX failed : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result == true)
                {
                    TransactionData.AddToPool(scTx);
                    AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                    P2PClient.SendTXMempool(scTx);//send out to mempool
                    return scTx;
                }
                else
                {
                    NFTLogUtility.Log($"Transaction Failed Verify and was not Sent to Mempool : {scMain.SmartContractUID}", "SmartContractService.MintSmartContractTx(SmartContractMain scMain)");
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return null;
        }

        #endregion

        #region TransferSmartContract
        public static async Task<Transaction?> TransferSmartContract(SmartContractMain scMain, string toAddress, string locators, string md5List = "NA")
        {
            Transaction? scTx = null;

            var scst = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);
            
            if(scst == null)
            {
                return null;
            }

            var account = AccountData.GetSingleAccount(scst.OwnerAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }
            var fromAddress = account.Address;

            var scData = SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            
            if (scData.Result.Item1 != null)
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
                var newSCInfo = new[]
                {
                    new { Function = "Transfer()", ContractUID = scMain.SmartContractUID, ToAddress = toAddress, Data = scBase64, Locators = locators, MD5List = md5List}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
            }

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = account.Address,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(account.Address),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result == true)
                {
                    TransactionData.AddToPool(scTx);
                    AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                    P2PClient.SendTXMempool(scTx);//send out to mempool
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return null;
        }

        #endregion

        #region BurnSmartContract
        public static async Task<Transaction?> BurnSmartContract(SmartContractMain scMain)
        {
            Transaction? scTx = null;

            var scst = SmartContractStateTrei.GetSmartContractState(scMain.SmartContractUID);

            if (scst == null)
            {
                return null;
            }

            var account = AccountData.GetSingleAccount(scst.OwnerAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            var scData = SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            if (scData.Result.Item1 != null)
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
                var newSCInfo = new[]
                {
                    new { Function = "Burn()", ContractUID = scMain.SmartContractUID, FromAddress = account.Address}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
            }

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = account.Address,
                ToAddress = account.Address,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(account.Address),
                TransactionType = TransactionType.NFT_BURN,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result == true)
                {
                    TransactionData.AddToPool(scTx);
                    AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                    P2PClient.SendTXMempool(scTx);//send out to mempool
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return null;
        }
        #endregion

        #region EvolveSmartContract
        public static async Task<Transaction?> EvolveSmartContract(string scUID, string toAddress)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if(smartContractStateTrei == null)
            {
                return null;
            }

            //Can't evolve if you are not the minter
            var account = AccountData.GetSingleAccount(smartContractStateTrei.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            //Can't evolve if the ToAddress does not own contract
            if(toAddress != smartContractStateTrei.OwnerAddress)
            {
                return null;
            }

            var minterAddress = smartContractStateTrei.MinterAddress;
            var evolve = await EvolvingFeature.GetNewEvolveState(smartContractStateTrei.ContractData);
            
            var evolveResult = evolve.Item1;
            if(evolveResult != true)
            {
                return null;
            }

            var evolveData = evolve.Item2;

            var bytes = Encoding.Unicode.GetBytes(evolveData);
            var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

            var newSCInfo = new[]
            {
                new { Function = "Evolve()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, Data = scBase64}
            };

            var txData = JsonConvert.SerializeObject(newSCInfo);
            

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = minterAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(minterAddress),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result == true)
                {
                    TransactionData.AddToPool(scTx);
                    AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                    P2PClient.SendTXMempool(scTx);//send out to mempool
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return null;
        }

        #endregion

        #region DevolveSmartContract
        public static async Task<Transaction?> DevolveSmartContract(string scUID, string toAddress)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (smartContractStateTrei == null)
            {
                return null;
            }

            //Can't evolve if you are not the minter
            var account = AccountData.GetSingleAccount(smartContractStateTrei.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            //Can't evolve if the ToAddress does not own contract
            if (toAddress != smartContractStateTrei.OwnerAddress)
            {
                return null;
            }

            var minterAddress = smartContractStateTrei.MinterAddress;
            var evolve = await EvolvingFeature.GetNewDevolveState(smartContractStateTrei.ContractData);

            var evolveResult = evolve.Item1;
            if (evolveResult != true)
            {
                return null;
            }

            var evolveData = evolve.Item2;

            var bytes = Encoding.Unicode.GetBytes(evolveData);
            var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

            var newSCInfo = new[]
            {
                new { Function = "Devolve()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, Data = scBase64}
            };

            var txData = JsonConvert.SerializeObject(newSCInfo);


            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = minterAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(minterAddress),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result == true)
                {
                    TransactionData.AddToPool(scTx);
                    AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                    P2PClient.SendTXMempool(scTx);//send out to mempool
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return null;
        }

        #endregion

        #region ChangeEvolveStateSpecific
        public static async Task<Transaction?> ChangeEvolveStateSpecific(string scUID, string toAddress, int evoState)
        {
            Transaction? scTx = null;

            var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (smartContractStateTrei == null)
            {
                return null;
            }

            //Can't evolve if you are not the minter
            var account = AccountData.GetSingleAccount(smartContractStateTrei.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            //Can't evolve if the ToAddress does not own contract
            if (toAddress != smartContractStateTrei.OwnerAddress)
            {
                return null;
            }

            var minterAddress = smartContractStateTrei.MinterAddress;
            var evolve = await EvolvingFeature.GetNewSpecificState(smartContractStateTrei.ContractData, evoState);

            var evolveResult = evolve.Item1;
            if (evolveResult != true)
            {
                return null;
            }

            var evolveData = evolve.Item2;

            var bytes = Encoding.Unicode.GetBytes(evolveData);
            var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

            var newSCInfo = new[]
            {
                new { Function = "ChangeEvolveStateSpecific()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, NewEvoState = evoState, Data = scBase64}
            };

            var txData = JsonConvert.SerializeObject(newSCInfo);


            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = minterAddress,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(minterAddress),
                TransactionType = TransactionType.NFT_TX,
                Data = txData
            };

            scTx.Fee = FeeCalcService.CalculateTXFee(scTx);

            scTx.Build();

            var senderBalance = AccountStateTrei.GetAccountBalance(account.Address);
            if ((scTx.Amount + scTx.Fee) > senderBalance)
            {
                return null;//balance insufficient
            }

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var txHash = scTx.Hash;
            var signature = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (signature == "ERROR")
            {
                return null; //TX sig failed
            }

            scTx.Signature = signature; //sigScript  = signature + '.' (this is a split char) + pubKey in Base58 format

            try
            {
                var result = await TransactionValidatorService.VerifyTX(scTx);
                if (result == true)
                {
                    TransactionData.AddToPool(scTx);
                    AccountData.UpdateLocalBalance(scTx.FromAddress, (scTx.Fee + scTx.Amount));
                    P2PClient.SendTXMempool(scTx);//send out to mempool
                    return scTx;
                }
                else
                {
                    var output = "Fail! Transaction Verify has failed.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }

            return null;
        }

        #endregion
    }
}
