using System;

namespace FootballPrediction.Core.Entities;

public class HistoricalTeamStrength
{
    public int Id { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal AttackStrengthHome { get; set; }
    public decimal AttackStrengthAway { get; set; }
    public decimal DefenseStrengthHome { get; set; }
    public decimal DefenseStrengthAway { get; set; }
}
