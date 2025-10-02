using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FootballPrediction.Infrastructure.Jobs;

public class FplBackgroundJobService
{
    private readonly FplDataScrapingService _fplDataService;
    private readonly InjuryAndNewsScrapingService _injuryNewsService;
    private readonly PlayerPredictionService _predictionService;
    private readonly GameweekService _gameweekService;
    private readonly ILogger<FplBackgroundJobService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public FplBackgroundJobService(
        FplDataScrapingService fplDataService,
        InjuryAndNewsScrapingService injuryNewsService,
        PlayerPredictionService predictionService,
        GameweekService gameweekService,
        ILogger<FplBackgroundJobService> logger,
        IServiceProvider serviceProvider)
    {
        _fplDataService = fplDataService;
        _injuryNewsService = injuryNewsService;
        _predictionService = predictionService;
        _gameweekService = gameweekService;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public async Task RunFullDataUpdateAsync()
    {
        try
        {
            _logger.LogInformation("Starting comprehensive FPL data update job");

            // Step 1: Update core FPL data (players, teams, fixtures)
            await _fplDataService.ScrapeBootstrapDataAsync();

            // Step 2: Update injury information
            await _injuryNewsService.ScrapeInjuryUpdatesAsync();

            // Step 3: Update transfer news
            await _injuryNewsService.ScrapeTransferNewsAsync();

            _logger.LogInformation("Completed comprehensive FPL data update job successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during comprehensive FPL data update");
            throw;
        }
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 15, 30 })]
    public async Task RunQuickDataUpdateAsync()
    {
        try
        {
            _logger.LogInformation("Starting quick FPL data update job");

            // Only update core FPL data for quick updates
            await _fplDataService.ScrapeBootstrapDataAsync();

            _logger.LogInformation("Completed quick FPL data update job successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during quick FPL data update");
            throw;
        }
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 20, 40 })]
    public async Task RunInjuryUpdateAsync()
    {
        try
        {
            _logger.LogInformation("Starting injury updates job");
            await _injuryNewsService.ScrapeInjuryUpdatesAsync();
            _logger.LogInformation("Completed injury updates job successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during injury updates");
            throw;
        }
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 20, 40 })]
    public async Task RunTransferNewsUpdateAsync()
    {
        try
        {
            _logger.LogInformation("Starting transfer news updates job");
            await _injuryNewsService.ScrapeTransferNewsAsync();
            _logger.LogInformation("Completed transfer news updates job successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during transfer news updates");
            throw;
        }
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 60 })]
    public async Task RunPredictionAnalysisAsync()
    {
        try
        {
            _logger.LogInformation("Starting prediction analysis job");
            
            // Get current gameweek from FPL API
            var currentGameweek = await _gameweekService.GetCurrentGameweekAsync();
            
            // Generate predictions for next gameweek
            var prediction = await _predictionService.GenerateGameweekPredictionAsync(currentGameweek + 1);
            
            _logger.LogInformation("Generated predictions for gameweek {Gameweek} with {PlayerCount} analyzed players", 
                prediction.Gameweek, 
                prediction.TopPerformers.Count + prediction.BestValue.Count + prediction.Differentials.Count);
            
            _logger.LogInformation("Completed prediction analysis job successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during prediction analysis");
            throw;
        }
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 120, 300 })]
    public async Task RunHistoricalDataCollectionAsync()
    {
        try
        {
            _logger.LogInformation("Starting historical data collection job");
            
            var dataCollectionService = _serviceProvider.GetRequiredService<AlternativeDataCollectionService>();
            var summary = await dataCollectionService.CreateMLTrainingDatasetAsync();
            
            _logger.LogInformation("Historical data collection completed. Success: {Success}, Records: {Records}", 
                summary.Success, summary.TotalRecordsCreated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during historical data collection");
            throw;
        }
    }

    public static void ScheduleRecurringJobs()
    {
        // Full comprehensive update - twice daily (morning and evening)
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "full-fpl-data-update",
            service => service.RunFullDataUpdateAsync(),
            "0 6,18 * * *", // 6 AM and 6 PM daily
            TimeZoneInfo.Local);

        // Quick data updates - every 2 hours during active periods
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "quick-fpl-data-update",
            service => service.RunQuickDataUpdateAsync(),
            "0 */2 * * *", // Every 2 hours
            TimeZoneInfo.Local);

        // Injury updates - three times daily
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "injury-updates",
            service => service.RunInjuryUpdateAsync(),
            "0 8,14,20 * * *", // 8 AM, 2 PM, 8 PM daily
            TimeZoneInfo.Local);

        // Transfer news updates - every 4 hours during transfer windows
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "transfer-news-updates",
            service => service.RunTransferNewsUpdateAsync(),
            "0 */4 * * *", // Every 4 hours
            TimeZoneInfo.Local);

        // Prediction analysis - runs after data updates
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "prediction-analysis",
            service => service.RunPredictionAnalysisAsync(),
            "30 7,19 * * *", // 30 minutes after data updates (7:30 AM and 7:30 PM)
            TimeZoneInfo.Local);

        // Weekend prediction updates (before matches)
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "weekend-predictions",
            service => service.RunPredictionAnalysisAsync(),
            "0 9 * * 5,6", // 9 AM on Friday and Saturday
            TimeZoneInfo.Local);

        // Weekend intensive updates (match days)
        RecurringJob.AddOrUpdate<FplBackgroundJobService>(
            "weekend-intensive-update",
            service => service.RunFullDataUpdateAsync(),
            "0 */1 * * 6,0", // Every hour on Saturday and Sunday
            TimeZoneInfo.Local);
    }

    public static void ScheduleInitialHistoricalDataCollection()
    {
        // Check if historical data exists, if not, schedule collection
        BackgroundJob.Enqueue<FplBackgroundJobService>(service => service.CheckAndInitializeHistoricalDataAsync());
    }

    [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 30 })]
    public async Task CheckAndInitializeHistoricalDataAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FplDbContext>();
            
            var historicalRecordCount = await context.HistoricalPlayerPerformances.CountAsync();
            
            if (historicalRecordCount < 100) // If we don't have enough historical data
            {
                _logger.LogInformation("Insufficient historical data found ({Count} records). Starting automatic collection...", 
                    historicalRecordCount);
                
                // Schedule historical data collection immediately
                BackgroundJob.Enqueue<FplBackgroundJobService>(service => service.RunHistoricalDataCollectionAsync());
            }
            else
            {
                _logger.LogInformation("Historical data already exists ({Count} records). Skipping automatic collection.", 
                    historicalRecordCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking historical data initialization");
        }
    }

    public static void ScheduleImmediateJobs()
    {
        // Schedule immediate execution for first-time setup
        BackgroundJob.Enqueue<FplBackgroundJobService>(service => service.RunFullDataUpdateAsync());

        // Check and initialize historical data if needed
        BackgroundJob.Schedule<FplBackgroundJobService>(
            service => service.CheckAndInitializeHistoricalDataAsync(),
            TimeSpan.FromMinutes(2));

        // Schedule injury update in 10 minutes
        BackgroundJob.Schedule<FplBackgroundJobService>(
            service => service.RunInjuryUpdateAsync(),
            TimeSpan.FromMinutes(10));

        // Schedule transfer news update in 15 minutes
        BackgroundJob.Schedule<FplBackgroundJobService>(
            service => service.RunTransferNewsUpdateAsync(),
            TimeSpan.FromMinutes(15));

        // Schedule prediction analysis after initial data load
        BackgroundJob.Schedule<FplBackgroundJobService>(
            service => service.RunPredictionAnalysisAsync(),
            TimeSpan.FromMinutes(20));
    }
}
