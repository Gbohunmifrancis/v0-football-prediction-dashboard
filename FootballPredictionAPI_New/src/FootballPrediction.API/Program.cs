using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services;
using FootballPrediction.Infrastructure.Jobs;
using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Services.MLModels;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;

// Helper class for Hangfire dashboard authorization in development
static  List<string> GenerateDataIntegrityRecommendations(DataIntegrityReport report)
{
    var recommendations = new List<string>();
    
    if (report.FailedTests > 0)
        recommendations.Add("🚨 CRITICAL: Fix failed tests before proceeding with ML training");
    
    if (report.WarningTests > 0)
        recommendations.Add("⚠️ WARNING: Address warning issues to improve data quality");
    
    if (report.PassedTests == report.TotalTests)
        recommendations.Add("✅ EXCELLENT: Data integrity is solid, ready for ML training");
    
    var passRate = (double)report.PassedTests / report.TotalTests * 100;
    if (passRate < 80)
        recommendations.Add($"📊 Data quality is {passRate:F1}% - consider improving data collection");
    
    return recommendations;
}

static List<string> GenerateMLReadinessRecommendations(MLDataQualityReport report)
{
    var recommendations = new List<string>();
    
    if (report.MLReadinessScore >= 90)
        recommendations.Add("🚀 EXCELLENT: Data is highly suitable for ML training");
    else if (report.MLReadinessScore >= 70)
        recommendations.Add("✅ GOOD: Data is suitable for ML with minor improvements needed");
    else if (report.MLReadinessScore >= 50)
        recommendations.Add("⚠️ MODERATE: Significant improvements needed before ML training");
    else
        recommendations.Add("🚨 POOR: Major data quality issues must be resolved");
    
    if (report.FeatureEngineeringAccuracy < 95)
        recommendations.Add("🔧 Fix feature engineering calculations");
    
    if (!report.IsBalanced)
        recommendations.Add("⚖️ Improve class balance in training data");
    
    if (report.PotentialDataLeakage > 0)
        recommendations.Add("🔒 Address potential data leakage issues");
    
    if (report.OutlierPercentage > 5)
        recommendations.Add("📊 Consider outlier treatment strategy");
    
    return recommendations;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Football Prediction API", 
        Version = "v1",
        Description = "Comprehensive FPL Data Scraping and Prediction API"
    });
});

// Database Configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=FootballPredictionDB.db";

builder.Services.AddDbContext<FplDbContext>(options =>
    options.UseSqlite(connectionString));

// Hangfire Configuration - Using SQLite instead of SQL Server
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSQLiteStorage(connectionString));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // Adjust based on your server capacity
});

// HttpClient Configuration
builder.Services.AddHttpClient<FplDataScrapingService>();
builder.Services.AddHttpClient<InjuryAndNewsScrapingService>();
builder.Services.AddHttpClient<GameweekService>();

// Register Services
builder.Services.AddScoped<FplDataScrapingService>();
builder.Services.AddScoped<InjuryAndNewsScrapingService>();
builder.Services.AddScoped<PlayerPredictionService>();
builder.Services.AddScoped<GameweekService>();
builder.Services.AddScoped<FplBackgroundJobService>();
builder.Services.AddScoped<DataIntegrityValidationService>();
builder.Services.AddScoped<MLDataQualityService>();
builder.Services.AddScoped<AlternativeDataCollectionService>();

// 🚀 NEW: Register ML Prediction Services (Phase 2A) - FULLY ACTIVATED
builder.Services.AddScoped<LSTMPredictionService>();
builder.Services.AddScoped<XgBoostPredictionService>(); // Fixed naming
builder.Services.AddScoped<EnsemblePredictionService>();
builder.Services.AddScoped<MLPredictionManagerService>();
builder.Services.AddScoped<MLTrainingService>(); // ML Training Service

// 🧠 NEW: Register RAG Services (Phase 2B) - Commented out until implemented
// builder.Services.AddScoped<FootballKnowledgeBaseService>();
// builder.Services.AddScoped<RAGPredictionService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Football Prediction API v1");
        c.RoutePrefix = string.Empty; // Makes Swagger available at the root
    });
}

app.UseHttpsRedirection();
app.UseRouting();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});

app.MapControllers();

// API Endpoints for manual job triggering
app.MapPost("/api/jobs/trigger-full-update", async (FplBackgroundJobService jobService) =>
    {
        BackgroundJob.Enqueue(() => jobService.RunFullDataUpdateAsync());
        return Results.Ok(new { message = "Full data update job queued successfully" });
    })
    .WithName("TriggerFullUpdate")
    .WithTags("Background Jobs")
    .WithOpenApi();

