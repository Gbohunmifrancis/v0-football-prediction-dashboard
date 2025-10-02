using FootballPrediction.Core.Models;
using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services.MLModels;

public class EnsemblePredictionService : IEnsemblePredictor
{
    private readonly FplDbContext _context;
    private readonly ILogger<EnsemblePredictionService> _logger;
    private readonly LSTMPredictionService _lstmService;
    private readonly XgBoostPredictionService _xgboostService;
    private readonly Dictionary<string, float> _modelWeights;
    private readonly Dictionary<string, ModelPerformanceMetrics> _modelPerformance;
    private readonly Dictionary<string, IMLPredictor> _predictors;
    private bool _isEnsembleTrained;

    public EnsemblePredictionService(
        FplDbContext context, 
        ILogger<EnsemblePredictionService> logger,
        LSTMPredictionService lstmService,
        XgBoostPredictionService xgboostService)
    {
        _context = context;
        _logger = logger;
        _lstmService = lstmService;
        _xgboostService = xgboostService;
        _modelWeights = new Dictionary<string, float>
        {
            ["LSTM"] = 0.4f,
            ["XGBoost"] = 0.6f
        };
        _modelPerformance = new Dictionary<string, ModelPerformanceMetrics>();
        _predictors = new Dictionary<string, IMLPredictor>
        {
            ["LSTM"] = lstmService,
            ["XGBoost"] = xgboostService
        };
        _isEnsembleTrained = false;
    }

