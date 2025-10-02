using FootballPrediction.Core.Entities;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FootballPrediction.Infrastructure.Services;

public class MLDataQualityService
{
    private readonly FplDbContext _context;
    private readonly ILogger<MLDataQualityService> _logger;

    public MLDataQualityService(FplDbContext context, ILogger<MLDataQualityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Run specific ML-focused data quality checks
    /// </summary>
    public async Task<MLDataQualityReport> ValidateMLDataQualityAsync()
    {
        _logger.LogInformation("Starting ML-specific data quality validation...");
        
        var report = new MLDataQualityReport
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            // 1. Feature Engineering Validation
            await ValidateFeatureEngineeringAsync(report);
            
            // 2. Training Set Balance
            await ValidateTrainingSetBalanceAsync(report);
            
            // 3. Data Leakage Detection
            await DetectDataLeakageAsync(report);
            
            // 4. Outlier Detection
            await DetectOutliersAsync(report);
            
            // 5. Missing Value Analysis
            await AnalyzeMissingValuesAsync(report);

            report.EndTime = DateTime.UtcNow;
            report.MLReadinessScore = CalculateMLReadinessScore(report);
            
            _logger.LogInformation("ML data quality validation completed. Readiness Score: {Score}/100", 
                report.MLReadinessScore);
            
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ML data quality validation");
            report.EndTime = DateTime.UtcNow;
            report.MLReadinessScore = 0;
            return report;
        }
    }

    private async Task ValidateFeatureEngineeringAsync(MLDataQualityReport report)
    {
        // Check if derived features are calculated correctly
        var samplesWithCorrectFeatures = await _context.HistoricalPlayerPerformances
            .Where(h => h.Minutes > 0)
            .Take(100)
            .ToListAsync();

        var correctCalculations = 0;
        foreach (var sample in samplesWithCorrectFeatures)
        {
            var expectedGoalsPerGame = sample.Minutes > 0 ? (decimal)sample.Goals * 90m / sample.Minutes : 0;
            if (Math.Abs(sample.GoalsPerGame - expectedGoalsPerGame) < 0.1m)
                correctCalculations++;
        }

        var accuracy = (double)correctCalculations / samplesWithCorrectFeatures.Count * 100;
        report.FeatureEngineeringAccuracy = accuracy;
        
        _logger.LogInformation("Feature engineering accuracy: {Accuracy}%", accuracy);
    }

    private async Task ValidateTrainingSetBalanceAsync(MLDataQualityReport report)
    {
        // Check class balance for different point ranges
        // Fix: Replace switch expression with traditional approach for LINQ compatibility
        var allRecords = await _context.HistoricalPlayerPerformances.ToListAsync();
        
        var pointRanges = allRecords
            .GroupBy(h => GetPointsRange(h.Points))
            .Select(g => new { Range = g.Key, Count = g.Count() })
            .ToList();

        var totalRecords = pointRanges.Sum(p => p.Count);
        report.ClassDistribution = pointRanges.ToDictionary(
            p => p.Range, 
            p => (double)p.Count / totalRecords * 100
        );

        // Check if distribution is reasonable (not too skewed)
        var maxPercentage = report.ClassDistribution.Values.Max();
        report.IsBalanced = maxPercentage < 60; // No single class should dominate

        _logger.LogInformation("Class distribution analysis completed. Balanced: {IsBalanced}", report.IsBalanced);
    }
    
    // Move this method outside of LINQ query to fix compilation error
    private static string GetPointsRange(int points)
    {
        if (points == 0) return "Zero";
        if (points >= 1 && points <= 3) return "Low (1-3)";
        if (points >= 4 && points <= 6) return "Medium (4-6)";
        if (points >= 7 && points <= 10) return "High (7-10)";
        if (points > 10) return "Exceptional (10+)";
        return "Invalid";
    }

    private async Task DetectDataLeakageAsync(MLDataQualityReport report)
    {
        // Check for potential data leakage (future information in past records)
        var suspiciousRecords = await _context.HistoricalPlayerPerformances
            .Where(h => h.LastUpdated < h.GameDate.AddDays(-7)) // Data recorded too early
            .CountAsync();

        report.PotentialDataLeakage = suspiciousRecords;
        
        _logger.LogInformation("Data leakage check: {SuspiciousRecords} potentially problematic records", 
            suspiciousRecords);
    }

    private async Task DetectOutliersAsync(MLDataQualityReport report)
    {
        // Statistical outlier detection using IQR method
        var allPoints = await _context.HistoricalPlayerPerformances
            .Select(h => h.Points)
            .ToListAsync();

        var sortedPoints = allPoints.OrderBy(p => p).ToList();
        var q1Index = sortedPoints.Count / 4;
        var q3Index = 3 * sortedPoints.Count / 4;
        
        var q1 = sortedPoints[q1Index];
        var q3 = sortedPoints[q3Index];
        var iqr = q3 - q1;
        
        var lowerBound = q1 - 1.5 * iqr;
        var upperBound = q3 + 1.5 * iqr;
        
        var outliers = allPoints.Count(p => p < lowerBound || p > upperBound);
        var outlierPercentage = (double)outliers / allPoints.Count * 100;
        
        report.OutlierPercentage = outlierPercentage;
        report.OutlierCount = outliers;
        
        _logger.LogInformation("Outlier detection: {OutlierPercentage}% ({OutlierCount} records)", 
            outlierPercentage, outliers);
    }

