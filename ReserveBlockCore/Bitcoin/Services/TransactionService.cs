using ImageMagick;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Reflection;
using static ReserveBlockCore.Models.Integrations;

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

                List<Coin> previousUnspentCoins = null;
                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, senderAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;
                    // Check if the coin list has changed
                    if (previousUnspentCoins != null && unspentCoins.SequenceEqual(previousUnspentCoins))
                    {
                        // If coin list is unchanged, no more UTXOs are available
                        return (false, "Not enough UTXOs to cover the amount and fees.");
                    }

                    previousUnspentCoins = new List<Coin>(unspentCoins);

                    if (!unspentCoins.Any())
                        return (false, "Could not find any UTXOs for inputs.");

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(chosenFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    // Check if total UTXO value is enough to cover the amount and the fee
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

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

                var txVerified = txBuilder.Verify(signedTransaction);

                if (txVerified)
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
                        Timestamp = TimeUtil.GetTime(),
                        BTCTx = signedTransaction,
                        TransactionType = BTCTransactionType.Send,
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

                List<Coin> previousUnspentCoins = null;
                while (!sufficientInputsFound)
                {
                    var coinList = GetUnspentCoins(sender, senderAddress, sendAmount + feeEstimate);
                    unspentCoins = coinList.Item1;
                    // Check if the coin list has changed
                    if (previousUnspentCoins != null && unspentCoins.SequenceEqual(previousUnspentCoins))
                    {
                        // If coin list is unchanged, no more UTXOs are available
                        return (false, "Not enough UTXOs to cover the amount and fees.");
                    }

                    previousUnspentCoins = new List<Coin>(unspentCoins);

                    if (!unspentCoins.Any())
                        return (false, "Could not find any UTXOs for inputs.");

                    int inputCount = unspentCoins.Count();
                    int outputCount = 2; // one for recipient, one for change

                    FeeRate feeRateCalc = new FeeRate(chosenFeeRate * 1000);

                    // Estimate the transaction size
                    int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount);

                    // Calculate the fee (in satoshis)
                    feeEstimate = feeRateCalc.GetFee(transactionSize);

                    ulong totalAmountRequired = amountToSend + feeEstimate;

                    // Check if total UTXO value is enough to cover the amount and the fee
                    ulong totalInputAmount = (ulong)unspentCoins.Sum(x => x.Amount.Satoshi);

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

                // Get the unspent output(s) (UTXOs) associated with the sender's address
                var coinList = GetUnspentCoins(transaction.FromAddress, senderAddress, transaction.Amount, transaction.BitcoinUTXOs);
                List<Coin> unspentCoins = coinList.Item1;

                if (!unspentCoins.Any())
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find any UTXOs for inputs." });

                var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

                unspentCoins.ForEach(x =>
                {
                    txBuilder.AddCoin(x);
                });

                txBuilder
                    .Send(recipientAddress, new Money(amountToSend, MoneyUnit.Satoshi))
                    .SetChange(senderAddress);

                Console.WriteLine($"TX builder Done.");

                // Get the count of inputs and outputs
                int inputCount = unspentCoins.Count();
                int outputCount = 2; // one for recipient, one for change

                FeeRate feeRate = new FeeRate(nFeeRate * 1000);

                int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount); // 1 input, 2 outputs

                // Calculate the fee (in satoshis)
                ulong fee = feeRate.GetFee(transactionSize);

                decimal totalAmountSpent = (amountToSend + fee) * SatoshiMultiplier;
                decimal originalAmountSpent = (transaction.Amount + transaction.Fee);

                byte[] privateKeyBytes = senderPrivateKeyHex.HexToByteArray();
                Key senderKey = new Key(privateKeyBytes);

                Console.WriteLine($"Fees calculated...");

                var signedTransaction = txBuilder
                    .AddKeys(senderKey)
                    .SendFees(new Money(fee, MoneyUnit.Satoshi))
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

                    if (coinList.Item2.Any())
                    {
                        coinList.Item2.ForEach(x =>
                        {
                            BitcoinUTXO.SpendUTXO(x.TxId, x.Address);
                        });
                    }

                    var hexTx = signedTransaction.ToHex();

                    var tx = new BitcoinTransaction
                    {
                        Fee = (fee * SatoshiMultiplier),
                        Amount = transaction.Amount,
                        FromAddress = transaction.FromAddress,
                        ToAddress = transaction.ToAddress,
                        FeeRate = nFeeRate,
                        Hash = signedTransaction.GetHash().ToString(),
                        Signature = hexTx,
                        Timestamp = TimeUtil.GetTime(),
                        TransactionType = BTCTransactionType.Send,
                        BitcoinUTXOs = coinList.Item2,
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

        private static (List<Coin>, List<BitcoinUTXO>) GetUnspentCoins(string btcAddress, BitcoinAddress address, decimal amountBeingSent)
        {
            var coinList = new List<Coin>();
            var spentList = new List<BitcoinUTXO>();

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

                    coinList.Add(coin);

                    spentList.Add(x);

                    if (utxoAmount > amountBeingSent)
                    {
                        break;
                    }
                }
            }

            return (coinList, spentList);
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
