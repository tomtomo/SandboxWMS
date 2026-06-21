using System.Collections.Concurrent;
using Wms.BuildingBlocks.Application.Caching;

namespace Wms.Platform.Local.Caching;

// What: Adapter Local untuk port ICacheStore (in-proc TTL cache; ADR-0011)
// Why: stand-in Redis/Azure Cache for Redis/Memorystore untuk environment Local — cache-aside store
// in-memory dengan TTL (ADR-0011 TTL-first). Singleton (state cache hidup selama proses). Adapter cloud
// (StackExchange.Redis) menggantikan tanpa menyentuh core/decorator (Hexagonal, FF#1).
// How: simpan (value, absoluteExpiry); Get cek expiry LAZY (expired → evict + null = miss → caller
// populate ulang); Set absolute expiry = now + ttl. Thread-safe (ConcurrentDictionary). Cast object→T
// aman karena key di-namespace per tipe (decorator: "masterdata:{type}:{id}").
public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult((T?)entry.Value);

            _entries.TryRemove(key, out _); // lazy eviction entri kedaluwarsa
        }

        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        where T : class
    {
        _entries[key] = new CacheEntry(value, DateTimeOffset.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);
}
