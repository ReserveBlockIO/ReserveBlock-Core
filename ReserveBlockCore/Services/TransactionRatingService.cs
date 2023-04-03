using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class TransactionRatingService
    {
        public static async Task<TransactionRating> GetTransactionRating(Transaction tx, bool dstShopTx = false)
        {
            TransactionRating rating = TransactionRating.F;//start at F in the event something is received we don't expected
            try
            {
                if (tx.TransactionType == TransactionType.TX)
                {
                    rating = await TXRating(tx);
                }
                if (tx.TransactionType == TransactionType.ADNR)
                {
                    rating = await ADNRRating(tx);
                }
                if (tx.TransactionType == TransactionType.DSTR)
                {
                    rating = await DecShopRating(tx);
                }
                if (tx.TransactionType == TransactionType.NFT_MINT ||
                    tx.TransactionType == TransactionType.NFT_SALE ||
                    tx.TransactionType == TransactionType.NFT_BURN ||
                    tx.TransactionType == TransactionType.NFT_TX)
                {
                    rating = await NFTRating(tx);
                }
                if(tx.TransactionType == TransactionType.VOTE_TOPIC)
                {
                    rating = await VoteTopicRating(tx);
                }
                if (tx.TransactionType == TransactionType.VOTE)
                {
                    rating = await VoteRating(tx);
                }

                return rating;
            }
            catch(Exception ex)
            {
                //something failed. should not happen unless TX is malformed or malicious, in which case it gets an F and won't be processed.
                ErrorLogUtility.LogError($"Error rating transaction. TXId = {tx.Hash}. Error Message: {ex.ToString()}", "TransactionRatingService.GetTransactionRating(Transaction tx)");
                return rating;
            }
        }

        private static async Task<TransactionRating> NFTRating(Transaction tx)
        {
            TransactionRating rating = TransactionRating.A;
            var mempool = TransactionData.GetMempool();
            var pool = TransactionData.GetPool();

            if (mempool != null)
            {
                if (mempool.Count() > 25)
                {
                    var txs = mempool.FindAll(x => x.FromAddress == tx.FromAddress &&
                    (x.TransactionType == TransactionType.NFT_MINT ||
                        x.TransactionType == TransactionType.NFT_SALE ||
                        x.TransactionType == TransactionType.NFT_BURN ||
                        x.TransactionType == TransactionType.NFT_TX));
                    if (txs.Count() > 25)
                    {
                        rating = TransactionRating.F; // Fail. Too many tx's being broadcasted from that address. 
                        txs.ForEach(x =>
                        {
                            x.TransactionRating = TransactionRating.E; // current TXs in mempool have been lowered to protect against spammers
                        });

                        pool.UpdateSafe(txs);
                    }
                    else
                    {
                        rating = TransactionRating.A;
                    }

                }
                else
                {
                    rating = TransactionRating.A;
                }
            }

            return rating;
        }


        private static async Task<TransactionRating> ADNRRating(Transaction tx)
        {
            TransactionRating rating = TransactionRating.A;
            var mempool = TransactionData.GetMempool();
            var pool = TransactionData.GetPool();
            if (mempool != null)
            {
                if (mempool.Count() > 0)
                {
                    var txs = mempool.FindAll(x => x.FromAddress == tx.FromAddress && x.TransactionType == TransactionType.ADNR);
                    if (txs.Count() > 0)
                    {
                        rating = TransactionRating.F; // Fail. you can only have 1 ADNR per address 
                    }
                    else
                    {
                        rating = TransactionRating.A;
                    }
                }
                else
                {
                    rating = TransactionRating.A;
                }
            }

            return rating;
        }

        private static async Task<TransactionRating> VoteRating(Transaction tx)
        {
            TransactionRating rating = TransactionRating.A;
            var mempool = TransactionData.GetMempool();
            var pool = TransactionData.GetPool();

            if (mempool != null)
            {
                if (mempool.Count() >= 2)
                {
                    var txs = mempool.FindAll(x => x.FromAddress == tx.FromAddress &&
                    (x.TransactionType == TransactionType.VOTE ||
                        x.TransactionType == TransactionType.VOTE_TOPIC));

                    if (txs.Count() > 1)
                    {
                        rating = TransactionRating.F; // Fail. Too many tx's being broadcasted from that address. 
                        txs.ForEach(x =>
                        {
                            x.TransactionRating = rating;
                        });

                        pool.UpdateSafe(txs);
                    }
                    else
                    {
                        rating = TransactionRating.A;
                    }

                }
                else
                {
                    rating = TransactionRating.A;
                }
            }

            return rating;
        }

        private static async Task<TransactionRating> VoteTopicRating(Transaction tx)
        {
            TransactionRating rating = TransactionRating.A;
            var mempool = TransactionData.GetMempool();
            var pool = TransactionData.GetPool();

            if (mempool != null)
            {
                if (mempool.Count() >= 2)
                {
                    var txs = mempool.FindAll(x => x.FromAddress == tx.FromAddress &&
                    (x.TransactionType == TransactionType.VOTE ||
                        x.TransactionType == TransactionType.VOTE_TOPIC));

                    if (txs.Count() > 1)
                    {
                        rating = TransactionRating.F; // Fail. Too many tx's being broadcasted from that address. 
                        txs.ForEach(x =>
                        {
                            x.TransactionRating = rating; 
                        });

                        pool.UpdateSafe(txs);
                    }
                    else
                    {
                        rating = TransactionRating.A;
                    }

                }
                else
                {
                    rating = TransactionRating.A;
                }
            }

            return rating;
        }
        private static async Task<TransactionRating> TXRating(Transaction tx)
        {
            TransactionRating rating = TransactionRating.A;
            var mempool = TransactionData.GetMempool();
            var pool = TransactionData.GetPool();

            if (tx.Amount >= 0.1M)
            {
                rating = TransactionRating.A;
            }

            if (tx.Amount < 0.1M && tx.Amount >= 0.01M)
            {
                rating = TransactionRating.B;
            }

            if (tx.Amount < 0.01M && tx.Amount >= 0.001M)
            {
                rating = TransactionRating.C;
            }
            if (tx.Amount < 0.001M)
            {
                rating = TransactionRating.D; //low priority tx
            }
            if (mempool != null)
            {
                if (mempool.Count() > 50)
                {
                    var txs = mempool.FindAll(x => x.FromAddress == tx.FromAddress && x.TransactionType == TransactionType.TX);
                    if (txs.Count() > 50)
                    {
                        rating = TransactionRating.F; // Fail. Too many tx's being broadcasted from that address. 
                        txs.ForEach(x =>
                        {
                            x.TransactionRating = TransactionRating.E; // current TXs in mempool have been lowered to protect against spammers
                        });

                        pool.UpdateSafe(txs);
                    }
                }
            }

            return rating;
        }
    }
}
