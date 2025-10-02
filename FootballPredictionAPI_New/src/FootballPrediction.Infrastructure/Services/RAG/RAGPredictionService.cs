using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services.MLModels;
using FootballPrediction.Infrastructure.Services.RAG;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services;

public class RAGPredictionService
{
    private readonly FplDbContext _context;
    private readonly ILogger<RAGPredictionService> _logger;
    private readonly MLPredictionManagerService _mlManager;
    private readonly FootballKnowledgeBaseService _knowledgeBase;
    private readonly EnsemblePredictionService _ensembleService;

    public RAGPredictionService(
        FplDbContext context,
        ILogger<RAGPredictionService> logger,
        MLPredictionManagerService mlManager,
        FootballKnowledgeBaseService knowledgeBase,
        EnsemblePredictionService ensembleService)
    {
        _context = context;
        _logger = logger;
        _mlManager = mlManager;
        _knowledgeBase = knowledgeBase;
        _ensembleService = ensembleService;
    }

    /// <summary>
    /// Generate contextually-aware predictions using RAG (Retrieval-Augmented Generation)
    /// This combines ML predictions with football domain knowledge for richer insights
    /// </summary>
    public async Task<RAGPredictionResult> GenerateContextualPredictionAsync(int playerId, int gameweek)
    {
        _logger.LogInformation("🧠 RAG: Generating contextual prediction for player {PlayerId}, gameweek {Gameweek}", 
            playerId, gameweek);

        try
        {
            // Step 1: Get ML prediction from ensemble
            var mlPrediction = await _mlManager.GetPlayerMLPredictionAsync(playerId, gameweek);
            
            // Step 2: Retrieve relevant football knowledge
            var relevantKnowledge = await RetrieveRelevantKnowledgeAsync(mlPrediction);
            
            // Step 3: Generate contextual insights
            var playerPredictionResult = new PlayerPredictionResult
            {
                PlayerId = mlPrediction.PlayerId,
                PlayerName = mlPrediction.PlayerName,
                Position = mlPrediction.Position,
                PredictedPoints = (double)mlPrediction.FinalPrediction,
                Confidence = mlPrediction.Confidence,
                ModelUsed = "Ensemble",
                FeatureImportance = string.Join(", ", mlPrediction.RiskFactors)
            };
            
            var contextualInsight = await _knowledgeBase.GetContextualInsightAsync(
                playerPredictionResult, 
                await GetPlayerFeatures(playerId, gameweek));
            
            // Step 4: Combine ML prediction with contextual knowledge
            var ragResult = new RAGPredictionResult
            {
                MLPrediction = playerPredictionResult,
                ContextualInsight = contextualInsight,
                RelevantKnowledge = relevantKnowledge,
                EnhancedReasoning = await GenerateEnhancedReasoning(mlPrediction, contextualInsight, relevantKnowledge),
                ConfidenceBoost = CalculateContextualConfidenceBoost(mlPrediction, relevantKnowledge),
                ActionableAdvice = await GenerateActionableAdvice(mlPrediction, contextualInsight),
                GeneratedAt = DateTime.UtcNow
            };

            // Step 5: Apply contextual adjustments to final prediction
            ragResult.ContextuallyAdjustedPoints = ApplyContextualAdjustments(
                mlPrediction.FinalPrediction, 
                relevantKnowledge, 
                contextualInsight);

            _logger.LogInformation("✅ RAG prediction complete: {Points:F1} pts (ML: {MLPoints:F1}, Contextual Adj: {Adj:+0.0;-0.0})", 
                ragResult.ContextuallyAdjustedPoints, 
                mlPrediction.FinalPrediction,
                ragResult.ContextuallyAdjustedPoints - mlPrediction.FinalPrediction);

            return ragResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG prediction for player {PlayerId}", playerId);
            throw;
        }
    }

