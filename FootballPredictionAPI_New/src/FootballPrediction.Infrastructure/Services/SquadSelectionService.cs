using FootballPrediction.Core.Entities;
using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services.MLModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services;

/// <summary>
/// Service for selecting optimal FPL squad based on ML predictions
/// Uses real ML models to pick the best 15-player squad within budget
/// </summary>
public class SquadSelectionService
{
    private readonly FplDbContext _context;
    private readonly ILogger<SquadSelectionService> _logger;
    private readonly RealMLPredictionService _realMLService;
    private readonly RealXGBoostPredictionService _lightGbmService;
    private readonly RealLSTMPredictionService _timeSeriesService;

    // FPL Squad Rules
    private const decimal MAX_BUDGET = 100.0m;
    private const int TOTAL_PLAYERS = 15;
    private const int GOALKEEPERS = 2;
    private const int DEFENDERS = 5;
    private const int MIDFIELDERS = 5;
    private const int FORWARDS = 3;
    private const int MAX_PLAYERS_PER_TEAM = 3;

    public SquadSelectionService(
        FplDbContext context,
        ILogger<SquadSelectionService> logger,
        RealMLPredictionService realMLService,
        RealXGBoostPredictionService lightGbmService,
        RealLSTMPredictionService timeSeriesService)
    {
        _context = context;
        _logger = logger;
        _realMLService = realMLService;
        _lightGbmService = lightGbmService;
        _timeSeriesService = timeSeriesService;
    }

