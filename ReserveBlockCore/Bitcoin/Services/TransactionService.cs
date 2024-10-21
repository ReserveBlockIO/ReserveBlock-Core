using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.Policy;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using ReserveBlockCore.Arbiter;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.SecretSharing.Cryptography;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using static ReserveBlockCore.Models.Integrations;
using static System.Net.Mime.MediaTypeNames;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class TransactionService
    {
        public static decimal BTCMultiplier = 100_000_000M;
        public static decimal SatoshiMultiplier = 0.00000001M;
        public static async Task<(bool, string)> SendTransaction(string sender, string receiver, decimal sendAmount, long chosenFeeRate, bool overrideInternalSend = false)
        {
            try
            {
                receiver = receiver.ToBTCAddressNormalize();

                var btcAccount = BitcoinAccount.GetBitcoinAccount(sender);
                var receiverAccount = BitcoinAccount.GetBitcoinAccount(receiver);
                if (btcAccount == null)
                    return (false, $"Could not find a bitcoin account for the following address: {sender}");

                if (btcAccount.Balance <= sendAmount)
                    return (false, $"Insufficient Balance: {btcAccount.Balance}");

                if (receiverAccount != null && !overrideInternalSend)
                    return (false, $"This is an internal send. Please use the override if you wish to do this.");

                if (sendAmount < Globals.BTCMinimumAmount)
                    return (false, $"This wallet does not support sends smaller than {Globals.BTCMinimumAmount} BTC.");

                Console.WriteLine($"Account Checks Passed.");

                string senderPrivateKeyHex = btcAccount.PrivateKey;

                BitcoinAddress senderAddress = BitcoinAddress.Create(sender, Globals.BTCNetwork);
                BitcoinAddress recipientAddress = BitcoinAddress.Create(receiver, Globals.BTCNetwork);

                ulong amountToSend = Convert.ToUInt32(sendAmount * BTCMultiplier);
                ulong feeEstimate = 0;
                bool sufficientInputsFound = false;
                List<Coin> unspentCoins = new List<Coin>();
                List<BitcoinUTXO> coinListBtcUTXOs = new List<BitcoinUTXO>();

                ulong previousTotalInputAmount = 0;

                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, senderAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;

                    if (!unspentCoins.Any())
                        return (false, "Could not find any UTXOs for inputs.");

                    // Check if the coin list has changed
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

                    // Check if the total input amount is unchanged
                    if (totalInputAmount == previousTotalInputAmount)
                    {
                        // If total input amount is unchanged, no more UTXOs are available
                        return (false, $"Not enough UTXOs to cover the amount and fees. Fee: {feeEstimate} Sats");
                    }

                    previousTotalInputAmount = totalInputAmount;

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(chosenFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    if (totalInputAmount >= totalAmountRequired)
                    {
                        sufficientInputsFound = true;
                        coinListBtcUTXOs = coinList.Item2;
                    }
                    else
                    {
                        // If inputs are not sufficient, try to get more or larger UTXOs
                        sendAmount += (decimal)feeEstimate / BTCMultiplier; // Increment the amount to cover fee in next iteration
                    }

                }

                var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

                txBuilder.AddCoins(unspentCoins.ToArray());

                txBuilder
                    .Send(recipientAddress, new Money(amountToSend, MoneyUnit.Satoshi))
                    .SetChange(senderAddress);

                Console.WriteLine($"TX builder Done.");

                // Get the count of inputs and outputs
                int finalInputCount = unspentCoins.Count();
                int finalOutputCount = 2; // one for recipient, one for change

                FeeRate feeRate = new FeeRate(chosenFeeRate * 1000);

                int finalTransactionSize = FeeCalcService.EstimateTransactionSize(finalInputCount, finalOutputCount);
                ulong finalFee = feeRate.GetFee(finalTransactionSize);

                decimal totalAmountSpent = (amountToSend + finalFee) * SatoshiMultiplier;

                byte[] privateKeyBytes = senderPrivateKeyHex.HexToByteArray();
                Key senderKey = new Key(privateKeyBytes);

                Console.WriteLine($"Fees calculated...");

                var signedTransaction = txBuilder
                    .AddKeys(senderKey)
                    .SendFees(new Money(finalFee, MoneyUnit.Satoshi))
                    .SetOptInRBF(true)
                    .BuildTransaction(true);

                Console.WriteLine($"Tx Has been signed");

                var txVerified = ValidateTransaction(signedTransaction, txBuilder);//txBuilder.Verify(fullySigned);

                if (txVerified.Item1)
                {
                    Console.WriteLine($"Tx Has been verified.");
                    btcAccount.Balance -= totalAmountSpent;
                    var btcDb = BitcoinAccount.GetBitcoin();
                    if (btcDb != null)
                        btcDb.UpdateSafe(btcAccount);

                    if (coinListBtcUTXOs.Any())
                    {
                        coinListBtcUTXOs.ForEach(x => {
                            BitcoinUTXO.SpendUTXO(x.TxId, x.Address);
                        });
                    }

                    var hexTx = signedTransaction.ToHex();
                    //return (true, $"{hexTx}");

                    string outputList = string.Join(",", coinListBtcUTXOs.Select(x => x.TxId));

                    var tx = new BitcoinTransaction
                    {
                        Fee = (finalFee * SatoshiMultiplier),
                        Amount = sendAmount,
                        FromAddress = sender,
                        ToAddress = receiver,
                        FeeRate = chosenFeeRate,
                        Hash = signedTransaction.GetHash().ToString(),
                        Signature = hexTx,
                        Timestamp = TimeUtil.GetTime(0,0,0,999),
                        TransactionType = !overrideInternalSend ? BTCTransactionType.Send : BTCTransactionType.SameWalletTransaction,
                        BitcoinUTXOs = coinListBtcUTXOs,
                    };

                    BitcoinTransaction.SaveBitcoinTX(tx);

                    Console.WriteLine($"Broadcast started @ {DateTime.Now}");

                    _ = BroadcastService.BroadcastTx(signedTransaction);

                    Console.WriteLine($"Broadcast completed @ {DateTime.Now}");

                    return (true, $"{tx.Hash}");
                }
                else
                {
                    Console.WriteLine($"Tx FAILED to verify.");
                    ErrorLogUtility.LogError($"Tx FAILED to verify.", "TransactionService.SendTransaction()");
                    return (false, txVerified.Item2);
                    
                }

                return (false, $"Unknown Error");
            }
            catch(Exception ex) 
            {
                ErrorLogUtility.LogError($"ERROR: {ex}", "TransactionService.SendTransaction()");
                return (false, $"Error: {ex}");
            }
            
        }

        public static async Task<(bool, string)> CalcuateFee(string sender, string receiver, decimal sendAmount, long chosenFeeRate)
        {
            try
            {
                receiver = receiver.ToBTCAddressNormalize();

                var btcAccount = BitcoinAccount.GetBitcoinAccount(sender);
                var receiverAccount = BitcoinAccount.GetBitcoinAccount(receiver);
                if (btcAccount == null)
                    return (false, $"Could not find a bitcoin account for the following address: {sender}");

                if (btcAccount.Balance <= sendAmount)
                    return (false, $"Insufficient Balance: {btcAccount.Balance}");

                if (sendAmount < Globals.BTCMinimumAmount)
                    return (false, $"This wallet does not support sends smaller than {Globals.BTCMinimumAmount} BTC.");

                Console.WriteLine($"Account Checks Passed.");

                string senderPrivateKeyHex = btcAccount.PrivateKey;

                BitcoinAddress senderAddress = BitcoinAddress.Create(sender, Globals.BTCNetwork);
                BitcoinAddress recipientAddress = BitcoinAddress.Create(receiver, Globals.BTCNetwork);

                ulong amountToSend = Convert.ToUInt32(sendAmount * BTCMultiplier);
                ulong feeEstimate = 0;
                bool sufficientInputsFound = false;
                List<Coin> unspentCoins = new List<Coin>();
                List<BitcoinUTXO> coinListBtcUTXOs = new List<BitcoinUTXO>();

                ulong previousTotalInputAmount = 0;
                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, senderAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;

                    if (!unspentCoins.Any())
                        return (false, "Could not find any UTXOs for inputs.");

                    // Check if the coin list has changed
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

                    // Check if the total input amount is unchanged
                    if (totalInputAmount == previousTotalInputAmount)
                    {
                        // If total input amount is unchanged, no more UTXOs are available
                        return (false, $"Not enough UTXOs to cover the amount and fees. Fee: {feeEstimate} Sats");
                    }

                    previousTotalInputAmount = totalInputAmount;

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(chosenFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    if (totalInputAmount >= totalAmountRequired)
                    {
                        sufficientInputsFound = true;
                        coinListBtcUTXOs = coinList.Item2;
                    }
                    else
                    {
                        // If inputs are not sufficient, try to get more or larger UTXOs
                        sendAmount += (decimal)feeEstimate / BTCMultiplier; // Increment the amount to cover fee in next iteration
                    }

                }

                var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

                unspentCoins.ForEach(x => {
                    txBuilder.AddCoin(x);
                });

                txBuilder
                    .Send(recipientAddress, new Money(amountToSend, MoneyUnit.Satoshi))
                    .SetChange(senderAddress);

                Console.WriteLine($"TX builder Done.");

                // Get the count of inputs and outputs
                int finalInputCount = unspentCoins.Count();
                int finalOutputCount = 2; // one for recipient, one for change

                FeeRate feeRate = new FeeRate(chosenFeeRate * 1000);

                int finalTransactionSize = FeeCalcService.EstimateTransactionSize(finalInputCount, finalOutputCount);
                ulong finalFee = feeRate.GetFee(finalTransactionSize);

                decimal totalFee = finalFee * SatoshiMultiplier;

                return (true, $"{totalFee}");


            }
            catch(Exception ex )
            {
                return (false, $"Error: {ex}");
            }
        }

        public static async Task<string> ReplaceByFeeTransaction(string txid, long nFeeRate)
        {
            try
            {
                var transaction = await BitcoinTransaction.GetTX(txid);

                if (transaction == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Transaction for TXID: {txid} was null." });

                var btcAccount = BitcoinAccount.GetBitcoinAccount(transaction.FromAddress);
                var receiverAccount = BitcoinAccount.GetBitcoinAccount(transaction.ToAddress);

                if (btcAccount == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find a bitcoin account for the following address: {transaction.FromAddress}" });

                if (btcAccount.Balance <= transaction.Amount)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Insufficient Balance: {btcAccount.Balance}" });

                Console.WriteLine($"Account Checks Passed.");

                string senderPrivateKeyHex = btcAccount.PrivateKey;

                BitcoinAddress senderAddress = BitcoinAddress.Create(transaction.FromAddress, Globals.BTCNetwork);
                BitcoinAddress recipientAddress = BitcoinAddress.Create(transaction.ToAddress, Globals.BTCNetwork);

                ulong amountToSend = Convert.ToUInt32(transaction.Amount * BTCMultiplier);
                ulong feeEstimate = 0;
                bool sufficientInputsFound = false;
                List<Coin> unspentCoins = new List<Coin>();
                List<BitcoinUTXO> coinListBtcUTXOs = new List<BitcoinUTXO>();
                ulong previousTotalInputAmount = 0;
                decimal sendAmount = transaction.Amount;
                string sender = transaction.FromAddress;

                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, senderAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;

                    if (!unspentCoins.Any())
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Could not find any UTXOs for inputs." });

                    // Check if the coin list has changed
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

                    // Check if the total input amount is unchanged
                    if (totalInputAmount == previousTotalInputAmount)
                    {
                        // If total input amount is unchanged, no more UTXOs are available
                        return JsonConvert.SerializeObject(new { Success = false, Message = $"Not enough UTXOs to cover the amount and fees. Fee: {feeEstimate} Sats" });
                    }

                    previousTotalInputAmount = totalInputAmount;

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(nFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    if (totalInputAmount >= totalAmountRequired)
                    {
                        sufficientInputsFound = true;
                        coinListBtcUTXOs = coinList.Item2;
                    }
                    else
                    {
                        // If inputs are not sufficient, try to get more or larger UTXOs
                        sendAmount += (decimal)feeEstimate / BTCMultiplier; // Increment the amount to cover fee in next iteration
                    }

                }

                var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

                unspentCoins.ForEach(x => {
                    txBuilder.AddCoin(x);
                });

                txBuilder
                    .Send(recipientAddress, new Money(amountToSend, MoneyUnit.Satoshi))
                    .SetChange(senderAddress);

                Console.WriteLine($"TX builder Done.");

                // Get the count of inputs and outputs
                int finalInputCount = unspentCoins.Count();
                int finalOutputCount = 2; // one for recipient, one for change

                FeeRate feeRate = new FeeRate(nFeeRate * 1000);

                int finalTransactionSize = FeeCalcService.EstimateTransactionSize(finalInputCount, finalOutputCount);
                ulong finalFee = feeRate.GetFee(finalTransactionSize);

                decimal totalAmountSpent = (amountToSend + finalFee) * SatoshiMultiplier;
                decimal originalAmountSpent = (transaction.Amount + transaction.Fee);

                byte[] privateKeyBytes = senderPrivateKeyHex.HexToByteArray();
                Key senderKey = new Key(privateKeyBytes);

                Console.WriteLine($"Fees calculated...");

                var signedTransaction = txBuilder
                    .AddKeys(senderKey)
                    .SendFees(new Money(finalFee, MoneyUnit.Satoshi))
                    .SetOptInRBF(true)
                    .BuildTransaction(true);

                Console.WriteLine($"Tx Has been signed");

                var txVerified = txBuilder.Verify(signedTransaction);

                if (txVerified)
                {
                    Console.WriteLine($"Tx Has been verified.");
                    btcAccount.Balance += originalAmountSpent;
                    btcAccount.Balance -= totalAmountSpent;
                    var btcDb = BitcoinAccount.GetBitcoin();
                    if (btcDb != null)
                        btcDb.UpdateSafe(btcAccount);

                    transaction.TransactionType = BTCTransactionType.Replaced;
                    BitcoinTransaction.UpdateBitcoinTX(transaction);

                    if (coinListBtcUTXOs.Any())
                    {
                        coinListBtcUTXOs.ForEach(x =>
                        {
                            BitcoinUTXO.SpendUTXO(x.TxId, x.Address);
                        });
                    }

                    var hexTx = signedTransaction.ToHex();

                    var tx = new BitcoinTransaction
                    {
                        Fee = (finalFee * SatoshiMultiplier),
                        Amount = transaction.Amount,
                        FromAddress = transaction.FromAddress,
                        ToAddress = transaction.ToAddress,
                        FeeRate = nFeeRate,
                        Hash = signedTransaction.GetHash().ToString(),
                        Signature = hexTx,
                        Timestamp = TimeUtil.GetTime(),
                        TransactionType = BTCTransactionType.Send,
                        BitcoinUTXOs = coinListBtcUTXOs
                    };

                    BitcoinTransaction.SaveBitcoinTX(tx);

                    Console.WriteLine($"Broadcast started @ {DateTime.Now}");

                    _ = BroadcastService.BroadcastTx(signedTransaction);

                    Console.WriteLine($"Broadcast completed @ {DateTime.Now}");

                    return JsonConvert.SerializeObject(new { Success = true, Message = $"RBF Transaction Broadcasted.", tx.Hash });
                }

                return JsonConvert.SerializeObject(new { Success = true, Message = $"TX not verified" });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = true, Message = $"TX not verified. ERROR: {ex}" });
            }
            
        }

        public static async Task<string> SendMultiSigTransactions(List<PubKey> pubKeys, decimal sendAmount, Account vfxAccount, string toAddress, string changeAddress, long chosenFeeRate, string scUID)
        {
            try
            {
                Script scriptPubKey = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(Globals.TotalArbiterThreshold, pubKeys.OrderBy(x => x.ScriptPubKey.ToString()).ToArray());
                Script redeemScript = scriptPubKey.PaymentScript;

                BitcoinAddress multiSigAddress = scriptPubKey.Hash.GetAddress(Globals.BTCNetwork);
                var sender = multiSigAddress.ToString();

                if (changeAddress != sender)
                    return await SCLogUtility.LogAndReturn($"Change Address did not equal sender.", "TransactionService.SendMultiSigTransactions()", false);

                BitcoinAddress recipientAddress = BitcoinAddress.Create(toAddress, Globals.BTCNetwork);

                //var sendAmount = new Money(amount, MoneyUnit.BTC);

                ulong amountToSend = Convert.ToUInt32(sendAmount * BTCMultiplier);
                ulong feeEstimate = 0;
                bool sufficientInputsFound = false;
                List<Coin> unspentCoins = new List<Coin>();
                List<BitcoinUTXO> coinListBtcUTXOs = new List<BitcoinUTXO>();
                List<CoinInput> coinInputs = new List<CoinInput>();

                ulong previousTotalInputAmount = 0;

                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, multiSigAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;

                    if (!unspentCoins.Any())
                        return await SCLogUtility.LogAndReturn($"Could not find any unspent coins.", "TransactionService.SendMultiSigTransactions()", false);
                    //return (false, "Could not find any UTXOs for inputs.");

                    // Check if the coin list has changed
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

                    // Check if the total input amount is unchanged
                    if (totalInputAmount == previousTotalInputAmount)
                    {
                        // If total input amount is unchanged, no more UTXOs are available
                        return await SCLogUtility.LogAndReturn($"Failed to find enough inputs for amount. Not enough in amount to cover fee.", "TransactionService.SendMultiSigTransactions()", false);
                    }

                    previousTotalInputAmount = totalInputAmount;

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(chosenFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    if (totalInputAmount >= totalAmountRequired)
                    {
                        sufficientInputsFound = true;
                        coinListBtcUTXOs = coinList.Item2;
                        coinInputs = coinList.Item3;
                    }
                    else
                    {
                        // If inputs are not sufficient, try to get more or larger UTXOs
                        sendAmount += (decimal)feeEstimate / BTCMultiplier; // Increment the amount to cover fee in next iteration
                    }

                }

                List<ScriptCoin> coinsToSpend = new List<ScriptCoin>();
                foreach (var coin in unspentCoins)
                {
                    ScriptCoin coinToSpend = new ScriptCoin(coin, scriptPubKey);
                    coinsToSpend.Add(coinToSpend);
                }

                foreach (var input in coinInputs)
                {
                    input.ScriptPubKey = scriptPubKey.ToHex();
                }

                var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

                txBuilder.AddCoins(coinsToSpend.ToArray());

                // Get the count of inputs and outputs
                int finalInputCount = unspentCoins.Count();
                int finalOutputCount = 2; // one for recipient, one for change, one for uniqueID message

                FeeRate feeRate = new FeeRate(chosenFeeRate * 1000);

                int finalTransactionSize = FeeCalcService.EstimateTransactionSize(finalInputCount, finalOutputCount);
                ulong finalFee = feeRate.GetFee(finalTransactionSize);

                ulong totalAmountSpent = (amountToSend - finalFee);

                if(amountToSend <= finalFee)
                {
                    return await SCLogUtility.LogAndReturn($"Not enough in amount to cover fee. Amount {amountToSend} - Fee: {finalFee}", "TransactionService.SendMultiSigTransactions()", false);
                }

                txBuilder
                    .Send(recipientAddress, new Money(totalAmountSpent, MoneyUnit.Satoshi))
                    .SetChange(multiSigAddress);

                var randomId = RandomStringUtility.GetRandomStringOnlyLetters(16);
                //string message = randomId;
                //byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                //var opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(messageBytes);

                //Experimental
                //txBuilder.Send(opReturnScript, Money.Zero);

                NBitcoin.Transaction unsigned = txBuilder
                    .SendFees(new Money(finalFee, MoneyUnit.Satoshi))
                    .SetOptInRBF(true)
                    .BuildTransaction(false);

                var bob = unsigned.Inputs;

                var unsignedHex = unsigned.ToHex();

                List<NBitcoin.Transaction> signedTransactionList = new List<NBitcoin.Transaction>();

                var timestamp = TimeUtil.GetTime();
                

                var sigData = new PostData.MultiSigSigningPostData
                {
                    TransactionData = unsignedHex,
                    ScriptCoinListData = coinInputs,
                    SCUID = scUID,
                    VFXAddress = vfxAccount.Address,
                    Timestamp = timestamp,
                    UniqueId = randomId,
                    Signature = ReserveBlockCore.Services.SignatureService.CreateSignature($"{vfxAccount.Address}.{timestamp}.{randomId}", vfxAccount.GetPrivKey, vfxAccount.PublicKey),
                    Amount = sendAmount
                };

                var postData = JsonConvert.SerializeObject(sigData);
                var httpContent = new StringContent(postData, Encoding.UTF8, "application/json");

                var myList = Globals.Arbiters.Where(x => x.EndOfService == null && x.StartOfService <= TimeUtil.GetTime()).ToList();
                var rnd = new Random();
                myList = myList.OrderBy(x => rnd.Next()).ToList();

                List<ReserveBlockCore.Models.Arbiter> randomArbs = myList.Take(Globals.TotalArbiterParties).ToList();

                foreach (var arbiter in randomArbs)
                {
                    using (var client = Globals.HttpClientFactory.CreateClient())
                    {
                        string url = $"http://{arbiter.IPAddress}:{Globals.ArbiterPort}/getsignedmultisig";

                        var response = await client.PostAsync(url, httpContent);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            if (string.IsNullOrEmpty(responseString))
                                return await SCLogUtility.LogAndReturn($"Failed to get a response from Arbiters - Point A.", "TransactionService.SendMultiSigTransactions()", false);

                            var responseData = JsonConvert.DeserializeObject<ResponseData.MultiSigSigningResponse>(responseString);
                            if (responseData == null)
                                return await SCLogUtility.LogAndReturn($"Failed to get a deserialize response from Arbiters - Point B.", "TransactionService.SendMultiSigTransactions()", false); ;

                            if (!responseData.Success)
                                return await SCLogUtility.LogAndReturn($"Received response, but it was not a success - Point C.", "TransactionService.SendMultiSigTransactions()", false);

                            var signedTx = NBitcoin.Transaction.Parse(responseData.SignedTransaction, Globals.BTCNetwork);

                            signedTransactionList.Add(signedTx);
                        }
                        else
                        {
                            //bad
                            var responseString = await response.Content.ReadAsStringAsync();
                            return await SCLogUtility.LogAndReturn($"Bad Status. Code; {response.StatusCode}. Response: {responseString}", "TransactionService.SendMultiSigTransactions()", false);
                        }
                    }
                }

                NBitcoin.Transaction fullySigned =
                txBuilder
                .AddCoins(coinsToSpend.ToArray())
                .CombineSignatures(signedTransactionList.ToArray());

                var txVerified = ValidateTransaction(fullySigned, txBuilder);//txBuilder.Verify(fullySigned);

                if (!txVerified.Item1)
                    return await SCLogUtility.LogAndReturn($"Transaction Failed to verify. Reason(s): {txVerified.Item2}", "TransactionService.SendMultiSigTransactions()", false);


                var hexTx = fullySigned.ToHex();
                var hashTx = fullySigned.GetHash();

                var tx = new BitcoinTransaction
                {
                    Fee = (finalFee * SatoshiMultiplier),
                    Amount = sendAmount,
                    FromAddress = sender,
                    ToAddress = recipientAddress.ToString(),
                    FeeRate = chosenFeeRate,
                    Hash = fullySigned.GetHash().ToString(),
                    Signature = hexTx,
                    Timestamp = TimeUtil.GetTime(0, 0, 0, 999),
                    TransactionType = BTCTransactionType.MultiSigSend,
                    BitcoinUTXOs = coinListBtcUTXOs,
                };

                BitcoinTransaction.SaveBitcoinTX(tx);

                Console.WriteLine($"Broadcast started @ {DateTime.Now}");

                _ = BroadcastService.BroadcastTx(fullySigned);

                Console.WriteLine($"Broadcast completed @ {DateTime.Now}");

                var wtx = await TokenizationService.CompleteTokenizedWithdrawal(vfxAccount.Address, vfxAccount, scUID, hashTx.ToString(), randomId);

                var scTx = wtx.Item1;

                var txresult = await TransactionValidatorService.VerifyTX(scTx, false, false, true);

                if (txresult.Item1 == true)
                {
                    scTx.TransactionStatus = TransactionStatus.Pending;

                    if (vfxAccount != null)
                    {
                        await WalletService.SendTransaction(scTx, vfxAccount);
                    }
                }

                return await SCLogUtility.LogAndReturn($"Transaction Success. Hash: {hashTx}", "TransactionService.SendMultiSigTransactions()", true);
            }
            catch (Exception ex)
            {
                return await SCLogUtility.LogAndReturn($"Unknown Error: {ex}", "TransactionService.SendMultiSigTransactions()", false);
            }

        }

        public static async Task<string> SendMultiSigTransactions(List<PubKey> pubKeys, decimal sendAmount, string toAddress, string changeAddress, long chosenFeeRate, string scUID, string signature, long timestamp, string vfxAddress, string uniqueId, bool isTest)
        {
            try
            {
                Script scriptPubKey = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(Globals.TotalArbiterThreshold, pubKeys.OrderBy(x => x.ScriptPubKey.ToString()).ToArray());
                Script redeemScript = scriptPubKey.PaymentScript;

                BitcoinAddress multiSigAddress = scriptPubKey.Hash.GetAddress(Globals.BTCNetwork);
                var sender = multiSigAddress.ToString();

                if (changeAddress != sender)
                    return await SCLogUtility.LogAndReturn($"Change Address did not equal sender.", "TransactionService.SendMultiSigTransactions()", false);

                BitcoinAddress recipientAddress = BitcoinAddress.Create(toAddress, Globals.BTCNetwork);

                ulong amountToSend = Convert.ToUInt32(sendAmount * BTCMultiplier);
                ulong feeEstimate = 0;
                bool sufficientInputsFound = false;
                List<Coin> unspentCoins = new List<Coin>();
                List<BitcoinUTXO> coinListBtcUTXOs = new List<BitcoinUTXO>();
                List<CoinInput> coinInputs = new List<CoinInput>();

                ulong previousTotalInputAmount = 0;

                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, multiSigAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;

                    if (!unspentCoins.Any())
                        return await SCLogUtility.LogAndReturn($"Could not find any unspent coins.", "TransactionService.SendMultiSigTransactions()", false);
                    //return (false, "Could not find any UTXOs for inputs.");

                    // Check if the coin list has changed
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

                    // Check if the total input amount is unchanged
                    if (totalInputAmount == previousTotalInputAmount)
                    {
                        // If total input amount is unchanged, no more UTXOs are available
                        return await SCLogUtility.LogAndReturn($"Failed to find enough inputs for amount. Not enough in amount to cover fee.", "TransactionService.SendMultiSigTransactions()", false);
                    }

                    previousTotalInputAmount = totalInputAmount;

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(chosenFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    if (totalInputAmount >= totalAmountRequired)
                    {
                        sufficientInputsFound = true;
                        coinListBtcUTXOs = coinList.Item2;
                        coinInputs = coinList.Item3;
                    }
                    else
                    {
                        // If inputs are not sufficient, try to get more or larger UTXOs
                        sendAmount += (decimal)feeEstimate / BTCMultiplier; // Increment the amount to cover fee in next iteration
                    }

                }

                List<ScriptCoin> coinsToSpend = new List<ScriptCoin>();
                foreach (var coin in unspentCoins)
                {
                    ScriptCoin coinToSpend = new ScriptCoin(coin, scriptPubKey);
                    coinsToSpend.Add(coinToSpend);
                }

                foreach (var input in coinInputs)
                {
                    input.ScriptPubKey = scriptPubKey.ToHex();
                }

                var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

                txBuilder.AddCoins(coinsToSpend.ToArray());

                // Get the count of inputs and outputs
                int finalInputCount = unspentCoins.Count();
                int finalOutputCount = 2; // one for recipient, one for change, one for uniqueID message

                FeeRate feeRate = new FeeRate(chosenFeeRate * 1000);

                int finalTransactionSize = FeeCalcService.EstimateTransactionSize(finalInputCount, finalOutputCount);
                ulong finalFee = feeRate.GetFee(finalTransactionSize);

                ulong totalAmountSpent = (amountToSend - finalFee);

                if (amountToSend <= finalFee)
                {
                    return await SCLogUtility.LogAndReturn($"Not enough in amount to cover fee. Amount {amountToSend} - Fee: {finalFee}", "TransactionService.SendMultiSigTransactions()", false);
                }

                txBuilder
                    .Send(recipientAddress, new Money(totalAmountSpent, MoneyUnit.Satoshi))
                    .SetChange(multiSigAddress);

                //string message = uniqueId;
                //byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                //var opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(messageBytes);

                //Experimental
                //txBuilder.Send(opReturnScript, Money.Zero);

                NBitcoin.Transaction unsigned = txBuilder
                    .SendFees(new Money(finalFee, MoneyUnit.Satoshi))
                    .SetOptInRBF(true)
                    .BuildTransaction(false);

                var bob = unsigned.Inputs;

                var unsignedHex = unsigned.ToHex();

                List<NBitcoin.Transaction> signedTransactionList = new List<NBitcoin.Transaction>();

                var sigData = new PostData.MultiSigSigningPostData
                {
                    TransactionData = unsignedHex,
                    ScriptCoinListData = coinInputs,
                    SCUID = scUID,
                    VFXAddress = vfxAddress,
                    Timestamp = timestamp,
                    UniqueId = uniqueId,
                    Signature = signature,
                    Amount = (amountToSend + finalFee)
                };

                var postData = JsonConvert.SerializeObject(sigData);
                var httpContent = new StringContent(postData, Encoding.UTF8, "application/json");

                var myList = Globals.Arbiters.Where(x => x.EndOfService == null && x.StartOfService <= TimeUtil.GetTime()).ToList();
                var rnd = new Random();
                myList = myList.OrderBy(x => rnd.Next()).ToList();

                List<ReserveBlockCore.Models.Arbiter> randomArbs = myList.Take(Globals.TotalArbiterParties).ToList();

                foreach (var arbiter in randomArbs)
                {
                    using (var client = Globals.HttpClientFactory.CreateClient())
                    {
                        string url = $"http://{arbiter.IPAddress}:{Globals.ArbiterPort}/getsignedmultisig";

                        var response = await client.PostAsync(url, httpContent);
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            if (string.IsNullOrEmpty(responseString))
                                return await SCLogUtility.LogAndReturn($"Failed to get a response from Arbiters - Point A.", "TransactionService.SendMultiSigTransactions()", false);

                            var responseData = JsonConvert.DeserializeObject<ResponseData.MultiSigSigningResponse>(responseString);
                            if (responseData == null)
                                return await SCLogUtility.LogAndReturn($"Failed to get a deserialize response from Arbiters - Point B.", "TransactionService.SendMultiSigTransactions()", false); ;

                            if (!responseData.Success)
                                return await SCLogUtility.LogAndReturn($"Received response, but it was not a success - Point C.", "TransactionService.SendMultiSigTransactions()", false);

                            var signedTx = NBitcoin.Transaction.Parse(responseData.SignedTransaction, Globals.BTCNetwork);

                            signedTransactionList.Add(signedTx);
                        }
                        else
                        {
                            //bad
                            return await SCLogUtility.LogAndReturn($"Bad Status. Code; {response.StatusCode}", "TransactionService.SendMultiSigTransactions()", false);
                        }
                    }
                }

                NBitcoin.Transaction fullySigned =
                txBuilder
                .AddCoins(coinsToSpend.ToArray())
                .CombineSignatures(signedTransactionList.ToArray());

                var txVerified = ValidateTransaction(fullySigned, txBuilder);//txBuilder.Verify(fullySigned);

                if (!txVerified.Item1)
                    return await SCLogUtility.LogAndReturn($"Transaction Failed to verify. Reason(s): {txVerified.Item2}", "TransactionService.SendMultiSigTransactions()", false);


                var hexTx = fullySigned.ToHex();
                var hashTx = fullySigned.GetHash();

                var tx = new BitcoinTransaction
                {
                    Fee = (finalFee * SatoshiMultiplier),
                    Amount = sendAmount,
                    FromAddress = sender,
                    ToAddress = recipientAddress.ToString(),
                    FeeRate = chosenFeeRate,
                    Hash = fullySigned.GetHash().ToString(),
                    Signature = hexTx,
                    Timestamp = TimeUtil.GetTime(0, 0, 0, 999),
                    TransactionType = BTCTransactionType.MultiSigSend,
                    BitcoinUTXOs = coinListBtcUTXOs,
                };

                BitcoinTransaction.SaveBitcoinTX(tx);

                Console.WriteLine($"Broadcast started @ {DateTime.Now}");

                if(!isTest)
                    _ = BroadcastService.BroadcastTx(fullySigned);

                Console.WriteLine($"Broadcast completed @ {DateTime.Now}");

                var log = await SCLogUtility.LogAndReturn($"Transaction Success. Hash: {hashTx}", "TransactionService.SendMultiSigTransactions()", true);

                return JsonConvert.SerializeObject(new { Success = false, Message = $"Transaction Success. Hash: {hashTx}", Hash = hashTx.ToString(), UniqueId = uniqueId, SmartContractUID = scUID });

                
            }
            catch (Exception ex)
            {
                return await SCLogUtility.LogAndReturn($"Unknown Error: {ex}", "TransactionService.SendMultiSigTransactions()", false);
            }

        }

        private static (List<Coin>, List<BitcoinUTXO>, List<CoinInput>) GetUnspentCoins(string btcAddress, BitcoinAddress address, decimal amountBeingSent)
        {
            var coinList = new List<Coin>();
            var spentList = new List<BitcoinUTXO>();
            var coinInputList = new List<CoinInput>();

            var utxoList = BitcoinUTXO.GetUnspetUTXOs(btcAddress);

            var utxoAmount = 0.0M;

            if (utxoList.Count != 0)
            {
                foreach (var x in utxoList)
                {
                    decimal value = x.Value * SatoshiMultiplier;
                    utxoAmount += value;

                    OutPoint outPoint = new OutPoint(uint256.Parse(x.TxId), x.Vout);
                    Coin coin = new Coin(outPoint, new TxOut(Money.Coins(value), address.ScriptPubKey));
                    coinInputList.Add(new CoinInput { Money = value, TxHash = x.TxId, Vout = x.Vout, RedeemScript = address.ScriptPubKey.ToHex() });

                    coinList.Add(coin);

                    spentList.Add(x);

                    if (utxoAmount > amountBeingSent)
                    {
                        break;
                    }
                }
            }

            return (coinList, spentList, coinInputList);
        }

        private static (bool, string) ValidateTransaction(NBitcoin.Transaction transaction, TransactionBuilder builder)
        {
            string errorResponses = "";
            if (!builder.Verify(transaction, out TransactionPolicyError[] errors))
            {
                bool first = true;
                foreach (var error in errors)
                {
                    if(first)
                        errorResponses += error.ToString();

                    errorResponses = errorResponses + ", " + error.ToString();
                }

                return (false,  errorResponses);
            }
            else
            {
                return (true, "");
            }
        }

        private static (List<Coin>, List<BitcoinUTXO>) GetUnspentCoins(string btcAddress, BitcoinAddress address, decimal amountBeingSent, List<BitcoinUTXO> prevUTXOList)
        {
            var coinList = new List<Coin>();
            var spentList = new List<BitcoinUTXO>();
            var amountGood = false;

            var utxoList = BitcoinUTXO.GetUnspetUTXOs(btcAddress);

            var utxoAmount = 0.0M;

            if (prevUTXOList.Count != 0)
            {
                foreach (var x in prevUTXOList)
                {
                    decimal value = x.Value * SatoshiMultiplier;
                    utxoAmount += value;

                    OutPoint outPoint = new OutPoint(uint256.Parse(x.TxId), x.Vout);
                    Coin coin = new Coin(outPoint, new TxOut(Money.Coins(value), address.ScriptPubKey));

                    coinList.Add(coin);

                    spentList.Add(x);

                    if (utxoAmount > amountBeingSent)
                    {
                        amountGood = true;
                        break;
                    }
                }
            }

            if (utxoList.Count != 0)
            {
                foreach (var x in utxoList)
                {
                    if (!prevUTXOList.Contains(x))
                    {

                        if (utxoAmount > amountBeingSent)
                        {
                            break;
                        }

                        decimal value = x.Value * SatoshiMultiplier;
                        utxoAmount += value;

                        OutPoint outPoint = new OutPoint(uint256.Parse(x.TxId), x.Vout);
                        Coin coin = new Coin(outPoint, new TxOut(Money.Coins(value), address.ScriptPubKey));

                        coinList.Add(coin);

                        spentList.Add(x);

                        
                    }

                }
            }

            return (coinList, spentList);
        }
    }
}
