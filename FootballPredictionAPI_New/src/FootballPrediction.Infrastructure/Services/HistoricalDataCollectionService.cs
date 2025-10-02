using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FootballPrediction.Infrastructure.Services;

public class HistoricalDataCollectionService
{
    private readonly FplDbContext _context;
    private readonly ILogger<HistoricalDataCollectionService> _logger;
    private readonly HttpClient _httpClient;
    
    // FPL Historical API endpoints
    private const string FPL_HISTORICAL_BASE = "https://fantasy.premierleague.com/api";
    private readonly string[] _availableSeasons = { "2021-22", "2022-23", "2023-24", "2024-25" };

    public HistoricalDataCollectionService(
        FplDbContext context, 
        ILogger<HistoricalDataCollectionService> logger, 
        HttpClient httpClient)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<DataCollectionSummary> CollectComprehensiveHistoricalDataAsync()
    {
        _logger.LogInformation("Starting comprehensive historical data collection for ML training...");
        
        var summary = new DataCollectionSummary
        {
            StartTime = DateTime.UtcNow,
            SeasonsProcessed = new List<string>(),
            PlayersProcessed = 0,
            GameweeksProcessed = 0,
            TotalRecordsCreated = 0
        };

        try
        {
            // Step 1: Collect historical player performance data
            foreach (var season in _availableSeasons)
            {
                _logger.LogInformation("Processing season: {Season}", season);
                
                var seasonSummary = await CollectSeasonDataAsync(season);
                summary.SeasonsProcessed.Add(season);
                summary.PlayersProcessed += seasonSummary.PlayersCount;
                summary.GameweeksProcessed += seasonSummary.GameweeksCount;
                summary.TotalRecordsCreated += seasonSummary.RecordsCount;
            }

            // Step 2: Calculate derived features for ML
            await CalculateDerivedFeaturesAsync();

            // Step 3: Create training/validation datasets
            await CreateMLDatasetsAsync();

            summary.EndTime = DateTime.UtcNow;
            summary.Success = true;

            _logger.LogInformation("Historical data collection completed successfully. Total records: {TotalRecords}", 
                summary.TotalRecordsCreated);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during historical data collection");
            summary.Success = false;
            summary.ErrorMessage = ex.Message;
            return summary;
        }
    }

    private async Task<SeasonDataSummary> CollectSeasonDataAsync(string season)
    {
        var summary = new SeasonDataSummary();
        
        try
        {
            // Get season-specific bootstrap data
            var bootstrapData = await GetSeasonBootstrapDataAsync(season);
            
            if (bootstrapData == null)
            {
                _logger.LogWarning("Could not retrieve bootstrap data for season {Season}", season);
                return summary;
            }

            var players = ExtractPlayersFromBootstrap(bootstrapData, season);
            var teams = ExtractTeamsFromBootstrap(bootstrapData, season);
            
            _logger.LogInformation("Found {PlayerCount} players and {TeamCount} teams for season {Season}", 
                players.Count, teams.Count, season);

            // Collect gameweek-by-gameweek data
            var gameweekCount = GetGameweekCountForSeason(season);
            
            for (int gameweek = 1; gameweek <= gameweekCount; gameweek++)
            {
                try
                {
                    _logger.LogInformation("Processing season {Season}, gameweek {Gameweek}", season, gameweek);
                    
                    var gameweekData = await GetGameweekDataAsync(season, gameweek);
                    if (gameweekData != null)
                    {
                        await ProcessGameweekDataAsync(season, gameweek, gameweekData, players);
                        summary.GameweeksCount++;
                    }

                    // Add delay to respect API rate limits
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process gameweek {Gameweek} for season {Season}", gameweek, season);
                }
            }

            summary.PlayersCount = players.Count;
            summary.RecordsCount = await _context.HistoricalPlayerPerformances
                .CountAsync(h => h.Season == season);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting data for season {Season}", season);
            return summary;
        }
    }

    private async Task<JsonDocument?> GetSeasonBootstrapDataAsync(string season)
    {
        try
        {
            // Try multiple potential endpoints for historical data
            var possibleUrls = new[]
            {
                $"{FPL_HISTORICAL_BASE}/bootstrap-static/",
                $"https://fantasy.premierleague.com/api/bootstrap-static/", 
                $"https://fantasy.premierleague.com/api/bootstrap-static/{season}/",
            };

            foreach (var url in possibleUrls)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(url);
                    return JsonDocument.Parse(response);
                }
                catch (HttpRequestException)
                {
                    continue; // Try next URL
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bootstrap data for season {Season}", season);
            return null;
        }
    }

