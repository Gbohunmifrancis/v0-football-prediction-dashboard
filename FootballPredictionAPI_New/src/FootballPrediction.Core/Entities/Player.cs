using System;
using System.Collections.Generic;

namespace FootballPrediction.Core.Entities;

public class Player
{
    public int Id { get; set; }
    public int FplId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string SecondName { get; set; } = string.Empty;
    public string WebName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal SelectedByPercent { get; set; }
    
    // Team information
    public int TeamId { get; set; }
    public Team? Team { get; set; }
    
    // Position and basic stats
    public string Position { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public decimal PointsPerGame { get; set; }
    public decimal Form { get; set; }
    
    // Transfer stats
    public int TransfersIn { get; set; }
    public int TransfersOut { get; set; }
    
    // Value metrics
    public decimal ValueForm { get; set; }
    public decimal ValueSeason { get; set; }
    
    // Performance stats
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
    
    // Advanced metrics
    public decimal Influence { get; set; }
    public decimal Creativity { get; set; }
    public decimal Threat { get; set; }
    public decimal IctIndex { get; set; }
    
    // Status and availability
    public string Status { get; set; } = string.Empty;
    public string News { get; set; } = string.Empty;
    public DateTime? NewsAdded { get; set; }
    public int? ChanceOfPlayingNextRound { get; set; }
    public int? ChanceOfPlayingThisRound { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public List<PlayerGameweekPerformance> GameweekPerformances { get; set; } = new();
}
