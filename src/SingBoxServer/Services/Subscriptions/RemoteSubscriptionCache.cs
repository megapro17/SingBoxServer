using System.Collections.Concurrent;

namespace SingBoxServer.Services.Subscriptions;

/// <summary>
/// Простой кэш для удалённых подписок с поддержкой TTL и полной очистки.
/// </summary>
public interface IRemoteSubscriptionCache
{
    Task<string> GetOrCreateAsync(string key, Func<Task<string>> factory, int ttlMinutes);
    void Clear();
}

public class RemoteSubscriptionCache : IRemoteSubscriptionCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<RemoteSubscriptionCache> _logger;

    public RemoteSubscriptionCache(ILogger<RemoteSubscriptionCache> logger)
    {
        _logger = logger;
    }

    public async Task<string> GetOrCreateAsync(string key, Func<Task<string>> factory, int ttlMinutes)
    {
        var now = DateTimeOffset.UtcNow;
        
        // Если есть в кэше и не истёк срок — возвращаем
        if (_cache.TryGetValue(key, out var entry) && (!entry.HasExpiration || entry.ExpirationTime > now))
        {
            return entry.Value;
        }

        // Иначе загружаем
        _logger.LogDebug("Кэш пропущен, загружаем: {Key}", key);
        var value = await factory();
        
        var expiration = ttlMinutes > 0 ? now.AddMinutes(ttlMinutes) : (DateTimeOffset?)null;
        _cache[key] = new CacheEntry(value, expiration);
        
        return value;
    }

    public void Clear()
    {
        _cache.Clear();
        _logger.LogInformation("Кэш удалённых подписок очищен.");
    }

    private sealed record CacheEntry(string Value, DateTimeOffset? ExpirationTime)
    {
        public bool HasExpiration => ExpirationTime.HasValue;
    }
}