    /// <summary>
    /// Generate comprehensive gameweek insights using RAG
    /// </summary>
    public async Task<RAGGameweekInsight> GenerateGameweekInsightAsync(int gameweek)
    {
        _logger.LogInformation("🧠 RAG: Generating comprehensive gameweek {Gameweek} insights", gameweek);

        try
        {
            // Get ML predictions for all players
            var mlPredictions = await _mlManager.GenerateMLGameweekPredictionsAsync(gameweek);
            
            // Generate contextual insights for top performers
            var enhancedTopPerformers = new List<EnhancedPlayerPrediction>();
            
            foreach (var player in mlPredictions.TopPerformers.Take(10))
            {
                var ragPrediction = await GenerateContextualPredictionAsync(player.PlayerId, gameweek);
                enhancedTopPerformers.Add(new EnhancedPlayerPrediction
                {
                    BasePrediction = player,
                    RAGPrediction = ragPrediction,
                    ContextualRating = CalculateContextualRating(ragPrediction)
                });
            }

            // Generate gameweek-specific insights
            var gameweekInsight = new RAGGameweekInsight
            {
                Gameweek = gameweek,
                GeneratedAt = DateTime.UtcNow,
                EnhancedTopPerformers = enhancedTopPerformers.OrderByDescending(p => p.ContextualRating).ToList(),
                GameweekNarrative = await GenerateGameweekNarrative(gameweek, enhancedTopPerformers),
                StrategicInsights = await GenerateStrategicInsights(enhancedTopPerformers),
                MarketTrends = await AnalyzeMarketTrends(enhancedTopPerformers),
                RiskWarnings = await IdentifyRiskWarnings(enhancedTopPerformers)
            };

            return gameweekInsight;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating RAG gameweek insight");
            throw;
        }
    }

    /// <summary>
    /// Generate natural language explanation for prediction reasoning
    /// </summary>
    public async Task<string> ExplainPredictionAsync(PlayerPredictionResult prediction, MLInputFeatures features)
    {
        var explanations = new List<string>();

        // Start with prediction summary
        explanations.Add($"🎯 **{prediction.PlayerName}** is predicted to score **{prediction.PredictedPoints:F1} points** in the upcoming gameweek.");

        // Add confidence context
        if (prediction.Confidence >= 0.8)
            explanations.Add("🔥 This is a **high-confidence** prediction based on strong historical patterns and current form.");
        else if (prediction.Confidence >= 0.6)
            explanations.Add("✅ This is a **reliable** prediction with good supporting data.");
        else
            explanations.Add("⚠️ This prediction has **moderate uncertainty** - consider additional factors.");

        // Add model reasoning
        if (prediction.ModelUsed == "Ensemble")
            explanations.Add("🧠 This prediction combines **LSTM time-series analysis** with **XGBoost feature analysis** for maximum accuracy.");

        // Add contextual factors
        var contextualInsight = await _knowledgeBase.GetContextualInsightAsync(prediction, features);
        explanations.Add($"📊 **Form Analysis**: {contextualInsight.FormAnalysis}");
        explanations.Add($"🏟️ **Fixture Context**: {contextualInsight.FixtureContext}");
        explanations.Add($"💰 **Value Assessment**: {contextualInsight.ValueAssessment}");

        // Add risk factors
        if (contextualInsight.RiskFactors.Any())
        {
            explanations.Add($"⚠️ **Risk Factors**: {string.Join(", ", contextualInsight.RiskFactors)}");
        }

        // Add transfer advice
        explanations.Add($"📈 **Transfer Advice**: {contextualInsight.TransferAdvice}");

        return string.Join("\n\n", explanations);
    }

    /// <summary>
    /// Public method to get player features for external use
    /// </summary>
    public async Task<MLInputFeatures> GetPlayerFeatures(int playerId, int gameweek)
    {
        return await GetPlayerFeaturesInternal(playerId, gameweek);
    }

    // Private helper methods
    private async Task<List<FootballKnowledgeItem>> RetrieveRelevantKnowledgeAsync(EnsemblePredictionResult mlPrediction)
    {
        var queryTerms = new List<string>
        {
            mlPrediction.Position.ToLower(),
            "prediction",
            "performance"
        };

        if (mlPrediction.FinalPrediction >= 8)
            queryTerms.Add("premium");
        else if (mlPrediction.FinalPrediction <= 3)
            queryTerms.Add("avoid");

        var query = string.Join(" ", queryTerms);
        return await _knowledgeBase.RetrieveRelevantKnowledgeAsync(query, queryTerms);
    }

