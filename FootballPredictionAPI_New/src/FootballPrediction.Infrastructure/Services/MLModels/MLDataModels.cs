using Microsoft.ML.Data;

namespace FootballPrediction.Infrastructure.Services.MLModels;

/// <summary>
/// Training data structure for ML.NET models
/// This is shared across all ML services (FastTree, LightGBM, TimeSeries)
/// </summary>
public class PlayerTrainingData
{
    // Input features
    public float Form { get; set; }
    public float PointsPerGame { get; set; }
    public float MinutesPerGame { get; set; }
    public float GoalsPerGame { get; set; }
    public float AssistsPerGame { get; set; }
    public float CleanSheetsPerGame { get; set; }
    public float GoalsConcededPerGame { get; set; }
    public float SavesPerGame { get; set; }
    public float BonusPointsPerGame { get; set; }
    public float ICTIndex { get; set; }
    public float Influence { get; set; }
    public float Creativity { get; set; }
    public float Threat { get; set; }
    public float PointsPerMillion { get; set; }
    public float TransfersInPerGame { get; set; }
    public float TransfersOutPerGame { get; set; }
    public float SelectedByPercent { get; set; }

    // Output label (what we're predicting)
    // CRITICAL: This attribute tells ML.NET this is the target variable
    [ColumnName("Label")]
    public float PredictedPoints { get; set; }
}

/// <summary>
/// Prediction output from regression models (FastTree, LightGBM)
/// </summary>
public class PlayerPrediction
{
    [ColumnName("Score")]
    public float Score { get; set; }
}

/// <summary>
/// Time series data structure for SSA forecasting
/// </summary>
public class PlayerTimeSeriesData
{
    public float Points { get; set; }
}

/// <summary>
/// Time series prediction output
/// </summary>
public class PlayerTimeSeriesPrediction
{
    [VectorType(1)]
    public float[] ForecastedPoints { get; set; } = new float[1];

    [VectorType(1)]
    public float[] LowerBound { get; set; } = new float[1];

    [VectorType(1)]
    public float[] UpperBound { get; set; } = new float[1];
}
