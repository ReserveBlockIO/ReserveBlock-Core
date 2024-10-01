using NBitcoin;
using ReserveBlockCore.Bitcoin.ElectrumX;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class AddressSyncService
    {
        public static async Task SyncAddress(string address)
        {
            bool electrumServerFound = false;
            Client client = null;
            while (!electrumServerFound)
            {
                var electrumServer = Globals.ClientSettings.Where(x => x.FailCount < 10).OrderBy(x => x.Count).FirstOrDefault();
                if (electrumServer != null)
                {
                    try
                    {
                        client = new Client(electrumServer.Host, electrumServer.Port, true);
                        var serverVersion = await client.GetServerVersion();

                        if (serverVersion == null)
                            throw new Exception("Bad server response or no connection.");

                        if (serverVersion.ProtocolVersion.Major != 1 && serverVersion.ProtocolVersion.Minor < 4)
                            throw new Exception("Bad version.");

                        electrumServerFound = true;
                        electrumServer.Count++;
                    }
                    catch (Exception ex)
                    {
                        //TODO: ADD LOGS
                        electrumServer.FailCount++;
                        electrumServer.Count++;
                        await Task.Delay(1000);
                    }

                }
                else
                {
                    //no servers found
                    return;
                }
                //TODO: ADD LOGS
                await Task.Delay(1000);

                if(client != null)
                {
                    await GetBalance(client, address);
                    await GetTxHistory(client, address);
                    await Getinputs(client, address);
                }
            }
        }
        private static async Task GetBalance(Client client, string address)
        {
            var balance = await client.GetBalance(address, false);
            var btcAccount = BitcoinAccount.GetBitcoin()?.FindOne(x => x.Address == address);
            if (btcAccount != null)
            {
                btcAccount.Balance = balance.Confirmed / 100_000_000M;
                BitcoinAccount.GetBitcoin()?.UpdateSafe(btcAccount);
            }
        }

        public static async Task GetTxHistory(Client client, string address)
        {
            var history = await client.GetHistory(address);
            if (history != null)
            {
                foreach (var bTx in history)
                {
                    if (bTx.Height > 0)
                    {
                        var blockHeader = await client.GetBlockHeaderHex(bTx.Height);
                        if (blockHeader != null)
                        {
                            var timestampHex = blockHeader.Hex.Substring(136, 8);

                            // Reverse the byte order (little-endian to big-endian)
                            var reversedTimestampHex = string.Join("", Enumerable.Range(0, 4).Select(i => timestampHex.Substring(i * 2, 2)).Reverse());

                            // Convert the reversed hex string to Unix timestamp
                            var timestampUnix = TimeUtil.GetTime(reversedTimestampHex);
                            var rawTx = await client.GetRawTx(bTx.TxHash);
                            var tx = Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                            var bitcoinAddress = BitcoinAddress.Create(address, Globals.BTCNetwork);
                            List<BitcoinAddress> outgoingAddrs = new List<BitcoinAddress>();
                            foreach (var input in tx.Inputs)
                            {
                                var result = await InputUtility.GetAddressFromInput(client, input);
                                if (result != null)
                                    outgoingAddrs.Add(result);
                            }

                            bool isOutgoing = outgoingAddrs.Any(inputAddress => inputAddress == bitcoinAddress);

                            var fromAddress = "";
                            var toAddress = "";
                            var amount = 0.0M;
                            foreach (var output in tx.Outputs)
                            {
                                var outputAddress = output.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);


                                // Heuristic: if the address is not the sender's address, it's likely a recipient
                                if (outputAddress == bitcoinAddress && isOutgoing)
                                {
                                    fromAddress = address;
                                    toAddress = outputAddress.ToString();
                                }
                                if (outputAddress != bitcoinAddress && isOutgoing)
                                {
                                    amount = output.Value.ToUnit(MoneyUnit.BTC);
                                }

                                if (outputAddress != bitcoinAddress && !isOutgoing)
                                {
                                    fromAddress = outputAddress.ToString();
                                    toAddress = address;
                                }
                                if (outputAddress == bitcoinAddress && !isOutgoing)
                                {
                                    amount = output.Value.ToUnit(MoneyUnit.BTC);
                                }
                            }

                            var totalInputAmount = await InputUtility.CalculateTotalInputAmount(client, tx);
                            var totalOutputAmount = tx.Outputs.Sum(o => o.Value);
                            var fee = totalInputAmount - totalOutputAmount;

                            var nTx = new BitcoinTransaction
                            {
                                Amount = amount,
                                BitcoinUTXOs = new List<BitcoinUTXO>(),
                                Fee = fee.ToUnit(MoneyUnit.BTC),
                                FeeRate = 0,
                                FromAddress = fromAddress,
                                ToAddress = toAddress,
                                Hash = bTx.TxHash,
                                IsConfirmed = true,
                                ConfirmedHeight = bTx.Height,
                                Signature = rawTx.RawTx.ToString(),
                                Timestamp = timestampUnix,
                                TransactionType = isOutgoing ? BTCTransactionType.Send : BTCTransactionType.Receive,
                            };

                            BitcoinTransaction.SaveBitcoinTX(nTx);
                        }
                    }
                }
            }
        }

        private static async Task Getinputs(Client client, string address)
        {
            var transactions = await client.GetListUnspent(address, false);
            if (transactions?.Count > 0)
            {
                var walletUtxoList = BitcoinUTXO.GetUTXOs(address);
                if (walletUtxoList != null)
                {
                    var utxoList = transactions;
                    if (utxoList?.Count > 0)
                    {
                        foreach (var item in utxoList)
                        {
                            var nUTXO = new BitcoinUTXO
                            {
                                Address = address,
                                IsUsed = false,
                                TxId = item.TxHash,
                                Value = (long)item.Value,
                                Vout = (int)item.TxPos
                            };

                            BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                        }
                    }
                }
                else
                {
                    if (transactions?.Count > 0)
                    {
                        foreach (var item in transactions)
                        {
                            var nUTXO = new BitcoinUTXO
                            {
                                Address = address,
                                IsUsed = false,
                                TxId = item.TxHash,
                                Value = (long)item.Value,
                                Vout = (int)item.TxPos
                            };

                            BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                        }
                    }
                }
            }
        }
    }
}
