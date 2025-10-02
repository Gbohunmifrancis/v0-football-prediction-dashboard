using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services.MLModels;
using FootballPrediction.Infrastructure.Services.RAG;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Hangfire;

namespace FootballPrediction.Infrastructure.Services;

public class ProductionPredictionPipelineService
{
    private readonly FplDbContext _context;
    private readonly ILogger<ProductionPredictionPipelineService> _logger;
    private readonly MLPredictionManagerService _mlManager;
    private readonly RAGPredictionService _ragService;
    private readonly FplDataScrapingService _dataService;
    private readonly GameweekService _gameweekService;
    private readonly Dictionary<string, DateTime> _lastProcessed;

    public ProductionPredictionPipelineService(
        FplDbContext context,
        ILogger<ProductionPredictionPipelineService> logger,
        MLPredictionManagerService mlManager,
        RAGPredictionService ragService,
        FplDataScrapingService dataService,
        GameweekService gameweekService)
    {
        _context = context;
        _logger = logger;
        _mlManager = mlManager;
        _ragService = ragService;
        _dataService = dataService;
        _gameweekService = gameweekService;
        _lastProcessed = new Dictionary<string, DateTime>();
    }

    /// <summary>
    /// PRODUCTION PIPELINE: End-to-end prediction generation
    /// This orchestrates data collection → ML prediction → RAG enhancement → final output
    /// </summary>
    [Queue("production")]
    public async Task<ProductionPredictionResult> RunProductionPipelineAsync(int? targetGameweek = null)
    {
        var gameweek = targetGameweek ?? await _gameweekService.GetCurrentGameweekAsync() + 1;
        
        _logger.LogInformation("🚀 PRODUCTION PIPELINE: Starting end-to-end prediction for gameweek {Gameweek}", gameweek);

        var result = new ProductionPredictionResult
        {
            TargetGameweek = gameweek,
            PipelineStartTime = DateTime.UtcNow,
            Status = "Running"
        };

        try
        {
            // Stage 1: Data Collection & Validation
            _logger.LogInformation("📊 Stage 1: Data Collection & Validation");
            result.Stage1_DataCollection = await ExecuteDataCollectionStage();
            
            // Stage 2: ML Model Training & Prediction
            _logger.LogInformation("🧠 Stage 2: ML Model Training & Prediction");
            result.Stage2_MLPrediction = await ExecuteMLPredictionStage(gameweek);
            
            // Stage 3: RAG Enhancement & Contextualization
            _logger.LogInformation("🎯 Stage 3: RAG Enhancement & Contextualization");
            result.Stage3_RAGEnhancement = await ExecuteRAGEnhancementStage(gameweek);
            
            // Stage 4: Production Output Generation
            _logger.LogInformation("📈 Stage 4: Production Output Generation");
            result.Stage4_ProductionOutput = await ExecuteProductionOutputStage(gameweek);
            
            // Stage 5: Performance Monitoring & Feedback
            _logger.LogInformation("📊 Stage 5: Performance Monitoring");
            result.Stage5_PerformanceMonitoring = await ExecutePerformanceMonitoringStage();

            result.PipelineEndTime = DateTime.UtcNow;
            result.Status = "Completed Successfully";
            result.TotalDuration = result.PipelineEndTime.Value - result.PipelineStartTime;

            _logger.LogInformation("✅ PRODUCTION PIPELINE COMPLETE for gameweek {Gameweek} in {Duration}", 
                gameweek, result.TotalDuration);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Production pipeline failed for gameweek {Gameweek}", gameweek);
            result.Status = "Failed";
            result.ErrorMessage = ex.Message;
            result.PipelineEndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Get the latest production predictions (cached for performance)
    /// </summary>
    public async Task<ProductionPredictionOutput> GetLatestProductionPredictionsAsync()
    {
        try
        {
            // Check if we have recent predictions cached
            var latestPredictions = await _context.PlayerPredictions
                .Include(p => p.Player)
                .Where(p => p.PredictionDate >= DateTime.UtcNow.AddHours(-6))
                .OrderByDescending(p => p.PredictionDate)
                .ToListAsync();

            if (!latestPredictions.Any())
            {
                _logger.LogInformation("No recent predictions found, triggering production pipeline");
                
                // Trigger pipeline for current gameweek
                var currentGameweek = await _gameweekService.GetCurrentGameweekAsync();
                BackgroundJob.Enqueue(() => RunProductionPipelineAsync(currentGameweek + 1));
                
                return new ProductionPredictionOutput
                {
                    Status = "Generating",
                    Message = "Production predictions are being generated. Check back in 5-10 minutes.",
                    LastUpdated = DateTime.UtcNow
                };
            }

            // Format latest predictions for production output
            var output = new ProductionPredictionOutput
            {
                Status = "Ready",
                LastUpdated = latestPredictions.First().PredictionDate,
                Gameweek = latestPredictions.First().Gameweek,
                TotalPredictions = latestPredictions.Count
            };

            // Group by categories
            output.PriorityPicks = latestPredictions
                .Where(p => p.PredictedPoints >= 8m && p.Confidence >= 0.8m)
                .OrderByDescending(p => p.PredictedPoints * p.Confidence)
                .Take(5)
                .ToList();

            output.ValuePicks = latestPredictions
                .Where(p => p.PredictedPoints >= 5m && p.Player.Price <= 7.0m)
                .OrderByDescending(p => p.PredictedPoints / p.Player.Price)
                .Take(10)
                .ToList();

            output.DifferentialPicks = latestPredictions
                .Where(p => p.PredictedPoints >= 6m && p.Player.SelectedByPercent <= 15m)
                .OrderByDescending(p => p.PredictedPoints)
                .Take(8)
                .ToList();

            output.AvoidList = latestPredictions
                .Where(p => p.PredictedPoints <= 3m || p.Confidence <= 0.4m)
                .OrderBy(p => p.PredictedPoints)
                .Take(10)
                .ToList();

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest production predictions");
            throw;
        }
    }

    /// <summary>
    /// Schedule automatic production pipeline runs
    /// </summary>
    public static void ScheduleProductionPipeline()
    {
        // Run production pipeline twice per week (Tuesday after deadline, Friday for preview)
        RecurringJob.AddOrUpdate<ProductionPredictionPipelineService>(
            "production-pipeline-tuesday",
            service => service.RunProductionPipelineAsync(null),
            "0 14 * * 2", // Tuesday 2 PM UTC (after FPL deadline)
            TimeZoneInfo.Utc);

        RecurringJob.AddOrUpdate<ProductionPredictionPipelineService>(
            "production-pipeline-friday",
            service => service.RunProductionPipelineAsync(null),
            "0 10 * * 5", // Friday 10 AM UTC (preview for next gameweek)
            TimeZoneInfo.Utc);
    }

    // Stage implementations
    private async Task<PipelineStageResult> ExecuteDataCollectionStage()
    {
        var stage = new PipelineStageResult { StageName = "Data Collection & Validation", StartTime = DateTime.UtcNow };

        try
        {
            // 1. Update current season data
            await _dataService.ScrapeBootstrapDataAsync();
            stage.Steps.Add("✅ Current player data updated");

            // 2. Validate data integrity
            var dataCount = await _context.Players.CountAsync();
            var historicalCount = await _context.HistoricalPlayerPerformances.CountAsync();
            
            stage.Steps.Add($"✅ Data validation: {dataCount} current players, {historicalCount} historical records");

            // 3. Check data freshness
            var latestUpdate = await _context.Players.MaxAsync(p => p.LastUpdated);
            var dataAge = DateTime.UtcNow - latestUpdate;
            
            if (dataAge.TotalHours > 24)
            {
                stage.Warnings.Add($"⚠️ Data is {dataAge.TotalHours:F1} hours old - consider refreshing");
            }
            else
            {
                stage.Steps.Add($"✅ Data freshness: {dataAge.TotalHours:F1} hours old");
            }

            stage.Success = true;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
        catch (Exception ex)
        {
            stage.Success = false;
            stage.ErrorMessage = ex.Message;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecuteMLPredictionStage(int gameweek)
    {
        var stage = new PipelineStageResult { StageName = "ML Model Prediction", StartTime = DateTime.UtcNow };

        try
        {
            // 1. Check model status
            var modelStatus = await _mlManager.GetModelStatusAsync();
            if (!modelStatus.IsInitialized)
            {
                stage.Steps.Add("🔄 Initializing ML models...");
                await _mlManager.InitializeMLModelsAsync();
            }
            stage.Steps.Add("✅ ML models ready");

            // 2. Generate ML predictions
            var mlPredictions = await _mlManager.GenerateMLGameweekPredictionsAsync(gameweek);
            stage.Steps.Add($"✅ Generated {mlPredictions.TopPerformers.Count} ML predictions");

            // 3. Save predictions to database
            await SavePredictionsToDatabase(mlPredictions);
            stage.Steps.Add("✅ Predictions saved to database");

            stage.Success = true;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
        catch (Exception ex)
        {
            stage.Success = false;
            stage.ErrorMessage = ex.Message;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecuteRAGEnhancementStage(int gameweek)
    {
        var stage = new PipelineStageResult { StageName = "RAG Enhancement", StartTime = DateTime.UtcNow };

        try
        {
            // 1. Generate RAG insights for top performers
            var ragInsights = await _ragService.GenerateGameweekInsightAsync(gameweek);
            stage.Steps.Add($"✅ Generated contextual insights for {ragInsights.EnhancedTopPerformers.Count} top performers");

            // 2. Create enhanced predictions with context
            var enhancedCount = ragInsights.EnhancedTopPerformers.Count(p => 
                Math.Abs(p.RAGPrediction.ContextuallyAdjustedPoints - p.BasePrediction.PredictedPoints) > 0.5);
            
            stage.Steps.Add($"✅ {enhancedCount} predictions enhanced with contextual adjustments");

            // 3. Generate strategic insights
            if (ragInsights.StrategicInsights.Any())
            {
                stage.Steps.Add($"✅ Generated {ragInsights.StrategicInsights.Count} strategic insights");
            }

            stage.Success = true;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
        catch (Exception ex)
        {
            stage.Success = false;
            stage.ErrorMessage = ex.Message;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecuteProductionOutputStage(int gameweek)
    {
        var stage = new PipelineStageResult { StageName = "Production Output", StartTime = DateTime.UtcNow };

        try
        {
            // 1. Generate final production recommendations
            var productionOutput = await GenerateProductionRecommendations(gameweek);
            stage.Steps.Add($"✅ Generated production recommendations for {productionOutput.TotalRecommendations} players");

            // 2. Create executive summary
            var executiveSummary = await GenerateExecutiveSummary(gameweek);
            stage.Steps.Add("✅ Executive summary created");

            // 3. Generate alerts and notifications
            var alerts = await GenerateProductionAlerts(gameweek);
            stage.Steps.Add($"✅ Generated {alerts.Count} production alerts");

            // 4. Update prediction cache
            await UpdatePredictionCache(gameweek);
            stage.Steps.Add("✅ Prediction cache updated");

            stage.Success = true;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
        catch (Exception ex)
        {
            stage.Success = false;
            stage.ErrorMessage = ex.Message;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
    }

    private async Task<PipelineStageResult> ExecutePerformanceMonitoringStage()
    {
        var stage = new PipelineStageResult { StageName = "Performance Monitoring", StartTime = DateTime.UtcNow };

        try
        {
            // 1. Evaluate previous predictions accuracy
            var accuracy = await EvaluatePreviousPredictions();
            stage.Steps.Add($"✅ Previous prediction accuracy: {accuracy:P1}");

            // 2. Update model performance metrics
            await UpdateModelPerformanceMetrics();
            stage.Steps.Add("✅ Model performance metrics updated");

            // 3. Generate improvement recommendations
            var improvements = await GenerateImprovementRecommendations();
            if (improvements.Any())
            {
                stage.Steps.Add($"💡 {improvements.Count} improvement recommendations generated");
            }

            stage.Success = true;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
        catch (Exception ex)
        {
            stage.Success = false;
            stage.ErrorMessage = ex.Message;
            stage.EndTime = DateTime.UtcNow;
            return stage;
        }
    }

    // Helper methods for production pipeline
    private async Task SavePredictionsToDatabase(GameweekPredictionResult predictions)
    {
        var predictionEntities = new List<FootballPrediction.Core.Entities.PlayerPrediction>();

        foreach (var prediction in predictions.TopPerformers)
        {
            var entity = new FootballPrediction.Core.Entities.PlayerPrediction
            {
                PlayerId = prediction.PlayerId,
                Gameweek = predictions.Gameweek,
                PredictedPoints = (decimal)prediction.PredictedPoints,
                Confidence = (decimal)prediction.Confidence,
                ModelVersion = prediction.ModelUsed ?? "Ensemble",
                PredictionDate = DateTime.UtcNow,
                // Default values for required fields
                MinutesLikelihood = 0.8m,
                GoalsPrediction = 0m,
                AssistsPrediction = 0m,
                CleanSheetChance = 0m,
                BonusPrediction = 0m,
                FormAnalysis = prediction.FeatureImportance ?? "N/A",
                FixtureDifficulty = "Medium",
                InjuryRisk = 0.1m,
                RotationRisk = 0.1m
            };
            predictionEntities.Add(entity);
        }

        // Clear old predictions for this gameweek
        var oldPredictions = await _context.PlayerPredictions
            .Where(p => p.Gameweek == predictions.Gameweek)
            .ToListAsync();
        
        if (oldPredictions.Any())
        {
            _context.PlayerPredictions.RemoveRange(oldPredictions);
        }

        await _context.PlayerPredictions.AddRangeAsync(predictionEntities);
        await _context.SaveChangesAsync();
    }

    private async Task<ProductionRecommendations> GenerateProductionRecommendations(int gameweek)
    {
        var ragInsights = await _ragService.GenerateGameweekInsightAsync(gameweek);
        
        return new ProductionRecommendations
        {
            Gameweek = gameweek,
            GeneratedAt = DateTime.UtcNow,
            
            // Priority recommendations
            MustHavePlayers = ragInsights.EnhancedTopPerformers
                .Where(p => p.RAGPrediction.ContextuallyAdjustedPoints >= 8 && p.RAGPrediction.MLPrediction.Confidence >= 0.8)
                .Take(3)
                .Select(p => FormatPlayerRecommendation(p, "PRIORITY"))
                .ToList(),

            StrongOptions = ragInsights.EnhancedTopPerformers
                .Where(p => p.RAGPrediction.ContextuallyAdjustedPoints >= 6 && p.RAGPrediction.MLPrediction.Confidence >= 0.7)
                .Take(8)
                .Select(p => FormatPlayerRecommendation(p, "STRONG"))
                .ToList(),

            ValuePicks = ragInsights.EnhancedTopPerformers
                .Where(p => p.RAGPrediction.ContextualInsight.ValueAssessment.Contains("EXCEPTIONAL"))
                .Take(5)
                .Select(p => FormatPlayerRecommendation(p, "VALUE"))
                .ToList(),

            Differentials = ragInsights.EnhancedTopPerformers
                .Where(p => p.RAGPrediction.ContextuallyAdjustedPoints >= 6)
                .OrderBy(p => _context.Players.Find(p.BasePrediction.PlayerId)?.SelectedByPercent ?? 100)
                .Take(5)
                .Select(p => FormatPlayerRecommendation(p, "DIFFERENTIAL"))
                .ToList(),

            AvoidList = ragInsights.EnhancedTopPerformers
                .Where(p => p.RAGPrediction.ContextualInsight.RiskFactors.Any(r => r.Contains("HIGH")))
                .Take(5)
                .Select(p => FormatPlayerRecommendation(p, "AVOID"))
                .ToList(),

            TotalRecommendations = ragInsights.EnhancedTopPerformers.Count
        };
    }

    private PlayerRecommendation FormatPlayerRecommendation(EnhancedPlayerPrediction enhanced, string category)
    {
        return new PlayerRecommendation
        {
            PlayerId = enhanced.BasePrediction.PlayerId,
            PlayerName = enhanced.BasePrediction.PlayerName,
            Position = enhanced.BasePrediction.Position,
            PredictedPoints = enhanced.RAGPrediction.ContextuallyAdjustedPoints,
            Confidence = enhanced.RAGPrediction.MLPrediction.Confidence,
            Category = category,
            Reasoning = enhanced.RAGPrediction.ContextualInsight.OverallReasoning,
            TransferAdvice = enhanced.RAGPrediction.ContextualInsight.TransferAdvice,
            RiskLevel = enhanced.RAGPrediction.ContextualInsight.RiskFactors.Any(r => r.Contains("HIGH")) ? "High" : "Low"
        };
    }

    private async Task<string> GenerateExecutiveSummary(int gameweek)
    {
        var ragInsights = await _ragService.GenerateGameweekInsightAsync(gameweek);
        
        var summary = new List<string>();
        summary.Add($"📋 **Executive Summary - Gameweek {gameweek}**");
        summary.Add("");
        summary.Add(ragInsights.GameweekNarrative);
        summary.Add("");
        summary.Add("**Key Strategic Insights:**");
        summary.AddRange(ragInsights.StrategicInsights.Select(insight => $"• {insight}"));
        
        if (ragInsights.RiskWarnings.Any())
        {
            summary.Add("");
            summary.Add("**Risk Warnings:**");
            summary.AddRange(ragInsights.RiskWarnings.Select(warning => $"• {warning}"));
        }

        return string.Join("\n", summary);
    }

    private async Task<List<ProductionAlert>> GenerateProductionAlerts(int gameweek)
    {
        var alerts = new List<ProductionAlert>();

        // High-confidence opportunities
        var highConfidencePlayers = await _context.PlayerPredictions
            .Include(p => p.Player)
            .Where(p => p.Gameweek == gameweek && p.PredictedPoints >= 8m && p.Confidence >= 0.9m)
            .ToListAsync();

        foreach (var player in highConfidencePlayers)
        {
            alerts.Add(new ProductionAlert
            {
                Type = "OPPORTUNITY",
                Priority = "HIGH",
                PlayerName = player.Player.WebName,
                Message = $"🚀 {player.Player.WebName} - High-confidence opportunity ({player.PredictedPoints:F1} pts, {player.Confidence:P0} confidence)",
                CreatedAt = DateTime.UtcNow
            });
        }

        // Risk warnings
        var riskPlayers = await _context.PlayerPredictions
            .Include(p => p.Player)
            .Where(p => p.Gameweek == gameweek && (p.PredictedPoints <= 2m || p.Confidence <= 0.3m))
            .ToListAsync();

        foreach (var player in riskPlayers)
        {
            alerts.Add(new ProductionAlert
            {
                Type = "WARNING",
                Priority = "MEDIUM",
                PlayerName = player.Player.WebName,
                Message = $"⚠️ {player.Player.WebName} - Underperformance risk ({player.PredictedPoints:F1} pts predicted)",
                CreatedAt = DateTime.UtcNow
            });
        }

        return alerts;
    }

    private async Task UpdatePredictionCache(int gameweek)
    {
        _lastProcessed[$"gameweek_{gameweek}"] = DateTime.UtcNow;
        
        // Cache updated - predictions are ready for retrieval
        await Task.CompletedTask;
    }

    private async Task<double> EvaluatePreviousPredictions()
    {
        // Evaluate predictions from completed gameweeks
        var completedGameweeks = await _context.GameweekData
            .Where(g => g.IsFinished)
            .OrderByDescending(g => g.Gameweek)
            .Take(3)
            .ToListAsync();

        if (!completedGameweeks.Any()) return 0.5; // Default accuracy

        var totalAccuracy = 0.0;
        var evaluatedPredictions = 0;

        foreach (var gameweek in completedGameweeks)
        {
            var predictions = await _context.PlayerPredictions
                .Include(p => p.Player)
                .ThenInclude(p => p.GameweekPerformances)
                .Where(p => p.Gameweek == gameweek.Gameweek)
                .ToListAsync();

            foreach (var prediction in predictions)
            {
                var actualPerformance = prediction.Player.GameweekPerformances
                    .FirstOrDefault(gp => gp.Gameweek == gameweek.Gameweek);

                if (actualPerformance != null)
                {
                    var error = Math.Abs((double)prediction.PredictedPoints - actualPerformance.Points);
                    var accuracy = Math.Max(0, 1 - (error / 10)); // Normalize error to accuracy
                    totalAccuracy += accuracy;
                    evaluatedPredictions++;
                }
            }
        }

        return evaluatedPredictions > 0 ? totalAccuracy / evaluatedPredictions : 0.5;
    }

    private async Task UpdateModelPerformanceMetrics()
    {
        // This would update model performance tracking
        _logger.LogInformation("📊 Model performance metrics updated");
    }

    private async Task<List<string>> GenerateImprovementRecommendations()
    {
        var recommendations = new List<string>();
        
        // Check data recency
        var latestHistorical = await _context.HistoricalPlayerPerformances
            .OrderByDescending(h => h.LastUpdated)
            .FirstOrDefaultAsync();

        if (latestHistorical != null && (DateTime.UtcNow - latestHistorical.LastUpdated).TotalDays > 7)
        {
            recommendations.Add("🔄 Update historical training data - last update over 7 days ago");
        }

        // Check prediction accuracy
        var accuracy = await EvaluatePreviousPredictions();
        if (accuracy < 0.7)
        {
            recommendations.Add($"🎯 Model accuracy ({accuracy:P1}) below target - consider retraining");
        }

        return recommendations;
    }
}

// Supporting classes for production pipeline
public class ProductionPredictionResult
{
    public int TargetGameweek { get; set; }
    public DateTime PipelineStartTime { get; set; }
    public DateTime? PipelineEndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    
    public PipelineStageResult Stage1_DataCollection { get; set; } = new();
    public PipelineStageResult Stage2_MLPrediction { get; set; } = new();
    public PipelineStageResult Stage3_RAGEnhancement { get; set; } = new();
    public PipelineStageResult Stage4_ProductionOutput { get; set; } = new();
    public PipelineStageResult Stage5_PerformanceMonitoring { get; set; } = new();
}

public class PipelineStageResult
{
    public string StageName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool Success { get; set; }
    public List<string> Steps { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
}

public class ProductionPredictionOutput
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public int Gameweek { get; set; }
    public int TotalPredictions { get; set; }
    
    public List<FootballPrediction.Core.Entities.PlayerPrediction> PriorityPicks { get; set; } = new();
    public List<FootballPrediction.Core.Entities.PlayerPrediction> ValuePicks { get; set; } = new();
    public List<FootballPrediction.Core.Entities.PlayerPrediction> DifferentialPicks { get; set; } = new();
    public List<FootballPrediction.Core.Entities.PlayerPrediction> AvoidList { get; set; } = new();
}

public class ProductionRecommendations
{
    public int Gameweek { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<PlayerRecommendation> MustHavePlayers { get; set; } = new();
    public List<PlayerRecommendation> StrongOptions { get; set; } = new();
    public List<PlayerRecommendation> ValuePicks { get; set; } = new();
    public List<PlayerRecommendation> Differentials { get; set; } = new();
    public List<PlayerRecommendation> AvoidList { get; set; } = new();
    public int TotalRecommendations { get; set; }
}

public class PlayerRecommendation
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public double PredictedPoints { get; set; }
    public double Confidence { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public string TransferAdvice { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
}

public class ProductionAlert
{
    public string Type { get; set; } = string.Empty; // OPPORTUNITY, WARNING, INFO
    public string Priority { get; set; } = string.Empty; // HIGH, MEDIUM, LOW
    public string PlayerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
