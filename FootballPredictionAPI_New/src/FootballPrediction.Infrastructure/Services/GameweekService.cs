using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FootballPrediction.Infrastructure.Services;

public class GameweekService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GameweekService> _logger;
    private const string FPL_API_BASE_URL = "https://fantasy.premierleague.com/api";
    
    // Cache the current gameweek to avoid repeated API calls
    private int? _cachedCurrentGameweek;
    private DateTime _cacheExpiry;

    public GameweekService(HttpClient httpClient, ILogger<GameweekService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<int> GetCurrentGameweekAsync()
    {
        try
        {
            // Return cached value if still valid (cache for 1 hour)
            if (_cachedCurrentGameweek.HasValue && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedCurrentGameweek.Value;
            }

            _logger.LogInformation("Fetching current gameweek from FPL API...");
            
            var response = await _httpClient.GetStringAsync($"{FPL_API_BASE_URL}/bootstrap-static/");
            var data = JsonDocument.Parse(response);
            
            var events = data.RootElement.GetProperty("events");
            
            // Find the current gameweek (the one that is active or most recent)
            int currentGameweek = 1;
            
            foreach (var eventElement in events.EnumerateArray())
            {
                var id = eventElement.GetProperty("id").GetInt32();
                var isCurrentProperty = eventElement.TryGetProperty("is_current", out var isCurrent);
                var isNextProperty = eventElement.TryGetProperty("is_next", out var isNext);
                
                if (isCurrentProperty && isCurrent.GetBoolean())
                {
                    currentGameweek = id;
                    break;
                }
                
                // If no current gameweek, look for the next one
                if (isNextProperty && isNext.GetBoolean())
                {
                    currentGameweek = Math.Max(1, id - 1); // Previous gameweek is current
                    break;
                }
                
                // Fallback: use the highest completed gameweek
                if (eventElement.TryGetProperty("finished", out var finished) && finished.GetBoolean())
                {
                    currentGameweek = Math.Max(currentGameweek, id);
                }
            }

            // Cache the result for 1 hour
            _cachedCurrentGameweek = currentGameweek;
            _cacheExpiry = DateTime.UtcNow.AddHours(1);
            
            _logger.LogInformation("Current gameweek determined as: {Gameweek}", currentGameweek);
            return currentGameweek;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current gameweek from FPL API, falling back to calculation");
            
            // Fallback to calculation if API fails
            return GetGameweekByCalculation();
        }
    }

    public async Task<GameweekInfo> GetGameweekInfoAsync(int gameweek)
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{FPL_API_BASE_URL}/bootstrap-static/");
            var data = JsonDocument.Parse(response);
            
            var events = data.RootElement.GetProperty("events");
            
            foreach (var eventElement in events.EnumerateArray())
            {
                var id = eventElement.GetProperty("id").GetInt32();
                if (id == gameweek)
                {
                    return new GameweekInfo
                    {
                        Id = id,
                        Name = eventElement.GetProperty("name").GetString() ?? $"Gameweek {id}",
                        DeadlineTime = DateTime.TryParse(eventElement.GetProperty("deadline_time").GetString(), out var deadline) 
                            ? deadline : DateTime.Now,
                        IsCurrent = eventElement.TryGetProperty("is_current", out var isCurrent) && isCurrent.GetBoolean(),
                        IsNext = eventElement.TryGetProperty("is_next", out var isNext) && isNext.GetBoolean(),
                        IsFinished = eventElement.TryGetProperty("finished", out var finished) && finished.GetBoolean(),
                        ChipPlays = GetSafeIntFromProperty(eventElement, "chip_plays"),
                        MostSelected = GetSafeIntFromProperty(eventElement, "most_selected"),
                        MostTransferredIn = GetSafeIntFromProperty(eventElement, "most_transferred_in"),
                        TopElement = GetSafeIntFromProperty(eventElement, "top_element"),
                        MostCaptained = GetSafeIntFromProperty(eventElement, "most_captained"),
                        MostViceCaptained = GetSafeIntFromProperty(eventElement, "most_vice_captained")
                    };
                }
            }

            // If gameweek not found, return default
            return new GameweekInfo
            {
                Id = gameweek,
                Name = $"Gameweek {gameweek}",
                DeadlineTime = DateTime.Now.AddDays(7),
                IsCurrent = false,
                IsNext = false,
                IsFinished = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching gameweek info for gameweek {Gameweek}", gameweek);
            throw;
        }
    }

    public async Task<List<GameweekInfo>> GetAllGameweeksAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{FPL_API_BASE_URL}/bootstrap-static/");
            var data = JsonDocument.Parse(response);
            
            var events = data.RootElement.GetProperty("events");
            var gameweeks = new List<GameweekInfo>();
            
            foreach (var eventElement in events.EnumerateArray())
            {
                var gameweekInfo = new GameweekInfo
                {
                    Id = eventElement.GetProperty("id").GetInt32(),
                    Name = eventElement.GetProperty("name").GetString() ?? $"Gameweek {eventElement.GetProperty("id").GetInt32()}",
                    DeadlineTime = DateTime.TryParse(eventElement.GetProperty("deadline_time").GetString(), out var deadline) 
                        ? deadline : DateTime.Now,
                    IsCurrent = eventElement.TryGetProperty("is_current", out var isCurrent) && isCurrent.GetBoolean(),
                    IsNext = eventElement.TryGetProperty("is_next", out var isNext) && isNext.GetBoolean(),
                    IsFinished = eventElement.TryGetProperty("finished", out var finished) && finished.GetBoolean(),
                    ChipPlays = GetSafeIntFromProperty(eventElement, "chip_plays"),
                    MostSelected = GetSafeIntFromProperty(eventElement, "most_selected"),
                    MostTransferredIn = GetSafeIntFromProperty(eventElement, "most_transferred_in"),
                    TopElement = GetSafeIntFromProperty(eventElement, "top_element"),
                    MostCaptained = GetSafeIntFromProperty(eventElement, "most_captained"),
                    MostViceCaptained = GetSafeIntFromProperty(eventElement, "most_vice_captained")
                };
                
                gameweeks.Add(gameweekInfo);
            }
            
            return gameweeks.OrderBy(g => g.Id).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all gameweeks");
            throw;
        }
    }

    private static int GetGameweekByCalculation()
    {
        // Fallback calculation method
        var seasonStart = new DateTime(2024, 8, 17); // 2024-25 season start
        var weeksSinceStart = (DateTime.UtcNow - seasonStart).Days / 7;
        return Math.Min(38, Math.Max(1, weeksSinceStart + 1));
    }

    private static int GetSafeIntFromProperty(JsonElement element, string propertyName)
    {
        try
        {
            if (element.TryGetProperty(propertyName, out var propertyValue))
            {
                // Handle both number and array types
                if (propertyValue.ValueKind == JsonValueKind.Number)
                {
                    return propertyValue.GetInt32();
                }
                else if (propertyValue.ValueKind == JsonValueKind.Array && propertyValue.GetArrayLength() > 0)
                {
                    return propertyValue[0].GetInt32();
                }
            }
        }
        catch
        {
            // Log or handle the error as needed
        }
        
        return 0; // Default value if not found or error occurs
    }
}

public class GameweekInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DeadlineTime { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsNext { get; set; }
    public bool IsFinished { get; set; }
    public int ChipPlays { get; set; }
    public int MostSelected { get; set; }
    public int MostTransferredIn { get; set; }
    public int TopElement { get; set; }
    public int MostCaptained { get; set; }
    public int MostViceCaptained { get; set; }
}
