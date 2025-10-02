using System;
using System.Collections.Generic;

namespace FootballPrediction.Core.Models;

public class MLInputFeatures
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public int OpponentStrength { get; set; }
    public double Form5Games { get; set; }
    public double MinutesPerGame { get; set; }
    public double Price { get; set; }
    public double InjuryRisk { get; set; }
    public double OwnershipPercent { get; set; }
    public List<double> HistoricalPoints { get; set; } = new();
    
    // Additional properties referenced in the codebase
    public int Gameweek { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public double GoalsPerGame { get; set; }
    public double AssistsPerGame { get; set; }
    public double PointsPerMillion { get; set; }
    public List<double> HistoricalMinutes { get; set; } = new();
    public List<double> HistoricalForm { get; set; } = new();
    public double TeamForm { get; set; }
    public double TeamAttackStrength { get; set; }
    public double TeamDefenseStrength { get; set; }
    public double FixtureDifficulty { get; set; }
    public double SeasonalTrend { get; set; }
}
