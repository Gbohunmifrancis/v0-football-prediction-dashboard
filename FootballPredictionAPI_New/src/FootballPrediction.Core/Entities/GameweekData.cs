using System.ComponentModel.DataAnnotations;

namespace FootballPrediction.Core.Entities;

public class GameweekData
{
    [Key]
    public int Id { get; set; }
    
    public int Gameweek { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsFinished { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime? DeadlineTime { get; set; }
    public int? HighestScore { get; set; }
    public decimal? AverageScore { get; set; }
    public int? TransfersMade { get; set; }
    public int? ChipPlays { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public ICollection<PlayerGameweekPerformance> PlayerPerformances { get; set; } = new List<PlayerGameweekPerformance>();
    public ICollection<Fixture> Fixtures { get; set; } = new List<Fixture>();
}

public class PlayerGameweekPerformance
{
    [Key]
    public int Id { get; set; }
    
    public int PlayerId { get; set; }
    public int Gameweek { get; set; }
    public int? GameweekDataId { get; set; }
    public int Points { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int CleanSheets { get; set; }
    public int GoalsConceded { get; set; }
    public int OwnGoals { get; set; }
    public int PenaltiesSaved { get; set; }
    public int PenaltiesMissed { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int Saves { get; set; }
    public int BonusPoints { get; set; }
    public int Bps { get; set; }
    public decimal Influence { get; set; }
    public decimal Creativity { get; set; }
    public decimal Threat { get; set; }
    public decimal IctIndex { get; set; }
    public decimal Value { get; set; }
    public bool WasHome { get; set; }
    public int OpponentTeamId { get; set; }
    public int TeamScore { get; set; }
    public int OpponentScore { get; set; }
    public DateTime KickoffTime { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public Player Player { get; set; } = null!;
    public GameweekData? GameweekData { get; set; }
}

public class Fixture
{
    [Key]
    public int Id { get; set; }
    
    public int FplId { get; set; }
    public int Gameweek { get; set; }
    public int? GameweekDataId { get; set; }
    public DateTime KickoffTime { get; set; }
    public int TeamHomeId { get; set; }
    public int TeamAwayId { get; set; }
    public int? TeamHomeScore { get; set; }
    public int? TeamAwayScore { get; set; }
    public bool Finished { get; set; }
    public int Minutes { get; set; }
    public bool ProvisionalStartTime { get; set; }
    public bool Started { get; set; }
    public int Difficulty { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public GameweekData? GameweekData { get; set; }
    public Team TeamHome { get; set; } = null!;
    public Team TeamAway { get; set; } = null!;
    public ICollection<PlayerFixture> PlayerFixtures { get; set; } = new List<PlayerFixture>();
}

public class PlayerFixture
{
    [Key]
    public int Id { get; set; }
    
    public int PlayerId { get; set; }
    public int FixtureId { get; set; }
    public int Difficulty { get; set; }
    public bool IsHome { get; set; }
    
    // Navigation properties
    public Player Player { get; set; } = null!;
    public Fixture Fixture { get; set; } = null!;
}