    private async Task<MLInputFeatures> GetPlayerFeaturesInternal(int playerId, int gameweek)
    {
        var player = await _context.Players
            .Include(p => p.Team)
            .Include(p => p.GameweekPerformances)
            .FirstOrDefaultAsync(p => p.Id == playerId);

        if (player == null)
            throw new ArgumentException($"Player {playerId} not found");

        // Build comprehensive feature set
        var features = new MLInputFeatures
        {
            PlayerId = playerId,
            PlayerName = player.WebName,
            Position = player.Position,
            Gameweek = gameweek,
            Price = (double)player.Price,
            OwnershipPercent = (double)player.SelectedByPercent
        };

        // Add recent performance data
        var recentPerformances = player.GameweekPerformances
            .OrderByDescending(p => p.Gameweek)
            .Take(10)
            .ToList();

        if (recentPerformances.Any())
        {
            features.Form5Games = recentPerformances.Take(5).Average(p => p.Points);
            features.MinutesPerGame = recentPerformances.Average(p => p.Minutes);
            features.GoalsPerGame = recentPerformances.Average(p => p.Goals);
            features.AssistsPerGame = recentPerformances.Average(p => p.Assists);
            features.HistoricalPoints = recentPerformances.Select(p => (double)p.Points).ToList();
            features.HistoricalMinutes = recentPerformances.Select(p => (double)p.Minutes).ToList();
            features.HistoricalForm = recentPerformances.Select(p => (double)p.Points).ToList();

            if (features.Price > 0)
            {
                features.PointsPerMillion = features.Form5Games / features.Price;
            }
        }

        // Add contextual features (would be enhanced with real fixture data)
        features.OpponentStrength = 3; // Average
        features.IsHome = true; // Default
        features.TeamForm = 5.0;
        features.TeamAttackStrength = 1.0;
        features.TeamDefenseStrength = 1.0;
        features.InjuryRisk = 0.1;
        features.FixtureDifficulty = 3;
        features.SeasonalTrend = 0.0;

        return features;
    }

    private async Task<string> GenerateEnhancedReasoning(
        EnsemblePredictionResult mlPrediction, 
        ContextualInsight contextualInsight, 
        List<FootballKnowledgeItem> relevantKnowledge)
    {
        var reasoning = new List<string>();

        // Start with ML reasoning
        reasoning.Add($"🤖 **ML Analysis**: {mlPrediction.EnsembleReasoning}");

        // Add contextual insights
        reasoning.Add($"📊 **Contextual Analysis**: {contextualInsight.OverallReasoning}");

        // Add relevant football knowledge
        if (relevantKnowledge.Any())
        {
            var topKnowledge = relevantKnowledge.First();
            reasoning.Add($"🏆 **Football Insight**: {topKnowledge.Content}");
        }

        // Add confidence explanation
        reasoning.Add($"🎯 **Confidence**: {contextualInsight.ConfidenceExplanation}");

        return string.Join("\n\n", reasoning);
    }

    private double CalculateContextualConfidenceBoost(EnsemblePredictionResult mlPrediction, List<FootballKnowledgeItem> knowledge)
    {
        double boost = 0;

        // Boost confidence if we have relevant supporting knowledge
        if (knowledge.Any(k => k.CalculatedRelevance > 0.7))
            boost += 0.1;

        // Boost if prediction aligns with known patterns
        var avgRelevance = knowledge.Average(k => k.CalculatedRelevance);
        boost += avgRelevance * 0.05;

        return Math.Min(0.15, boost); // Max 15% confidence boost
    }

    private async Task<List<string>> GenerateActionableAdvice(EnsemblePredictionResult mlPrediction, ContextualInsight insight)
    {
        var advice = new List<string>();

        // Transfer decisions
        if (mlPrediction.FinalPrediction >= 8 && mlPrediction.Confidence > 0.8)
        {
            advice.Add($"🚀 **STRONG BUY**: Transfer in {mlPrediction.PlayerName} - excellent predicted return with high confidence");
        }
        else if (mlPrediction.FinalPrediction >= 6 && mlPrediction.Confidence > 0.7)
        {
            advice.Add($"✅ **CONSIDER**: {mlPrediction.PlayerName} shows good potential - monitor fixtures and form");
        }
        else if (mlPrediction.FinalPrediction < 3 || mlPrediction.Confidence < 0.4)
        {
            advice.Add($"❌ **AVOID**: {mlPrediction.PlayerName} - poor predicted performance or high uncertainty");
        }

        // Captaincy advice
        if (mlPrediction.FinalPrediction >= 10 && mlPrediction.Confidence > 0.8)
        {
            advice.Add($"⭐ **CAPTAIN CANDIDATE**: {mlPrediction.PlayerName} has excellent captaincy potential");
        }

        // Timing advice
        if (insight.ValueAssessment.Contains("EXCEPTIONAL"))
        {
            advice.Add($"⏰ **URGENT**: {mlPrediction.PlayerName} offers exceptional value - likely to rise in price");
        }

        // Risk management
        if (insight.RiskFactors.Any(r => r.Contains("HIGH")))
        {
            advice.Add($"⚠️ **MONITOR**: {mlPrediction.PlayerName} has risk factors - watch team news closely");
        }

        return advice;
    }