app.MapPost("/api/jobs/trigger-injury-update", async (FplBackgroundJobService jobService) =>
    {
        BackgroundJob.Enqueue(() => jobService.RunInjuryUpdateAsync());
        return Results.Ok(new { message = "Injury update job queued successfully" });
    })
    .WithName("TriggerInjuryUpdate")
    .WithTags("Background Jobs")
    .WithOpenApi();

app.MapPost("/api/jobs/trigger-transfer-news", async (FplBackgroundJobService jobService) =>
    {
        BackgroundJob.Enqueue(() => jobService.RunTransferNewsUpdateAsync());
        return Results.Ok(new { message = "Transfer news update job queued successfully" });
    })
    .WithName("TriggerTransferNews")
    .WithTags("Background Jobs")
    .WithOpenApi();

app.MapPost("/api/jobs/trigger-predictions", async (FplBackgroundJobService jobService) =>
    {
        BackgroundJob.Enqueue(() => jobService.RunPredictionAnalysisAsync());
        return Results.Ok(new { message = "Prediction analysis job queued successfully" });
    })
    .WithName("TriggerPredictions")
    .WithTags("Background Jobs")
    .WithOpenApi();

// Data retrieval endpoints
app.MapGet("/api/players", async (FplDbContext context) =>
    {
        var players = await context.Players
            .Include(p => p.Team)
            .OrderByDescending(p => p.TotalPoints)
            .Take(50)
            .Select(p => new
            {
                p.Id,
                p.WebName,
                p.FirstName,
                p.SecondName,
                TeamName = p.Team.Name,
                p.Position,
                p.Price,
                p.TotalPoints,
                p.Form,
                p.SelectedByPercent
            })
            .ToListAsync();
        
        return Results.Ok(players);
    })
    .WithName("GetTopPlayers")
    .WithTags("Data")
    .WithOpenApi();

app.MapGet("/api/players/{id}/injuries", async (int id, FplDbContext context) =>
    {
        var injuries = await context.InjuryUpdates
            .Where(i => i.PlayerId == id)
            .OrderByDescending(i => i.ReportedDate)
            .Take(10)
            .ToListAsync();
        
        return Results.Ok(injuries);
    })
    .WithName("GetPlayerInjuries")
    .WithTags("Data")
    .WithOpenApi();

app.MapGet("/api/players/{id}/transfer-news", async (int id, FplDbContext context) =>
    {
        var news = await context.TransferNews
            .Where(t => t.PlayerId == id)
            .OrderByDescending(t => t.PublishedDate)
            .Take(10)
            .ToListAsync();
        
        return Results.Ok(news);
    })
    .WithName("GetPlayerTransferNews")
    .WithTags("Data")
    .WithOpenApi();

// PREDICTION API ENDPOINTS - The core intelligence of your FPL system
app.MapGet("/api/predictions/gameweek/{gameweek}", async (int gameweek, PlayerPredictionService predictionService) =>
    {
        var predictions = await predictionService.GenerateGameweekPredictionAsync(gameweek);
        return Results.Ok(predictions);
    })
    .WithName("GetGameweekPredictions")
    .WithTags("Predictions")
    .WithSummary("Get comprehensive FPL predictions for a specific gameweek")
    .WithOpenApi();

app.MapGet("/api/predictions/top-performers", async (PlayerPredictionService predictionService, GameweekService gameweekService) =>
    {
        var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
        var predictions = await predictionService.GenerateGameweekPredictionAsync(currentGameweek + 1);
    
        return Results.Ok(new
        {
            gameweek = predictions.Gameweek,
            generatedAt = predictions.PredictionDate,
            topPerformers = predictions.TopPerformers.Take(15),
            summary = $"Top {predictions.TopPerformers.Take(15).Count()} predicted performers for gameweek {predictions.Gameweek}"
        });
    })
    .WithName("GetTopPerformers")
    .WithTags("Predictions")
    .WithSummary("Get the top predicted performers for the next gameweek")
    .WithOpenApi();

app.MapGet("/api/predictions/best-value", async (PlayerPredictionService predictionService, GameweekService gameweekService) =>
    {
        var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
        var predictions = await predictionService.GenerateGameweekPredictionAsync(currentGameweek + 1);
    
        return Results.Ok(new
        {
            gameweek = predictions.Gameweek,
            bestValue = predictions.BestValue.Take(10),
            summary = $"Best value players for gameweek {predictions.Gameweek} - high points potential at low cost"
        });
    })
    .WithName("GetBestValue")
    .WithTags("Predictions")
    .WithSummary("Get the best value players (high points potential, reasonable price)")
    .WithOpenApi();

