using FootballPrediction.Core.Models;
using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services.MLModels;

/// <summary>
/// Service responsible for training ML models with historical FPL data
/// </summary>
public class MLTrainingService
{
    private readonly FplDbContext _context;
    private readonly ILogger<MLTrainingService> _logger;
    private readonly LSTMPredictionService _lstmService;
    private readonly XgBoostPredictionService _xgboostService;
    private readonly EnsemblePredictionService _ensembleService;

    public MLTrainingService(
        FplDbContext context,
        ILogger<MLTrainingService> logger,
        LSTMPredictionService lstmService,
        XgBoostPredictionService xgboostService,
        EnsemblePredictionService ensembleService)
    {
        _context = context;
        _logger = logger;
        _lstmService = lstmService;
        _xgboostService = xgboostService;
        _ensembleService = ensembleService;
    }

    /// <summary>
    /// Train all ML models with historical data
    /// </summary>
    public async Task<TrainingResult> TrainAllModelsAsync(int minGameweeks = 5)
    {
        _logger.LogInformation("🎓 Starting ML model training process...");
        var result = new TrainingResult
        {
            StartTime = DateTime.UtcNow,
            Success = true
        };

        try
        {
            // 1. Prepare training data from historical performances
            _logger.LogInformation("📊 Preparing training data from historical performances...");
            var trainingData = await PrepareTrainingDataAsync(minGameweeks);
            
            if (!trainingData.Any())
            {
                _logger.LogWarning("⚠️ No training data available. Need at least {MinGW} gameweeks of historical data.", minGameweeks);
                result.Success = false;
                result.Message = $"Insufficient training data. Need at least {minGameweeks} gameweeks of history.";
                return result;
            }

            result.TrainingDataSize = trainingData.Count;
            _logger.LogInformation("✅ Prepared {Count} training samples", trainingData.Count);

            // 2. Split data into train/validation sets (80/20 split)
            var splitIndex = (int)(trainingData.Count * 0.8);
            var trainSet = trainingData.Take(splitIndex).ToList();
            var validationSet = trainingData.Skip(splitIndex).ToList();
            
            _logger.LogInformation("📈 Train set: {TrainCount} | Validation set: {ValCount}", 
                trainSet.Count, validationSet.Count);

            // 3. Train LSTM model
            _logger.LogInformation("🧠 Training LSTM model...");
            await _lstmService.TrainAsync(trainSet);
            result.LstmTrained = true;
            _logger.LogInformation("✅ LSTM training completed");

            // 4. Train XGBoost model
            _logger.LogInformation("🌳 Training XGBoost model...");
            await _xgboostService.TrainAsync(trainSet);
            result.XgBoostTrained = true;
            _logger.LogInformation("✅ XGBoost training completed");

            // 5. Train ensemble
            _logger.LogInformation("🎯 Training Ensemble model...");
            await _ensembleService.TrainEnsembleAsync(trainSet);
            result.EnsembleTrained = true;
            _logger.LogInformation("✅ Ensemble training completed");

            // 6. Validate models
            _logger.LogInformation("🔍 Validating models on validation set...");
            var validationMetrics = await ValidateModelsAsync(validationSet);
            result.ValidationAccuracy = validationMetrics.Accuracy;
            result.ValidationMAE = validationMetrics.MeanAbsoluteError;

            result.EndTime = DateTime.UtcNow;
            result.Message = $"Training completed successfully. Validation MAE: {validationMetrics.MeanAbsoluteError:F2}";
            
            _logger.LogInformation("✅ ML training completed successfully in {Duration}s", 
                (result.EndTime - result.StartTime).TotalSeconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during ML model training");
            result.Success = false;
            result.Message = $"Training failed: {ex.Message}";
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Prepare training data from historical player performances
    /// </summary>
    private async Task<List<MLTrainingData>> PrepareTrainingDataAsync(int minGameweeks)
    {
        var trainingData = new List<MLTrainingData>();

        try
        {
            // Get historical performances with sufficient data
            var historicalData = await _context.HistoricalPlayerPerformances
                .Include(h => h.Player)
                .ThenInclude(p => p.Team)
                .Where(h => h.Gameweek >= minGameweeks)
                .OrderBy(h => h.GameDate)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} historical performance records", historicalData.Count);

            // Group by player to calculate features
            var playerGroups = historicalData.GroupBy(h => h.PlayerId);

            foreach (var playerGroup in playerGroups)
            {
                var performances = playerGroup.OrderBy(p => p.GameDate).ToList();
                
                // Need at least minGameweeks of data to calculate features
                if (performances.Count < minGameweeks)
                    continue;

                // Create training samples for each gameweek (after we have enough history)
                for (int i = minGameweeks; i < performances.Count; i++)
                {
                    var currentGW = performances[i];
                    var previousGWs = performances.Take(i).TakeLast(minGameweeks).ToList();

                    var features = BuildFeaturesFromHistory(currentGW, previousGWs);
                    
                    trainingData.Add(new MLTrainingData
                    {
                        Features = features,
                        ActualPoints = currentGW.Points,
                        GameDate = currentGW.GameDate,
                        Season = currentGW.Season
                    });
                }
            }

            _logger.LogInformation("Created {Count} training samples from historical data", trainingData.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing training data");
        }

        return trainingData;
    }

    /// <summary>
    /// Build ML input features from historical performance data
    /// </summary>
    private MLInputFeatures BuildFeaturesFromHistory(
        HistoricalPlayerPerformance current, 
        List<HistoricalPlayerPerformance> history)
    {
        var recentForm = history.TakeLast(5).Average(h => h.Points);
        var avgMinutes = history.Average(h => h.Minutes);
        var historicalPoints = history.Select(h => (double)h.Points).ToList();

        return new MLInputFeatures
        {
            PlayerId = current.PlayerId,
            PlayerName = current.PlayerName,
            Position = current.Position,
            TeamName = current.TeamName,
            Form5Games = recentForm,
            MinutesPerGame = avgMinutes,
            Price = (double)current.Price,
            OwnershipPercent = (double)current.OwnershipPercent,
            OpponentStrength = current.OpponentStrength,
            IsHome = current.WasHome,
            InjuryRisk = current.IsPlayingNextWeek ? 0.0 : 0.5,
            HistoricalPoints = historicalPoints
        };
    }

    /// <summary>
    /// Validate models against validation set
    /// </summary>
    private async Task<ModelPerformanceMetrics> ValidateModelsAsync(List<MLTrainingData> validationSet)
    {
        var predictions = new List<double>();
        var actuals = new List<float>();

        foreach (var sample in validationSet.Take(100)) // Sample for validation
        {
            try
            {
                var prediction = await _ensembleService.PredictAsync(sample.Features);
                predictions.Add(prediction.FinalPrediction);
                actuals.Add(sample.ActualPoints);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating sample");
            }
        }

        if (!predictions.Any())
        {
            return new ModelPerformanceMetrics
            {
                ModelName = "Validation",
                Accuracy = 0,
                MeanAbsoluteError = 999
            };
        }

        // Calculate MAE
        var mae = predictions.Zip(actuals, (pred, actual) => Math.Abs(pred - actual)).Average();
        
        // Calculate accuracy (within 2 points)
        var accurateCount = predictions.Zip(actuals, (pred, actual) => Math.Abs(pred - actual) <= 2.0 ? 1 : 0).Sum();
        var accuracy = (double)accurateCount / predictions.Count;

        return new ModelPerformanceMetrics
        {
            ModelName = "Ensemble Validation",
            MeanAbsoluteError = mae,
            Accuracy = accuracy,
            LastTrainedDate = DateTime.UtcNow,
            TrainingDataSize = validationSet.Count
        };
    }

    private static decimal ParseDecimalFromString(string value)
    {
        return decimal.TryParse(value, out var result) ? result : 0m;
    }
}

/// <summary>
/// Result of training operation
/// </summary>
public class TrainingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TrainingDataSize { get; set; }
    public bool LstmTrained { get; set; }
    public bool XgBoostTrained { get; set; }
    public bool EnsembleTrained { get; set; }
    public double ValidationAccuracy { get; set; }
    public double ValidationMAE { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double DurationSeconds => (EndTime - StartTime).TotalSeconds;
}