    /// <summary>
    /// Select optimal squad for a gameweek using ML predictions
    /// </summary>
    public async Task<SquadSelectionResult> SelectOptimalSquadAsync(int gameweek, SquadSelectionStrategy strategy = SquadSelectionStrategy.Balanced)
    {
        _logger.LogInformation("🎯 Starting squad selection for gameweek {Gameweek} using strategy: {Strategy}", 
            gameweek, strategy);

        var startTime = DateTime.UtcNow;
        var result = new SquadSelectionResult
        {
            Gameweek = gameweek,
            Strategy = strategy.ToString(),
            SelectionDate = DateTime.UtcNow
        };

        try
        {
            // Step 1: Get all available players
            var allPlayers = await GetAvailablePlayersAsync();
            _logger.LogInformation("📊 Found {Count} available players", allPlayers.Count);

            if (allPlayers.Count < TOTAL_PLAYERS)
            {
                result.Success = false;
                result.Message = "Insufficient players in database";
                return result;
            }

            // Step 2: Get ML predictions for all players
            var playerPredictions = await GetPlayerPredictionsAsync(allPlayers, gameweek, strategy);
            _logger.LogInformation("🤖 Generated {Count} ML predictions", playerPredictions.Count);

            // Step 3: Select optimal squad using algorithm
            var selectedSquad = await SelectSquadWithConstraintsAsync(playerPredictions, strategy);

            if (selectedSquad.Count != TOTAL_PLAYERS)
            {
                result.Success = false;
                result.Message = $"Could not select full squad. Only selected {selectedSquad.Count} players";
                return result;
            }

            // Step 4: Build result
            result.Success = true;
            result.SelectedPlayers = selectedSquad;
            result.TotalCost = selectedSquad.Sum(p => p.Price);
            result.PredictedTotalPoints = selectedSquad.Sum(p => p.PredictedPoints);
            result.RemainingBudget = MAX_BUDGET - result.TotalCost;
            result.SelectionDuration = DateTime.UtcNow - startTime;

            // Step 5: Suggest starting XI
            result.SuggestedStartingXI = SelectStartingXI(selectedSquad);
            result.Captain = SelectCaptain(result.SuggestedStartingXI);
            result.ViceCaptain = SelectViceCaptain(result.SuggestedStartingXI, result.Captain);

            result.Message = $"✅ Successfully selected optimal squad with {result.TotalCost:F1}m spent";

            _logger.LogInformation("✅ Squad selection completed in {Duration:F2}s - Total Cost: £{Cost}m, Predicted Points: {Points:F1}",
                result.SelectionDuration.TotalSeconds, result.TotalCost, result.PredictedTotalPoints);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during squad selection");
            result.Success = false;
            result.Message = $"Selection failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Get all available players (not injured/suspended)
    /// </summary>
    private async Task<List<Player>> GetAvailablePlayersAsync()
    {
        return await _context.Players
            .Include(p => p.Team)
            .Where(p => p.Status == "a" || p.Status == "d") // Available or doubtful
            .Where(p => p.Price > 0) // Valid price
            .OrderByDescending(p => p.TotalPoints)
            .ToListAsync();
    }

    /// <summary>
    /// Get ML predictions for all players
    /// </summary>
    private async Task<List<PlayerSquadPrediction>> GetPlayerPredictionsAsync(
        List<Player> players, 
        int gameweek,
        SquadSelectionStrategy strategy)
    {
        var predictions = new List<PlayerSquadPrediction>();

        foreach (var player in players)
        {
            try
            {
                // Get predictions from different models based on strategy
                double predictedPoints = 0;
                double confidence = 0;

                switch (strategy)
                {
                    case SquadSelectionStrategy.Balanced:
                        // Use ensemble of all three models
                        predictedPoints = await GetEnsemblePrediction(player, gameweek);
                        confidence = 0.85;
                        break;

                    case SquadSelectionStrategy.Aggressive:
                        // Use LightGBM (best for high scorers)
                        var lgbmFeatures = BuildMLInputFeatures(player);
                        var lgbmResult = await _lightGbmService.PredictAsync(lgbmFeatures);
                        predictedPoints = lgbmResult.PredictedPoints * 1.1; // Boost predictions
                        confidence = lgbmResult.Confidence;
                        break;

                    case SquadSelectionStrategy.Conservative:
                        // Use FastTree (most stable)
                        var ftResult = await _realMLService.PredictPlayerPointsAsync(player, gameweek);
                        predictedPoints = ftResult.PredictedPoints * 0.9; // Conservative estimate
                        confidence = ftResult.Confidence;
                        break;

                    case SquadSelectionStrategy.ValueHunting:
                        // Focus on points per million
                        predictedPoints = await GetEnsemblePrediction(player, gameweek);
                        // Boost value players
                        var pointsPerMillion = predictedPoints / (double)player.Price;
                        if (pointsPerMillion > 0.6) predictedPoints *= 1.15;
                        confidence = 0.80;
                        break;
                }

                predictions.Add(new PlayerSquadPrediction
                {
                    PlayerId = player.Id,
                    PlayerName = player.WebName,
                    Position = player.Position,
                    Team = player.Team?.Name ?? "Unknown",
                    TeamId = player.TeamId,
                    Price = player.Price,
                    PredictedPoints = Math.Round(predictedPoints, 2),
                    Confidence = confidence,
                    Form = player.Form,
                    PointsPerMillion = predictedPoints / (double)player.Price,
                    Status = player.Status,
                    SelectedByPercent = player.SelectedByPercent
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get prediction for player {PlayerId}", player.Id);
            }
        }

        return predictions;
    }

    /// <summary>
    /// Get ensemble prediction from all three models
    /// </summary>
    private async Task<double> GetEnsemblePrediction(Player player, int gameweek)
    {
        var predictions = new List<double>();

        try
        {
            // FastTree
            var ftResult = await _realMLService.PredictPlayerPointsAsync(player, gameweek);
            predictions.Add(ftResult.PredictedPoints);

            // LightGBM
            var lgbmFeatures = BuildMLInputFeatures(player);
            var lgbmResult = await _lightGbmService.PredictAsync(lgbmFeatures);
            predictions.Add(lgbmResult.PredictedPoints);

            // Time Series
            var tsResult = await _timeSeriesService.PredictAsync(lgbmFeatures);
            predictions.Add(tsResult.PredictedPoints);

            // Weighted average: LightGBM 40%, FastTree 35%, TimeSeries 25%
            return predictions[1] * 0.40 + predictions[0] * 0.35 + predictions[2] * 0.25;
        }
        catch
        {
            // Fallback to player form if predictions fail
            return (double)player.Form;
        }
    }

    /// <summary>
    /// Select squad with FPL constraints (budget, positions, max per team)
    /// </summary>
    private async Task<List<PlayerSquadPrediction>> SelectSquadWithConstraintsAsync(
        List<PlayerSquadPrediction> predictions,
        SquadSelectionStrategy strategy)
    {
        var selectedSquad = new List<PlayerSquadPrediction>();
        decimal currentCost = 0;

        // Sort by value (points per million) for better selection
        var sortedPredictions = strategy == SquadSelectionStrategy.ValueHunting
            ? predictions.OrderByDescending(p => p.PointsPerMillion).ToList()
            : predictions.OrderByDescending(p => p.PredictedPoints * p.Confidence).ToList();

        // Select by position
        var gkSelection = SelectByPosition(sortedPredictions, "Goalkeeper", GOALKEEPERS, currentCost);
        selectedSquad.AddRange(gkSelection.players);
        currentCost = gkSelection.newCost;

        var defSelection = SelectByPosition(sortedPredictions, "Defender", DEFENDERS, currentCost);
        selectedSquad.AddRange(defSelection.players);
        currentCost = defSelection.newCost;

        var midSelection = SelectByPosition(sortedPredictions, "Midfielder", MIDFIELDERS, currentCost);
        selectedSquad.AddRange(midSelection.players);
        currentCost = midSelection.newCost;

        var fwdSelection = SelectByPosition(sortedPredictions, "Forward", FORWARDS, currentCost);
        selectedSquad.AddRange(fwdSelection.players);

        return selectedSquad;
    }

    /// <summary>
    /// Select players for a specific position
    /// </summary>
    private (List<PlayerSquadPrediction> players, decimal newCost) SelectByPosition(
        List<PlayerSquadPrediction> allPlayers,
        string position,
        int count,
        decimal currentCost)
    {
        var selected = new List<PlayerSquadPrediction>();
        var teamCounts = new Dictionary<int, int>();

        var positionPlayers = allPlayers
            .Where(p => p.Position == position)
            .Where(p => currentCost + p.Price <= MAX_BUDGET)
            .ToList();

        foreach (var player in positionPlayers)
        {
            if (selected.Count >= count) break;

            // Check max players per team constraint
            if (!teamCounts.ContainsKey(player.TeamId))
                teamCounts[player.TeamId] = 0;

            if (teamCounts[player.TeamId] >= MAX_PLAYERS_PER_TEAM)
                continue;

            // Check budget constraint
            if (currentCost + player.Price > MAX_BUDGET)
                continue;

            selected.Add(player);
            currentCost += player.Price;
            teamCounts[player.TeamId]++;
        }

        return (selected, currentCost);
    }

    /// <summary>
    /// Select best starting XI from the 15-player squad
    /// </summary>
    private List<PlayerSquadPrediction> SelectStartingXI(List<PlayerSquadPrediction> squad)
    {
        var startingXI = new List<PlayerSquadPrediction>();

        // Formation: 1 GK, 3-5 DEF, 2-5 MID, 1-3 FWD (must total 11)
        // Select best 1 GK
        startingXI.AddRange(squad.Where(p => p.Position == "Goalkeeper")
            .OrderByDescending(p => p.PredictedPoints).Take(1));

        // Select best 4 DEF
        startingXI.AddRange(squad.Where(p => p.Position == "Defender")
            .OrderByDescending(p => p.PredictedPoints).Take(4));

        // Select best 4 MID
        startingXI.AddRange(squad.Where(p => p.Position == "Midfielder")
            .OrderByDescending(p => p.PredictedPoints).Take(4));

        // Select best 2 FWD
        startingXI.AddRange(squad.Where(p => p.Position == "Forward")
            .OrderByDescending(p => p.PredictedPoints).Take(2));

        return startingXI;
    }

    /// <summary>
    /// Select captain (highest predicted points in starting XI)
    /// </summary>
    private PlayerSquadPrediction? SelectCaptain(List<PlayerSquadPrediction> startingXI)
    {
        return startingXI.OrderByDescending(p => p.PredictedPoints * p.Confidence).FirstOrDefault();
    }

    /// <summary>
    /// Select vice captain (second highest predicted points)
    /// </summary>
    private PlayerSquadPrediction? SelectViceCaptain(List<PlayerSquadPrediction> startingXI, PlayerSquadPrediction? captain)
    {
        return startingXI
            .Where(p => captain == null || p.PlayerId != captain.PlayerId)
            .OrderByDescending(p => p.PredictedPoints * p.Confidence)
            .FirstOrDefault();
    }

    private MLInputFeatures BuildMLInputFeatures(Player player)
    {
        return new MLInputFeatures
        {
            PlayerId = player.Id,
            PlayerName = player.WebName,
            Position = player.Position,
            Form5Games = (double)player.Form,
            MinutesPerGame = 75, // Estimate
            Price = (double)player.Price,
            OwnershipPercent = (double)player.SelectedByPercent,
            OpponentStrength = 10,
            IsHome = true,
            InjuryRisk = player.Status == "d" ? 0.3 : 0.0,
            HistoricalPoints = new List<double>()
        };
    }
}

/// <summary>
/// Squad selection strategies
/// </summary>
public enum SquadSelectionStrategy
{
    Balanced,       // Use ensemble of all models
    Aggressive,     // Focus on high scorers
    Conservative,   // Play it safe
    ValueHunting    // Points per million optimization
}

/// <summary>
/// Player prediction for squad selection
/// </summary>
public class PlayerSquadPrediction
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public decimal Price { get; set; }
    public double PredictedPoints { get; set; }
    public double Confidence { get; set; }
    public decimal Form { get; set; }
    public double PointsPerMillion { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal SelectedByPercent { get; set; }
}

/// <summary>
/// Squad selection result
/// </summary>
public class SquadSelectionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Gameweek { get; set; }
    public string Strategy { get; set; } = string.Empty;
    public DateTime SelectionDate { get; set; }
    public TimeSpan SelectionDuration { get; set; }
    
    public List<PlayerSquadPrediction> SelectedPlayers { get; set; } = new();
    public List<PlayerSquadPrediction> SuggestedStartingXI { get; set; } = new();
    public PlayerSquadPrediction? Captain { get; set; }
    public PlayerSquadPrediction? ViceCaptain { get; set; }
    
    public decimal TotalCost { get; set; }
    public decimal RemainingBudget { get; set; }
    public double PredictedTotalPoints { get; set; }
    
    public Dictionary<string, int> PositionBreakdown => SelectedPlayers
        .GroupBy(p => p.Position)
        .ToDictionary(g => g.Key, g => g.Count());
    
    public Dictionary<string, int> TeamBreakdown => SelectedPlayers
        .GroupBy(p => p.Team)
        .ToDictionary(g => g.Key, g => g.Count());
}