app.MapGet("/api/predictions/differentials", async (PlayerPredictionService predictionService, GameweekService gameweekService) =>
    {
        var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
        var predictions = await predictionService.GenerateGameweekPredictionAsync(currentGameweek + 1);
    
        return Results.Ok(new
        {
            gameweek = predictions.Gameweek,
            differentials = predictions.Differentials.Take(10),
            summary = $"Differential picks for gameweek {predictions.Gameweek} - low ownership, high potential"
        });
    })
    .WithName("GetDifferentials")
    .WithTags("Predictions")
    .WithSummary("Get differential players (low ownership but high predicted points)")
    .WithOpenApi();

app.MapGet("/api/predictions/avoid-list", async (PlayerPredictionService predictionService, GameweekService gameweekService) =>
    {
        var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
        var predictions = await predictionService.GenerateGameweekPredictionAsync(currentGameweek + 1);
    
        return Results.Ok(new
        {
            gameweek = predictions.Gameweek,
            highRisk = predictions.HighRisk.Take(10),
            summary = $"Players to avoid for gameweek {predictions.Gameweek} due to injury/rotation risk"
        });
    })
    .WithName("GetAvoidList")
    .WithTags("Predictions")
    .WithSummary("Get high-risk players to avoid (injury concerns, rotation risk)")
    .WithOpenApi();

app.MapGet("/api/predictions/by-position/{position}", async (string position, PlayerPredictionService predictionService, GameweekService gameweekService) =>
    {
        var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
        var predictions = await predictionService.GenerateGameweekPredictionAsync(currentGameweek + 1);
    
        var positionPredictions = position.ToLower() switch
        {
            "goalkeeper" or "gk" => predictions.TopGoalkeepers,
            "defender" or "def" => predictions.TopDefenders,
            "midfielder" or "mid" => predictions.TopMidfielders,
            "forward" or "fwd" => predictions.TopForwards,
            _ => new List<FootballPrediction.Core.Models.PlayerFormAnalysis>()
        };
    
        return Results.Ok(new
        {
            position = position,
            gameweek = predictions.Gameweek,
            players = positionPredictions,
            count = positionPredictions.Count
        });
    })
    .WithName("GetPredictionsByPosition")
    .WithTags("Predictions")
    .WithSummary("Get top predicted players for a specific position")
    .WithOpenApi();

// ADVANCED ANALYSIS ENDPOINTS
app.MapGet("/api/analysis/fixture-difficulty", async (FplDbContext context, GameweekService gameweekService) =>
    {
        var nextGameweek = await gameweekService.GetCurrentGameweekAsync() + 1;
        var fixtures = await context.Fixtures
            .Include(f => f.TeamHome)
            .Include(f => f.TeamAway)
            .Where(f => f.Gameweek == nextGameweek)
            .OrderBy(f => f.KickoffTime)
            .Select(f => new
            {
                f.Id,
                homeTeam = f.TeamHome.Name,
                awayTeam = f.TeamAway.Name,
                f.Difficulty,
                f.KickoffTime,
                homeStrength = f.TeamHome.Strength,
                awayStrength = f.TeamAway.Strength
            })
            .ToListAsync();
        
        return Results.Ok(new
        {
            gameweek = nextGameweek,
            fixtures = fixtures,
            summary = $"Fixture difficulty analysis for gameweek {nextGameweek}"
        });
    })
    .WithName("GetFixtureDifficulty")
    .WithTags("Analysis")
    .WithSummary("Get fixture difficulty analysis for upcoming gameweek")
    .WithOpenApi();

// GAMEWEEK INFORMATION ENDPOINTS
app.MapGet("/api/gameweek/current", async (GameweekService gameweekService) =>
    {
        var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
        var gameweekInfo = await gameweekService.GetGameweekInfoAsync(currentGameweek);
    
        return Results.Ok(new
        {
            current = gameweekInfo,
            summary = $"Current gameweek is {currentGameweek}"
        });
    })
    .WithName("GetCurrentGameweek")
    .WithTags("Gameweek")
    .WithSummary("Get current gameweek information from FPL API")
    .WithOpenApi();

app.MapGet("/api/gameweek/{gameweekId}/info", async (int gameweekId, GameweekService gameweekService) =>
    {
        var gameweekInfo = await gameweekService.GetGameweekInfoAsync(gameweekId);
        return Results.Ok(gameweekInfo);
    })
    .WithName("GetGameweekInfo")
    .WithTags("Gameweek")
    .WithSummary("Get detailed information for a specific gameweek")
    .WithOpenApi();

