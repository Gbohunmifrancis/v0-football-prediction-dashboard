using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FootballPrediction.Core.Models;
using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services.RAG;

public class FootballKnowledgeBaseService
{
    private readonly FplDbContext _context;
    private readonly ILogger<FootballKnowledgeBaseService> _logger;
    private readonly Dictionary<string, List<FootballKnowledgeItem>> _knowledgeBase;
    private bool _isInitialized;

    public FootballKnowledgeBaseService(FplDbContext context, ILogger<FootballKnowledgeBaseService> logger)
    {
        _context = context;
        _logger = logger;
        _knowledgeBase = new Dictionary<string, List<FootballKnowledgeItem>>();
        _isInitialized = false;
    }

    /// <summary>
    /// Initialize the football knowledge base with FPL insights and patterns
    /// </summary>
    public async Task InitializeKnowledgeBaseAsync()
    {
        _logger.LogInformation("🧠 Initializing Football Knowledge Base for RAG system...");

        try
        {
            await BuildPlayerInsights();
            await BuildPositionalStrategies();
            await BuildFixtureAnalysis();
            await BuildHistoricalPatterns();
            await BuildTransferTrends();
            await BuildInjuryInsights();
            await BuildFormAnalysis();

            _isInitialized = true;
            _logger.LogInformation("✅ Football Knowledge Base initialized with {Categories} categories", _knowledgeBase.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing knowledge base");
            throw;
        }
    }

    /// <summary>
    /// Retrieve relevant context for a prediction to enhance ML output
    /// </summary>
    public async Task<ContextualInsight> GetContextualInsightAsync(PlayerPredictionResult prediction, MLInputFeatures features)
    {
        if (!_isInitialized)
        {
            await InitializeKnowledgeBaseAsync();
        }

        var insight = new ContextualInsight
        {
            PlayerId = prediction.PlayerId,
            PlayerName = prediction.PlayerName,
            PredictedPoints = prediction.PredictedPoints,
            Confidence = prediction.Confidence
        };

        try
        {
            // Retrieve relevant knowledge based on prediction context
            insight.PositionalStrategy = await GetPositionalStrategy(features.Position, prediction.PredictedPoints);
            insight.FixtureContext = await GetFixtureContext(features);
            insight.FormAnalysis = await GetFormAnalysis(features);
            insight.ValueAssessment = await GetValueAssessment(features, prediction.PredictedPoints);
            insight.RiskFactors = await GetRiskFactors(features);
            insight.HistoricalComparison = await GetHistoricalComparison(features);
            insight.TransferAdvice = await GetTransferAdvice(features, prediction);

            // Generate overall reasoning
            insight.OverallReasoning = GenerateOverallReasoning(insight);
            insight.ConfidenceExplanation = GenerateConfidenceExplanation(prediction, features);

            return insight;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contextual insight for player {PlayerId}", prediction.PlayerId);
            return insight; // Return partial insight
        }
    }

    /// <summary>
    /// Get strategic insights for team selection
    /// </summary>
    public async Task<TeamSelectionInsight> GetTeamSelectionInsightAsync(List<PlayerPredictionResult> predictions, int budget = 1000)
    {
        var insight = new TeamSelectionInsight
        {
            TotalBudget = budget,
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            insight.SquadBalance = AnalyzeSquadBalance(predictions);
            insight.CaptainOptions = GetCaptainRecommendations(predictions);
            insight.DifferentialOpportunities = await GetDifferentialOpportunities(predictions);
            insight.RiskManagement = GetRiskManagementAdvice(predictions);
            insight.BudgetOptimization = await OptimizeBudget(predictions, budget);

            return insight;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating team selection insight");
            return insight;
        }
    }

    private async Task<List<FootballKnowledgeItem>> BuildPlayerInsights()
    {
        return await AsyncHelper.ExecuteDbOperationAsync(_context, async () =>
        {
            var playerInsights = new List<FootballKnowledgeItem>();

            // Build insights from historical player data
            var topPerformers = await _context.HistoricalPlayerPerformances
                .GroupBy(h => h.PlayerId)
                .Select(g => new
                {
                    PlayerId = g.Key,
                    PlayerName = g.First().PlayerName,
                    Position = g.First().Position,
                    AvgPoints = Convert.ToDouble(g.Average(h => h.Points)),
                    Consistency = g.Select(h => Convert.ToDouble(h.Points)).ToList(),
                    HomeForm = Convert.ToDouble(g.Where(h => h.WasHome).Average(h => h.Points)),
                    AwayForm = Convert.ToDouble(g.Where(h => !h.WasHome).Average(h => h.Points))
                })
                .Where(p => p.AvgPoints > 4)
                .ToListAsync();

            foreach (var player in topPerformers)
            {
                var insight = new FootballKnowledgeItem
                {
                    Category = "PlayerInsights",
                    Key = $"player_{player.PlayerId}",
                    Title = $"{player.PlayerName} Performance Profile",
                    Content = $"Avg: {player.AvgPoints:F1} pts, Consistency: {CalculateConsistency(player.Consistency):F2}, Home: {player.HomeForm:F1}, Away: {player.AwayForm:F1}",
                    Relevance = player.AvgPoints / 20.0,
                    Tags = new List<string> { player.Position, "performance", "historical" }
                };
                playerInsights.Add(insight);
            }

            return playerInsights;
        });
    }

    private async Task BuildPositionalStrategies()
    {
        await Task.CompletedTask; // Make async method valid
        var strategies = new List<FootballKnowledgeItem>
        {
            new()
            {
                Category = "PositionalStrategy",
                Key = "goalkeeper_strategy",
                Title = "Goalkeeper Selection Strategy",
                Content = "Focus on clean sheet potential, save bonus, and fixture difficulty. Premium goalkeepers (£5.5+) often provide better value through save points and bonus.",
                Relevance = 1.0,
                Tags = new List<string> { "Goalkeeper", "strategy", "clean_sheets" }
            },
            new()
            {
                Category = "PositionalStrategy", 
                Key = "defender_strategy",
                Title = "Defender Selection Strategy",
                Content = "Balance between attacking threat and clean sheet potential. Wing-backs offer attacking returns, while center-backs provide consistency through clean sheets.",
                Relevance = 1.0,
                Tags = new List<string> { "Defender", "strategy", "attacking_returns" }
            },
            new()
            {
                Category = "PositionalStrategy",
                Key = "midfielder_strategy", 
                Title = "Midfielder Selection Strategy",
                Content = "Prioritize attacking midfielders and playmakers. Look for penalty takers, set-piece specialists, and players in advanced positions.",
                Relevance = 1.0,
                Tags = new List<string> { "Midfielder", "strategy", "attacking_mid" }
            },
            new()
            {
                Category = "PositionalStrategy",
                Key = "forward_strategy",
                Title = "Forward Selection Strategy", 
                Content = "Target high-volume shooters and penalty takers. Consider team's attacking style and player's position in the penalty area.",
                Relevance = 1.0,
                Tags = new List<string> { "Forward", "strategy", "goals" }
            }
        };

        _knowledgeBase["PositionalStrategy"] = strategies;
    }

    private async Task BuildFixtureAnalysis()
    {
        await AsyncHelper.ExecuteDbOperationAsync(_context, async () =>
        {
            var fixtureInsights = new List<FootballKnowledgeItem>();

            // Analyze team strengths and weaknesses from historical data
            var teamStats = await _context.HistoricalTeamStrengths
                .GroupBy(h => h.TeamName)
                .Select(g => new
                {
                    TeamName = g.Key,
                    AvgAttackHome = Convert.ToDouble(g.Average(h => h.AttackStrengthHome)),
                    AvgAttackAway = Convert.ToDouble(g.Average(h => h.AttackStrengthAway)),
                    AvgDefenseHome = Convert.ToDouble(g.Average(h => h.DefenseStrengthHome)),
                    AvgDefenseAway = Convert.ToDouble(g.Average(h => h.DefenseStrengthAway))
                })
                .ToListAsync();

            foreach (var team in teamStats)
            {
                var insight = new FootballKnowledgeItem
                {
                    Category = "FixtureAnalysis",
                    Key = $"team_{team.TeamName.ToLower().Replace(" ", "_")}",
                    Title = $"{team.TeamName} Fixture Analysis",
                    Content = $"Home Attack: {team.AvgAttackHome:F2}, Away Attack: {team.AvgAttackAway:F2}, Home Defense: {team.AvgDefenseHome:F2}, Away Defense: {team.AvgDefenseAway:F2}",
                    Relevance = Math.Max(team.AvgAttackHome, team.AvgAttackAway),
                    Tags = new List<string> { "fixture", "team_strength", team.TeamName.ToLower() }
                };
                fixtureInsights.Add(insight);
            }

            _knowledgeBase["FixtureAnalysis"] = fixtureInsights;
            return true;
        });
    }

    private async Task BuildHistoricalPatterns()
    {
        await Task.CompletedTask; // Make async method valid
        var patterns = new List<FootballKnowledgeItem>
        {
            new()
            {
                Category = "HistoricalPatterns",
                Key = "home_advantage",
                Title = "Home Advantage Impact",
                Content = "Players typically score 0.5-1.5 more points at home. Defenders and goalkeepers benefit most from home clean sheet potential.",
                Relevance = 0.8,
                Tags = new List<string> { "home_advantage", "clean_sheets", "historical" }
            },
            new()
            {
                Category = "HistoricalPatterns",
                Key = "captaincy_patterns",
                Title = "Captaincy Success Patterns",
                Content = "Premium forwards (£9.0+) and attacking midfielders show highest captaincy returns. Home fixtures increase success rate by 25%.",
                Relevance = 0.9,
                Tags = new List<string> { "captaincy", "premium_players", "home_fixtures" }
            },
            new()
            {
                Category = "HistoricalPatterns",
                Key = "double_gameweeks",
                Title = "Double Gameweek Strategy",
                Content = "Players with two fixtures in a gameweek score 1.8x average points. Prioritize players with good fixture combinations.",
                Relevance = 0.9,
                Tags = new List<string> { "double_gameweek", "fixtures", "strategy" }
            }
        };

        _knowledgeBase["HistoricalPatterns"] = patterns;
    }

    private async Task BuildTransferTrends()
    {
        await Task.CompletedTask; // Make async method valid
        var trends = new List<FootballKnowledgeItem>
        {
            new()
            {
                Category = "TransferTrends",
                Key = "price_rises",
                Title = "Price Rise Patterns",
                Content = "Players rising in price typically continue strong form for 3-4 gameweeks. Early transfers capture value before template formation.",
                Relevance = 0.7,
                Tags = new List<string> { "price_changes", "transfers", "value" }
            },
            new()
            {
                Category = "TransferTrends",
                Key = "bandwagon_effect",
                Title = "Template Player Analysis",
                Content = "High ownership players (>30%) become 'essential' but offer lower differential potential. Monitor for potential falls.",
                Relevance = 0.6,
                Tags = new List<string> { "ownership", "template", "differentials" }
            }
        };

        _knowledgeBase["TransferTrends"] = trends;
    }

    private async Task BuildInjuryInsights()
    {
        await Task.CompletedTask; // Make async method valid
        var injuryInsights = new List<FootballKnowledgeItem>
        {
            new()
            {
                Category = "InjuryInsights",
                Key = "return_patterns",
                Title = "Post-Injury Performance",
                Content = "Players returning from injury typically need 2-3 games to reach full performance. Monitor minutes and involvement.",
                Relevance = 0.8,
                Tags = new List<string> { "injury", "recovery", "minutes" }
            },
            new()
            {
                Category = "InjuryInsights",
                Key = "rotation_risk",
                Title = "Rotation Risk Factors",
                Content = "Players over 30 or with injury history face higher rotation risk during fixture congestion periods.",
                Relevance = 0.7,
                Tags = new List<string> { "rotation", "age", "fixture_congestion" }
            }
        };

        _knowledgeBase["InjuryInsights"] = injuryInsights;
    }

    private async Task BuildFormAnalysis()
    {
        await Task.CompletedTask; // Make async method valid
        var formInsights = new List<FootballKnowledgeItem>
        {
            new()
            {
                Category = "FormAnalysis",
                Key = "form_sustainability",
                Title = "Form Sustainability Patterns",
                Content = "Exceptional form (8+ average over 3 games) typically regresses within 3-5 gameweeks. Look for underlying performance metrics.",
                Relevance = 0.9,
                Tags = new List<string> { "form", "regression", "sustainability" }
            },
            new()
            {
                Category = "FormAnalysis",
                Key = "new_signing_patterns",
                Title = "New Signing Integration",
                Content = "New signings typically take 3-5 gameweeks to integrate. Monitor training reports and manager comments for integration timeline.",
                Relevance = 0.6,
                Tags = new List<string> { "new_signings", "integration", "manager_comments" }
            }
        };

        _knowledgeBase["FormAnalysis"] = formInsights;
    }

    // Contextual retrieval methods
    public async Task<List<FootballKnowledgeItem>> RetrieveRelevantKnowledgeAsync(string query, List<string> tags, int maxResults = 5)
    {
        await Task.CompletedTask; // Make async method valid
        var relevantItems = new List<FootballKnowledgeItem>();

        foreach (var category in _knowledgeBase.Values)
        {
            foreach (var item in category)
            {
                var relevanceScore = CalculateRelevance(item, query, tags);
                if (relevanceScore > 0.3)
                {
                    item.CalculatedRelevance = relevanceScore;
                    relevantItems.Add(item);
                }
            }
        }

        return relevantItems
            .OrderByDescending(item => item.CalculatedRelevance)
            .Take(maxResults)
            .ToList();
    }

    private double CalculateRelevance(FootballKnowledgeItem item, string query, List<string> tags)
    {
        double relevance = item.Relevance;

        // Tag matching
        var matchingTags = item.Tags.Intersect(tags, StringComparer.OrdinalIgnoreCase).Count();
        if (matchingTags > 0)
        {
            relevance += matchingTags * 0.2;
        }

        // Content similarity (simple keyword matching)
        if (!string.IsNullOrEmpty(query))
        {
            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var contentWords = item.Content.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wordMatches = queryWords.Intersect(contentWords).Count();
            relevance += wordMatches * 0.1;
        }

        return Math.Min(1.0, relevance);
    }

    private async Task<string> GetPositionalStrategy(string position, double predictedPoints)
    {
        var strategies = _knowledgeBase.GetValueOrDefault("PositionalStrategy", new List<FootballKnowledgeItem>());
        var positionStrategy = strategies.FirstOrDefault(s => s.Tags.Contains(position));

        if (positionStrategy != null)
        {
            var baseAdvice = positionStrategy.Content;
            
            if (predictedPoints >= 8)
                return $"🌟 PREMIUM PICK: {baseAdvice} This player shows exceptional predicted performance.";
            else if (predictedPoints >= 6)
                return $"✅ SOLID PICK: {baseAdvice} Strong predicted performance for this position.";
            else if (predictedPoints >= 4)
                return $"⚠️ MODERATE: {baseAdvice} Average predicted performance, consider alternatives.";
            else
                return $"❌ AVOID: {baseAdvice} Below-average predicted performance.";
        }

        return "Standard positional analysis applies.";
    }

    private async Task<string> GetFixtureContext(MLInputFeatures features)
    {
        await Task.CompletedTask; // Make async method valid
        var context = new List<string>();

        if (features.IsHome)
            context.Add("🏠 HOME advantage (+0.5-1.5 pts expected boost)");
        else
            context.Add("✈️ AWAY fixture (slightly reduced clean sheet potential)");

        var difficultyText = features.OpponentStrength switch
        {
            1 => "🟢 VERY EASY opponent - excellent scoring opportunity",
            2 => "🟡 EASY opponent - good scoring potential", 
            3 => "🟠 MODERATE opponent - standard expectations",
            4 => "🔴 DIFFICULT opponent - limited scoring chances",
            5 => "⚫ VERY DIFFICULT opponent - avoid unless essential",
            _ => "❓ Unknown opponent strength"
        };
        context.Add(difficultyText);

        return string.Join(" | ", context);
    }

    private async Task<string> GetFormAnalysis(MLInputFeatures features)
    {
        var formText = features.Form5Games switch
        {
            >= 8 => "🔥 EXCEPTIONAL form - but watch for regression",
            >= 6 => "✅ GOOD form - sustainable performance level",
            >= 4 => "📊 AVERAGE form - monitor for improvement",
            >= 2 => "📉 POOR form - avoid unless fixtures improve",
            _ => "🚨 TERRIBLE form - definitely avoid"
        };

        var minutesAnalysis = features.MinutesPerGame switch
        {
            >= 80 => "⭐ Guaranteed starter",
            >= 60 => "✅ Regular starter with some rotation risk",
            >= 30 => "⚠️ Squad player - high rotation risk", 
            _ => "❌ Rarely plays - avoid"
        };

        return $"{formText} | {minutesAnalysis}";
    }

    private async Task<string> GetValueAssessment(MLInputFeatures features, double predictedPoints)
    {
        await Task.CompletedTask;
        if (features.Price <= 0) return "Price data unavailable";

        var pointsPerMillion = predictedPoints / features.Price;
        
        var valueText = pointsPerMillion switch
        {
            >= 2.0 => "💎 EXCEPTIONAL value - strong buy",
            >= 1.5 => "✅ GOOD value - recommended",
            >= 1.0 => "📊 FAIR value - consider other factors",
            >= 0.5 => "⚠️ POOR value - expensive for predicted output",
            _ => "❌ TERRIBLE value - overpriced"
        };

        return $"{valueText} ({pointsPerMillion:F2} pts/£m)";
    }

    private async Task<List<string>> GetRiskFactors(MLInputFeatures features)
    {
        await Task.CompletedTask;
        var risks = new List<string>();

        if (features.InjuryRisk > 0.7)
            risks.Add("🚨 HIGH injury risk");
        else if (features.InjuryRisk > 0.4)
            risks.Add("⚠️ MODERATE injury risk");

        if (features.MinutesPerGame < 60)
            risks.Add("🔄 Rotation risk due to limited minutes");

        if (features.OwnershipPercent > 50)
            risks.Add("📈 High ownership - template player risk");

        if (features.Form5Games < 3)
            risks.Add("📉 Poor recent form");

        return risks.Any() ? risks : new List<string> { "✅ Low risk profile" };
    }

    private async Task<string> GetHistoricalComparison(MLInputFeatures features)
    {
        var historical = await _context.HistoricalPlayerPerformances
            .Where(h => h.PlayerId == features.PlayerId)
            .Select(h => Convert.ToDouble(h.Points))
            .ToListAsync();

        if (!historical.Any())
            return "No historical comparison available";

        var historicalAvg = historical.Average();
        var comparison = features.Form5Games.CompareTo(historicalAvg);

        return comparison switch
        {
            > 0 => $"📈 ABOVE historical average ({historicalAvg:F1} pts) - form is strong",
            0 => $"📊 AT historical average ({historicalAvg:F1} pts) - typical performance",
            < 0 => $"📉 BELOW historical average ({historicalAvg:F1} pts) - underperforming"
        };
    }

    private async Task<string> GetTransferAdvice(MLInputFeatures features, PlayerPredictionResult prediction)
    {
        if (prediction.PredictedPoints >= 8 && prediction.Confidence > 0.8)
            return "🚀 PRIORITY TRANSFER - High confidence, excellent predicted return";
        
        if (prediction.PredictedPoints >= 6 && prediction.Confidence > 0.7)
            return "✅ GOOD TRANSFER - Solid predicted performance";
            
        if (prediction.PredictedPoints >= 4 && features.OwnershipPercent < 10)
            return "💎 DIFFERENTIAL OPPORTUNITY - Low ownership, decent predicted points";
            
        if (prediction.PredictedPoints < 3 || prediction.Confidence < 0.4)
            return "❌ AVOID TRANSFER - Poor predicted performance or low confidence";
            
        return "📊 MONITOR - Consider other factors before transferring";
    }

    private string GenerateOverallReasoning(ContextualInsight insight)
    {
        var reasoning = new List<string>();

        reasoning.Add($"🎯 Predicted: {insight.PredictedPoints:F1} points");
        reasoning.Add($"🔍 Confidence: {insight.Confidence:P1}");
        
        if (insight.FixtureContext.Contains("EASY"))
            reasoning.Add("📈 Favorable fixture");
        
        if (insight.FormAnalysis.Contains("GOOD") || insight.FormAnalysis.Contains("EXCEPTIONAL"))
            reasoning.Add("🔥 Strong recent form");
            
        if (insight.ValueAssessment.Contains("GOOD") || insight.ValueAssessment.Contains("EXCEPTIONAL"))
            reasoning.Add("💰 Excellent value");

        return string.Join(" | ", reasoning);
    }

    private string GenerateConfidenceExplanation(PlayerPredictionResult prediction, MLInputFeatures features)
    {
        var explanations = new List<string>();

        if (prediction.Confidence >= 0.8)
            explanations.Add("🎯 HIGH confidence - Strong historical data and model agreement");
        else if (prediction.Confidence >= 0.6)
            explanations.Add("✅ GOOD confidence - Reliable prediction with minor uncertainties");
        else if (prediction.Confidence >= 0.4)
            explanations.Add("⚠️ MODERATE confidence - Some uncertainty in prediction");
        else
            explanations.Add("❌ LOW confidence - High uncertainty, use with caution");

        if (features.HistoricalPoints.Count < 5)
            explanations.Add("📊 Limited historical data affects confidence");

        if (features.InjuryRisk > 0.5)
            explanations.Add("🚨 Injury concerns reduce confidence");

        return string.Join(" | ", explanations);
    }

    // Helper methods
    private double CalculateConsistency(List<double> points)
    {
        if (points.Count < 2) return 0;
        var mean = points.Average();
        var variance = points.Average(p => Math.Pow(p - mean, 2));
        return 1.0 / (1.0 + Math.Sqrt(variance));
    }

    private string AnalyzeSquadBalance(List<PlayerPredictionResult> predictions)
    {
        var gkCount = predictions.Count(p => p.Position == "Goalkeeper");
        var defCount = predictions.Count(p => p.Position == "Defender");
        var midCount = predictions.Count(p => p.Position == "Midfielder");
        var fwdCount = predictions.Count(p => p.Position == "Forward");

        return $"Squad composition: {gkCount} GK, {defCount} DEF, {midCount} MID, {fwdCount} FWD";
    }

    private List<string> GetCaptainRecommendations(List<PlayerPredictionResult> predictions)
    {
        return predictions
            .Where(p => p.PredictedPoints >= 8 && p.Confidence > 0.7)
            .OrderByDescending(p => p.PredictedPoints * p.Confidence)
            .Take(3)
            .Select(p => string.Format("⭐ {0} ({1:F1} pts, {2:P0} confidence)", 
                p.PlayerName, p.PredictedPoints, p.Confidence))
            .ToList();
    }

    private async Task<List<string>> GetDifferentialOpportunities(List<PlayerPredictionResult> predictions)
    {
        var playerIds = predictions.Select(p => p.PlayerId).ToList();
        var players = await _context.Players
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p);

        return predictions
            .Where(p => p.PredictedPoints >= 6 && p.Confidence > 0.6)
            .OrderBy(p => players.GetValueOrDefault(p.PlayerId)?.SelectedByPercent ?? 100)
            .Take(3)
            .Select(p => string.Format("💎 {0} (Low ownership, {1:F1} pts predicted)", 
                p.PlayerName, p.PredictedPoints))
            .ToList();
    }

