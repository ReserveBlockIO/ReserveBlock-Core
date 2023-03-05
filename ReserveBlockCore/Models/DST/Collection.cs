using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.Text.RegularExpressions;

namespace ReserveBlockCore.Models.DST
{
    public class Collection
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool CollectionLive { get; set; }
        public bool IsDefault { get; set; }

        #region Get Collection Db
        public static LiteDB.ILiteCollection<Collection>? GetCollectionDb()
        {
            try
            {
                var collectionDb = DbContext.DB_DST.GetCollection<Collection>(DbContext.RSRV_COLLECTION);
                return collectionDb;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Collection.GetCollectionDb()");
                return null;
            }

        }

        #endregion

        #region Get Default Collection
        public static Collection? GetDefaultCollection()
        {
            var collectionDb = GetCollectionDb();

            if (collectionDb != null)
            {
                var defCollection = collectionDb.Query().Where(x => x.IsDefault).FirstOrDefault();
                if (defCollection == null)
                {
                    return null;
                }

                return defCollection;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Collection
        public static IEnumerable<Collection>? GetAllCollections()
        {
            var collectionDb = GetCollectionDb();

            if (collectionDb != null)
            {
                var collections = collectionDb.Query().Where(x => true).ToEnumerable();
                if (collections.Count() == 0)
                {
                    return null;
                }

                return collections;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get Single Collection
        public static Collection? GetSingleCollection(int collectionId)
        {
            var collectionDb = GetCollectionDb();

            if (collectionDb != null)
            {
                var collection = collectionDb.Query().Where(x => x.Id == collectionId).FirstOrDefault();
                if (collection == null)
                {
                    return null;
                }

                return collection;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Update/Set Default Collection
        public static (bool, string) ChangeDefaultCollection(int collectionId)
        {
            var collectionDb = GetCollectionDb();
            var defaultCollection = GetDefaultCollection();
            if(collectionDb != null)
            {
                if (defaultCollection != null)
                {
                    defaultCollection.IsDefault = false;
                    
                    var singleCollection = GetSingleCollection(collectionId);
                    if (singleCollection != null)
                    {
                        singleCollection.IsDefault = true;
                        collectionDb.UpdateSafe(singleCollection);
                        collectionDb.UpdateSafe(defaultCollection);
                        return (true, $"Collection: {defaultCollection.Name} is no longer the default and {defaultCollection.Name} is the new default.");
                    }
                    else
                    {
                        return (false, $"Could not find the collection associated with that Id.");
                    }

                }
                else
                {
                    var singleCollection = GetSingleCollection(collectionId);
                    if(singleCollection != null)
                    {
                        singleCollection.IsDefault = true;
                        collectionDb.UpdateSafe(singleCollection);
                    }
                    else
                    {
                        return (false, $"Could not find the collection associated with that Id.");
                    }
                }
            }
            return (false, $"Failed to update default collection.");
        }
        #endregion

        #region Save Collection
        public static async Task<(bool, string)> SaveCollection(Collection collection)
        {
            var singleCollection = GetSingleCollection(collection.Id);
            var collectionDb = GetCollectionDb();
            var defaultCollection = GetDefaultCollection();

            var wordCount = collection.Description.ToWordCountCheck(200);
            var descLength = collection.Description.ToLengthCheck(1200);
            var nameLength = collection.Name.ToLengthCheck(64);

            if (!wordCount || !descLength)
                return (false, $"Failed to insert/update. Description Word Count Allowed: {200}. Description length allowed: {1200}");

            if(!nameLength)
                return (false, $"Failed to insert/update. Name length allowed: {64}");

            if (singleCollection == null)
            {
                if (collectionDb != null)
                {
                    collectionDb.InsertSafe(collection);
                    return (true, "Collection saved.");
                }
            }
            else
            {
                if (collectionDb != null)
                {
                    collectionDb.UpdateSafe(collection);
                    return (true, "Collection updated.");
                }
            }
            return (false, "Collection DB was null.");
        }

        #endregion

        #region Delete Collection
        public static async  Task<(bool, string)> DeleteCollection(int collectionId)
        {
            var singleCollection = GetSingleCollection(collectionId);
            if (singleCollection != null)
            {
                var collectionDb = GetCollectionDb();
                if (collectionDb != null)
                {
                    if(singleCollection.IsDefault)
                    {
                        return (false, "Cannot delete the default collection. Please switch default to another collection and try to delete again.");
                    }
                    collectionDb.DeleteSafe(collectionId);
                    return (true, "Collection deleted.");
                }
                else
                {
                    return (false, "Collection DB was null.");
                }
            }
            return (false, "Collection was not present.");

        }

        #endregion

    }
}