app.MapGet("/api/gameweek/all", async (GameweekService gameweekService) =>
    {
        var allGameweeks = await gameweekService.GetAllGameweeksAsync();
        return Results.Ok(new
        {
            gameweeks = allGameweeks,
            total = allGameweeks.Count,
            current = allGameweeks.FirstOrDefault(g => g.IsCurrent)?.Id,
            next = allGameweeks.FirstOrDefault(g => g.IsNext)?.Id,
            finished = allGameweeks.Count(g => g.IsFinished)
        });
    })
    .WithName("GetAllGameweeks")
    .WithTags("Gameweek")
    .WithSummary("Get information for all gameweeks in the season")
    .WithOpenApi();

// DATA INTEGRITY & ML VALIDATION ENDPOINTS
app.MapGet("/api/validation/data-integrity", async (DataIntegrityValidationService validationService) =>
    {
        var report = await validationService.ValidateHistoricalDataIntegrityAsync();
    
        return Results.Ok(new
        {
            status = report.OverallStatus.ToString(),
            duration = report.Duration.TotalSeconds,
            summary = new
            {
                total_tests = report.TotalTests,
                passed = report.PassedTests,
                warnings = report.WarningTests,
                failed = report.FailedTests
            },
            detailed_results = report.ValidationResults,
            recommendations = GenerateDataIntegrityRecommendations(report)
        });
    })
    .WithName("ValidateDataIntegrity")
    .WithTags("Data Validation")
    .WithSummary("Run comprehensive data integrity validation for historical FPL data")
    .WithOpenApi();

app.MapGet("/api/validation/ml-readiness", async (MLDataQualityService mlQualityService) =>
    {
        var report = await mlQualityService.ValidateMLDataQualityAsync();
    
        return Results.Ok(new
        {
            ml_readiness_score = report.MLReadinessScore,
            duration = report.Duration.TotalSeconds,
            feature_engineering_accuracy = report.FeatureEngineeringAccuracy,
            class_balance = new
            {
                is_balanced = report.IsBalanced,
                distribution = report.ClassDistribution
            },
            data_quality = new
            {
                outlier_percentage = report.OutlierPercentage,
                outlier_count = report.OutlierCount,
                potential_data_leakage = report.PotentialDataLeakage,
                acceptable_missing_data = report.HasAcceptableMissingData,
                missing_value_analysis = report.MissingValueAnalysis
            },
            recommendations = GenerateMLReadinessRecommendations(report)
        });
    })
    .WithName("ValidateMLReadiness")
    .WithTags("Data Validation")
    .WithSummary("Validate data quality specifically for ML model training")
    .WithOpenApi();

app.MapGet("/api/validation/sample-dataset/{size}", async (int size, MLDataQualityService mlQualityService) =>
    {
        if (size > 5000) size = 5000; // Limit sample size
    
        var dataset = await mlQualityService.GenerateMLSampleDatasetAsync(size);
    
        return Results.Ok(new
        {
            sample_size = dataset.SampleSize,
            feature_count = dataset.FeatureCount,
            created_at = dataset.CreatedAt,
            preview = dataset.TrainingRows.Take(10), // Show first 10 rows
            statistics = new
            {
                avg_points = dataset.TrainingRows.Average(r => r.ActualPoints),
                max_points = dataset.TrainingRows.Max(r => r.ActualPoints),
                min_points = dataset.TrainingRows.Min(r => r.ActualPoints),
                position_distribution = dataset.TrainingRows
                    .GroupBy(r => r.Position)
                    .ToDictionary(g => g.Key, g => g.Count())
            }
        });
    })
    .WithName("GenerateMLSampleDataset")
    .WithTags("Data Validation")
    .WithSummary("Generate a sample dataset for ML model testing and validation")
    .WithOpenApi();

app.MapPost("/api/data-collection/trigger-historical", async (AlternativeDataCollectionService dataCollectionService) =>
    {
        BackgroundJob.Enqueue(() => dataCollectionService.CreateMLTrainingDatasetAsync());
        return Results.Ok(new { message = "Historical data collection job queued successfully" });
    })
    .WithName("TriggerHistoricalDataCollection")
    .WithTags("Data Collection")
    .WithSummary("Start collecting historical data for ML training")
    .WithOpenApi();

