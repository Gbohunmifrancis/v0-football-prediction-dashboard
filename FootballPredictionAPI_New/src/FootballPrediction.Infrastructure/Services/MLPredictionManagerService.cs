using FootballPrediction.Core.Models;
using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services.MLModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services;

public class MLPredictionManagerService
{
    private readonly FplDbContext _context;
    private readonly ILogger<MLPredictionManagerService> _logger;
    private readonly EnsemblePredictionService _ensembleService;
    private readonly LSTMPredictionService _lstmService;
    private readonly XgBoostPredictionService _xgboostService;
    private bool _modelsInitialized;

    public MLPredictionManagerService(
        FplDbContext context,
        ILogger<MLPredictionManagerService> logger,
        EnsemblePredictionService ensembleService,
        LSTMPredictionService lstmService,
        XgBoostPredictionService xgboostService)
    {
        _context = context;
        _logger = logger;
        _ensembleService = ensembleService;
        _lstmService = lstmService;
        _xgboostService = xgboostService;
        _modelsInitialized = false;
    }

    /// <summary>
    /// Initialize and train all ML models using historical data
    /// This replaces the simple if/else logic with sophisticated ML predictions
    /// </summary>
    public async Task InitializeMLModelsAsync()
    {
        _logger.LogInformation("🚀 Initializing ML prediction models - replacing simple if/else logic");

        try
        {
            // Load training data from historical database
            var trainingData = await LoadTrainingDataAsync();
            
            if (trainingData.Count < 1000)
            {
                _logger.LogWarning("⚠️ Limited training data ({Count} records). Models may have reduced accuracy", trainingData.Count);
            }
            else
            {
                _logger.LogInformation("📊 Loaded {Count} training samples for ML model training", trainingData.Count);
            }

            // Train individual models in parallel for speed
            _logger.LogInformation("🧠 Training LSTM (Time Series Neural Network)...");
            var lstmTask = _lstmService.TrainAsync(trainingData);

            _logger.LogInformation("🌳 Training XGBoost (Gradient Boosting Trees)...");
            var xgboostTask = _xgboostService.TrainAsync(trainingData);

            // Wait for both models to complete training
            await Task.WhenAll(lstmTask, xgboostTask);

            // Train ensemble to combine models intelligently
            _logger.LogInformation("🎯 Training Ensemble (Smart Model Combination)...");
            await _ensembleService.TrainEnsembleAsync(trainingData);

            _modelsInitialized = true;
            _logger.LogInformation("✅ ML Models successfully initialized and trained!");
            _logger.LogInformation("🔥 Advanced ML prediction system is now ACTIVE - no more simple if/else logic!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error initializing ML models");
            throw;
        }
    }