    public async Task<EnsemblePredictionResult> PredictAsync(MLInputFeatures features)
    {
        _logger.LogInformation("Ensemble: Predicting for player {PlayerName} (ID: {PlayerId})", features.PlayerName, features.PlayerId);

        try
        {
            var modelPredictions = new Dictionary<string, float>();
            var totalWeight = 0f;
            var weightedSum = 0f;

            // Get predictions from individual models
            foreach (var predictor in _predictors)
            {
                try
                {
                    var prediction = await predictor.Value.PredictAsync(features);
                    modelPredictions[predictor.Key] = (float)prediction.PredictedPoints;
                    
                    var weight = _modelWeights.GetValueOrDefault(predictor.Key, 1f);
                    weightedSum += (float)prediction.PredictedPoints * weight;
                    totalWeight += weight;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get prediction from {ModelName}", predictor.Key);
                }
            }

            var finalPrediction = totalWeight > 0 ? weightedSum / totalWeight : 0f;
            var confidence = CalculateConfidence(modelPredictions);

            return new EnsemblePredictionResult
            {
                PlayerId = features.PlayerId,
                PlayerName = features.PlayerName,
                Position = features.Position,
                TeamName = features.TeamName,
                FinalPrediction = finalPrediction,
                Confidence = confidence,
                ModelPredictions = modelPredictions,
                ModelWeights = _modelWeights,
                RiskFactors = await CalculateRiskFactors(features),
                PredictionDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ensemble prediction for player {PlayerId}", features.PlayerId);
            throw;
        }
    }

    public async Task<IEnumerable<EnsemblePredictionResult>> PredictBatchAsync(IEnumerable<MLInputFeatures> features)
    {
        var results = new List<EnsemblePredictionResult>();
        
        foreach (var feature in features)
        {
            try
            {
                var result = await PredictAsync(feature);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to predict for player {PlayerId}", feature.PlayerId);
            }
        }

        return results;
    }

    public async Task<ModelPerformanceMetrics> GetEnsembleMetricsAsync()
    {
        // Calculate ensemble performance metrics
        var metrics = new ModelPerformanceMetrics
        {
            ModelName = "Ensemble",
            LastTrainedDate = DateTime.UtcNow,
            TrainingDataSize = 0
        };

        try
        {
            // Get metrics from individual models and combine them
            var lstmMetrics = await _lstmService.GetModelMetricsAsync();
            var xgboostMetrics = await _xgboostService.GetModelMetricsAsync();

            // Simple weighted average of metrics
            var lstmWeight = _modelWeights["LSTM"];
            var xgboostWeight = _modelWeights["XGBoost"];
            var totalWeight = lstmWeight + xgboostWeight;

            metrics.RSquared = (lstmMetrics.RSquared * lstmWeight + xgboostMetrics.RSquared * xgboostWeight) / totalWeight;
            metrics.RootMeanSquaredError = (lstmMetrics.RootMeanSquaredError * lstmWeight + xgboostMetrics.RootMeanSquaredError * xgboostWeight) / totalWeight;
            metrics.MeanAbsoluteError = (lstmMetrics.MeanAbsoluteError * lstmWeight + xgboostMetrics.MeanAbsoluteError * xgboostWeight) / totalWeight;
            metrics.TrainingDataSize = Math.Max(lstmMetrics.TrainingDataSize, xgboostMetrics.TrainingDataSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate ensemble metrics");
        }

        return metrics;
    }

    public async Task AddPredictorAsync(IMLPredictor predictor, float weight)
    {
        _predictors[predictor.ModelName] = predictor;
        _modelWeights[predictor.ModelName] = weight;
        _logger.LogInformation("Added predictor {ModelName} with weight {Weight}", predictor.ModelName, weight);
        await Task.CompletedTask;
    }

    public async Task RemovePredictorAsync(string modelName)
    {
        _predictors.Remove(modelName);
        _modelWeights.Remove(modelName);
        _logger.LogInformation("Removed predictor {ModelName}", modelName);
        await Task.CompletedTask;
    }

    public async Task UpdateWeightsAsync(Dictionary<string, float> newWeights)
    {
        foreach (var weight in newWeights)
        {
            if (_predictors.ContainsKey(weight.Key))
            {
                _modelWeights[weight.Key] = weight.Value;
            }
        }
        _logger.LogInformation("Updated model weights");
        await Task.CompletedTask;
    }

    private float CalculateConfidence(Dictionary<string, float> predictions)
    {
        if (predictions.Count < 2) return 0.5f;

        var values = predictions.Values.ToList();
        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
        var standardDeviation = Math.Sqrt(variance);
        
        // Calculate coefficient of variation (CV)
        var coefficientOfVariation = mean > 0 ? standardDeviation / mean : 1.0;

        // Multiple confidence factors
        // 1. Agreement between models (lower CV = higher confidence)
        var agreementScore = Math.Max(0.0, 1.0 - coefficientOfVariation);
        
        // 2. Prediction magnitude confidence (extreme predictions less confident)
        var magnitudeScore = CalculateMagnitudeConfidence(mean);
        
        // 3. Model performance based confidence
        var performanceScore = CalculatePerformanceBasedConfidence();
        
        // Weighted combination
        var confidence = 
            agreementScore * 0.5 +      // Model agreement most important
            magnitudeScore * 0.3 +      // Magnitude check
            performanceScore * 0.2;     // Historical performance
        
        return (float)Math.Max(0.1, Math.Min(1.0, confidence));
    }
    
    private double CalculateMagnitudeConfidence(double prediction)
    {
        // More confident in mid-range predictions (2-8 points)
        // Less confident in extreme predictions
        if (prediction >= 2 && prediction <= 8)
            return 1.0;
        else if (prediction > 8 && prediction <= 12)
            return 0.8;
        else if (prediction > 12)
            return 0.6; // High predictions less certain
        else if (prediction >= 1 && prediction < 2)
            return 0.7;
        else
            return 0.5; // Very low predictions uncertain
    }
    
    private double CalculatePerformanceBasedConfidence()
    {
        // If we have model performance metrics, use them
        if (_modelPerformance.Any())
        {
            var avgAccuracy = _modelPerformance.Values.Average(m => m.Accuracy);
            return avgAccuracy;
        }
        
        // Default based on known model characteristics
        return 0.75;
    }

    private async Task<List<string>> CalculateRiskFactors(MLInputFeatures features)
    {
        var riskFactors = new List<string>();

        try
        {
            // 1. Injury and Availability Risks
            if (features.InjuryRisk > 0.5)
                riskFactors.Add("⚠️ Very high injury risk - avoid");
            else if (features.InjuryRisk > 0.3)
                riskFactors.Add("⚠️ Elevated injury risk");
            else if (features.InjuryRisk > 0.1)
                riskFactors.Add("💛 Minor injury concern");

            // 2. Form and Performance Risks
            if (features.Form5Games < 2.0)
                riskFactors.Add("📉 Very poor recent form");
            else if (features.Form5Games < 3.5)
                riskFactors.Add("📉 Below average form");
            
            // 3. Playing Time Risks
            if (features.MinutesPerGame < 30)
                riskFactors.Add("⏱️ Bench/rotation player - very limited minutes");
            else if (features.MinutesPerGame < 60)
                riskFactors.Add("⏱️ Rotation risk - inconsistent playing time");
            else if (features.MinutesPerGame < 75)
                riskFactors.Add("⏱️ Moderate playing time");

            // 4. Fixture Difficulty Risks
            if (features.OpponentStrength < 5)
                riskFactors.Add("🛡️ Facing top-tier opposition");
            else if (features.OpponentStrength < 10)
                riskFactors.Add("🛡️ Difficult fixture");
            
            // 5. Price and Value Risks
            if (features.Price > 10.0 && features.Form5Games < 5.0)
                riskFactors.Add("💰 Premium price not justified by form");
            else if (features.Price < 5.0 && features.MinutesPerGame > 80)
                riskFactors.Add("💎 Budget gem opportunity");

            // 6. Ownership Risks
            if (features.OwnershipPercent > 40)
                riskFactors.Add("👥 Highly owned - template player");
            else if (features.OwnershipPercent < 2)
                riskFactors.Add("🔹 Differential pick - low ownership");

            // 7. Historical Performance Risks
            if (features.HistoricalPoints.Any())
            {
                var avgHistorical = features.HistoricalPoints.Average();
                var variance = features.HistoricalPoints.Sum(x => Math.Pow(x - avgHistorical, 2)) / features.HistoricalPoints.Count;
                
                if (variance > 15)
                    riskFactors.Add("📊 Highly inconsistent performer");
                else if (variance < 3)
                    riskFactors.Add("✅ Very consistent performer");
            }

            // 8. Position-specific risks
            if (features.Position == "Goalkeeper" && features.OpponentStrength < 8)
                riskFactors.Add("🥅 GK facing strong attack");
            else if (features.Position == "Defender" && features.OpponentStrength < 8)
                riskFactors.Add("🛡️ Defender - low clean sheet chance");
            else if (features.Position == "Forward" && features.MinutesPerGame < 70)
                riskFactors.Add("⚽ Forward with limited minutes");

            // 9. Positive indicators (opportunities)
            if (features.IsHome && features.Form5Games > 5)
                riskFactors.Add("🏠 Home form player in good form");
            
            if (features.OpponentStrength > 15 && features.MinutesPerGame > 75)
                riskFactors.Add("✨ Easy fixture + guaranteed starter");
            
            // If no risk factors found, add positive note
            if (riskFactors.Count == 0)
                riskFactors.Add("✅ Low risk - solid pick");

            await Task.CompletedTask; // Maintain async signature for future DB queries
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating risk factors for player {PlayerId}", features.PlayerId);
            riskFactors.Add("⚠️ Unable to assess all risk factors");
        }

        return riskFactors;
    }

    public async Task TrainEnsembleAsync(List<MLTrainingData> trainingData)
    {
        _logger.LogInformation("Training Ensemble model with {Count} data points", trainingData.Count);
        
        try
        {
            // Train individual models
            await _lstmService.TrainAsync(trainingData);
            await _xgboostService.TrainAsync(trainingData);
            
            // Update ensemble weights based on individual model performance
            _isEnsembleTrained = true;
            
            _logger.LogInformation("Ensemble model training completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error training Ensemble model");
            throw;
        }
    }

    public async Task<EnsemblePredictionResult> PredictEnsembleAsync(MLInputFeatures features)
    {
        // Alias for PredictAsync to maintain interface compatibility
        return await PredictAsync(features);
    }
}