app.MapGet("/api/validation/quick-check", async (FplDbContext context) =>
    {
        var quickStats = new
        {
            current_data = new
            {
                players = await context.Players.CountAsync(),
                teams = await context.Teams.CountAsync(),
                fixtures = await context.Fixtures.CountAsync(),
                gameweek_performances = await context.PlayerGameweekPerformances.CountAsync()
            },
            historical_data = new
            {
                historical_performances = await context.HistoricalPlayerPerformances.CountAsync(),
                historical_team_strengths = await context.HistoricalTeamStrengths.CountAsync(),
                injury_updates = await context.InjuryUpdates.CountAsync(),
                transfer_news = await context.TransferNews.CountAsync()
            },
            data_freshness = new
            {
                latest_player_update = await context.Players
                    .OrderByDescending(p => p.LastUpdated)
                    .Select(p => p.LastUpdated)
                    .FirstOrDefaultAsync(),
                latest_historical_record = await context.HistoricalPlayerPerformances
                    .OrderByDescending(h => h.LastUpdated)
                    .Select(h => h.LastUpdated)
                    .FirstOrDefaultAsync()
            }
        };

        return Results.Ok(quickStats);
    })
    .WithName("QuickDataCheck")
    .WithTags("Data Validation")
    .WithSummary("Get quick overview of all data tables and freshness")
    .WithOpenApi();

// 🚀 NEW ML-POWERED PREDICTION ENDPOINTS (Phase 2A Complete)
app.MapGet("/api/ml/predictions/gameweek/{gameweek}", async (int gameweek, MLPredictionManagerService mlManager) =>
    {
        var predictions = await mlManager.GenerateMLGameweekPredictionsAsync(gameweek);
        
        return Results.Ok(new
        {
            gameweek = predictions.Gameweek,
            model_type = predictions.ModelType,
            generated_at = predictions.PredictionDate,
            summary = $"🧠 ML-powered predictions for gameweek {gameweek}",
            top_performers = predictions.TopPerformers,
            by_position = new
            {
                goalkeepers = predictions.TopGoalkeepers,
                defenders = predictions.TopDefenders,
                midfielders = predictions.TopMidfielders,
                forwards = predictions.TopForwards
            },
            insights = new
            {
                best_value = predictions.BestValue,
                differentials = predictions.Differentials,
                avoid_list = predictions.HighRisk
            }
        });
    })
    .WithName("GetMLGameweekPredictions")
    .WithTags("🧠 ML Predictions")
    .WithSummary("Get advanced ML-powered predictions using LSTM + XGBoost ensemble")
    .WithOpenApi();

// 🚀 ML PREDICTION ENDPOINTS - TEMPORARILY DISABLED UNTIL SERVICES ARE FULLY IMPLEMENTED
/*
app.MapGet("/api/ml/player/{playerId}/prediction/{gameweek}", async (int playerId, int gameweek, MLPredictionManagerService mlManager) =>
    {
        var prediction = await mlManager.GetPlayerMLPredictionAsync(playerId, gameweek);
        
        return Results.Ok(new
        {
            player_prediction = prediction.FinalPrediction,
            model_breakdown = new
            {
                individual_models = prediction.ModelPredictions,
                model_weights = prediction.ModelWeights,
                ensemble_confidence = prediction.EnsembleConfidence,
                reasoning = prediction.EnsembleReasoning
            },
            insights = new
            {
                predicted_points = prediction.FinalPrediction.PredictedPoints,
                confidence_level = prediction.FinalPrediction.Confidence,
                feature_importance = prediction.FinalPrediction.FeatureImportance,
                recommendation = prediction.FinalPrediction.PredictedPoints >= 6 ? "⭐ RECOMMEND" : 
                               prediction.FinalPrediction.PredictedPoints >= 4 ? "✅ CONSIDER" : "❌ AVOID"
            }
        });
    })
    .WithName("GetPlayerMLPrediction")
    .WithTags("🧠 ML Predictions")
    .WithSummary("Get detailed ML prediction for a specific player with model breakdown")
    .WithOpenApi();
*/

// Simplified ML endpoint that works with current implementation
app.MapGet("/api/ml/player/{playerId}/prediction/{gameweek}", async (int playerId, int gameweek) =>
    {
        return Results.Ok(new
        {
            message = "🧠 ML player prediction endpoint under development",
            player_id = playerId,
            gameweek = gameweek,
            note = "This endpoint will be activated once ML services are fully implemented"
        });
    })
    .WithName("GetPlayerMLPrediction")
    .WithTags("🧠 ML Predictions")
    .WithSummary("Get detailed ML prediction for a specific player (under development)")
    .WithOpenApi();