    private double ApplyContextualAdjustments(double mlPrediction, List<FootballKnowledgeItem> knowledge, ContextualInsight insight)
    {
        double adjustment = 0;

        // Fixture difficulty adjustment
        if (insight.FixtureContext.Contains("VERY EASY"))
            adjustment += 1.0;
        else if (insight.FixtureContext.Contains("EASY"))
            adjustment += 0.5;
        else if (insight.FixtureContext.Contains("DIFFICULT"))
            adjustment -= 0.5;
        else if (insight.FixtureContext.Contains("VERY DIFFICULT"))
            adjustment -= 1.0;

        // Form trend adjustment
        if (insight.FormAnalysis.Contains("EXCEPTIONAL"))
            adjustment += 0.5;
        else if (insight.FormAnalysis.Contains("POOR") || insight.FormAnalysis.Contains("TERRIBLE"))
            adjustment -= 0.5;

        // Risk factor adjustment
        var highRiskFactors = insight.RiskFactors.Count(r => r.Contains("HIGH") || r.Contains("🚨"));
        adjustment -= highRiskFactors * 0.3;

        // Knowledge-based adjustment
        var supportiveKnowledge = knowledge.Count(k => k.CalculatedRelevance > 0.7);
        adjustment += supportiveKnowledge * 0.2;

        return Math.Max(0, mlPrediction + adjustment);
    }

    private double CalculateContextualRating(RAGPredictionResult ragPrediction)
    {
        var mlScore = ragPrediction.MLPrediction.PredictedPoints * ragPrediction.MLPrediction.Confidence;
        var contextualBonus = ragPrediction.ConfidenceBoost * 10;
        var adjustmentImpact = Math.Abs(ragPrediction.ContextuallyAdjustedPoints - ragPrediction.MLPrediction.PredictedPoints);

        return mlScore + contextualBonus + adjustmentImpact;
    }

    private async Task<string> GenerateGameweekNarrative(int gameweek, List<EnhancedPlayerPrediction> topPredictions)
    {
        var narrative = new List<string>();

        narrative.Add($"📖 **Gameweek {gameweek} Intelligence Report**");
        narrative.Add("");

        if (topPredictions.Any())
        {
            var topPlayer = topPredictions.First();
            narrative.Add($"🌟 **Standout Performer**: {topPlayer.BasePrediction.PlayerName} leads predictions with {topPlayer.RAGPrediction.ContextuallyAdjustedPoints:F1} points expected.");
        }

        // Analyze position trends
        var positionTrends = topPredictions
            .GroupBy(p => p.BasePrediction.Position)
            .Select(g => new { Position = g.Key, AvgPoints = g.Average(p => p.RAGPrediction.ContextuallyAdjustedPoints) })
            .OrderByDescending(p => p.AvgPoints);

        var strongestPosition = positionTrends.First();
        narrative.Add($"🎯 **Position Focus**: {strongestPosition.Position}s show strongest predicted performance this gameweek ({strongestPosition.AvgPoints:F1} avg).");

        // Market insights
        var highValuePlayers = topPredictions.Count(p => p.RAGPrediction.ContextualInsight.ValueAssessment.Contains("EXCEPTIONAL"));
        if (highValuePlayers > 0)
        {
            narrative.Add($"💎 **Value Opportunities**: {highValuePlayers} exceptional value picks identified for potential price rises.");
        }

        return string.Join("\n", narrative);
    }

