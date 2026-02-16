using System;
using System.Collections.Concurrent;
// TAG: #PERFORMANCE #CACHING #PHASE_3_OPTIMIZATION #VERSION_2_1

namespace NecessaryAdminTool.Helpers
{
    /// <summary>
    /// Centralized caching helper for expensive operations
    /// TAG: #OPERATION_CACHING #TTL_CACHE
    /// </summary>
    public static class CacheHelper
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();

        private class CacheEntry
        {
            public object Result { get; set; }
            public DateTime CachedAt { get; set; }
        }

        /// <summary>
        /// Gets a cached value or computes it if not cached/expired
        /// </summary>
        /// <typeparam name="T">Type of cached value</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="factory">Factory function to create value if not cached</param>
        /// <param name="ttlMinutes">Time-to-live in minutes (default: 5)</param>
        /// <returns>Cached or newly computed value</returns>
        public static T GetCached<T>(string key, Func<T> factory, int ttlMinutes = 5)
        {
            if (string.IsNullOrEmpty(key))
                return factory();

            // Try to get from cache
            if (_cache.TryGetValue(key, out var cached))
            {
                // Check if cache entry is still valid
                if ((DateTime.Now - cached.CachedAt).TotalMinutes < ttlMinutes)
                {
                    return (T)cached.Result;
                }

                // Cache expired - remove it
                _cache.TryRemove(key, out _);
            }

            // Compute new value
            var result = factory();

            // Store in cache
            _cache[key] = new CacheEntry
            {
                Result = result,
                CachedAt = DateTime.Now
            };

            return result;
        }

        /// <summary>
        /// Invalidates a specific cache entry
        /// </summary>
        /// <param name="key">Cache key to invalidate</param>
        public static void InvalidateCache(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Invalidates all cache entries matching a pattern
        /// </summary>
        /// <param name="keyPattern">Pattern to match (e.g., "computer:*")</param>
        public static void InvalidateCachePattern(string keyPattern)
        {
            if (string.IsNullOrEmpty(keyPattern))
                return;

            var pattern = keyPattern.Replace("*", "");
            foreach (var key in _cache.Keys)
            {
                if (key.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Clears all cached entries
        /// </summary>
        public static void ClearAllCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Gets count of cached entries
        /// </summary>
        public static int CacheCount => _cache.Count;

        /// <summary>
        /// Removes expired cache entries (cleanup maintenance)
        /// </summary>
        /// <param name="ttlMinutes">Time-to-live threshold</param>
        public static int CleanExpiredCache(int ttlMinutes = 5)
        {
            int removedCount = 0;
            var now = DateTime.Now;

            foreach (var kvp in _cache)
            {
                if ((now - kvp.Value.CachedAt).TotalMinutes >= ttlMinutes)
                {
                    if (_cache.TryRemove(kvp.Key, out _))
                    {
                        removedCount++;
                    }
                }
            }

            return removedCount;
        }
    }
}
