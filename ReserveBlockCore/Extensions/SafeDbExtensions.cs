using LiteDB;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace ReserveBlockCore.Extensions
{
    public static class DbExtensions
    {
        private static ConcurrentDictionary<string, SemaphoreSlim> DbSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();        

        private static SemaphoreSlim GetSlim<T>(this ILiteCollection<T> col)
        {
            if (!DbSemaphores.TryGetValue(col.Name, out SemaphoreSlim DbSemaphore))
            {
                DbSemaphore = new SemaphoreSlim(1, 1);
                if (!DbSemaphores.TryAdd(col.Name, DbSemaphore))
                    DbSemaphores.TryGetValue(col.Name, out DbSemaphore);
            }

            return DbSemaphore;
        }

        //
        // Summary:
        //     Get collection name
        public static string Name<T>(ILiteCollection<T> col)
        {
            return col.Name;
        }

        //
        // Summary:
        //     Get collection auto id type
        public static BsonAutoId AutoId<T>(ILiteCollection<T> col)
        {
            return col.AutoId;
        }

        //
        // Summary:
        //     Getting entity mapper from current collection. Returns null if collection are
        //     BsonDocument type
        public static EntityMapper EntityMapper<T>(ILiteCollection<T> col)
        {
            return col.EntityMapper;
        }

        //
        // Summary:
        //     Run an include action in each document returned by Find(), FindById(), FindOne()
        //     and All() methods to load DbRef documents Returns a new Collection with this
        //     action included
        public static ILiteCollection<T> Include<T, K>(ILiteCollection<T> col, Expression<Func<T, K>> keySelector)
        {
            return col.Include(keySelector);
        }

        //
        // Summary:
        //     Run an include action in each document returned by Find(), FindById(), FindOne()
        //     and All() methods to load DbRef documents Returns a new Collection with this
        //     action included
        public static ILiteCollection<T> Include<T>(ILiteCollection<T> col, BsonExpression keySelector)
        {
            return col.Include(keySelector);
        }

        //
        // Summary:
        //     Insert or Update a document in this collection.
        public static bool Upsert<T>(this ILiteCollection<T> col, T entity)
        {
            var DbSemaphore =  col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Upsert(entity);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Insert or Update all documents
        public static int Upsert<T>(ILiteCollection<T> col, IEnumerable<T> entities)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Upsert(entities);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Insert or Update a document in this collection.
        public static bool Upsert<T>(ILiteCollection<T> col, BsonValue id, T entity)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Upsert(id, entity);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Update a document in this collection. Returns false if not found document in
        //     collection
        public static bool Update<T>(ILiteCollection<T> col, T entity)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Update(entity);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Update a document in this collection. Returns false if not found document in
        //     collection
        public static bool Update<T>(ILiteCollection<T> col, BsonValue id, T entity)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Update(id, entity);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Update all documents
        public static int Update<T>(ILiteCollection<T> col, IEnumerable<T> entities)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Update(entities);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Update many documents based on transform expression. This expression must return
        //     a new document that will be replaced over current document (according with predicate).
        //     Eg: col.UpdateMany("{ Name: UPPER($.Name), Age }", "_id > 0")
        public static int UpdateMany<T>(ILiteCollection<T> col, BsonExpression transform, BsonExpression predicate)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.UpdateMany(transform, predicate);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Update many document based on merge current document with extend expression.
        //     Use your class with initializers. Eg: col.UpdateMany(x => new Customer { Name
        //     = x.Name.ToUpper(), Salary: 100 }, x => x.Name == "John")
        public static int UpdateMany<T>(ILiteCollection<T> col, Expression<Func<T, T>> extend, Expression<Func<T, bool>> predicate)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.UpdateMany(extend, predicate);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Insert a new entity to this collection. Document Id must be a new value in collection
        //     - Returns document Id
        public static BsonValue Insert<T>(ILiteCollection<T> col, T entity)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Insert(entity);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Insert a new document to this collection using passed id value.
        public static void Insert<T>(ILiteCollection<T> col, BsonValue id, T entity)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                col.Insert(id, entity);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Insert an array of new documents to this collection. Document Id must be a new
        //     value in collection. Can be set buffer size to commit at each N documents
        public static int Insert<T>(ILiteCollection<T> col, IEnumerable<T> entities)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Insert(entities);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Implements bulk insert documents in a collection. Usefull when need lots of documents.
        public static int InsertBulk<T>(ILiteCollection<T> col, IEnumerable<T> entities, int batchSize = 5000)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.InsertBulk(entities, batchSize);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }


        //
        // Summary:
        //     Create a new permanent index in all documents inside this collections if index
        //     not exists already. Returns true if index was created or false if already exits
        //
        // Parameters:
        //   name:
        //     Index name - unique name for this collection
        //
        //   expression:
        //     Create a custom expression function to be indexed
        //
        //   unique:
        //     If is a unique index
        public static bool EnsureIndex<T>(ILiteCollection<T> col, string name, BsonExpression expression, bool unique = false)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.EnsureIndex(name, expression, unique);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Create a new permanent index in all documents inside this collections if index
        //     not exists already. Returns true if index was created or false if already exits
        //
        // Parameters:
        //   expression:
        //     Document field/expression
        //
        //   unique:
        //     If is a unique index
        public static bool EnsureIndex<T>(ILiteCollection<T> col, BsonExpression expression, bool unique = false)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.EnsureIndex(expression, unique);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Create a new permanent index in all documents inside this collections if index
        //     not exists already.
        //
        // Parameters:
        //   keySelector:
        //     LinqExpression to be converted into BsonExpression to be indexed
        //
        //   unique:
        //     Create a unique keys index?
        public static bool EnsureIndex<T, K>(ILiteCollection<T> col, Expression<Func<T, K>> keySelector, bool unique = false)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.EnsureIndex(keySelector, unique);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Create a new permanent index in all documents inside this collections if index
        //     not exists already.
        //
        // Parameters:
        //   name:
        //     Index name - unique name for this collection
        //
        //   keySelector:
        //     LinqExpression to be converted into BsonExpression to be indexed
        //
        //   unique:
        //     Create a unique keys index?
        public static bool EnsureIndex<T, K>(ILiteCollection<T> col, string name, Expression<Func<T, K>> keySelector, bool unique = false)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.EnsureIndex(name, keySelector, unique);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Drop index and release slot for another index
        public static bool DropIndex<T>(ILiteCollection<T> col, string name)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.DropIndex(name);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Delete a single document on collection based on _id index. Returns true if document
        //     was deleted
        public static bool Delete<T>(ILiteCollection<T> col, BsonValue id)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.Delete(id);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Delete all documents inside collection. Returns how many documents was deleted.
        //     Run inside current transaction
        public static int DeleteAll<T>(ILiteCollection<T> col)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.DeleteAll();
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Delete all documents based on predicate expression. Returns how many documents
        //     was deleted
        public static int DeleteMany<T>(ILiteCollection<T> col, BsonExpression predicate)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.DeleteMany(predicate);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Delete all documents based on predicate expression. Returns how many documents
        //     was deleted
        public static int DeleteMany<T>(ILiteCollection<T> col, string predicate, BsonDocument parameters)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.DeleteMany(predicate, parameters);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Delete all documents based on predicate expression. Returns how many documents
        //     was deleted
        public static int DeleteMany<T>(ILiteCollection<T> col, string predicate, params BsonValue[] args)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.DeleteMany(predicate, args);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Delete all documents based on predicate expression. Returns how many documents
        //     was deleted
        public static int DeleteMany<T>(ILiteCollection<T> col, Expression<Func<T, bool>> predicate)
        {
            var DbSemaphore = col.GetSlim();
            DbSemaphore.Wait();
            try
            {
                return col.DeleteMany(predicate);
            }
            finally
            {
                DbSemaphore.Release();
            }
        }

        //
        // Summary:
        //     Return a new LiteQueryable to build more complex queries
        public static ILiteQueryable<T> Query<T>(ILiteCollection<T> col)
        {
            return col.Query();
        }

        //
        // Summary:
        //     Find documents inside a collection using predicate expression.
        public static IEnumerable<T> Find<T>(ILiteCollection<T> col, BsonExpression predicate, int skip = 0, int limit = int.MaxValue)
        
        {
            return col.Find(predicate, skip, limit);
        }

        //
        // Summary:
        //     Find documents inside a collection using query definition.
        public static IEnumerable<T> Find<T>(ILiteCollection<T> col, Query query, int skip = 0, int limit = int.MaxValue)
        {
            return col.Find(query, skip, limit);
        }

        //
        // Summary:
        //     Find documents inside a collection using predicate expression.
        public static IEnumerable<T> Find<T>(ILiteCollection<T> col, Expression<Func<T, bool>> predicate, int skip = 0, int limit = int.MaxValue)
        {
            return col.Find(predicate, skip, limit);
        }

        //
        // Summary:
        //     Find a document using Document Id. Returns null if not found.
        public static T FindById<T>(ILiteCollection<T> col, BsonValue id)
        {
            return col.FindById(id);
        }

        //
        // Summary:
        //     Find the first document using predicate expression. Returns null if not found
        public static T FindOne<T>(ILiteCollection<T> col, BsonExpression predicate)
        {
            return col.FindOne(predicate);
        }

        //
        // Summary:
        //     Find the first document using predicate expression. Returns null if not found
        public static T FindOne<T>(ILiteCollection<T> col, string predicate, BsonDocument parameters)
        {
            return col.FindOne(predicate, parameters);
        }

        //
        // Summary:
        //     Find the first document using predicate expression. Returns null if not found
        public static T FindOne<T>(ILiteCollection<T> col, BsonExpression predicate, params BsonValue[] args)
        {
            return col.FindOne(predicate, args);
        }

        //
        // Summary:
        //     Find the first document using predicate expression. Returns null if not found
        public static T FindOne<T>(ILiteCollection<T> col, Expression<Func<T, bool>> predicate)
        {
            return col.FindOne(predicate);
        }

        //
        // Summary:
        //     Find the first document using defined query structure. Returns null if not found
        public static T FindOne<T>(ILiteCollection<T> col, Query query)
        {
            return col.FindOne(query);
        }

        //
        // Summary:
        //     Returns all documents inside collection order by _id index.
        public static IEnumerable<T> FindAll<T>(ILiteCollection<T> col)
        {
            return col.FindAll();
        }

        //
        // Summary:
        //     Get document count using property on collection.
        public static int Count<T>(ILiteCollection<T> col)
        {
            return col.Count();
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any document.
        //     Needs indexes on query expression
        public static int Count<T>(ILiteCollection<T> col, BsonExpression predicate)
        {
            return col.Count(predicate);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any document.
        //     Needs indexes on query expression
        public static int Count<T>(ILiteCollection<T> col, string predicate, BsonDocument parameters)
        {
            return col.Count(predicate, parameters);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any document.
        //     Needs indexes on query expression
        public static int Count<T>(ILiteCollection<T> col, string predicate, params BsonValue[] args)
        {
            return col.Count(predicate, args);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
        public static int Count<T>(ILiteCollection<T> col, Expression<Func<T, bool>> predicate)
        {
            return col.Count(predicate);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
         public static int Count<T>(ILiteCollection<T> col, Query query)
        {
            return col.Count(query);
        }

        //
        // Summary:
        //     Get document count using property on collection.
        public static long LongCount<T>(ILiteCollection<T> col)
        {
            return col.LongCount();
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
        public static long LongCount<T>(ILiteCollection<T> col, BsonExpression predicate)
        {
            return col.LongCount(predicate);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
        public static long LongCount<T>(ILiteCollection<T> col, string predicate, BsonDocument parameters)
        {
            return col.LongCount(predicate, parameters);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
        public static long LongCount<T>(ILiteCollection<T> col, string predicate, params BsonValue[] args)
        {
            return col.LongCount(predicate, args);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
        public static long LongCount<T>(ILiteCollection<T> col, Expression<Func<T, bool>> predicate)
        {
            return col.LongCount(predicate);
        }

        //
        // Summary:
        //     Count documents matching a query. This method does not deserialize any documents.
        //     Needs indexes on query expression
        public static long LongCount<T>(ILiteCollection<T> col, Query query)
        {
            return col.LongCount(query);
        }

        //
        // Summary:
        //     Returns true if query returns any document. This method does not deserialize
        //     any document. Needs indexes on query expression
        public static bool Exists<T>(ILiteCollection<T> col, BsonExpression predicate)
        {
            return col.Exists(predicate);
        }

        //
        // Summary:
        //     Returns true if query returns any document. This method does not deserialize
        //     any document. Needs indexes on query expression
        public static bool Exists<T>(ILiteCollection<T> col, string predicate, BsonDocument parameters)
        {
            return col.Exists(predicate, parameters);
        }

        //
        // Summary:
        //     Returns true if query returns any document. This method does not deserialize
        //     any document. Needs indexes on query expression
        public static bool Exists<T>(ILiteCollection<T> col, string predicate, params BsonValue[] args)
        {
            return col.Exists(predicate, args);
        }

        //
        // Summary:
        //     Returns true if query returns any document. This method does not deserialize
        //     any document. Needs indexes on query expression
        public static bool Exists<T>(ILiteCollection<T> col, Expression<Func<T, bool>> predicate)
        {
            return col.Exists(predicate);
        }

        //
        // Summary:
        //     Returns true if query returns any document. This method does not deserialize
        //     any document. Needs indexes on query expression
        public static bool Exists<T>(ILiteCollection<T> col, Query query)
        {
            return col.Exists(query);
        }

        //
        // Summary:
        //     Returns the min value from specified key value in collection
        public static BsonValue Min<T>(ILiteCollection<T> col, BsonExpression keySelector)
        {
            return col.Min(keySelector);
        }

        //
        // Summary:
        //     Returns the min value of _id index
        public static BsonValue Min<T>(ILiteCollection<T> col)
        {
            return col.Min();
        }

        //
        // Summary:
        //     Returns the min value from specified key value in collection
        public static K Min<K, T>(ILiteCollection<T> col, Expression<Func<T, K>> keySelector)
        {
            return col.Min(keySelector);
        }

        //
        // Summary:
        //     Returns the max value from specified key value in collection
        public static BsonValue Max<T>(ILiteCollection<T> col, BsonExpression keySelector)
        {
            return col.Max(keySelector);
        }

        //
        // Summary:
        //     Returns the max _id index key value
        public static BsonValue Max<T>(ILiteCollection<T> col)
        {
            return col.Max();
        }

        //
        // Summary:
        //     Returns the last/max field using a linq expression
        public static K Max<K, T>(ILiteCollection<T> col, Expression<Func<T, K>> keySelector)
        {
            return col.Max(keySelector);
        }
    }
}
