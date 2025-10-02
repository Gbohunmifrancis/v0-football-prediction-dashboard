namespace FootballPrediction.Core.Models;

public class PlayerPredictionResult
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public double PredictedPoints { get; set; }
    public double Confidence { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public string FeatureImportance { get; set; } = string.Empty;
}
