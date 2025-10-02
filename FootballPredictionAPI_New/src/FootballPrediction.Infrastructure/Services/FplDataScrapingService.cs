using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.Json;

namespace FootballPrediction.Infrastructure.Services;

public class FplDataScrapingService
{
    private readonly FplDbContext _context;
    private readonly ILogger<FplDataScrapingService> _logger;
    private readonly HttpClient _httpClient;
    private const string FPL_API_BASE_URL = "https://fantasy.premierleague.com/api";
    private const string PREMIER_LEAGUE_URL = "https://www.premierleague.com";

    public FplDataScrapingService(FplDbContext context, ILogger<FplDataScrapingService> logger, HttpClient httpClient)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task ScrapeBootstrapDataAsync()
    {
        try
        {
            _logger.LogInformation("Starting FPL bootstrap data scraping...");
            
            var response = await _httpClient.GetStringAsync($"{FPL_API_BASE_URL}/bootstrap-static/");
            var data = JsonDocument.Parse(response);
            
            await ProcessTeamsAsync(data.RootElement.GetProperty("teams"));
            await ProcessPlayersAsync(data.RootElement.GetProperty("elements"));
            await ProcessFixturesAsync();
            
            _logger.LogInformation("Completed FPL bootstrap data scraping successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while scraping FPL bootstrap data");
            throw;
        }
    }