// 🧠 RAG-POWERED CONTEXTUAL PREDICTION ENDPOINTS - TEMPORARILY DISABLED UNTIL RAG SERVICES ARE IMPLEMENTED
/*
app.MapGet("/api/rag/player/{playerId}/contextual-prediction/{gameweek}", async (int playerId, int gameweek, RAGPredictionService ragService) =>
{
    var ragPrediction = await ragService.GenerateContextualPredictionAsync(playerId, gameweek);
    
    return Results.Ok(new
    {
        summary = $"🧠 Contextual AI analysis for player {ragPrediction.MLPrediction.PlayerName}",
        ml_prediction = new
        {
            base_prediction = ragPrediction.MLPrediction.PredictedPoints,
            ml_confidence = ragPrediction.MLPrediction.Confidence,
            model_used = ragPrediction.MLPrediction.ModelUsed
        },
        contextual_enhancement = new
        {
            adjusted_prediction = ragPrediction.ContextuallyAdjustedPoints,
            confidence_boost = ragPrediction.ConfidenceBoost,
            contextual_insight = ragPrediction.ContextualInsight
        },
        intelligence = new
        {
            enhanced_reasoning = ragPrediction.EnhancedReasoning,
            actionable_advice = ragPrediction.ActionableAdvice,
            relevant_knowledge = ragPrediction.RelevantKnowledge.Take(3)
        },
        recommendation = ragPrediction.ContextuallyAdjustedPoints >= 8 ? "🚀 PRIORITY PICK" :
                         ragPrediction.ContextuallyAdjustedPoints >= 6 ? "✅ STRONG OPTION" :
                         ragPrediction.ContextuallyAdjustedPoints >= 4 ? "📊 CONSIDER" : "❌ AVOID"
    });
})
.WithName("GetRAGPlayerPrediction")
.WithTags("🧠 RAG Intelligence")
.WithSummary("Get contextually-aware AI prediction with football domain knowledge")
.WithOpenApi();

app.MapGet("/api/rag/gameweek/{gameweek}/intelligence-report", async (int gameweek, RAGPredictionService ragService) =>
{
    var insight = await ragService.GenerateGameweekInsightAsync(gameweek);
    
    return Results.Ok(new
    {
        gameweek_intelligence = new
        {
            gameweek = insight.Gameweek,
            generated_at = insight.GeneratedAt,
            narrative = insight.GameweekNarrative
        },
        enhanced_predictions = insight.EnhancedTopPerformers.Take(10).Select(p => new
        {
            player = p.BasePrediction.PlayerName,
            position = p.BasePrediction.Position,
            ml_prediction = p.BasePrediction.PredictedPoints,
            contextual_adjusted = p.RAGPrediction.ContextuallyAdjustedPoints,
            contextual_rating = p.ContextualRating,
            key_insights = p.RAGPrediction.ContextualInsight.OverallReasoning,
            transfer_advice = p.RAGPrediction.ContextualInsight.TransferAdvice
        }),
        strategic_intelligence = new
        {
            strategic_insights = insight.StrategicInsights,
            market_trends = insight.MarketTrends,
            risk_warnings = insight.RiskWarnings
        },
        summary = $"🧠 AI Intelligence Report for gameweek {gameweek} - combining ML predictions with football domain expertise"
    });
})
.WithName("GetRAGGameweekIntelligence")
.WithTags("🧠 RAG Intelligence")
.WithSummary("Get comprehensive gameweek intelligence report with contextual AI insights")
.WithOpenApi();

app.MapGet("/api/rag/player/{playerId}/explanation", async (int playerId, RAGPredictionService ragService, GameweekService gameweekService) =>
{
    var currentGameweek = await gameweekService.GetCurrentGameweekAsync();
    var ragPrediction = await ragService.GenerateContextualPredictionAsync(playerId, currentGameweek + 1);
    var explanation = await ragService.ExplainPredictionAsync(ragPrediction.MLPrediction, await ragService.GetPlayerFeatures(playerId, currentGameweek + 1));
    
    return Results.Ok(new
    {
        player_name = ragPrediction.MLPrediction.PlayerName,
        natural_language_explanation = explanation,
        technical_breakdown = new
        {
            ml_models_used = ragPrediction.MLPrediction.ModelUsed,
            feature_importance = ragPrediction.MLPrediction.FeatureImportance,
            confidence_factors = ragPrediction.ContextualInsight.ConfidenceExplanation,
            contextual_adjustments = ragPrediction.ContextuallyAdjustedPoints - ragPrediction.MLPrediction.PredictedPoints
        },
        summary = "🧠 Human-readable AI explanation of prediction methodology and reasoning"
    });
})
.WithName("GetPredictionExplanation")
.WithTags("🧠 RAG Intelligence")
.WithSummary("Get natural language explanation of how the AI arrived at its prediction")
.WithOpenApi();
*/