    private async Task AnalyzeMissingValuesAsync(MLDataQualityReport report)
    {
        var totalRecords = await _context.HistoricalPlayerPerformances.CountAsync();
        
        var missingValueAnalysis = new Dictionary<string, double>
        {
            ["PlayerName"] = await CalculateMissingPercentageAsync("PlayerName", totalRecords),
            ["Position"] = await CalculateMissingPercentageAsync("Position", totalRecords),
            ["TeamName"] = await CalculateMissingPercentageAsync("TeamName", totalRecords),
            ["Form5Games"] = await CalculateZeroPercentageAsync("Form5Games", totalRecords)
        };

        report.MissingValueAnalysis = missingValueAnalysis;
        report.HasAcceptableMissingData = missingValueAnalysis.Values.All(v => v < 10); // Less than 10% missing
        
        _logger.LogInformation("Missing value analysis completed. Acceptable: {Acceptable}", 
            report.HasAcceptableMissingData);
    }

    private async Task<double> CalculateMissingPercentageAsync(string fieldName, int totalRecords)
    {
        // Fix: Replace switch expression with if-else for LINQ compatibility
        Task<int> query;
        if (fieldName == "PlayerName")
            query = _context.HistoricalPlayerPerformances.CountAsync(h => string.IsNullOrEmpty(h.PlayerName));
        else if (fieldName == "Position")
            query = _context.HistoricalPlayerPerformances.CountAsync(h => string.IsNullOrEmpty(h.Position));
        else if (fieldName == "TeamName")
            query = _context.HistoricalPlayerPerformances.CountAsync(h => string.IsNullOrEmpty(h.TeamName));
        else
            query = Task.FromResult(0);

        var missing = await query;
        return (double)missing / totalRecords * 100;
    }

    private async Task<double> CalculateZeroPercentageAsync(string fieldName, int totalRecords)
    {
        // Fix: Replace switch expression and nameof with string comparison
        int zeroValues;
        if (fieldName == "Form5Games")
            zeroValues = await _context.HistoricalPlayerPerformances.CountAsync(h => h.Form5Games == 0);
        else
            zeroValues = 0;

        return (double)zeroValues / totalRecords * 100;
    }

    private static double CalculateMLReadinessScore(MLDataQualityReport report)
    {
        var score = 100.0;
        
        // Deduct points for issues
        if (report.FeatureEngineeringAccuracy < 95) score -= 20;
        if (!report.IsBalanced) score -= 15;
        if (report.PotentialDataLeakage > 0) score -= 25;
        if (report.OutlierPercentage > 5) score -= 10;
        if (!report.HasAcceptableMissingData) score -= 20;
        
        return Math.Max(0, score);
    }

    /// <summary>
    /// Generate sample data for ML model testing
    /// </summary>
    public async Task<MLSampleDataset> GenerateMLSampleDatasetAsync(int sampleSize = 1000)
    {
        _logger.LogInformation("Generating ML sample dataset with {SampleSize} records", sampleSize);

        var dataset = new MLSampleDataset();
        
        try
        {
            // Fix: Use a different approach for random sampling that works with SQLite
            // First get the total count, then use Skip/Take with a random offset
            var totalRecords = await _context.HistoricalPlayerPerformances.CountAsync();
            
            if (totalRecords == 0)
            {
                _logger.LogWarning("No historical data found for ML sample generation");
                return dataset;
            }
            
            var random = new Random();
            var skipCount = totalRecords > sampleSize ? random.Next(0, totalRecords - sampleSize) : 0;
            
            var sampleData = await _context.HistoricalPlayerPerformances
                .Skip(skipCount)
                .Take(sampleSize)
                .Select(h => new MLTrainingRow
                {
                    PlayerId = h.PlayerId,
                    PlayerName = h.PlayerName,
                    Position = h.Position,
                    
                    // Features (X)
                    Form5Games = (double)h.Form5Games,
                    MinutesPerGame = (double)h.MinutesPerGame,
                    GoalsPerGame = (double)h.GoalsPerGame,
                    AssistsPerGame = (double)h.AssistsPerGame,
                    Price = (double)h.Price,
                    OpponentStrength = h.OpponentStrength,
                    IsHome = h.WasHome ? 1.0 : 0.0,
                    
                    // Target (y)
                    ActualPoints = h.Points,
                    
                    // Metadata
                    Season = h.Season,
                    Gameweek = h.Gameweek
                })
                .ToListAsync();

            dataset.TrainingRows = sampleData;
            dataset.FeatureCount = 7; // Number of features
            dataset.SampleSize = sampleData.Count;
            dataset.CreatedAt = DateTime.UtcNow;

            _logger.LogInformation("Generated ML sample dataset with {ActualSize} records", sampleData.Count);
            
            return dataset;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ML sample dataset");
            throw;
        }
    }
}

// Supporting classes for ML data quality
public class MLDataQualityReport
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double MLReadinessScore { get; set; }
    public double FeatureEngineeringAccuracy { get; set; }
    public bool IsBalanced { get; set; }
    public Dictionary<string, double> ClassDistribution { get; set; } = new();
    public int PotentialDataLeakage { get; set; }
    public double OutlierPercentage { get; set; }
    public int OutlierCount { get; set; }
    public Dictionary<string, double> MissingValueAnalysis { get; set; } = new();
    public bool HasAcceptableMissingData { get; set; }
    
    public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;
}

public class MLSampleDataset
{
    public List<MLTrainingRow> TrainingRows { get; set; } = new();
    public int SampleSize { get; set; }
    public int FeatureCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MLTrainingRow
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public int Gameweek { get; set; }
    
    // Features (X variables)
    public double Form5Games { get; set; }
    public double MinutesPerGame { get; set; }
    public double GoalsPerGame { get; set; }
    public double AssistsPerGame { get; set; }
    public double Price { get; set; }
    public double OpponentStrength { get; set; }
    public double IsHome { get; set; }
    
    // Target (y variable)
    public int ActualPoints { get; set; }
}
