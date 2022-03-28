using ReserveBlockCore.Data;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Utilities
{
    public class BlockRollbackUtility
    {
        public static async Task<bool> RollbackBlocks(int numBlocksRollback)
        {
            Program.IsResyncing = true;
            Program.StopAllTimers = true;
            var height = BlockchainData.GetHeight();
            var newHeight = height - (long)numBlocksRollback;

            var blocks = Block.GetBlocks();
            blocks.DeleteMany(x => x.Height > newHeight);
            DbContext.DB.Checkpoint();

            var result = await ResetTreis();

            Program.IsResyncing = false;
            Program.StopAllTimers = false;

            return result;
        }

        public static async Task<bool> ResetTreis()
        {
            var blockChain = BlockchainData.GetBlocks().FindAll();
            var failCount = 0;
            List<Block> failBlocks = new List<Block>();

            var transactions = TransactionData.GetAll();
            var stateTrei = StateData.GetAccountStateTrei();
            var worldTrei = WorldTrei.GetWorldTrei();

            transactions.DeleteAll();//delete all local transactions
            stateTrei.DeleteAll(); //removes all state trei data
            worldTrei.DeleteAll();  //removes the state trei

            DbContext.DB.Checkpoint();
            DbContext.DB_AccountStateTrei.Checkpoint();
            DbContext.DB_WorldStateTrei.Checkpoint();

            var accounts = AccountData.GetAccounts();
            var accountList = accounts.FindAll().ToList();
            if (accountList.Count() > 0)
            {
                foreach (var account in accountList)
                {
                    account.Balance = 0M;
                    accounts.Update(account);//updating local record with synced state trei
                }
            }

            foreach (var block in blockChain)
            {
                var result = await BlockchainRescanUtility.ValidateBlock(block, true);
                if (result != false)
                {
                    StateData.UpdateTreis(block);

                    foreach (Transaction transaction in block.Transactions)
                    {
                        var mempool = TransactionData.GetPool();

                        var mempoolTx = mempool.FindAll().Where(x => x.Hash == transaction.Hash).FirstOrDefault();
                        if (mempoolTx != null)
                        {
                            mempool.DeleteMany(x => x.Hash == transaction.Hash);
                        }

                        var account = AccountData.GetAccounts().FindAll().Where(x => x.Address == transaction.ToAddress).FirstOrDefault();
                        if (account != null)
                        {
                            AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                            var txdata = TransactionData.GetAll();
                            txdata.Insert(transaction);
                        }

                        //Adds sent TX to wallet
                        var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == transaction.FromAddress);
                        if (fromAccount != null)
                        {
                            var txData = TransactionData.GetAll();
                            var fromTx = transaction;
                            fromTx.Amount = transaction.Amount * -1M;
                            fromTx.Fee = transaction.Fee * -1M;
                            txData.Insert(fromTx);
                            AccountData.UpdateLocalBalance(fromAccount.Address, (transaction.Amount + transaction.Fee));
                        }
                    }
                }
                else
                {
                    //issue with chain and must redownload
                    failBlocks.Add(block);
                    failCount++;
                }
            }

            if (failCount == 0)
            {
                return true;
            }
            else
            {
                //chain is invalid. Delete and redownload
                return false;
            }
        }
    }
}
