using System;
using System.Collections.Generic;
using FootballPrediction.Core.Entities;

namespace FootballPrediction.Core.Models;

public class PlayerFormAnalysis
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    
    // Form Metrics
    public decimal RecentForm { get; set; } // Last 5 games average
    public decimal SeasonForm { get; set; }
    public decimal HomeForm { get; set; }
    public decimal AwayForm { get; set; }
    
    // Fixture Analysis
    public decimal FixtureDifficulty { get; set; }
    public List<UpcomingFixture> NextFiveFixtures { get; set; } = new();
    
    // Performance Indicators
    public decimal GoalsPerGame { get; set; }
    public decimal AssistsPerGame { get; set; }
    public decimal MinutesPerGame { get; set; }
    public decimal BonusPointsPerGame { get; set; }
    public decimal CleanSheetProbability { get; set; }
    
    // Risk Factors
    public decimal InjuryRisk { get; set; }
    public decimal RotationRisk { get; set; }
    public bool HasRecentNews { get; set; }
    public string RecentNews { get; set; } = string.Empty;
    
    // Value Analysis
    public decimal ValueScore { get; set; } // Points per million
    public decimal TransferTrend { get; set; } // In/Out ratio
    public decimal OwnershipPercentage { get; set; }
    
    // Final Prediction
    public decimal PredictedPoints { get; set; }
    public decimal Confidence { get; set; }
    public string Recommendation { get; set; } = string.Empty; // BUY, HOLD, SELL
    public DateTime AnalysisDate { get; set; }
}

public class UpcomingFixture
{
    public int Gameweek { get; set; }
    public string Opponent { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public int Difficulty { get; set; }
    public DateTime KickoffTime { get; set; }
}

public class GameweekPrediction
{
    public int Gameweek { get; set; }
    public DateTime PredictionDate { get; set; }
    public List<PlayerFormAnalysis> TopPerformers { get; set; } = new();
    public List<PlayerFormAnalysis> BestValue { get; set; } = new();
    public List<PlayerFormAnalysis> HighRisk { get; set; } = new();
    public List<PlayerFormAnalysis> Differentials { get; set; } = new();
    
    // Position-specific recommendations
    public List<PlayerFormAnalysis> TopGoalkeepers { get; set; } = new();
    public List<PlayerFormAnalysis> TopDefenders { get; set; } = new();
    public List<PlayerFormAnalysis> TopMidfielders { get; set; } = new();
    public List<PlayerFormAnalysis> TopForwards { get; set; } = new();
}
