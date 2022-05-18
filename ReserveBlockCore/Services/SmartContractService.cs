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
        public static async Task<Transaction?> MintSmartContractTx(SmartContractMain scMain)
        {
            Transaction? scTx = null;

            var account = AccountData.GetSingleAccount(scMain.MinterAddress);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            var scData = SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            if(scData.Result.Item1 != null)
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
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

        public static async Task<Transaction?> TransferSmartContract(SmartContractMain scMain, string toAddress)
        {
            Transaction? scTx = null;

            var account = AccountData.GetSingleAccount(scMain.Address);
            if (account == null)
            {
                return null;//Minter address is not found
            }

            scMain.Address = toAddress;
            var scData = SmartContractReaderService.ReadSmartContract(scMain);

            var txData = "";

            if (scData.Result.Item1 != null)
            {
                var bytes = Encoding.Unicode.GetBytes(scData.Result.Item1);
                var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();
                var newSCInfo = new[]
                {
                    new { Function = "Transfer(toAddress : string)", ContractUID = scMain.SmartContractUID, ToAddress = toAddress, Data = scBase64}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
            }

            scTx = new Transaction
            {
                Timestamp = TimeUtil.GetTime(),
                FromAddress = scMain.Address,
                ToAddress = toAddress,
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(scMain.Address),
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
                    //P2PClient.SendTXMempool(scTx);//send out to mempool
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
    }
}
