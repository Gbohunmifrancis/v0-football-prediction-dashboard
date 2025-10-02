using System;
using System.Collections.Generic;

namespace FootballPrediction.Core.Entities;

public class HistoricalPlayerPerformance
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int FplPlayerId { get; set; }
    public string Season { get; set; } = string.Empty; // e.g., "2023-24"
    public int Gameweek { get; set; }
    
    // Performance Metrics
    public int Points { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int CleanSheets { get; set; }
    public int GoalsConceded { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Saves { get; set; }
    public int BonusPoints { get; set; }
    public int Bps { get; set; }
    
    // Advanced Metrics
    public decimal Influence { get; set; }
    public decimal Creativity { get; set; }
    public decimal Threat { get; set; }
    public decimal IctIndex { get; set; }
    
    // Context Data
    public string Position { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public bool WasHome { get; set; }
    public string OpponentTeam { get; set; } = string.Empty;
    public int OpponentStrength { get; set; }
    public int TeamScore { get; set; }
    public int OpponentScore { get; set; }
    public decimal Price { get; set; }
    public decimal OwnershipPercent { get; set; }
    
    // Derived Features for ML
    public decimal Form5Games { get; set; } // Last 5 games average
    public decimal HomeAwayForm { get; set; }
    public decimal MinutesPerGame { get; set; }
    public decimal GoalsPerGame { get; set; }
    public decimal AssistsPerGame { get; set; }
    public decimal PointsPerMillion { get; set; }
    public bool IsPlayingNextWeek { get; set; }
    
    // Timestamps
    public DateTime GameDate { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation Properties
    public virtual Player Player { get; set; } = null!;
    public virtual Team Team { get; set; } = null!;
}