    private async Task<List<string>> GenerateStrategicInsights(List<EnhancedPlayerPrediction> predictions)
    {
        var insights = new List<string>();

        // Captain analysis
        var captainCandidates = predictions.Count(p => p.RAGPrediction.ContextuallyAdjustedPoints >= 10);
        if (captainCandidates > 0)
        {
            insights.Add($"⭐ {captainCandidates} strong captain candidates identified with 10+ point potential");
        }

        // Differential analysis
        var differentials = predictions.Count(p => 
            p.RAGPrediction.ContextuallyAdjustedPoints >= 6 && 
            p.RAGPrediction.ContextualInsight.ValueAssessment.Contains("GOOD"));
        
        if (differentials > 0)
        {
            insights.Add($"💎 {differentials} differential opportunities with strong upside potential");
        }

        // Risk management
        var riskPlayers = predictions.Count(p => 
            p.RAGPrediction.ContextualInsight.RiskFactors.Any(r => r.Contains("HIGH")));
        
        if (riskPlayers > 0)
        {
            insights.Add($"⚠️ {riskPlayers} players flagged with elevated risk factors - monitor team news");
        }

        return insights;
    }

    private async Task<List<string>> AnalyzeMarketTrends(List<EnhancedPlayerPrediction> predictions)
    {
        var trends = new List<string>();

        // Price change predictions
        var risingPlayers = predictions.Count(p => 
            p.RAGPrediction.ContextualInsight.ValueAssessment.Contains("EXCEPTIONAL") ||
            p.RAGPrediction.ContextuallyAdjustedPoints >= 8);

        if (risingPlayers > 0)
        {
            trends.Add($"📈 {risingPlayers} players likely to rise in price based on predicted performance");
        }

        // Template formation
        var templateCandidates = predictions.Count(p => 
            p.RAGPrediction.ContextuallyAdjustedPoints >= 7 && 
            p.RAGPrediction.MLPrediction.Confidence > 0.75);

        if (templateCandidates > 0)
        {
            trends.Add($"🔥 {templateCandidates} players showing template potential - expect ownership increases");
        }

        return trends;
    }

    private async Task<List<string>> IdentifyRiskWarnings(List<EnhancedPlayerPrediction> predictions)
    {
        var warnings = new List<string>();

        // Injury concerns
        var injuryRisks = predictions.Count(p => 
            p.RAGPrediction.ContextualInsight.RiskFactors.Any(r => r.Contains("injury")));

        if (injuryRisks > 0)
        {
            warnings.Add($"🚨 {injuryRisks} players flagged with injury concerns - monitor pressers");
        }

        // Rotation risks
        var rotationRisks = predictions.Count(p => 
            p.RAGPrediction.ContextualInsight.RiskFactors.Any(r => r.Contains("rotation")));

        if (rotationRisks > 0)
        {
            warnings.Add($"🔄 {rotationRisks} players at rotation risk - check team news and training reports");
        }

        // Form concerns
        var formConcerns = predictions.Count(p => 
            p.RAGPrediction.ContextualInsight.FormAnalysis.Contains("POOR"));

        if (formConcerns > 0)
        {
            warnings.Add($"📉 {formConcerns} players showing poor form trends - consider alternatives");
        }

        return warnings;
    }

    private double CalculateVariance(double[] values)
    {
        if (values.Length <= 1) return 0;
        var mean = values.Average();
        return values.Average(v => Math.Pow(v - mean, 2));
    }
}

// Supporting classes for RAG system
public class RAGPredictionResult
{
    public PlayerPredictionResult MLPrediction { get; set; } = new();
    public ContextualInsight ContextualInsight { get; set; } = new();
    public List<FootballKnowledgeItem> RelevantKnowledge { get; set; } = new();
    public string EnhancedReasoning { get; set; } = string.Empty;
    public double ConfidenceBoost { get; set; }
    public List<string> ActionableAdvice { get; set; } = new();
    public double ContextuallyAdjustedPoints { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class RAGGameweekInsight
{
    public int Gameweek { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<EnhancedPlayerPrediction> EnhancedTopPerformers { get; set; } = new();
    public string GameweekNarrative { get; set; } = string.Empty;
    public List<string> StrategicInsights { get; set; } = new();
    public List<string> MarketTrends { get; set; } = new();
    public List<string> RiskWarnings { get; set; } = new();
}

public class EnhancedPlayerPrediction
{
    public PlayerPredictionResult BasePrediction { get; set; } = new();
    public RAGPredictionResult RAGPrediction { get; set; } = new();
    public double ContextualRating { get; set; }
}
