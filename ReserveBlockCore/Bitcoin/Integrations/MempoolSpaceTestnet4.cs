using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using System.Linq;

namespace ReserveBlockCore.Bitcoin.Integrations
{
    public class MempoolSpaceTestnet4
    {
        public static async Task GetAddressBalance(string address, string vfxAddress, bool isTokenAddress = false)
        {
            var baseUri = GetBaseURL();
            var uri = $"{baseUri}/address/{address}";

            try
            {
                using (var client = Globals.HttpClientFactory.CreateClient())
                {
                    var httpResponse = await client.GetAsync(uri);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var responseContent = await httpResponse.Content.ReadAsStringAsync();
                        if (responseContent != null)
                        {
                            Root? response = JsonConvert.DeserializeObject<Root>(responseContent);
                            if (response != null)
                            {
                                var returnedAddress = response.address;
                                var btcAccount = BitcoinAccount.GetBitcoin()?.FindOne(x => x.Address == returnedAddress);
                                if (btcAccount != null)
                                {
                                    btcAccount.Balance = (response.chain_stats.funded_txo_sum - response.chain_stats.spent_txo_sum) / 100_000_000M;
                                    BitcoinAccount.GetBitcoin()?.UpdateSafe(btcAccount);
                                }
                                if (isTokenAddress)
                                {
                                    var balance = (response.chain_stats.funded_txo_sum - response.chain_stats.spent_txo_sum) / 100_000_000M;
                                    await TokenizedBitcoin.UpdateBalance(returnedAddress, balance, vfxAddress);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public static async Task GetAddressUTXO(string address)
        {
            var baseUri = GetBaseURL();
            var uri = $"{baseUri}/address/{address}/utxo";

            try
            {
                using (var client = Globals.HttpClientFactory.CreateClient())
                {
                    var httpResponse = await client.GetAsync(uri);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var responseContent = await httpResponse.Content.ReadAsStringAsync();
                        if (responseContent != null)
                        {
                            List<Transaction>? transactions = JsonConvert.DeserializeObject<List<Transaction>>(responseContent);
                            if (transactions?.Count > 0)
                            {
                                var walletUtxoList = BitcoinUTXO.GetUTXOs(address);
                                if(walletUtxoList != null)
                                {
                                    var utxoList = transactions;
                                    if(utxoList?.Count > 0)
                                    {
                                        foreach (var item in utxoList)
                                        {
                                            var nUTXO = new BitcoinUTXO { 
                                                Address = address,
                                                IsUsed = false,
                                                TxId = item.txid,
                                                Value = item.value,
                                                Vout = item.vout
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
                                                TxId = item.txid,
                                                Value = item.value,
                                                Vout = item.vout
                                            };

                                            BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                                        }
                                    }
                                }
                                
                                //TODO:perform audit and update values as needed.
                                //Remove them from DB saves.
                                //Push them into memory
                                //Perform audit after every tx send
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }
        public class Status
        {
            public bool confirmed { get; set; }
            public int block_height { get; set; }
            public string block_hash { get; set; }
            public long block_time { get; set; }
        }

        public class Transaction
        {
            public string txid { get; set; }
            public int vout { get; set; }
            public Status status { get; set; }
            public long value { get; set; }
        }
        public static string GetBaseURL()
        {
            if (Globals.BTCNetwork == NBitcoin.Network.Main)
                return "https://mempool.space/api";

            if(Globals.BTCNetwork == NBitcoin.Network.TestNet4)
                return "https://mempool.space/testnet4/api";

            return "https://mempool.space/testnet/api";
        }
        public class ChainStats
        {
            public int funded_txo_count { get; set; }
            public int funded_txo_sum { get; set; }
            public int spent_txo_count { get; set; }
            public int spent_txo_sum { get; set; }
            public int tx_count { get; set; }
        }

        public class MempoolStats
        {
            public int funded_txo_count { get; set; }
            public int funded_txo_sum { get; set; }
            public int spent_txo_count { get; set; }
            public int spent_txo_sum { get; set; }
            public int tx_count { get; set; }
        }

        public class Root
        {
            public string address { get; set; }
            public ChainStats chain_stats { get; set; }
            public MempoolStats mempool_stats { get; set; }
        }
    }
}