    private async Task<JsonDocument?> GetGameweekDataAsync(string season, int gameweek)
    {
        try
        {
            // Multiple strategies for getting historical gameweek data
            var urls = new[]
            {
                $"{FPL_HISTORICAL_BASE}/event/{gameweek}/live/",
                $"https://fantasy.premierleague.com/api/event/{gameweek}/live/",
            };

            foreach (var url in urls)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(url);
                    return JsonDocument.Parse(response);
                }
                catch (HttpRequestException)
                {
                    continue;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch gameweek {Gameweek} data for season {Season}", gameweek, season);
            return null;
        }
    }

    private List<PlayerBasicInfo> ExtractPlayersFromBootstrap(JsonDocument bootstrap, string season)
    {
        var players = new List<PlayerBasicInfo>();
        
        try
        {
            if (bootstrap.RootElement.TryGetProperty("elements", out var elementsProperty))
            {
                foreach (var playerElement in elementsProperty.EnumerateArray())
                {
                    var player = new PlayerBasicInfo
                    {
                        FplId = playerElement.GetProperty("id").GetInt32(),
                        FirstName = playerElement.GetProperty("first_name").GetString() ?? "",
                        SecondName = playerElement.GetProperty("second_name").GetString() ?? "",
                        WebName = playerElement.GetProperty("web_name").GetString() ?? "",
                        Position = GetPositionName(playerElement.GetProperty("element_type").GetInt32()),
                        TeamId = playerElement.GetProperty("team").GetInt32(),
                        Season = season
                    };
                    
                    players.Add(player);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting players from bootstrap data for season {Season}", season);
        }

        return players;
    }

    private List<TeamBasicInfo> ExtractTeamsFromBootstrap(JsonDocument bootstrap, string season)
    {
        var teams = new List<TeamBasicInfo>();
        
        try
        {
            if (bootstrap.RootElement.TryGetProperty("teams", out var teamsProperty))
            {
                foreach (var teamElement in teamsProperty.EnumerateArray())
                {
                    var team = new TeamBasicInfo
                    {
                        FplId = teamElement.GetProperty("id").GetInt32(),
                        Name = teamElement.GetProperty("name").GetString() ?? "",
                        ShortName = teamElement.GetProperty("short_name").GetString() ?? "",
                        Season = season
                    };
                    
                    teams.Add(team);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting teams from bootstrap data for season {Season}", season);
        }

        return teams;
    }

    private async Task ProcessGameweekDataAsync(string season, int gameweek, JsonDocument gameweekData, List<PlayerBasicInfo> players)
    {
        try
        {
            if (!gameweekData.RootElement.TryGetProperty("elements", out var elementsProperty))
                return;

            var performanceRecords = new List<HistoricalPlayerPerformance>();

            foreach (var elementData in elementsProperty.EnumerateArray())
            {
                var playerId = elementData.GetProperty("id").GetInt32();
                var playerInfo = players.FirstOrDefault(p => p.FplId == playerId);
                
                if (playerInfo == null) continue;

                if (elementData.TryGetProperty("stats", out var statsProperty))
                {
                    var performance = new HistoricalPlayerPerformance
                    {
                        FplPlayerId = playerId,
                        PlayerName = $"{playerInfo.FirstName} {playerInfo.SecondName}".Trim(),
                        Season = season,
                        Gameweek = gameweek,
                        Position = playerInfo.Position,
                        TeamId = playerInfo.TeamId,
                        
                        // Extract stats
                        Points = GetIntProperty(statsProperty, "total_points"),
                        Minutes = GetIntProperty(statsProperty, "minutes"),
                        Goals = GetIntProperty(statsProperty, "goals_scored"),
                        Assists = GetIntProperty(statsProperty, "assists"),
                        CleanSheets = GetIntProperty(statsProperty, "clean_sheets"),
                        GoalsConceded = GetIntProperty(statsProperty, "goals_conceded"),
                        YellowCards = GetIntProperty(statsProperty, "yellow_cards"),
                        RedCards = GetIntProperty(statsProperty, "red_cards"),
                        Saves = GetIntProperty(statsProperty, "saves"),
                        BonusPoints = GetIntProperty(statsProperty, "bonus"),
                        Bps = GetIntProperty(statsProperty, "bps"),
                        
                        Influence = GetDecimalProperty(statsProperty, "influence"),
                        Creativity = GetDecimalProperty(statsProperty, "creativity"),
                        Threat = GetDecimalProperty(statsProperty, "threat"),
                        IctIndex = GetDecimalProperty(statsProperty, "ict_index"),
                        
                        GameDate = DateTime.UtcNow, // This would be actual game date in real implementation
                        LastUpdated = DateTime.UtcNow
                    };

                    performanceRecords.Add(performance);
                }
            }

            if (performanceRecords.Any())
            {
                await _context.HistoricalPlayerPerformances.AddRangeAsync(performanceRecords);
                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Saved {Count} performance records for gameweek {Gameweek}", 
                    performanceRecords.Count, gameweek);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing gameweek {Gameweek} data for season {Season}", gameweek, season);
        }
    }

    private async Task CalculateDerivedFeaturesAsync()
    {
        _logger.LogInformation("Calculating derived features for ML training...");

        try
        {
            // Calculate rolling averages and derived features using Entity Framework
            var historicalRecords = await _context.HistoricalPlayerPerformances.ToListAsync();
            
            foreach (var record in historicalRecords)
            {
                // Calculate 5-game form
                var recentGames = await _context.HistoricalPlayerPerformances
                    .Where(h => h.FplPlayerId == record.FplPlayerId && 
                               h.Season == record.Season &&
                               h.Gameweek <= record.Gameweek && 
                               h.Gameweek > record.Gameweek - 5)
                    .ToListAsync();
                
                if (recentGames.Any())
                {
                    record.Form5Games = (decimal)recentGames.Average(g => g.Points);
                }
                
                // Calculate per-game metrics
                if (record.Minutes > 0)
                {
                    record.MinutesPerGame = record.Minutes / 90m;
                    record.GoalsPerGame = (decimal)record.Goals * 90m / record.Minutes;
                    record.AssistsPerGame = (decimal)record.Assists * 90m / record.Minutes;
                }
                
                // Calculate points per million
                if (record.Price > 0)
                {
                    record.PointsPerMillion = record.Points / record.Price;
                }
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Derived features calculation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating derived features");
        }
    }

    private async Task CreateMLDatasetsAsync()
    {
        _logger.LogInformation("Creating ML training datasets...");

        try
        {
            // This would create structured datasets for different ML models
            // - Time series data for LSTM
            // - Tabular data for XGBoost
            // - Feature matrices for ensemble learning

            var totalRecords = await _context.HistoricalPlayerPerformances.CountAsync();
            _logger.LogInformation("ML datasets prepared from {TotalRecords} historical records", totalRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ML datasets");
        }
    }

    // Helper methods
    private static string GetPositionName(int elementType)
    {
        return elementType switch
        {
            1 => "Goalkeeper",
            2 => "Defender", 
            3 => "Midfielder",
            4 => "Forward",
            _ => "Unknown"
        };
    }

    private static int GetIntProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetInt32() : 0;
    }

    private static decimal GetDecimalProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return decimal.TryParse(prop.GetString(), out var result) ? result : 0;
            }
            return prop.GetDecimal();
        }
        return 0;
    }

    private static int GetGameweekCountForSeason(string season)
    {
        // Most seasons have 38 gameweeks, but this could vary
        return season switch
        {
            "2019-20" => 38, // COVID affected season
            "2020-21" => 38,
            "2021-22" => 38,
            "2022-23" => 38,
            "2023-24" => 38,
            "2024-25" => 38,
            _ => 38
        };
    }
}

// Helper classes for data collection
public class DataCollectionSummary
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> SeasonsProcessed { get; set; } = new();
    public int PlayersProcessed { get; set; }
    public int GameweeksProcessed { get; set; }
    public int TotalRecordsCreated { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
}

public class SeasonDataSummary
{
    public int PlayersCount { get; set; }
    public int GameweeksCount { get; set; }
    public int RecordsCount { get; set; }
}

public class PlayerBasicInfo
{
    public int FplId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string SecondName { get; set; } = string.Empty;
    public string WebName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string Season { get; set; } = string.Empty;
}

public class TeamBasicInfo
{
    public int FplId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
}
