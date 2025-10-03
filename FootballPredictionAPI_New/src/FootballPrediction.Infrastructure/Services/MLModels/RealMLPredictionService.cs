using FootballPrediction.Core.Entities;
using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services.MLModels;

/// <summary>
/// REAL ML Prediction Service - Uses trained ML.NET models
/// Replaces formula-based predictions with actual machine learning
/// </summary>
public class RealMLPredictionService
{
    private readonly FplDbContext _context;
    private readonly ILogger<RealMLPredictionService> _logger;
    private readonly RealMLTrainingService _trainingService;
    private bool _modelLoaded = false;

    public RealMLPredictionService(
        FplDbContext context,
        ILogger<RealMLPredictionService> logger,
        RealMLTrainingService trainingService)
    {
        _context = context;
        _logger = logger;
        _trainingService = trainingService;
    }

    /// <summary>
    /// Ensure the ML model is loaded and ready
    /// </summary>
    private async Task<bool> EnsureModelLoadedAsync()
    {
        if (_modelLoaded) return true;

        // Try to load existing model
        var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "fpl_prediction_model.zip");
        
        if (File.Exists(modelPath))
        {
            _logger.LogInformation("📦 Loading existing ML model...");
            _modelLoaded = await _trainingService.LoadModelAsync(modelPath);
            
            if (_modelLoaded)
            {
                _logger.LogInformation("✅ ML model loaded successfully");
                return true;
            }
        }

        // Model doesn't exist or failed to load - train a new one
        _logger.LogInformation("🎓 No trained model found. Training new model...");
        var trainingResult = await _trainingService.TrainModelAsync();
        
        if (trainingResult.Success)
        {
            _modelLoaded = true;
            _logger.LogInformation("✅ Model trained and ready");
            _logger.LogInformation("📊 Training Stats: R²={RSquared:F4}, RMSE={RMSE:F2}, MAE={MAE:F2}", 
                trainingResult.RSquared, 
                trainingResult.RootMeanSquaredError, 
                trainingResult.MeanAbsoluteError);
            return true;
        }

        _logger.LogError("❌ Failed to train model: {Message}", trainingResult.Message);
        return false;
    }

    /// <summary>
    /// Predict player points using REAL ML model
    /// </summary>
    public async Task<PredictionResult> PredictPlayerPointsAsync(Player player, int gameweek)
    {
        try
        {
            // Ensure model is loaded
            if (!await EnsureModelLoadedAsync())
            {
                return FallbackPrediction(player, "Model not available");
            }

            // Get historical performance data
            var historical = await _context.HistoricalPlayerPerformances
                .Where(h => h.PlayerId == player.Id)
                .OrderByDescending(h => h.Id)
                .FirstOrDefaultAsync();

            if (historical == null)
            {
                return FallbackPrediction(player, "No historical data");
            }

            // Prepare input features for the ML model
            var inputData = new PlayerTrainingData
            {
                Form = (float)historical.Form5Games,
                PointsPerGame = (float)historical.PointsPerMillion, // Use available metric
                MinutesPerGame = (float)historical.MinutesPerGame,
                GoalsPerGame = (float)historical.GoalsPerGame,
                AssistsPerGame = (float)historical.AssistsPerGame,
                CleanSheetsPerGame = (float)historical.CleanSheets, // Actual count
                GoalsConcededPerGame = (float)historical.GoalsConceded, // Actual count
                SavesPerGame = (float)historical.Saves, // Actual count
                BonusPointsPerGame = (float)historical.BonusPoints,
                ICTIndex = (float)historical.IctIndex, // Note: lowercase 'ct'
                Influence = (float)historical.Influence,
                Creativity = (float)historical.Creativity,
                Threat = (float)historical.Threat,
                PointsPerMillion = (float)historical.PointsPerMillion,
                TransfersInPerGame = 0, // Not available in entity
                TransfersOutPerGame = 0, // Not available in entity
                SelectedByPercent = (float)player.SelectedByPercent
            };

            // Make prediction using trained ML model
            var prediction = _trainingService.Predict(inputData);

            // Calculate confidence based on data quality
            var confidence = CalculateConfidence(historical, player);

            // Ensure prediction is within reasonable bounds
            prediction = Math.Max(0, Math.Min(prediction, 20)); // Cap at 0-20 points

            return new PredictionResult
            {
                PlayerId = player.Id,
                PlayerName = player.WebName,
                Gameweek = gameweek,
                PredictedPoints = Math.Round(prediction, 2),
                Confidence = confidence,
                Method = "RealML-FastTree",
                Features = new Dictionary<string, double>
                {
                    { "Form", inputData.Form },
                    { "PointsPerGame", inputData.PointsPerGame },
                    { "MinutesPerGame", inputData.MinutesPerGame },
                    { "ICTIndex", inputData.ICTIndex }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ML prediction failed for player {PlayerId}", player.Id);
            return FallbackPrediction(player, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate prediction confidence based on data quality
    /// </summary>
    private double CalculateConfidence(HistoricalPlayerPerformance historical, Player player)
    {
        double confidence = 0.5; // Base confidence

        // Higher confidence for players with more minutes (better data quality)
        if (historical.Minutes > 800) confidence += 0.15; // ~10 full games
        else if (historical.Minutes > 400) confidence += 0.10; // ~5 full games
        else if (historical.Minutes > 0) confidence += 0.05;

        // Higher confidence for players with consistent form
        if (historical.Form5Games > 5) confidence += 0.10;
        else if (historical.Form5Games > 3) confidence += 0.05;

        // Higher confidence for highly selected players (proven quality)
        if (player.SelectedByPercent > 10) confidence += 0.10;
        else if (player.SelectedByPercent > 5) confidence += 0.05;

        // Higher confidence for players with good average minutes
        if (historical.MinutesPerGame > 80) confidence += 0.10;
        else if (historical.MinutesPerGame > 60) confidence += 0.05;

        return Math.Round(Math.Min(confidence, 0.95), 2); // Cap at 95%
    }

    /// <summary>
    /// Fallback prediction when ML model is unavailable
    /// </summary>
    private PredictionResult FallbackPrediction(Player player, string reason)
    {
        _logger.LogWarning("⚠️ Using fallback prediction for {Player}: {Reason}", player.WebName, reason);

        // Simple fallback based on form
        var prediction = (double)player.Form;

        return new PredictionResult
        {
            PlayerId = player.Id,
            PlayerName = player.WebName,
            Gameweek = 0,
            PredictedPoints = prediction,
            Confidence = 0.3, // Low confidence for fallback
            Method = "Fallback",
            Features = new Dictionary<string, double>
            {
                { "Form", (double)player.Form },
                { "Reason", 0 }
            }
        };
    }

    /// <summary>
    /// Retrain the model with fresh data
    /// </summary>
    public async Task<MLTrainingResult> RetrainModelAsync()
    {
        _logger.LogInformation("🔄 Retraining ML model with latest data...");
        _modelLoaded = false;
        return await _trainingService.TrainModelAsync();
    }
}