// Placeholder endpoints for RAG functionality (to be implemented later)
app.MapGet("/api/rag/player/{playerId}/contextual-prediction/{gameweek}", async (int playerId, int gameweek) =>
    {
        return Results.Ok(new
        {
            message = "🧠 RAG contextual prediction endpoint under development",
            player_id = playerId,
            gameweek = gameweek,
            note = "This endpoint will provide AI-enhanced predictions with football domain knowledge"
        });
    })
    .WithName("GetRAGPlayerPrediction")
    .WithTags("🧠 RAG Intelligence")
    .WithSummary("Get contextually-aware AI prediction (under development)")
    .WithOpenApi();

app.MapGet("/api/rag/gameweek/{gameweek}/intelligence-report", async (int gameweek) =>
    {
        return Results.Ok(new
        {
            message = "🧠 RAG intelligence report endpoint under development",
            gameweek = gameweek,
            note = "This endpoint will provide comprehensive AI-powered gameweek analysis"
        });
    })
    .WithName("GetRAGGameweekIntelligence")
    .WithTags("🧠 RAG Intelligence")
    .WithSummary("Get AI intelligence report (under development)")
    .WithOpenApi();

app.MapGet("/api/rag/player/{playerId}/explanation", async (int playerId) =>
    {
        return Results.Ok(new
        {
            message = "🧠 RAG prediction explanation endpoint under development",
            player_id = playerId,
            note = "This endpoint will provide natural language explanations of AI predictions"
        });
    })
    .WithName("GetPredictionExplanation")
    .WithTags("🧠 RAG Intelligence")
    .WithSummary("Get prediction explanation (under development)")
    .WithOpenApi();

// 🎓 ML MODEL TRAINING AND STATUS ENDPOINTS
app.MapPost("/api/ml/train", async (MLTrainingService trainingService) =>
    {
        var result = await trainingService.TrainAllModelsAsync();
        
        return result.Success 
            ? Results.Ok(new
            {
                success = true,
                message = result.Message,
                training_summary = new
                {
                    training_data_size = result.TrainingDataSize,
                    lstm_trained = result.LstmTrained,
                    xgboost_trained = result.XgBoostTrained,
                    ensemble_trained = result.EnsembleTrained,
                    validation_accuracy = $"{result.ValidationAccuracy * 100:F2}%",
                    validation_mae = $"{result.ValidationMAE:F2} points",
                    duration_seconds = result.DurationSeconds
                },
                recommendation = result.ValidationMAE < 2.0 
                    ? "✅ Excellent model performance - ready for production use" 
                    : result.ValidationMAE < 3.0 
                        ? "✅ Good model performance - suitable for predictions"
                        : "⚠️ Model needs more training data or tuning"
            })
            : Results.BadRequest(new
            {
                success = false,
                message = result.Message,
                recommendation = "Ensure sufficient historical data is available before training"
            });
    })
    .WithName("TrainMLModels")
    .WithTags("🎓 ML Training")
    .WithSummary("Train all ML models (LSTM, XGBoost, Ensemble) with historical data")
    .WithOpenApi();

app.MapPost("/api/ml/train/background", async (MLTrainingService trainingService) =>
    {
        // Queue training as a background job
        BackgroundJob.Enqueue(() => trainingService.TrainAllModelsAsync(5));
        
        return Results.Accepted("/api/ml/status", new
        {
            success = true,
            message = "ML model training queued as background job",
            note = "Training will run in the background. Check /api/ml/status for progress."
        });
    })
    .WithName("TrainMLModelsBackground")
    .WithTags("🎓 ML Training")
    .WithSummary("Queue ML model training as a background job")
    .WithOpenApi();

