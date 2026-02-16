using System;
using System.Net;
using System.Threading.Tasks;
// TAG: #PERFORMANCE #DNS_CACHING #PHASE_3_OPTIMIZATION #VERSION_2_1

namespace NecessaryAdminTool.Helpers
{
    /// <summary>
    /// DNS resolution helper with caching support
    /// TAG: #DNS_RESOLUTION #OPERATION_CACHING
    /// </summary>
    public static class DnsHelper
    {
        /// <summary>
        /// Gets host entry with 10-minute cache
        /// </summary>
        /// <param name="hostNameOrAddress">Hostname or IP address</param>
        /// <returns>Cached or fresh IPHostEntry</returns>
        public static async Task<IPHostEntry> GetHostEntryAsync(string hostNameOrAddress)
        {
            if (string.IsNullOrEmpty(hostNameOrAddress))
                throw new ArgumentNullException(nameof(hostNameOrAddress));

            string cacheKey = $"dns:host:{hostNameOrAddress.ToLowerInvariant()}";

            return await Task.Run(() =>
            {
                return CacheHelper.GetCached(cacheKey, () =>
                {
                    try
                    {
                        return Dns.GetHostEntry(hostNameOrAddress);
                    }
                    catch
                    {
                        // Don't cache failures
                        return null;
                    }
                }, ttlMinutes: 10);
            });
        }

        /// <summary>
        /// Gets host addresses with 10-minute cache
        /// </summary>
        /// <param name="hostNameOrAddress">Hostname or IP address</param>
        /// <returns>Cached or fresh IP addresses</returns>
        public static async Task<IPAddress[]> GetHostAddressesAsync(string hostNameOrAddress)
        {
            if (string.IsNullOrEmpty(hostNameOrAddress))
                throw new ArgumentNullException(nameof(hostNameOrAddress));

            string cacheKey = $"dns:addresses:{hostNameOrAddress.ToLowerInvariant()}";

            return await Task.Run(() =>
            {
                return CacheHelper.GetCached(cacheKey, () =>
                {
                    try
                    {
                        return Dns.GetHostAddresses(hostNameOrAddress);
                    }
                    catch
                    {
                        // Don't cache failures
                        return null;
                    }
                }, ttlMinutes: 10);
            });
        }

        /// <summary>
        /// Invalidates DNS cache for a specific host
        /// </summary>
        /// <param name="hostNameOrAddress">Hostname or IP address</param>
        public static void InvalidateDnsCache(string hostNameOrAddress)
        {
            if (string.IsNullOrEmpty(hostNameOrAddress))
                return;

            string lowerHost = hostNameOrAddress.ToLowerInvariant();
            CacheHelper.InvalidateCache($"dns:host:{lowerHost}");
            CacheHelper.InvalidateCache($"dns:addresses:{lowerHost}");
        }

        /// <summary>
        /// Clears all DNS cache entries
        /// </summary>
        public static void ClearDnsCache()
        {
            CacheHelper.InvalidateCachePattern("dns:*");
        }
    }
}
