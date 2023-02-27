using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models.DST
{
    public class Store
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool StoreLive { get; set; }
        public bool IsDefault { get; set; }

        #region Get Store Db
        public static LiteDB.ILiteCollection<Store>? GetStoreDb()
        {
            try
            {
                var storeDb = DbContext.DB_DST.GetCollection<Store>(DbContext.RSRV_STORE);
                return storeDb;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Store.GetStoreDb()");
                return null;
            }

        }

        #endregion

        #region Get Default Store
        public static Store? GetDefaultStore()
        {
            var storeDb = GetStoreDb();

            if (storeDb != null)
            {
                var defStore = storeDb.Query().Where(x => x.IsDefault).FirstOrDefault();
                if (defStore == null)
                {
                    return null;
                }

                return defStore;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Stores
        public static IEnumerable<Store>? GetAllStores()
        {
            var storeDb = GetStoreDb();

            if (storeDb != null)
            {
                var stores = storeDb.Query().Where(x => true).ToEnumerable();
                if (stores.Count() == 0)
                {
                    return null;
                }

                return stores;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get Single Store
        public static Store? GetSingleStore(int storeId)
        {
            var storeDb = GetStoreDb();

            if (storeDb != null)
            {
                var store = storeDb.Query().Where(x => x.Id == storeId).FirstOrDefault();
                if (store == null)
                {
                    return null;
                }

                return store;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Update/Set Default Store
        public static (bool, string) ChangeDefaultStore(int storeId)
        {
            var storeDb = GetStoreDb();
            var defaultStore = GetDefaultStore();
            if(storeDb != null)
            {
                if (defaultStore != null)
                {
                    defaultStore.IsDefault = false;
                    
                    var singleStore = GetSingleStore(storeId);
                    if (singleStore != null)
                    {
                        singleStore.IsDefault = true;
                        storeDb.UpdateSafe(singleStore);
                        storeDb.UpdateSafe(defaultStore);
                        return (true, $"Store: {defaultStore.Name} is no longer the default and {singleStore.Name} is the new default.");
                    }
                    else
                    {
                        return (false, $"Could not find the store associated with that Id.");
                    }

                }
                else
                {
                    var singleStore = GetSingleStore(storeId);
                    if(singleStore != null)
                    {
                        singleStore.IsDefault = true;
                        storeDb.UpdateSafe(singleStore);
                    }
                    else
                    {
                        return (false, $"Could not find the store associated with that Id.");
                    }
                }
            }
            return (false, $"Failed to update default store.");
        }
        #endregion

        #region Save Store
        public static async Task<(bool, string)> SaveStore(Store store)
        {
            var singleStore = GetSingleStore(store.Id);
            var storeDb = GetStoreDb();
            var defaultStore = GetDefaultStore();
            if (singleStore == null)
            {
                if (storeDb != null)
                {
                    storeDb.InsertSafe(store);
                    return (true, "Store saved.");
                }
            }
            else
            {
                if (storeDb != null)
                {
                    storeDb.UpdateSafe(store);
                    return (true, "Store updated.");
                }
            }
            return (false, "Store DB was null.");
        }

        #endregion

        #region Delete Store
        public static async  Task<(bool, string)> DeleteStore(int storeId)
        {
            var singleStore = GetSingleStore(storeId);
            if (singleStore != null)
            {
                var storeDb = GetStoreDb();
                if (storeDb != null)
                {
                    if(singleStore.IsDefault)
                    {
                        return (false, "Cannot delete the default store. Please switch default to another store and try to delete again.");
                    }
                    storeDb.DeleteSafe(storeId);
                    return (true, "Store deleted.");
                }
                else
                {
                    return (false, "Store DB was null.");
                }
            }
            return (false, "Store was not present.");

        }

        #endregion
    }
}
