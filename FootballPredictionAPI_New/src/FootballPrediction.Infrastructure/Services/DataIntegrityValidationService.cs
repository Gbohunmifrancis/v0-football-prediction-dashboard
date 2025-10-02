using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services;

public class DataIntegrityValidationService
{
    private readonly FplDbContext _context;
    private readonly ILogger<DataIntegrityValidationService> _logger;

    public DataIntegrityValidationService(FplDbContext context, ILogger<DataIntegrityValidationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Comprehensive data integrity validation for ML training datasets
    /// </summary>
    public async Task<DataIntegrityReport> ValidateHistoricalDataIntegrityAsync()
    {
        _logger.LogInformation("Starting comprehensive historical data integrity validation...");
        
        var report = new DataIntegrityReport
        {
            StartTime = DateTime.UtcNow,
            ValidationResults = new List<ValidationResult>()
        };

        try
        {
            // 1. Basic Data Completeness Tests
            await ValidateDataCompletenessAsync(report);
            
            // 2. Data Quality Tests
            await ValidateDataQualityAsync(report);
            
            // 3. Data Consistency Tests
            await ValidateDataConsistencyAsync(report);
            
            // 4. ML Readiness Tests
            await ValidateMLReadinessAsync(report);
            
            // 5. Performance Validation Tests
            await ValidatePerformanceLogicAsync(report);
            
            // 6. Foreign Key Integrity Tests
            await ValidateForeignKeyIntegrityAsync(report);
            
            // 7. Data Distribution Tests
            await ValidateDataDistributionAsync(report);

            report.EndTime = DateTime.UtcNow;
            report.OverallStatus = DetermineOverallStatus(report.ValidationResults);
            
            _logger.LogInformation("Data integrity validation completed. Status: {Status}", report.OverallStatus);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data integrity validation");
            report.EndTime = DateTime.UtcNow;
            report.OverallStatus = ValidationStatus.Failed;
            report.ValidationResults.Add(new ValidationResult
            {
                TestName = "Overall Validation",
                Status = ValidationStatus.Failed,
                ErrorMessage = ex.Message
            });
            return report;
        }
    }

    private async Task ValidateDataCompletenessAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating data completeness...");

        // Test 1: Historical Player Performance completeness
        var playerPerformanceCount = await _context.HistoricalPlayerPerformances.CountAsync();
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Historical Player Performance Count",
            Status = playerPerformanceCount > 1000 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = playerPerformanceCount.ToString(),
            ExpectedValue = "> 1000",
            Message = $"Found {playerPerformanceCount} historical player performance records"
        });

        // Test 2: Season coverage
        var seasonsCount = await _context.HistoricalPlayerPerformances
            .Select(h => h.Season)
            .Distinct()
            .CountAsync();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Season Coverage",
            Status = seasonsCount >= 2 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = seasonsCount.ToString(),
            ExpectedValue = ">= 2",
            Message = $"Data covers {seasonsCount} seasons"
        });

        // Test 3: Gameweek coverage
        var gameweeksPerSeason = await _context.HistoricalPlayerPerformances
            .GroupBy(h => h.Season)
            .Select(g => new { Season = g.Key, GameweekCount = g.Select(x => x.Gameweek).Distinct().Count() })
            .ToListAsync();

        foreach (var season in gameweeksPerSeason)
        {
            report.ValidationResults.Add(new ValidationResult
            {
                TestName = $"Gameweek Coverage - {season.Season}",
                Status = season.GameweekCount >= 30 ? ValidationStatus.Passed : ValidationStatus.Warning,
                Value = season.GameweekCount.ToString(),
                ExpectedValue = ">= 30",
                Message = $"Season {season.Season} has {season.GameweekCount} gameweeks of data"
            });
        }

        // Test 4: Required fields completeness
        var missingPlayerNames = await _context.HistoricalPlayerPerformances
            .CountAsync(h => string.IsNullOrEmpty(h.PlayerName));
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Player Names Completeness",
            Status = missingPlayerNames == 0 ? ValidationStatus.Passed : ValidationStatus.Failed,
            Value = missingPlayerNames.ToString(),
            ExpectedValue = "0",
            Message = $"Found {missingPlayerNames} records with missing player names"
        });
    }

    private async Task ValidateDataQualityAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating data quality...");

        // Test 1: Points range validation (FPL points should be 0-30 typically)
        var invalidPoints = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Points < 0 || h.Points > 50);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Points Range Validation",
            Status = invalidPoints == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = invalidPoints.ToString(),
            ExpectedValue = "0",
            Message = $"Found {invalidPoints} records with points outside normal range (0-50)"
        });

        // Test 2: Minutes played validation (0-90 minutes per game)
        var invalidMinutes = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Minutes < 0 || h.Minutes > 120);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Minutes Range Validation",
            Status = invalidMinutes == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = invalidMinutes.ToString(),
            ExpectedValue = "0",
            Message = $"Found {invalidMinutes} records with invalid minutes played"
        });

        // Test 3: Goals validation (reasonable range)
        var invalidGoals = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Goals < 0 || h.Goals > 10);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Goals Range Validation",
            Status = invalidGoals == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = invalidGoals.ToString(),
            ExpectedValue = "0",
            Message = $"Found {invalidGoals} records with unrealistic goal counts"
        });

        // Test 4: Price validation (FPL prices are typically 4.0-15.0)
        var invalidPrices = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Price < 3.0m || h.Price > 20.0m);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Price Range Validation",
            Status = invalidPrices == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = invalidPrices.ToString(),
            ExpectedValue = "0",
            Message = $"Found {invalidPrices} records with prices outside expected range (3.0-20.0)"
        });
    }

    private async Task ValidateDataConsistencyAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating data consistency...");

        // Test 1: Player consistency across seasons
        var playersWithInconsistentPositions = await _context.HistoricalPlayerPerformances
            .GroupBy(h => h.PlayerName)
            .Where(g => g.Select(x => x.Position).Distinct().Count() > 1)
            .CountAsync();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Player Position Consistency",
            Status = playersWithInconsistentPositions < 10 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = playersWithInconsistentPositions.ToString(),
            ExpectedValue = "< 10",
            Message = $"Found {playersWithInconsistentPositions} players with position changes across seasons"
        });

        // Test 2: Duplicate detection
        var duplicateRecords = await _context.HistoricalPlayerPerformances
            .GroupBy(h => new { h.FplPlayerId, h.Season, h.Gameweek })
            .Where(g => g.Count() > 1)
            .CountAsync();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Duplicate Records Check",
            Status = duplicateRecords == 0 ? ValidationStatus.Passed : ValidationStatus.Failed,
            Value = duplicateRecords.ToString(),
            ExpectedValue = "0",
            Message = $"Found {duplicateRecords} duplicate records"
        });

        // Test 3: Gameweek sequence validation
        var seasonsWithMissingGameweeks = await ValidateGameweekSequences();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Gameweek Sequence Integrity",
            Status = seasonsWithMissingGameweeks.Count == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = seasonsWithMissingGameweeks.Count.ToString(),
            ExpectedValue = "0",
            Message = $"Found {seasonsWithMissingGameweeks.Count} seasons with missing gameweek sequences"
        });
    }

    private async Task ValidateMLReadinessAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating ML readiness...");

        // Test 1: Feature completeness for ML
        var recordsWithMissingFeatures = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Form5Games == 0 && h.MinutesPerGame == 0 && h.GoalsPerGame == 0);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "ML Feature Completeness",
            Status = recordsWithMissingFeatures < 1000 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = recordsWithMissingFeatures.ToString(),
            ExpectedValue = "< 1000",
            Message = $"Found {recordsWithMissingFeatures} records with missing derived ML features"
        });

        // Test 2: Target variable distribution (points distribution should be realistic)
        var pointsDistribution = await GetPointsDistribution();
        var mostCommonPoints = pointsDistribution.OrderByDescending(p => p.Count).First();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Target Variable Distribution",
            Status = mostCommonPoints.Points <= 3 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = $"Most common: {mostCommonPoints.Points} points ({mostCommonPoints.Count} records)",
            ExpectedValue = "Most common points should be 0-3",
            Message = "Points distribution appears realistic for FPL data"
        });

        // Test 3: Training data volume per position
        var positionCounts = await _context.HistoricalPlayerPerformances
            .GroupBy(h => h.Position)
            .Select(g => new { Position = g.Key, Count = g.Count() })
            .ToListAsync();

        foreach (var position in positionCounts)
        {
            report.ValidationResults.Add(new ValidationResult
            {
                TestName = $"Training Data Volume - {position.Position}",
                Status = position.Count >= 500 ? ValidationStatus.Passed : ValidationStatus.Warning,
                Value = position.Count.ToString(),
                ExpectedValue = ">= 500",
                Message = $"{position.Position} has {position.Count} training records"
            });
        }
    }

    private async Task ValidatePerformanceLogicAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating performance logic...");

        // Test 1: Goals vs Points correlation (players with goals should generally have more points)
        var playersWithGoalsButLowPoints = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Goals > 0 && h.Points < 2);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Goals-Points Correlation",
            Status = playersWithGoalsButLowPoints < 100 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = playersWithGoalsButLowPoints.ToString(),
            ExpectedValue = "< 100",
            Message = $"Found {playersWithGoalsButLowPoints} records with goals but unusually low points"
        });

        // Test 2: Minutes vs Performance correlation
        var playersWithNoMinutesButPoints = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.Minutes == 0 && h.Points > 1);
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Minutes-Points Logic",
            Status = playersWithNoMinutesButPoints == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = playersWithNoMinutesButPoints.ToString(),
            ExpectedValue = "0",
            Message = $"Found {playersWithNoMinutesButPoints} records with points but no minutes played"
        });

        // Test 3: Clean sheets logic (only goalkeepers and defenders should have clean sheets)
        var invalidCleanSheets = await _context.HistoricalPlayerPerformances
            .CountAsync(h => h.CleanSheets > 0 && !h.Position.Contains("Goalkeeper") && !h.Position.Contains("Defender"));
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Clean Sheets Logic",
            Status = invalidCleanSheets == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = invalidCleanSheets.ToString(),
            ExpectedValue = "0",
            Message = $"Found {invalidCleanSheets} midfielders/forwards with clean sheets"
        });
    }

    private async Task ValidateForeignKeyIntegrityAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating foreign key integrity...");

        // Test 1: Player ID consistency
        var orphanedPlayerRecords = await _context.HistoricalPlayerPerformances
            .Where(h => !_context.Players.Any(p => p.Id == h.PlayerId))
            .CountAsync();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Player Foreign Key Integrity",
            Status = orphanedPlayerRecords == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = orphanedPlayerRecords.ToString(),
            ExpectedValue = "0",
            Message = $"Found {orphanedPlayerRecords} historical records with invalid player references"
        });

        // Test 2: Team ID consistency
        var orphanedTeamRecords = await _context.HistoricalPlayerPerformances
            .Where(h => !_context.Teams.Any(t => t.Id == h.TeamId))
            .CountAsync();
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Team Foreign Key Integrity",
            Status = orphanedTeamRecords == 0 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = orphanedTeamRecords.ToString(),
            ExpectedValue = "0",
            Message = $"Found {orphanedTeamRecords} historical records with invalid team references"
        });
    }

    private async Task ValidateDataDistributionAsync(DataIntegrityReport report)
    {
        _logger.LogInformation("Validating data distribution...");

        // Test 1: Position distribution (should have reasonable spread)
        var positionDistribution = await _context.HistoricalPlayerPerformances
            .GroupBy(h => h.Position)
            .Select(g => new { Position = g.Key, Count = g.Count(), Percentage = (double)g.Count() * 100.0 / _context.HistoricalPlayerPerformances.Count() })
            .ToListAsync();

        var forwardPercentage = positionDistribution.FirstOrDefault(p => p.Position.Contains("Forward"))?.Percentage ?? 0;
        
        report.ValidationResults.Add(new ValidationResult
        {
            TestName = "Position Distribution Balance",
            Status = forwardPercentage >= 15 && forwardPercentage <= 35 ? ValidationStatus.Passed : ValidationStatus.Warning,
            Value = $"{forwardPercentage:F1}%",
            ExpectedValue = "15-35%",
            Message = $"Forwards represent {forwardPercentage:F1}% of the dataset"
        });

        // Test 2: Seasonal balance
        var seasonalDistribution = await _context.HistoricalPlayerPerformances
            .GroupBy(h => h.Season)
            .Select(g => new { Season = g.Key, Count = g.Count() })
            .ToListAsync();

        if (seasonalDistribution.Count > 1)
        {
            var maxCount = seasonalDistribution.Max(s => s.Count);
            var minCount = seasonalDistribution.Min(s => s.Count);
            var balance = (double)minCount / maxCount;
            
            report.ValidationResults.Add(new ValidationResult
            {
                TestName = "Seasonal Data Balance",
                Status = balance >= 0.7 ? ValidationStatus.Passed : ValidationStatus.Warning,
                Value = $"{balance:F2}",
                ExpectedValue = ">= 0.7",
                Message = $"Data balance ratio between seasons: {balance:F2}"
            });
        }
    }

    // Helper methods
    private async Task<List<string>> ValidateGameweekSequences()
    {
        var problemSeasons = new List<string>();
        
        // Fix: Split the complex query to avoid SQLite APPLY operation issues
        var allRecords = await _context.HistoricalPlayerPerformances
            .Select(h => new { h.Season, h.Gameweek })
            .ToListAsync();
            
        var seasonGameweeks = allRecords
            .GroupBy(h => h.Season)
            .Select(g => new { 
                Season = g.Key, 
                Gameweeks = g.Select(x => x.Gameweek).Distinct().OrderBy(x => x).ToList() 
            })
            .ToList();

        foreach (var season in seasonGameweeks)
        {
            var expectedGameweeks = Enumerable.Range(1, 38).ToList();
            var missingGameweeks = expectedGameweeks.Except(season.Gameweeks).ToList();
            
            if (missingGameweeks.Count > 8) // Allow for some missing data
            {
                problemSeasons.Add(season.Season);
            }
        }

        return problemSeasons;
    }

    private async Task<List<(int Points, int Count)>> GetPointsDistribution()
    {
        // Fix: Handle empty dataset gracefully and ensure SQLite compatibility
        var totalRecords = await _context.HistoricalPlayerPerformances.CountAsync();
        if (totalRecords == 0)
        {
            return new List<(int Points, int Count)> { (0, 0) };
        }
        
        return await _context.HistoricalPlayerPerformances
            .GroupBy(h => h.Points)
            .Select(g => new { Points = g.Key, Count = g.Count() })
            .OrderBy(x => x.Points)
            .Select(x => ValueTuple.Create(x.Points, x.Count))
            .ToListAsync();
    }

    private static ValidationStatus DetermineOverallStatus(List<ValidationResult> results)
    {
        if (results.Any(r => r.Status == ValidationStatus.Failed))
            return ValidationStatus.Failed;
        
        if (results.Any(r => r.Status == ValidationStatus.Warning))
            return ValidationStatus.Warning;
            
        return ValidationStatus.Passed;
    }
}

// Supporting classes for validation results
public class DataIntegrityReport
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ValidationStatus OverallStatus { get; set; }
    public List<ValidationResult> ValidationResults { get; set; } = new();
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
    
    public int PassedTests => ValidationResults.Count(r => r.Status == ValidationStatus.Passed);
    public int WarningTests => ValidationResults.Count(r => r.Status == ValidationStatus.Warning);
    public int FailedTests => ValidationResults.Count(r => r.Status == ValidationStatus.Failed);
    public int TotalTests => ValidationResults.Count;
}

public class ValidationResult
{
    public string TestName { get; set; } = string.Empty;
    public ValidationStatus Status { get; set; }
    public string Value { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum ValidationStatus
{
    Passed,
    Warning,
    Failed
}
