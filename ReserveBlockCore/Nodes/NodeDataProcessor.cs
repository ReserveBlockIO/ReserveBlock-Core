using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Nodes
{
    public class NodeDataProcessor
    {
        public static async Task ProcessData(string message, string data)
        {
            if (message == null || message == "")
            {
                return;
            }
            else
            {
                if(message == "tx")
                {
                    var transaction = JsonConvert.DeserializeObject<Transaction>(data);
                    if(transaction != null)
                    {
                        var mempool = TransactionData.GetPool();
                        if (mempool.Count() != 0)
                        {
                            var txFound = mempool.FindOne(x => x.Hash == transaction.Hash);
                            if (txFound == null)
                            {
                                var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);
                                
                                var txResult = TransactionValidatorService.VerifyTX(transaction); //sends tx to connected peers
                                if (txResult == true && dblspndChk == false)
                                {
                                    mempool.Insert(transaction);

                                }
                            }

                        }
                        else
                        {
                            var dblspndChk = await TransactionData.DoubleSpendCheck(transaction);

                            var txResult = TransactionValidatorService.VerifyTX(transaction);
                            if (txResult == true && dblspndChk == false)
                            {
                                mempool.Insert(transaction);
                            }
                        }
                    }
                    
                }
            }
        }
    }
}
