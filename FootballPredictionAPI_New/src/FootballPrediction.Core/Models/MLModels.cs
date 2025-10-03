using System;
using System.Collections.Generic;
using Microsoft.ML.Data;

namespace FootballPrediction.Core.Models;

// ML.NET Input Model for Player Performance Prediction
public class PlayerPerformanceInput
{
    [LoadColumn(0)]
    public float PlayerId { get; set; }
    
    [LoadColumn(1)]
    public float Position { get; set; } // Encoded: GK=1, DEF=2, MID=3, FWD=4
    
    [LoadColumn(2)]
    public float IsHome { get; set; } // 1 for home, 0 for away
    
    [LoadColumn(3)]
    public float OpponentStrength { get; set; } // 1-20 (team strength ranking)
    
    [LoadColumn(4)]
    public float Form5Games { get; set; } // Average points in last 5 games
    
    [LoadColumn(5)]
    public float MinutesPerGame { get; set; } // Average minutes played per game
    
    [LoadColumn(6)]
    public float Price { get; set; } // Current price in FPL
    
    [LoadColumn(7)]
    public float InjuryRisk { get; set; } // 0-1 injury probability
    
    [LoadColumn(8)]
    public float OwnershipPercent { get; set; } // Percentage of managers owning the player
    
    [LoadColumn(9)]
    public float SeasonAverage { get; set; } // Season average points
    
    [LoadColumn(10)]
    public float HomeAdvantage { get; set; } // Home vs away performance difference
    
    [LoadColumn(11)]
    public float TeamStrength { get; set; } // Overall team strength rating
    
    [LoadColumn(12), ColumnName("Label")]
    public float Points { get; set; } // Target: Points scored in the game
}

// ML.NET Output Model for Player Performance Prediction
public class PlayerPerformanceOutput
{
    [ColumnName("Score")]
    public float PredictedPoints { get; set; }
    
    public float[] Probability { get; set; } = Array.Empty<float>();
}

// Input for Team Strength Prediction
public class TeamStrengthInput
{
    [LoadColumn(0)]
    public float TeamId { get; set; }
    
    [LoadColumn(1)]
    public float HomeGoalsScored { get; set; }
    
    [LoadColumn(2)]
    public float AwayGoalsScored { get; set; }
    
    [LoadColumn(3)]
    public float HomeGoalsConceded { get; set; }
    
    [LoadColumn(4)]
    public float AwayGoalsConceded { get; set; }
    
    [LoadColumn(5)]
    public float RecentForm { get; set; } // Points from last 5 games
    
    [LoadColumn(6), ColumnName("Label")]
    public float OverallStrength { get; set; } // Target: Overall team strength rating
}

// Output for Team Strength Prediction
public class TeamStrengthOutput
{
    [ColumnName("Score")]
    public float PredictedStrength { get; set; }
}

// Fixture Difficulty Prediction Input
public class FixtureDifficultyInput
{
    [LoadColumn(0)]
    public float TeamStrength { get; set; }
    
    [LoadColumn(1)]
    public float OpponentStrength { get; set; }
    
    [LoadColumn(2)]
    public float IsHome { get; set; }
    
    [LoadColumn(3)]
    public float RecentForm { get; set; }
    
    [LoadColumn(4)]
    public float OpponentRecentForm { get; set; }
    
    [LoadColumn(5), ColumnName("Label")]
    public float Difficulty { get; set; } // 1-5 difficulty rating
}

// Fixture Difficulty Prediction Output
public class FixtureDifficultyOutput
{
    [ColumnName("Score")]
    public float PredictedDifficulty { get; set; }
}

// Training data model for historical analysis
public class MLTrainingData
{
    public List<PlayerPerformanceInput> PlayerPerformanceData { get; set; } = new();
    public List<TeamStrengthInput> TeamStrengthData { get; set; } = new();
    public List<FixtureDifficultyInput> FixtureDifficultyData { get; set; } = new();
    
    // Additional properties for training
    public MLInputFeatures? Features { get; set; }
    public float ActualPoints { get; set; }
    public DateTime GameDate { get; set; }
    public string Season { get; set; } = string.Empty;
}

// Model performance metrics
public class ModelPerformanceMetrics
{
    public double RSquared { get; set; }
    public double RootMeanSquaredError { get; set; }
    public double MeanAbsoluteError { get; set; }
    public double MeanSquaredError { get; set; }
    public DateTime LastTrainedDate { get; set; }
    public int TrainingDataSize { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
}

// Prediction confidence levels
public enum PredictionConfidence
{
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4
}

// Enhanced prediction result with confidence
public class EnhancedPredictionResult
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public float PredictedPoints { get; set; }
    public PredictionConfidence Confidence { get; set; }
    public float ConfidenceScore { get; set; } // 0-1
    public string[] FactorsInfluencing { get; set; } = Array.Empty<string>();
    public DateTime PredictionDate { get; set; }
}

// Simple prediction result for ML.NET services
public class PredictionResult
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int Gameweek { get; set; }
    public double PredictedPoints { get; set; }
    public double Confidence { get; set; }
    public string Method { get; set; } = string.Empty;
    public Dictionary<string, double> Features { get; set; } = new();
}

// Ensemble prediction result
public class EnsemblePredictionResult
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public float FinalPrediction { get; set; }
    public float Confidence { get; set; }
    public Dictionary<string, float> ModelPredictions { get; set; } = new();
    public Dictionary<string, float> ModelWeights { get; set; } = new();
    public List<string> RiskFactors { get; set; } = new();
    public DateTime PredictionDate { get; set; }
    
    // Missing properties that Program.cs is trying to access
    public float EnsembleConfidence { get; set; }
    public string EnsembleReasoning { get; set; } = string.Empty;
}

// Player prediction for production pipeline
public class PlayerPrediction
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public float PredictedPoints { get; set; }
    public float Confidence { get; set; }
    public decimal CurrentPrice { get; set; }
    public float ValueScore { get; set; } // Points per million
    public bool IsRecommended { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
    public DateTime PredictionDate { get; set; }
}

// ML Predictor Interfaces
public interface IMLPredictor
{
    Task<PlayerPredictionResult> PredictAsync(MLInputFeatures features);
    Task<ModelPerformanceMetrics> GetModelMetricsAsync();
    Task TrainModelAsync(IEnumerable<MLInputFeatures> trainingData);
    string ModelName { get; }
}

public interface IEnsemblePredictor
{
    Task<EnsemblePredictionResult> PredictAsync(MLInputFeatures features);
    Task<IEnumerable<EnsemblePredictionResult>> PredictBatchAsync(IEnumerable<MLInputFeatures> features);
    Task<ModelPerformanceMetrics> GetEnsembleMetricsAsync();
    Task AddPredictorAsync(IMLPredictor predictor, float weight);
    Task RemovePredictorAsync(string modelName);
    Task UpdateWeightsAsync(Dictionary<string, float> newWeights);
}
