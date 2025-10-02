using FootballPrediction.Core.Entities;
using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services;

public class PlayerPredictionService
{
    private readonly FplDbContext _context;
    private readonly ILogger<PlayerPredictionService> _logger;

    public PlayerPredictionService(FplDbContext context, ILogger<PlayerPredictionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GameweekPrediction> GenerateGameweekPredictionAsync(int gameweek)
    {
        _logger.LogInformation("Generating predictions for gameweek {Gameweek}", gameweek);

        var players = await GetPlayersWithAnalysisDataAsync();
        var playerAnalyses = new List<PlayerFormAnalysis>();

        foreach (var player in players)
        {
            try
            {
                var analysis = await AnalyzePlayerAsync(player, gameweek);
                playerAnalyses.Add(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze player {PlayerId}", player.Id);
            }
        }

        var prediction = new GameweekPrediction
        {
            Gameweek = gameweek,
            PredictionDate = DateTime.UtcNow,
            TopPerformers = playerAnalyses
                .OrderByDescending(p => p.PredictedPoints)
                .Take(20)
                .ToList(),
            BestValue = playerAnalyses
                .Where(p => p.CurrentPrice <= 8.0m)
                .OrderByDescending(p => p.ValueScore)
                .Take(15)
                .ToList(),
            HighRisk = playerAnalyses
                .Where(p => p.InjuryRisk > 0.3m || p.RotationRisk > 0.4m)
                .OrderByDescending(p => p.InjuryRisk + p.RotationRisk)
                .Take(10)
                .ToList(),
            Differentials = playerAnalyses
                .Where(p => p.OwnershipPercentage < 10 && p.PredictedPoints > 6)
                .OrderByDescending(p => p.PredictedPoints - (p.OwnershipPercentage / 10))
                .Take(15)
                .ToList()
        };

        // Position-specific top picks
        prediction.TopGoalkeepers = GetTopByPosition(playerAnalyses, "Goalkeeper", 5);
        prediction.TopDefenders = GetTopByPosition(playerAnalyses, "Defender", 10);
        prediction.TopMidfielders = GetTopByPosition(playerAnalyses, "Midfielder", 10);
        prediction.TopForwards = GetTopByPosition(playerAnalyses, "Forward", 8);

        _logger.LogInformation("Generated predictions for {PlayerCount} players", playerAnalyses.Count);
        return prediction;
    }

    private async Task<PlayerFormAnalysis> AnalyzePlayerAsync(Player player, int targetGameweek)
    {
        var analysis = new PlayerFormAnalysis
        {
            PlayerId = player.Id,
            PlayerName = $"{player.FirstName} {player.SecondName}".Trim(),
            Position = player.Position,
            TeamName = player.Team?.Name ?? "Unknown",
            CurrentPrice = player.Price,
            AnalysisDate = DateTime.UtcNow
        };

        // Calculate form metrics
        await CalculateFormMetricsAsync(analysis, player);

        // Analyze upcoming fixtures
        await AnalyzeUpcomingFixturesAsync(analysis, player, targetGameweek);

        // Calculate performance indicators
        CalculatePerformanceIndicators(analysis, player);

        // Assess risk factors
        await AssessRiskFactorsAsync(analysis, player);

        // Calculate value metrics
        CalculateValueMetrics(analysis, player);

        // Generate final prediction
        GenerateFinalPrediction(analysis, player);

        return analysis;
    }

    private async Task CalculateFormMetricsAsync(PlayerFormAnalysis analysis, Player player)
    {
        var recentPerformances = await _context.PlayerGameweekPerformances
            .Where(p => p.PlayerId == player.Id)
            .OrderByDescending(p => p.Gameweek)
            .Take(5)
            .ToListAsync();

        if (recentPerformances.Any())
        {
            analysis.RecentForm = (decimal)recentPerformances.Average(p => p.Points);
        }

        analysis.SeasonForm = player.PointsPerGame;
        analysis.GoalsPerGame = player.Minutes > 0 ? (decimal)player.Goals / (player.Minutes / 90m) : 0;
        analysis.AssistsPerGame = player.Minutes > 0 ? (decimal)player.Assists / (player.Minutes / 90m) : 0;
        analysis.MinutesPerGame = player.Minutes > 0 ? player.Minutes / Math.Max(1, recentPerformances.Count) : 0;
    }

    private async Task AnalyzeUpcomingFixturesAsync(PlayerFormAnalysis analysis, Player player, int targetGameweek)
    {
        var upcomingFixtures = await _context.Fixtures
            .Include(f => f.TeamHome)
            .Include(f => f.TeamAway)
            .Where(f => f.Gameweek >= targetGameweek && f.Gameweek < targetGameweek + 5)
            .Where(f => f.TeamHomeId == player.TeamId || f.TeamAwayId == player.TeamId)
            .OrderBy(f => f.Gameweek)
            .ToListAsync();

        analysis.NextFiveFixtures = upcomingFixtures.Select(f => new UpcomingFixture
        {
            Gameweek = f.Gameweek,
            Opponent = f.TeamHomeId == player.TeamId ? f.TeamAway.Name : f.TeamHome.Name,
            IsHome = f.TeamHomeId == player.TeamId,
            Difficulty = f.TeamHomeId == player.TeamId ? f.Difficulty : 6 - f.Difficulty, // Reverse for away
            KickoffTime = f.KickoffTime
        }).ToList();

        // Calculate average fixture difficulty
        if (analysis.NextFiveFixtures.Any())
        {
            analysis.FixtureDifficulty = (decimal)analysis.NextFiveFixtures.Average(f => f.Difficulty);
        }
    }

    private void CalculatePerformanceIndicators(PlayerFormAnalysis analysis, Player player)
    {
        var gamesPlayed = player.Minutes / 90m;
        if (gamesPlayed > 0)
        {
            analysis.BonusPointsPerGame = player.BonusPoints / gamesPlayed;
            
            if (player.Position == "Defender" || player.Position == "Goalkeeper")
            {
                analysis.CleanSheetProbability = (decimal)player.CleanSheets / gamesPlayed;
            }
        }

        // ICT Index indicates overall involvement
        analysis.ValueScore = player.IctIndex > 0 ? player.TotalPoints / Math.Max(0.1m, player.Price) : 0;
    }

    private async Task AssessRiskFactorsAsync(PlayerFormAnalysis analysis, Player player)
    {
        // Check for recent injury news
        var recentInjuries = await _context.InjuryUpdates
            .Where(i => i.PlayerId == player.Id && i.ReportedDate > DateTime.UtcNow.AddDays(-14))
            .OrderByDescending(i => i.ReportedDate)
            .FirstOrDefaultAsync();

        if (recentInjuries != null)
        {
            analysis.InjuryRisk = recentInjuries.Severity switch
            {
                "Major" => 0.8m,
                "Medium" => 0.4m,
                "Minor" => 0.2m,
                _ => 0.1m
            };
            analysis.HasRecentNews = true;
            analysis.RecentNews = recentInjuries.Description;
        }

        // Rotation risk based on minutes played and team strength
        if (player.Minutes > 0)
        {
            var minutesRatio = (decimal)player.Minutes / (38 * 90); // Assuming 38 games max
            analysis.RotationRisk = minutesRatio < 0.7m ? (1 - minutesRatio) : 0.1m;
        }

        // FPL status indicators
        analysis.InjuryRisk = Math.Max(analysis.InjuryRisk, 
            player.ChanceOfPlayingNextRound < 50 ? 0.6m : 
            player.ChanceOfPlayingNextRound < 75 ? 0.3m : 0.1m);
    }

    private void CalculateValueMetrics(PlayerFormAnalysis analysis, Player player)
    {
        analysis.OwnershipPercentage = player.SelectedByPercent;
        
        // Transfer trend (positive means more transfers in)
        analysis.TransferTrend = player.TransfersIn > 0 || player.TransfersOut > 0 
            ? (decimal)(player.TransfersIn - player.TransfersOut) / Math.Max(1, player.TransfersIn + player.TransfersOut)
            : 0;

        // Value score considering price
        analysis.ValueScore = player.Price > 0 ? player.TotalPoints / player.Price : 0;
    }

    private void GenerateFinalPrediction(PlayerFormAnalysis analysis, Player player)
    {
        var basePoints = analysis.RecentForm > 0 ? analysis.RecentForm : analysis.SeasonForm;
        
        // Adjust for fixture difficulty (easier fixtures = higher points)
        var fixtureMultiplier = analysis.FixtureDifficulty > 0 ? (6 - analysis.FixtureDifficulty) / 3m : 1m;
        
        // Adjust for form trend
        var formMultiplier = analysis.RecentForm > analysis.SeasonForm ? 1.2m : 0.9m;
        
        // Adjust for injury/rotation risk
        var riskMultiplier = 1 - (analysis.InjuryRisk * 0.5m) - (analysis.RotationRisk * 0.3m);
        
        analysis.PredictedPoints = Math.Max(0, basePoints * fixtureMultiplier * formMultiplier * riskMultiplier);
        
        // Confidence based on data availability and consistency
        analysis.Confidence = CalculateConfidence(analysis, player);
        
        // Generate recommendation
        analysis.Recommendation = GenerateRecommendation(analysis);
    }

    private decimal CalculateConfidence(PlayerFormAnalysis analysis, Player player)
    {
        var confidence = 0.5m; // Base confidence
        
        // More minutes played = higher confidence
        if (player.Minutes > 1000) confidence += 0.2m;
        else if (player.Minutes > 500) confidence += 0.1m;
        
        // Recent form consistency
        if (analysis.RecentForm > 0) confidence += 0.1m;
        
        // Low injury risk increases confidence
        if (analysis.InjuryRisk < 0.2m) confidence += 0.1m;
        
        // Fixture data available
        if (analysis.NextFiveFixtures.Any()) confidence += 0.1m;
        
        return Math.Min(1m, Math.Max(0.1m, confidence));
    }

    private string GenerateRecommendation(PlayerFormAnalysis analysis)
    {
        if (analysis.InjuryRisk > 0.5m) return "AVOID";
        if (analysis.PredictedPoints > 8 && analysis.ValueScore > 2) return "STRONG BUY";
        if (analysis.PredictedPoints > 6 && analysis.ValueScore > 1.5m) return "BUY";
        if (analysis.PredictedPoints > 4) return "HOLD";
        return "SELL";
    }

    private List<PlayerFormAnalysis> GetTopByPosition(List<PlayerFormAnalysis> analyses, string position, int count)
    {
        return analyses
            .Where(p => p.Position == position)
            .OrderByDescending(p => p.PredictedPoints)
            .Take(count)
            .ToList();
    }

    private async Task<List<Player>> GetPlayersWithAnalysisDataAsync()
    {
        return await _context.Players
            .Include(p => p.Team)
            .Where(p => p.Minutes > 90) // Only include players who have played
            .ToListAsync();
    }
}
