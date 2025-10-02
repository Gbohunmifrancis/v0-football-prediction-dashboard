using System.ComponentModel.DataAnnotations;

namespace FootballPrediction.Core.Entities;

public class Team
{
    [Key]
    public int Id { get; set; }
    
    public int FplId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int Code { get; set; }
    public int Strength { get; set; }
    public int StrengthOverallHome { get; set; }
    public int StrengthOverallAway { get; set; }
    public int StrengthAttackHome { get; set; }
    public int StrengthAttackAway { get; set; }
    public int StrengthDefenceHome { get; set; }
    public int StrengthDefenceAway { get; set; }
    public int Position { get; set; }
    public int Played { get; set; }
    public int Win { get; set; }
    public int Draw { get; set; }
    public int Loss { get; set; }
    public int Points { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public DateTime LastUpdated { get; set; }
    
    // Navigation properties
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Fixture> HomeFixtures { get; set; } = new List<Fixture>();
    public ICollection<Fixture> AwayFixtures { get; set; } = new List<Fixture>();
}
