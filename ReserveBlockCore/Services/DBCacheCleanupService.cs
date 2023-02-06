using LiteDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace ReserveBlockCore.Services
{
    public class DBCacheCleanupService
    {
        private const int CACHE_SIZE_MAX_LIMIT = 1024;
        private const int CLEANUP_SECONDS = 60 * 15; // 15 Minutes

        private static FieldInfo _ScalarCache;
        private static FieldInfo _EnumerableCache;
        private static Timer _DBCacheCleanupTimer;

        public static void Enable(bool enable)
        {
            if (enable)
            {
                _ScalarCache ??= GetScalarExpressionCache();
                _EnumerableCache ??= GetEnumerableExpressionCache();

                if (_ScalarCache == null || _EnumerableCache == null)
                {
                    //cancel. Should not be null
                }
                else
                {
                    var ts = TimeSpan.FromSeconds(CLEANUP_SECONDS);
                    _DBCacheCleanupTimer ??= new Timer(OnCleanupCallback, null, ts, ts);
                }
            }
            else if (_DBCacheCleanupTimer != null)
            {
                _DBCacheCleanupTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                _DBCacheCleanupTimer.Dispose();
                _DBCacheCleanupTimer = null;
            }
        }

        private static FieldInfo GetScalarExpressionCache()
        {
            var bsonExpType = typeof(BsonExpression);
            return bsonExpType.GetField("_cacheScalar", BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static FieldInfo GetEnumerableExpressionCache()
        {
            var bsonExpType = typeof(BsonExpression);
            return bsonExpType.GetField("_cacheEnumerable", BindingFlags.NonPublic | BindingFlags.Static);
        }

        public static void OnCleanupCallback(object _)
        {
            try
            {
                _ScalarCache ??= GetScalarExpressionCache();
                _EnumerableCache ??= GetEnumerableExpressionCache();

                var scalarExpressionCache = _ScalarCache.GetValue(null);
                if (scalarExpressionCache is IDictionary scalarCacheDictionary)
                {
                    ClearDictionary(scalarCacheDictionary);
                }
                

                var enumerableExpressionCache = _EnumerableCache.GetValue(null);
                if (enumerableExpressionCache is IDictionary enumerableCacheDictionary)
                {
                    ClearDictionary(enumerableCacheDictionary);
                }
                
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"An exception occurred. Error: {ex.ToString()}", "DBCacheCleanupService.OnCleanupCallback()");
            }
        }

        private static void ClearDictionary(IDictionary dictionary)
        {
            var currentSize = dictionary.Count;
            if (currentSize > CACHE_SIZE_MAX_LIMIT)
            {
                dictionary.Clear();
            }
        }
    }
}