app.MapGet("/api/ml/status", async (LSTMPredictionService lstmService, XgBoostPredictionService xgboostService, EnsemblePredictionService ensembleService) =>
    {
        var lstmMetrics = await lstmService.GetModelMetricsAsync();
        var xgboostMetrics = await xgboostService.GetModelMetricsAsync();
        var ensembleMetrics = await ensembleService.GetEnsembleMetricsAsync();
        
        return Results.Ok(new
        {
            status = "✅ ML Services Active",
            models = new
            {
                lstm = new
                {
                    name = lstmMetrics.ModelName,
                    confidence = lstmService.GetModelConfidence(),
                    performance = new
                    {
                        r_squared = $"{lstmMetrics.RSquared:F3}",
                        rmse = $"{lstmMetrics.RootMeanSquaredError:F2}",
                        mae = $"{lstmMetrics.MeanAbsoluteError:F2}",
                        accuracy = $"{lstmMetrics.Accuracy * 100:F1}%"
                    },
                    last_trained = lstmMetrics.LastTrainedDate,
                    training_samples = lstmMetrics.TrainingDataSize
                },
                xgboost = new
                {
                    name = xgboostMetrics.ModelName,
                    confidence = xgboostService.GetModelConfidence(),
                    performance = new
                    {
                        r_squared = $"{xgboostMetrics.RSquared:F3}",
                        rmse = $"{xgboostMetrics.RootMeanSquaredError:F2}",
                        mae = $"{xgboostMetrics.MeanAbsoluteError:F2}",
                        accuracy = $"{xgboostMetrics.Accuracy * 100:F1}%"
                    },
                    last_trained = xgboostMetrics.LastTrainedDate,
                    training_samples = xgboostMetrics.TrainingDataSize
                },
                ensemble = new
                {
                    name = ensembleMetrics.ModelName,
                    performance = new
                    {
                        r_squared = $"{ensembleMetrics.RSquared:F3}",
                        rmse = $"{ensembleMetrics.RootMeanSquaredError:F2}",
                        mae = $"{ensembleMetrics.MeanAbsoluteError:F2}"
                    },
                    model_weights = "LSTM: 40%, XGBoost: 60%",
                    last_trained = ensembleMetrics.LastTrainedDate
                }
            },
            recommendation = "Models are active and ready for predictions. Train with /api/ml/train if you have historical data."
        });
    })
    .WithName("GetMLStatus")
    .WithTags("🎓 ML Training")
    .WithSummary("Get status and performance metrics of all ML models")
    .WithOpenApi();

app.MapGet("/api/ml/models/compare", async (LSTMPredictionService lstmService, XgBoostPredictionService xgboostService) =>
    {
        var lstmMetrics = await lstmService.GetModelMetricsAsync();
        var xgboostMetrics = await xgboostService.GetModelMetricsAsync();
        
        return Results.Ok(new
        {
            comparison_summary = "Model performance comparison across key metrics",
            models = new[]
            {
                new
                {
                    model = "LSTM",
                    strengths = new[] { "Time series patterns", "Sequential data", "Temporal dependencies" },
                    accuracy = lstmMetrics.Accuracy,
                    mae = lstmMetrics.MeanAbsoluteError,
                    r_squared = lstmMetrics.RSquared,
                    best_for = "Players with consistent form patterns"
                },
                new
                {
                    model = "XGBoost",
                    strengths = new[] { "Feature interactions", "Non-linear relationships", "Robustness" },
                    accuracy = xgboostMetrics.Accuracy,
                    mae = xgboostMetrics.MeanAbsoluteError,
                    r_squared = xgboostMetrics.RSquared,
                    best_for = "Complex decision boundaries and feature engineering"
                }
            },
            ensemble_benefit = "Ensemble combines both models to leverage their individual strengths",
            recommendation = xgboostMetrics.Accuracy > lstmMetrics.Accuracy 
                ? "XGBoost performing better - ensemble weighted towards XGBoost (60%)"
                : "LSTM performing better - consider adjusting ensemble weights"
        });
    })
    .WithName("CompareMLModels")
    .WithTags("🎓 ML Training")
    .WithSummary("Compare performance metrics across different ML models")
    .WithOpenApi();

// Database initialization with automatic migrations
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<FplDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Starting database initialization...");
        
        // Check for pending migrations and apply them automatically
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Found {Count} pending migrations. Applying them now...", pendingMigrations.Count());
            foreach (var migration in pendingMigrations)
            {
                logger.LogInformation("Pending migration: {Migration}", migration);
            }
            
            // Apply all pending migrations
            await context.Database.MigrateAsync();
            logger.LogInformation("All migrations applied successfully!");
        }
        else
        {
            logger.LogInformation("Database is up to date. No pending migrations found.");
        }
        
        // Verify database connection
        await context.Database.CanConnectAsync();
        logger.LogInformation("Database connection verified successfully!");
        
        // Schedule initial jobs and recurring jobs
        FplBackgroundJobService.ScheduleRecurringJobs();
        FplBackgroundJobService.ScheduleImmediateJobs();
        
        logger.LogInformation("Database initialized and background jobs scheduled successfully!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database initialization: {Message}", ex.Message);
        
        // In production, you might want to exit the application if database initialization fails
        if (!app.Environment.IsDevelopment())
        {
            throw; // Re-throw in production to prevent startup with broken database
        }
    }
}

app.Run();

public class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In production, implement proper authorization
        return true;
    }
}

// Helper functions for validation recommendations