    /// <summary>
    /// Generate ML-powered predictions for a gameweek (replaces the old PlayerPredictionService logic)
    /// </summary>
    public async Task<GameweekPredictionResult> GenerateMLGameweekPredictionsAsync(int gameweek)
    {
        _logger.LogInformation("🎯 Generating ML-powered predictions for gameweek {Gameweek}", gameweek);

        if (!_modelsInitialized)
        {
            _logger.LogWarning("⚠️ ML models not initialized, initializing now...");
            await InitializeMLModelsAsync();
        }

        try
        {
            var result = new GameweekPredictionResult
            {
                Gameweek = gameweek,
                PredictionDate = DateTime.UtcNow,
                ModelType = "Advanced ML Ensemble"
            };

            // Get all players (Status values: "a" = available, "d" = doubtful, "i" = injured, "s" = suspended, "u" = unavailable)
            // First, try to get active players
            var players = await _context.Players
                .Include(p => p.Team)
                .Include(p => p.GameweekPerformances)
                .ToListAsync();

            _logger.LogInformation("📊 Total players in database: {TotalCount}", players.Count);

            // Filter out only truly unavailable players if we have data
            if (players.Any())
            {
                players = players
                    .Where(p => p.Status != "u" && p.Status != "s") // Exclude unavailable and suspended
                    .ToList();
                
                _logger.LogInformation("🔍 Generating predictions for {PlayerCount} available players using ML ensemble", players.Count);
                
                // Log some status examples
                var statusSample = players.Take(5).Select(p => new { p.Name, p.Status }).ToList();
                _logger.LogInformation("📝 Sample player statuses: {Statuses}", System.Text.Json.JsonSerializer.Serialize(statusSample));
            }
            else
            {
                _logger.LogWarning("⚠️ No players found in database!");
                return result;
            }

            var predictions = new List<PlayerPredictionResult>();
            var errorCount = 0;

            // Generate ML predictions for each player
            foreach (var player in players)
            {
                try
                {
                    var features = await BuildMLFeaturesForPlayer(player, gameweek);
                    var ensemblePrediction = await _ensembleService.PredictEnsembleAsync(features);
                    
                    // Convert EnsemblePredictionResult to PlayerPredictionResult
                    predictions.Add(new PlayerPredictionResult
                    {
                        PlayerId = ensemblePrediction.PlayerId,
                        PlayerName = ensemblePrediction.PlayerName,
                        Position = ensemblePrediction.Position,
                        PredictedPoints = (double)ensemblePrediction.FinalPrediction,
                        Confidence = (double)ensemblePrediction.Confidence,
                        ModelUsed = "Ensemble",
                        FeatureImportance = string.Join(", ", ensemblePrediction.RiskFactors)
                    });
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogWarning(ex, "Error predicting for player {PlayerName} (ID: {PlayerId})", player.WebName, player.Id);
                }
            }

            _logger.LogInformation("✅ Generated {SuccessCount} predictions ({ErrorCount} errors)", predictions.Count, errorCount);

            // Categorize predictions by position and performance
            result.TopPerformers = predictions.OrderByDescending(p => p.PredictedPoints * p.Confidence).Take(15).ToList();
            result.TopGoalkeepers = predictions.Where(p => p.Position == "Goalkeeper").OrderByDescending(p => p.PredictedPoints).Take(5).ToList();
            result.TopDefenders = predictions.Where(p => p.Position == "Defender").OrderByDescending(p => p.PredictedPoints).Take(5).ToList();
            result.TopMidfielders = predictions.Where(p => p.Position == "Midfielder").OrderByDescending(p => p.PredictedPoints).Take(5).ToList();
            result.TopForwards = predictions.Where(p => p.Position == "Forward").OrderByDescending(p => p.PredictedPoints).Take(5).ToList();

            // Best value picks (high points per price ratio)
            result.BestValue = predictions
                .Where(p => p.PredictedPoints > 4 && p.Confidence > 0.6)
                .OrderByDescending(p => p.PredictedPoints / GetPlayerPrice(p.PlayerId))
                .Take(10)
                .ToList();

            // Differential picks (low ownership, high predicted points)
            result.Differentials = predictions
                .Where(p => p.PredictedPoints > 6 && p.Confidence > 0.7)
                .OrderBy(p => GetPlayerOwnership(p.PlayerId))
                .Take(10)
                .ToList();

            // High risk players to avoid
            result.HighRisk = predictions
                .Where(p => p.Confidence < 0.4 || p.PredictedPoints < 2)
                .OrderBy(p => p.Confidence)
                .Take(10)
                .ToList();

            // Log summary if we have predictions
            if (result.TopPerformers.Any())
            {
                _logger.LogInformation("🎯 ML Predictions Complete - Top performer: {TopPlayer} ({Points:F1} pts, {Confidence:P1} confidence)", 
                    result.TopPerformers.First().PlayerName, 
                    result.TopPerformers.First().PredictedPoints,
                    result.TopPerformers.First().Confidence);
            }
            else
            {
                _logger.LogWarning("⚠️ ML Predictions Complete - No predictions generated. Database may be empty or no eligible players found.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ML gameweek predictions");
            throw;
        }
    }

    /// <summary>
    /// Get detailed ML prediction for a specific player
    /// </summary>
    public async Task<EnsemblePredictionResult> GetPlayerMLPredictionAsync(int playerId, int gameweek)
    {
        if (!_modelsInitialized)
        {
            await InitializeMLModelsAsync();
        }

        var player = await _context.Players
            .Include(p => p.Team)
            .Include(p => p.GameweekPerformances)
            .FirstOrDefaultAsync(p => p.Id == playerId);

        if (player == null)
        {
            throw new ArgumentException($"Player with ID {playerId} not found");
        }

        var features = await BuildMLFeaturesForPlayer(player, gameweek);
        return await _ensembleService.PredictEnsembleAsync(features);
    }

    /// <summary>
    /// Get model performance metrics
    /// </summary>
    public async Task<MLModelStatusReport> GetModelStatusAsync()
    {
        var report = new MLModelStatusReport
        {
            IsInitialized = _modelsInitialized,
            LastUpdated = DateTime.UtcNow,
            ModelConfidences = new Dictionary<string, double>
            {
                ["LSTM"] = _lstmService.GetModelConfidence(),
                ["XGBoost"] = _xgboostService.GetModelConfidence(),
                ["Ensemble"] = (_lstmService.GetModelConfidence() + _xgboostService.GetModelConfidence()) / 2
            }
        };

        if (_modelsInitialized)
        {
            var trainingDataCount = await _context.HistoricalPlayerPerformances.CountAsync();
            report.TrainingDataSize = trainingDataCount;
            report.Status = "✅ All ML models active and ready";
        }
        else
        {
            report.Status = "⚠️ ML models not initialized";
        }

        return report;
    }

    // Private helper methods
    private async Task<List<MLTrainingData>> LoadTrainingDataAsync()
    {
        var historicalData = await _context.HistoricalPlayerPerformances
            .Include(h => h.Player)
            .Where(h => h.Minutes > 0) // Only include games where player actually played
            .OrderBy(h => h.Season)
            .ThenBy(h => h.Gameweek)
            .ToListAsync();

        var trainingData = new List<MLTrainingData>();

        foreach (var record in historicalData)
        {
            var features = new MLInputFeatures
            {
                PlayerId = record.PlayerId,
                PlayerName = record.PlayerName,
                Position = record.Position,
                Gameweek = record.Gameweek,
                Form5Games = (double)record.Form5Games,
                MinutesPerGame = (double)record.MinutesPerGame,
                GoalsPerGame = (double)record.GoalsPerGame,
                AssistsPerGame = (double)record.AssistsPerGame,
                PointsPerMillion = (double)record.PointsPerMillion,
                OpponentStrength = record.OpponentStrength,
                IsHome = record.WasHome,
                Price = (double)record.Price,
                OwnershipPercent = (double)record.OwnershipPercent,
                // Set defaults for features not in historical data
                TeamForm = 5.0,
                TeamAttackStrength = 1.0,
                TeamDefenseStrength = 1.0,
                InjuryRisk = 0.1,
                FixtureDifficulty = record.OpponentStrength,
                SeasonalTrend = 0.0
            };

            // Add historical context if available
            var playerHistory = historicalData
                .Where(h => h.PlayerId == record.PlayerId && h.GameDate < record.GameDate)
                .OrderByDescending(h => h.GameDate)
                .Take(10)
                .ToList();

            if (playerHistory.Any())
            {
                features.HistoricalPoints = playerHistory.Select(h => (double)h.Points).ToList();
                features.HistoricalMinutes = playerHistory.Select(h => (double)h.Minutes).ToList();
                features.HistoricalForm = playerHistory.Select(h => (double)h.Points).ToList();
            }

            trainingData.Add(new MLTrainingData
            {
                Features = features,
                ActualPoints = record.Points,
                GameDate = record.GameDate,
                Season = record.Season
            });
        }

        return trainingData;
    }

    private async Task<MLInputFeatures> BuildMLFeaturesForPlayer(Player player, int gameweek)
    {
        var features = new MLInputFeatures
        {
            PlayerId = player.Id,
            PlayerName = player.WebName,
            Position = player.Position,
            Gameweek = gameweek,
            Price = (double)player.Price,
            OwnershipPercent = (double)player.SelectedByPercent
        };

        // Get recent performance data
        var recentPerformances = player.GameweekPerformances
            .OrderByDescending(p => p.Gameweek)
            .Take(10)
            .ToList();

        if (recentPerformances.Any())
        {
            features.Form5Games = recentPerformances.Take(5).Average(p => p.Points);
            features.MinutesPerGame = recentPerformances.Average(p => p.Minutes);
            features.GoalsPerGame = recentPerformances.Average(p => p.Goals);
            features.AssistsPerGame = recentPerformances.Average(p => p.Assists);
            features.HistoricalPoints = recentPerformances.Select(p => (double)p.Points).ToList();
            features.HistoricalMinutes = recentPerformances.Select(p => (double)p.Minutes).ToList();
            features.HistoricalForm = recentPerformances.Select(p => (double)p.Points).ToList();

            if (features.Price > 0)
            {
                features.PointsPerMillion = features.Form5Games / features.Price;
            }
        }

        // Set contextual features (would be enhanced with real fixture/opponent data)
        features.OpponentStrength = 3; // Average - would be calculated from fixture data
        features.IsHome = true; // Default - would be determined from fixture data
        features.TeamForm = 5.0; // Average - would be calculated from team performance
        features.TeamAttackStrength = 1.0; // Would be calculated from team stats
        features.TeamDefenseStrength = 1.0; // Would be calculated from team stats
        features.InjuryRisk = 0.1; // Low risk default
        features.FixtureDifficulty = 3; // Average difficulty
        features.SeasonalTrend = 0.0; // Neutral trend

        return features;
    }

    private double GetPlayerPrice(int playerId)
    {
        var player = _context.Players.Find(playerId);
        return player != null ? (double)player.Price : 5.0;
    }

    private double GetPlayerOwnership(int playerId)
    {
        var player = _context.Players.Find(playerId);
        return player != null ? (double)player.SelectedByPercent : 10.0;
    }
}

// Supporting classes for ML Manager
public class GameweekPredictionResult
{
    public int Gameweek { get; set; }
    public DateTime PredictionDate { get; set; }
    public string ModelType { get; set; } = string.Empty;
    
    public List<PlayerPredictionResult> TopPerformers { get; set; } = new();
    public List<PlayerPredictionResult> TopGoalkeepers { get; set; } = new();
    public List<PlayerPredictionResult> TopDefenders { get; set; } = new();
    public List<PlayerPredictionResult> TopMidfielders { get; set; } = new();
    public List<PlayerPredictionResult> TopForwards { get; set; } = new();
    public List<PlayerPredictionResult> BestValue { get; set; } = new();
    public List<PlayerPredictionResult> Differentials { get; set; } = new();
    public List<PlayerPredictionResult> HighRisk { get; set; } = new();
}

public class MLModelStatusReport
{
    public bool IsInitialized { get; set; }
    public DateTime LastUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, double> ModelConfidences { get; set; } = new();
    public int TrainingDataSize { get; set; }
}
