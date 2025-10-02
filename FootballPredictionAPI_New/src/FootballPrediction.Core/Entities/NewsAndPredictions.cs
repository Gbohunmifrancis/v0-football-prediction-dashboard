using System.ComponentModel.DataAnnotations;

namespace FootballPrediction.Core.Entities;

public class InjuryUpdate
{
    [Key]
    public int Id { get; set; }
    
    public int PlayerId { get; set; }
    public string InjuryType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Minor, Major, Long-term
    public string Description { get; set; } = string.Empty;
    public DateTime ReportedDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string Status { get; set; } = string.Empty; // Active, Recovered, Ongoing
    public string Source { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public Player Player { get; set; } = null!;
}

public class TransferNews
{
    [Key]
    public int Id { get; set; }
    
    public int PlayerId { get; set; }
    public string NewsType { get; set; } = string.Empty; // Transfer_In, Transfer_Out, Loan, Contract_Extension
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Reliability { get; set; } = string.Empty; // High, Medium, Low
    public int? FromTeamId { get; set; }
    public int? ToTeamId { get; set; }
    public decimal? TransferFee { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public Player Player { get; set; } = null!;
}

public class HeadToHeadRecord
{
    [Key]
    public int Id { get; set; }
    
    public int Team1Id { get; set; }
    public int Team2Id { get; set; }
    public int Team1Wins { get; set; }
    public int Team2Wins { get; set; }
    public int Draws { get; set; }
    public int TotalMatches { get; set; }
    public int Team1GoalsFor { get; set; }
    public int Team1GoalsAgainst { get; set; }
    public int Team2GoalsFor { get; set; }
    public int Team2GoalsAgainst { get; set; }
    public decimal Team1AverageGoals { get; set; }
    public decimal Team2AverageGoals { get; set; }
    public DateTime LastMeeting { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class PlayerPrediction
{
    [Key]
    public int Id { get; set; }
    
    public int PlayerId { get; set; }
    public int Gameweek { get; set; }
    public decimal PredictedPoints { get; set; }
    public decimal MinutesLikelihood { get; set; }
    public decimal GoalsPrediction { get; set; }
    public decimal AssistsPrediction { get; set; }
    public decimal CleanSheetChance { get; set; }
    public decimal BonusPrediction { get; set; }
    public string FormAnalysis { get; set; } = string.Empty;
    public string FixtureDifficulty { get; set; } = string.Empty;
    public decimal InjuryRisk { get; set; }
    public decimal RotationRisk { get; set; }
    public decimal Confidence { get; set; }
    public DateTime PredictionDate { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    
    // Navigation properties
    public Player Player { get; set; } = null!;
}