    private List<string> GetRiskManagementAdvice(List<PlayerPredictionResult> predictions)
    {
        var advice = new List<string>();
        
        var lowConfidencePlayers = predictions.Where(p => p.Confidence < 0.5).ToList();
        if (lowConfidencePlayers.Any())
        {
            advice.Add($"⚠️ {lowConfidencePlayers.Count} players have low prediction confidence - consider alternatives");
        }
        
        var highRiskPlayers = predictions.Where(p => p.PredictedPoints < 2).ToList();
        if (highRiskPlayers.Any())
        {
            advice.Add($"🚨 {highRiskPlayers.Count} players predicted under 2 points - high risk");
        }
        
        return advice.Any() ? advice : new List<string> { "✅ Risk profile looks acceptable" };
    }

    private async Task<string> OptimizeBudget(List<PlayerPredictionResult> predictions, int budget)
    {
        var playerIds = predictions.Select(p => p.PlayerId).ToList();
        var players = await _context.Players
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p);

        var totalCost = predictions.Sum(p => Convert.ToDouble(players.GetValueOrDefault(p.PlayerId)?.Price ?? 5.0m));
        var remaining = budget - totalCost;

        if (remaining >= 10)
            return $"💰 {remaining:C1}m remaining - room for upgrades";
        else if (remaining >= 0)
            return $"✅ {remaining:C1}m remaining - tight but manageable";
        else
            return $"🚨 {Math.Abs(remaining):C1}m OVER budget - need downgrades";
    }
}

// Supporting classes for RAG system
public class FootballKnowledgeItem
{
    public string Category { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public List<string> Tags { get; set; } = new();
    public double CalculatedRelevance { get; set; }
}

public class ContextualInsight
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public double PredictedPoints { get; set; }
    public double Confidence { get; set; }
    
    public string PositionalStrategy { get; set; } = string.Empty;
    public string FixtureContext { get; set; } = string.Empty;
    public string FormAnalysis { get; set; } = string.Empty;
    public string ValueAssessment { get; set; } = string.Empty;
    public List<string> RiskFactors { get; set; } = new();
    public string HistoricalComparison { get; set; } = string.Empty;
    public string TransferAdvice { get; set; } = string.Empty;
    public string OverallReasoning { get; set; } = string.Empty;
    public string ConfidenceExplanation { get; set; } = string.Empty;
}

public class TeamSelectionInsight
{
    public int TotalBudget { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string SquadBalance { get; set; } = string.Empty;
    public List<string> CaptainOptions { get; set; } = new();
    public List<string> DifferentialOpportunities { get; set; } = new();
    public List<string> RiskManagement { get; set; } = new();
    public string BudgetOptimization { get; set; } = string.Empty;
}