    private async Task ProcessTeamsAsync(JsonElement teamsData)
    {
        _logger.LogInformation("Processing teams data...");
        
        foreach (var teamElement in teamsData.EnumerateArray())
        {
            try
            {
                var fplId = teamElement.GetProperty("id").GetInt32();
                var existingTeam = await _context.Teams.FirstOrDefaultAsync(t => t.FplId == fplId);
                
                if (existingTeam == null)
                {
                    var team = new Team
                    {
                        FplId = fplId,
                        Name = GetStringProperty(teamElement, "name"),
                        ShortName = GetStringProperty(teamElement, "short_name"),
                        Code = GetIntProperty(teamElement, "code"),
                        Strength = GetIntProperty(teamElement, "strength"),
                        StrengthOverallHome = GetIntProperty(teamElement, "strength_overall_home"),
                        StrengthOverallAway = GetIntProperty(teamElement, "strength_overall_away"),
                        StrengthAttackHome = GetIntProperty(teamElement, "strength_attack_home"),
                        StrengthAttackAway = GetIntProperty(teamElement, "strength_attack_away"),
                        StrengthDefenceHome = GetIntProperty(teamElement, "strength_defence_home"),
                        StrengthDefenceAway = GetIntProperty(teamElement, "strength_defence_away"),
                        Position = GetIntProperty(teamElement, "position"),
                        Played = GetIntProperty(teamElement, "played"),
                        Win = GetIntProperty(teamElement, "win"),
                        Draw = GetIntProperty(teamElement, "draw"),
                        Loss = GetIntProperty(teamElement, "loss"),
                        Points = GetIntProperty(teamElement, "points"),
                        GoalsFor = GetIntProperty(teamElement, "goals_for"),
                        GoalsAgainst = GetIntProperty(teamElement, "goals_against"),
                        GoalDifference = GetIntProperty(teamElement, "goal_difference"),
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    _context.Teams.Add(team);
                }
                else
                {
                    // Update existing team
                    existingTeam.Name = GetStringProperty(teamElement, "name");
                    existingTeam.Strength = GetIntProperty(teamElement, "strength");
                    existingTeam.Position = GetIntProperty(teamElement, "position");
                    existingTeam.Played = GetIntProperty(teamElement, "played");
                    existingTeam.Win = GetIntProperty(teamElement, "win");
                    existingTeam.Draw = GetIntProperty(teamElement, "draw");
                    existingTeam.Loss = GetIntProperty(teamElement, "loss");
                    existingTeam.Points = GetIntProperty(teamElement, "points");
                    existingTeam.GoalsFor = GetIntProperty(teamElement, "goals_for");
                    existingTeam.GoalsAgainst = GetIntProperty(teamElement, "goals_against");
                    existingTeam.GoalDifference = GetIntProperty(teamElement, "goal_difference");
                    existingTeam.LastUpdated = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing team data for FPL ID: {FplId}", 
                    teamElement.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : -1);
                continue; // Skip this team and continue with the next one
            }
        }
        
        await _context.SaveChangesAsync();
        _logger.LogInformation("Teams data processing completed");
    }

    private async Task ProcessPlayersAsync(JsonElement playersData)
    {
        _logger.LogInformation("Processing players data...");
        
        foreach (var playerElement in playersData.EnumerateArray())
        {
            try
            {
                var fplId = GetIntProperty(playerElement, "id");
                if (fplId == 0) continue; // Skip if no valid ID
                
                var existingPlayer = await _context.Players.FirstOrDefaultAsync(p => p.FplId == fplId);
                
                if (existingPlayer == null)
                {
                    var player = new Player
                    {
                        FplId = fplId,
                        FirstName = GetStringProperty(playerElement, "first_name"),
                        SecondName = GetStringProperty(playerElement, "second_name"),
                        WebName = GetStringProperty(playerElement, "web_name"),
                        TeamId = GetIntProperty(playerElement, "team"),
                        Position = GetPositionName(GetIntProperty(playerElement, "element_type")),
                        Price = GetDecimalProperty(playerElement, "now_cost") / 10,
                        TotalPoints = GetIntProperty(playerElement, "total_points"),
                        PointsPerGame = GetDecimalProperty(playerElement, "points_per_game"),
                        Form = GetDecimalProperty(playerElement, "form"),
                        TransfersIn = GetIntProperty(playerElement, "transfers_in"),
                        TransfersOut = GetIntProperty(playerElement, "transfers_out"),
                        SelectedByPercent = GetDecimalProperty(playerElement, "selected_by_percent"),
                        ValueForm = GetDecimalProperty(playerElement, "value_form"),
                        ValueSeason = GetDecimalProperty(playerElement, "value_season"),
                        Minutes = GetIntProperty(playerElement, "minutes"),
                        Goals = GetIntProperty(playerElement, "goals_scored"),
                        Assists = GetIntProperty(playerElement, "assists"),
                        CleanSheets = GetIntProperty(playerElement, "clean_sheets"),
                        GoalsConceded = GetIntProperty(playerElement, "goals_conceded"),
                        YellowCards = GetIntProperty(playerElement, "yellow_cards"),
                        RedCards = GetIntProperty(playerElement, "red_cards"),
                        Saves = GetIntProperty(playerElement, "saves"),
                        BonusPoints = GetIntProperty(playerElement, "bonus"),
                        Bps = GetIntProperty(playerElement, "bps"),
                        Influence = GetDecimalProperty(playerElement, "influence"),
                        Creativity = GetDecimalProperty(playerElement, "creativity"),
                        Threat = GetDecimalProperty(playerElement, "threat"),
                        IctIndex = GetDecimalProperty(playerElement, "ict_index"),
                        Status = GetStringProperty(playerElement, "status"),
                        News = GetStringProperty(playerElement, "news"),
                        NewsAdded = ParseDateTime(GetStringProperty(playerElement, "news_added")),
                        ChanceOfPlayingNextRound = (int?)GetDecimalProperty(playerElement, "chance_of_playing_next_round"),
                        ChanceOfPlayingThisRound = (int?)GetDecimalProperty(playerElement, "chance_of_playing_this_round"),
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    _context.Players.Add(player);
                }
                else
                {
                    // Update existing player
                    UpdateExistingPlayer(existingPlayer, playerElement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing player data for FPL ID: {FplId}", 
                    GetIntProperty(playerElement, "id"));
                continue; // Skip this player and continue with the next one
            }
        }
        
        await _context.SaveChangesAsync();
        _logger.LogInformation("Players data processing completed");
    }

    private void UpdateExistingPlayer(Player player, JsonElement playerElement)
    {
        try
        {
            player.Price = GetDecimalProperty(playerElement, "now_cost") / 10;
            player.TotalPoints = GetIntProperty(playerElement, "total_points");
            player.PointsPerGame = GetDecimalProperty(playerElement, "points_per_game");
            player.Form = GetDecimalProperty(playerElement, "form");
            player.TransfersIn = GetIntProperty(playerElement, "transfers_in");
            player.TransfersOut = GetIntProperty(playerElement, "transfers_out");
            player.SelectedByPercent = GetDecimalProperty(playerElement, "selected_by_percent");
            player.ValueForm = GetDecimalProperty(playerElement, "value_form");
            player.ValueSeason = GetDecimalProperty(playerElement, "value_season");
            player.Minutes = GetIntProperty(playerElement, "minutes");
            player.Goals = GetIntProperty(playerElement, "goals_scored");
            player.Assists = GetIntProperty(playerElement, "assists");
            player.CleanSheets = GetIntProperty(playerElement, "clean_sheets");
            player.GoalsConceded = GetIntProperty(playerElement, "goals_conceded");
            player.YellowCards = GetIntProperty(playerElement, "yellow_cards");
            player.RedCards = GetIntProperty(playerElement, "red_cards");
            player.Saves = GetIntProperty(playerElement, "saves");
            player.BonusPoints = GetIntProperty(playerElement, "bonus");
            player.Bps = GetIntProperty(playerElement, "bps");
            player.Influence = GetDecimalProperty(playerElement, "influence");
            player.Creativity = GetDecimalProperty(playerElement, "creativity");
            player.Threat = GetDecimalProperty(playerElement, "threat");
            player.IctIndex = GetDecimalProperty(playerElement, "ict_index");
            player.Status = GetStringProperty(playerElement, "status");
            player.News = GetStringProperty(playerElement, "news");
            player.NewsAdded = ParseDateTime(GetStringProperty(playerElement, "news_added"));
            player.ChanceOfPlayingNextRound = (int?)GetDecimalProperty(playerElement, "chance_of_playing_next_round");
            player.ChanceOfPlayingThisRound = (int?)GetDecimalProperty(playerElement, "chance_of_playing_this_round");
            player.LastUpdated = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating player data for ID: {PlayerId}", player.Id);
        }
    }

    private async Task ProcessFixturesAsync()
    {
        try
        {
            _logger.LogInformation("Processing fixtures data...");
            
            var response = await _httpClient.GetStringAsync($"{FPL_API_BASE_URL}/fixtures/");
            var fixturesData = JsonDocument.Parse(response);
            
            foreach (var fixtureElement in fixturesData.RootElement.EnumerateArray())
            {
                var fplId = fixtureElement.GetProperty("id").GetInt32();
                var existingFixture = await _context.Fixtures.FirstOrDefaultAsync(f => f.FplId == fplId);
                
                if (existingFixture == null)
                {
                    var fixture = new Fixture
                    {
                        FplId = fplId,
                        Gameweek = fixtureElement.GetProperty("event").GetInt32(),
                        KickoffTime = DateTime.Parse(fixtureElement.GetProperty("kickoff_time").GetString() ?? DateTime.Now.ToString()),
                        TeamHomeId = fixtureElement.GetProperty("team_h").GetInt32(),
                        TeamAwayId = fixtureElement.GetProperty("team_a").GetInt32(),
                        TeamHomeScore = fixtureElement.TryGetProperty("team_h_score", out var homeScore) && homeScore.ValueKind != JsonValueKind.Null
                            ? homeScore.GetInt32() : null,
                        TeamAwayScore = fixtureElement.TryGetProperty("team_a_score", out var awayScore) && awayScore.ValueKind != JsonValueKind.Null
                            ? awayScore.GetInt32() : null,
                        Finished = fixtureElement.GetProperty("finished").GetBoolean(),
                        Minutes = fixtureElement.GetProperty("minutes").GetInt32(),
                        ProvisionalStartTime = fixtureElement.GetProperty("provisional_start_time").GetBoolean(),
                        Started = fixtureElement.GetProperty("started").GetBoolean(),
                        Difficulty = fixtureElement.GetProperty("team_h_difficulty").GetInt32(),
                        LastUpdated = DateTime.UtcNow
                    };
                    
                    _context.Fixtures.Add(fixture);
                }
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation("Fixtures data processing completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing fixtures data");
        }
    }

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

    private static DateTime? ParseDateTime(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString) || dateString == "null")
            return null;
            
        return DateTime.TryParse(dateString, out var result) ? result : null;
    }

    // Helper methods for safe property access
    private static string GetStringProperty(JsonElement element, string propertyName, string defaultValue = "")
    {
        return element.TryGetProperty(propertyName, out var prop) ? (prop.GetString() ?? defaultValue) : defaultValue;
    }

    private static int GetIntProperty(JsonElement element, string propertyName, int defaultValue = 0)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetInt32() : defaultValue;
    }

    private static decimal GetDecimalProperty(JsonElement element, string propertyName, decimal defaultValue = 0m)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return decimal.TryParse(prop.GetString(), out var result) ? result : defaultValue;
            }
            return prop.GetDecimal();
        }
        return defaultValue;
    }
}
