using NBitcoin;
using NBitcoin.Protocol;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using static ReserveBlockCore.Models.Integrations;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class TransactionService
    {
        public static decimal BTCMultiplier = 100_000_000M;
        public static decimal SatoshiMultiplier = 0.00000001M;
        public static async Task<(bool, string)> SendTransaction(string sender, string receiver, decimal sendAmount, long chosenFeeRate, bool overrideInternalSend = false)
        {

            receiver = receiver.ToBTCAddressNormalize();

            var btcAccount = BitcoinAccount.GetBitcoinAccount(sender);
            var receiverAccount = BitcoinAccount.GetBitcoinAccount(receiver);
            if (btcAccount == null)
                return (false, $"Could not find a bitcoin account for the following address: {sender}");

            if(btcAccount.Balance <= sendAmount)
                return (false, $"Insufficient Balance: {btcAccount.Balance}");

            if(receiverAccount != null && !overrideInternalSend)
                return (false, $"This is an internal send. Please use the override if you wish to do this.");

            if(sendAmount < Globals.BTCMinimumAmount)
                return (false, $"This wallet does not support sends smaller than {Globals.BTCMinimumAmount} BTC.");

            Console.WriteLine($"Account Checks Passed.");

            string senderPrivateKeyHex = btcAccount.PrivateKey;

            BitcoinAddress senderAddress = BitcoinAddress.Create(sender, Globals.BTCNetwork);
            BitcoinAddress recipientAddress = BitcoinAddress.Create(receiver, Globals.BTCNetwork);

            ulong amountToSend = Convert.ToUInt32(sendAmount * BTCMultiplier);

            // Get the unspent output(s) (UTXOs) associated with the sender's address
            var coinList = GetUnspentCoins(sender, senderAddress, sendAmount);
            List<Coin> unspentCoins = coinList.Item1;

            if(!unspentCoins.Any())
                return (false, $"Could not find any UTXOs for inputs.");

            var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

            unspentCoins.ForEach(x => {
                txBuilder.AddCoin(x);
            });

            txBuilder
                .Send(recipientAddress, new Money(amountToSend, MoneyUnit.Satoshi))
                .SetChange(senderAddress);

            Console.WriteLine($"TX builder Done.");

            // Get the count of inputs and outputs
            int inputCount = unspentCoins.Count();
            int outputCount = 2; // one for recipient, one for change

            FeeRate feeRate = new FeeRate(chosenFeeRate * 1000);

            int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount); // 1 input, 2 outputs

            // Calculate the fee (in satoshis)
            ulong fee = feeRate.GetFee(transactionSize);

            decimal totalAmountSpent = (amountToSend + fee) * SatoshiMultiplier;

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

            if(txVerified)
            {
                Console.WriteLine($"Tx Has been verified.");
                btcAccount.Balance -= totalAmountSpent;
                var btcDb = BitcoinAccount.GetBitcoin();
                if(btcDb != null)
                    btcDb.UpdateSafe(btcAccount);

                if (coinList.Item2.Any())
                {
                    coinList.Item2.ForEach(x => {
                        BitcoinUTXO.SpendUTXO(x.TxId, x.Address);
                    });
                }

                var hexTx = signedTransaction.ToHex();
                //return (true, $"{hexTx}");

                var tx = new BitcoinTransaction {
                    Fee = (fee * SatoshiMultiplier),
                    Amount = sendAmount,
                    FromAddress = sender,
                    ToAddress = receiver,
                    FeeRate = chosenFeeRate,
                    Hash = signedTransaction.GetHash().ToString(),
                    Signature = hexTx,
                    Timestamp = TimeUtil.GetTime(),
                    TransactionType = BTCTransactionType.Send
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
            }
            
            return (false, $"Unknown Error");
        }

        public static async Task<(bool, string)> GetTransactionFee(string sender, string receiver, decimal sendAmount, long chosenFeeRate)
        {
            var btcAccount = BitcoinAccount.GetBitcoinAccount(sender);
            var receiverAccount = BitcoinAccount.GetBitcoinAccount(receiver);
            if (btcAccount == null)
                return (false, $"Could not find a bitcoin account for the following address: {sender}");

            if (btcAccount.Balance <= sendAmount)
                return (false, $"Insufficient Balance: {btcAccount.Balance}");

            string senderPrivateKeyHex = btcAccount.PrivateKey;

            BitcoinAddress senderAddress = BitcoinAddress.Create(sender, Globals.BTCNetwork);
            BitcoinAddress recipientAddress = BitcoinAddress.Create(receiver, Globals.BTCNetwork);

            ulong amountToSend = Convert.ToUInt32(sendAmount * BTCMultiplier);

            // Get the unspent output(s) (UTXOs) associated with the sender's address
            var coinList = GetUnspentCoins(sender, senderAddress, sendAmount);
            List<Coin> unspentCoins = coinList.Item1;

            if (!unspentCoins.Any())
                return (false, $"Could not find any UTXOs for inputs.");

            var txBuilder = Globals.BTCNetwork.CreateTransactionBuilder();

            unspentCoins.ForEach(x => {
                txBuilder.AddCoin(x);
            });

            txBuilder
                .Send(recipientAddress, new Money(amountToSend, MoneyUnit.Satoshi))
                .SetChange(senderAddress);

            // Get the count of inputs and outputs
            int inputCount = unspentCoins.Count();
            int outputCount = 2; // one for recipient, one for change

            FeeRate feeRate = new FeeRate(chosenFeeRate * 1000);

            int transactionSize = FeeCalcService.EstimateTransactionSize(inputCount, outputCount); // 1 input, 2 outputs

            // Calculate the fee (in satoshis)
            ulong fee = feeRate.GetFee(transactionSize);           

            return (true, $"{fee}");
        }

        private static (List<Coin>, List<BitcoinUTXO>) GetUnspentCoins(string btcAddress, BitcoinAddress address, decimal amountBeingSent)
        {
            var coinList = new List<Coin>();
            var spentList = new List<BitcoinUTXO>();
            
            var utxoList = BitcoinUTXO.GetUnspetUTXOs(btcAddress);

            var utxoAmount = 0.0M;

            if(utxoList.Count != 0)
            {
                foreach (var x in utxoList)
                {
                    decimal value = x.Value * SatoshiMultiplier;
                    utxoAmount += value;

                    OutPoint outPoint = new OutPoint(uint256.Parse(x.TxId), x.Vout);
                    Coin coin = new Coin(outPoint, new TxOut(Money.Coins(value), address.ScriptPubKey));

                    coinList.Add(coin);

                    spentList.Add(x);

                    if (utxoAmount >= amountBeingSent)
                    {
                        break;
                    }
                }
            }

            return (coinList, spentList);
        }

    }
}
