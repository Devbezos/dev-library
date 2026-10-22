using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace DevClient.Clients
{
    /// <summary>
    /// The one in-memory cache for third-party API answers (Raider.IO, Warcraft Logs, Battle.net, ...). Only answers that
    /// came back are kept: a fetch that throws is never cached, and callers choose how long a "not found" (null) lives.
    /// Concurrent misses for a key share one fetch. Strings are sized by length (about 1 unit per KB) and everything
    /// else is 1 unit, so the cache stays under <see cref="DefaultSizeLimit"/> and evicts the oldest-used first.
    /// Keys must name everything that changes the answer; put secrets in them only through <see cref="Fingerprint"/>.
    /// </summary>
    public sealed class ApiResponseCache : IDisposable
    {
        /// <summary>About 64 MB of response text.</summary>
        public const long DefaultSizeLimit = 64 * 1024;

        /// <summary>The process-wide cache, for clients built without dependency injection.</summary>
        public static ApiResponseCache Shared { get; } = new();

        private readonly MemoryCache _cache;
        private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inFlight = new();

        public ApiResponseCache(long sizeLimit = DefaultSizeLimit, TimeProvider? time = null) =>
            _cache = new MemoryCache(new MemoryCacheOptions
            {
                SizeLimit = sizeLimit,
                Clock = time is null ? null : new Clock(time),
            });

        /// <summary>
        /// The cached answer for <paramref name="key"/>, else <paramref name="fetch"/>'s, kept for <paramref name="ttl"/>
        /// of that answer (zero or less: not kept).
        /// </summary>
        public async Task<T> GetOrFetch<T>(string key, Func<Task<T>> fetch, Func<T, TimeSpan> ttl)
        {
            if (_cache.TryGetValue(key, out var hit))
                return (T)hit!;

            var shared = _inFlight.GetOrAdd(key, k => new Lazy<Task<object?>>(async () =>
            {
                try
                {
                    var value = await fetch();
                    var keepFor = ttl(value);
                    if (keepFor > TimeSpan.Zero)
                        _cache.Set(key, (object?)value, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = keepFor,
                            Size = value is string s ? s.Length / 1024 + 1 : 1,
                        });
                    return value;
                }
                finally
                {
                    _inFlight.TryRemove(key, out _);
                }
            }));
            return (T)(await shared.Value)!;
        }

        /// <summary>Keeps an answer for <paramref name="ttl"/>, and a null ("not found") for <paramref name="missingTtl"/> (default: not kept).</summary>
        public Task<T> GetOrFetch<T>(string key, TimeSpan ttl, Func<Task<T>> fetch, TimeSpan? missingTtl = null) =>
            GetOrFetch(key, fetch, value => value is null ? missingTtl ?? TimeSpan.Zero : ttl);

        /// <summary>Forgets one answer, e.g. after a write that changes it.</summary>
        public void Remove(string key) => _cache.Remove(key);

        /// <summary>Forgets everything.</summary>
        public void Clear() => _cache.Compact(1.0);

        /// <summary>A short one-way hash of a credential, so a key can tell whose keys fetched an answer without holding them.</summary>
        public static string Fingerprint(string? secret) =>
            string.IsNullOrEmpty(secret) ? "none" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)))[..16];

        public void Dispose() => _cache.Dispose();

        private sealed class Clock(TimeProvider time) : ISystemClock
        {
            public DateTimeOffset UtcNow => time.GetUtcNow();
        }
    }
}
