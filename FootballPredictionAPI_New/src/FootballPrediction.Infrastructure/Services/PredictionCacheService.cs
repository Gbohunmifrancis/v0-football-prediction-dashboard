using System.Collections.Concurrent;

namespace FootballPrediction.Infrastructure.Services;

/// <summary>
/// Simple in-memory cache for prediction results
/// </summary>
public class PredictionCacheService
{
    private readonly ConcurrentDictionary<string, CachedPrediction> _cache;
    private readonly TimeSpan _cacheExpiration;

    public PredictionCacheService()
    {
        _cache = new ConcurrentDictionary<string, CachedPrediction>();
        _cacheExpiration = TimeSpan.FromMinutes(30); // Cache predictions for 30 minutes
    }

    public bool TryGetPrediction(int playerId, int gameweek, out double prediction, out double confidence)
    {
        var key = GetCacheKey(playerId, gameweek);
        
        if (_cache.TryGetValue(key, out var cached) && !cached.IsExpired(_cacheExpiration))
        {
            prediction = cached.PredictedPoints;
            confidence = cached.Confidence;
            return true;
        }

        prediction = 0;
        confidence = 0;
        return false;
    }

    public void SetPrediction(int playerId, int gameweek, double prediction, double confidence)
    {
        var key = GetCacheKey(playerId, gameweek);
        _cache[key] = new CachedPrediction
        {
            PredictedPoints = prediction,
            Confidence = confidence,
            CachedAt = DateTime.UtcNow
        };
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public void ClearGameweek(int gameweek)
    {
        var keysToRemove = _cache.Keys.Where(k => k.EndsWith($"_{gameweek}")).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    private string GetCacheKey(int playerId, int gameweek) => $"player_{playerId}_gw_{gameweek}";

    private class CachedPrediction
    {
        public double PredictedPoints { get; set; }
        public double Confidence { get; set; }
        public DateTime CachedAt { get; set; }

        public bool IsExpired(TimeSpan expiration) => DateTime.UtcNow - CachedAt > expiration;
    }
}
