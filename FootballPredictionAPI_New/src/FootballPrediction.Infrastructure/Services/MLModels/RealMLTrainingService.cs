using FootballPrediction.Core.Entities;
using FootballPrediction.Core.Models;
using FootballPrediction.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FootballPrediction.Infrastructure.Services.MLModels;

/// <summary>
/// REAL Machine Learning Training Service using ML.NET
/// This service trains actual ML models on historical FPL data
/// </summary>
public class RealMLTrainingService
{
    private readonly FplDbContext _context;
    private readonly ILogger<RealMLTrainingService> _logger;
    private readonly MLContext _mlContext;
    private ITransformer? _trainedModel;
    private DataViewSchema? _modelSchema;

    public RealMLTrainingService(
        FplDbContext context,
        ILogger<RealMLTrainingService> logger)
    {
        _context = context;
        _logger = logger;
        _mlContext = new MLContext(seed: 42); // Fixed seed for reproducibility
    }

    /// <summary>
    /// Train ML model using historical player performance data
    /// </summary>
    public async Task<MLTrainingResult> TrainModelAsync()
    {
        _logger.LogInformation("🎓 Starting REAL ML training with ML.NET...");
        var startTime = DateTime.UtcNow;

        try
        {
            // Step 1: Load historical training data from database
            var trainingData = await LoadTrainingDataAsync();
            
            if (trainingData.Count < 100)
            {
                _logger.LogWarning("⚠️ Insufficient training data ({Count} records). Need at least 100.", trainingData.Count);
                return new MLTrainingResult
                {
                    Success = false,
                    Message = $"Insufficient training data: {trainingData.Count} records (need at least 100)",
                    TrainingDuration = DateTime.UtcNow - startTime
                };
            }

            _logger.LogInformation("📊 Loaded {Count} training samples", trainingData.Count);

            // Step 2: Convert to ML.NET data view
            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // Step 3: Split into training and test sets (80/20 split)
            var trainTestSplit = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

            // Step 4: Build the ML pipeline
            var pipeline = BuildMLPipeline();

            _logger.LogInformation("🔧 Training FastTree regression model...");

            // Step 5: Train the model
            _trainedModel = pipeline.Fit(trainTestSplit.TrainSet);
            _modelSchema = trainTestSplit.TrainSet.Schema;

            _logger.LogInformation("✅ Model trained successfully");

            // Step 6: Evaluate model performance
            var evaluation = EvaluateModel(trainTestSplit.TestSet);

            // Step 7: Save the trained model to disk
            var modelPath = await SaveModelAsync();

            var trainingDuration = DateTime.UtcNow - startTime;
            _logger.LogInformation("🎉 ML Training Complete in {Duration:F2}s", trainingDuration.TotalSeconds);

            return new MLTrainingResult
            {
                Success = true,
                Message = "Real ML model trained successfully using ML.NET FastTree",
                TrainingDuration = trainingDuration,
                ModelPath = modelPath,
                TrainingSamples = trainingData.Count,
                TestSamples = (int)(trainingData.Count * 0.2),
                RSquared = evaluation.RSquared,
                RootMeanSquaredError = evaluation.RootMeanSquaredError,
                MeanAbsoluteError = evaluation.MeanAbsoluteError,
                LossFunction = evaluation.LossFunction
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ML Training failed");
            return new MLTrainingResult
            {
                Success = false,
                Message = $"Training failed: {ex.Message}",
                TrainingDuration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Load historical player performance data for training
    /// </summary>
    private async Task<List<PlayerTrainingData>> LoadTrainingDataAsync()
    {
        _logger.LogInformation("📥 Loading historical performance data...");

        var historicalData = await _context.HistoricalPlayerPerformances
            .Include(h => h.Player)
            .Where(h => h.Points > 0) // Only include players who scored points
            .Take(10000) // Limit to recent data for faster training
            .ToListAsync();

        var trainingData = historicalData.Select(h => new PlayerTrainingData
        {
            // Features (inputs)
            Form = (float)h.Form5Games,
            PointsPerGame = (float)h.PointsPerMillion, // Use available metric
            MinutesPerGame = (float)h.MinutesPerGame,
            GoalsPerGame = (float)h.GoalsPerGame,
            AssistsPerGame = (float)h.AssistsPerGame,
            CleanSheetsPerGame = (float)h.CleanSheets, // Actual count, not per game
            GoalsConcededPerGame = (float)h.GoalsConceded, // Actual count
            SavesPerGame = (float)h.Saves, // Actual count
            BonusPointsPerGame = (float)h.BonusPoints,
            ICTIndex = (float)h.IctIndex, // Note: lowercase 'ct'
            Influence = (float)h.Influence,
            Creativity = (float)h.Creativity,
            Threat = (float)h.Threat,
            PointsPerMillion = (float)h.PointsPerMillion,
            TransfersInPerGame = 0, // Not available in entity
            TransfersOutPerGame = 0, // Not available in entity
            SelectedByPercent = (float)(h.Player?.SelectedByPercent ?? 0),

            // Label (output - what we want to predict)
            PredictedPoints = (float)h.Points
        }).ToList();

        _logger.LogInformation("✅ Prepared {Count} training samples with {Features} features", 
            trainingData.Count, 17);

        return trainingData;
    }

    /// <summary>
    /// Build the ML.NET training pipeline
    /// </summary>
    private IEstimator<ITransformer> BuildMLPipeline()
    {
        _logger.LogInformation("🔨 Building ML pipeline...");

        // Create feature vector from all input columns
        var featureColumns = new[]
        {
            nameof(PlayerTrainingData.Form),
            nameof(PlayerTrainingData.PointsPerGame),
            nameof(PlayerTrainingData.MinutesPerGame),
            nameof(PlayerTrainingData.GoalsPerGame),
            nameof(PlayerTrainingData.AssistsPerGame),
            nameof(PlayerTrainingData.CleanSheetsPerGame),
            nameof(PlayerTrainingData.GoalsConcededPerGame),
            nameof(PlayerTrainingData.SavesPerGame),
            nameof(PlayerTrainingData.BonusPointsPerGame),
            nameof(PlayerTrainingData.ICTIndex),
            nameof(PlayerTrainingData.Influence),
            nameof(PlayerTrainingData.Creativity),
            nameof(PlayerTrainingData.Threat),
            nameof(PlayerTrainingData.PointsPerMillion),
            nameof(PlayerTrainingData.TransfersInPerGame),
            nameof(PlayerTrainingData.TransfersOutPerGame),
            nameof(PlayerTrainingData.SelectedByPercent)
        };

        // Build pipeline:
        // 1. Concatenate all features into a single vector
        // 2. Normalize features to improve training
        // 3. Train FastTree regression model
        var pipeline = _mlContext.Transforms.Concatenate("Features", featureColumns)
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.Regression.Trainers.FastTree(
                labelColumnName: "Label", // Must match [ColumnName("Label")] attribute
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 10,
                learningRate: 0.2
            ));

        return pipeline;
    }

    /// <summary>
    /// Evaluate the trained model on test data
    /// </summary>
    private RegressionMetrics EvaluateModel(IDataView testData)
    {
        _logger.LogInformation("📈 Evaluating model performance...");

        var predictions = _trainedModel!.Transform(testData);
        var metrics = _mlContext.Regression.Evaluate(predictions, 
            labelColumnName: "Label"); // Must match [ColumnName("Label")] attribute

        _logger.LogInformation("📊 Model Performance Metrics:");
        _logger.LogInformation("   R² (R-Squared): {RSquared:F4} (1.0 = perfect fit)", metrics.RSquared);
        _logger.LogInformation("   RMSE (Root Mean Squared Error): {RMSE:F2} points", metrics.RootMeanSquaredError);
        _logger.LogInformation("   MAE (Mean Absolute Error): {MAE:F2} points", metrics.MeanAbsoluteError);
        _logger.LogInformation("   Loss Function: {Loss:F4}", metrics.LossFunction);

        return metrics;
    }

    /// <summary>
    /// Save the trained model to disk
    /// </summary>
    private async Task<string> SaveModelAsync()
    {
        var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "fpl_prediction_model.zip");
        var modelDirectory = Path.GetDirectoryName(modelPath);
        
        if (!Directory.Exists(modelDirectory))
        {
            Directory.CreateDirectory(modelDirectory!);
        }

        _mlContext.Model.Save(_trainedModel!, _modelSchema!, modelPath);
        _logger.LogInformation("💾 Model saved to: {Path}", modelPath);

        return await Task.FromResult(modelPath);
    }

    /// <summary>
    /// Make a prediction using the trained model
    /// </summary>
    public float Predict(PlayerTrainingData input)
    {
        if (_trainedModel == null)
        {
            throw new InvalidOperationException("Model not trained. Call TrainModelAsync() first.");
        }

        var predictionEngine = _mlContext.Model.CreatePredictionEngine<PlayerTrainingData, PlayerPrediction>(_trainedModel);
        var prediction = predictionEngine.Predict(input);

        return prediction.Score;
    }

    /// <summary>
    /// Load a previously trained model from disk
    /// </summary>
    public async Task<bool> LoadModelAsync(string modelPath)
    {
        try
        {
            if (!File.Exists(modelPath))
            {
                _logger.LogWarning("⚠️ Model file not found: {Path}", modelPath);
                return false;
            }

            _trainedModel = _mlContext.Model.Load(modelPath, out _modelSchema);
            _logger.LogInformation("✅ Model loaded from: {Path}", modelPath);
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to load model from: {Path}", modelPath);
            return false;
        }
    }
}
/// <summary>
/// Result of ML training
/// </summary>
public class MLTrainingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan TrainingDuration { get; set; }
    public string? ModelPath { get; set; }
    public int TrainingSamples { get; set; }
    public int TestSamples { get; set; }
    
    // Model performance metrics
    public double RSquared { get; set; }
    public double RootMeanSquaredError { get; set; }
    public double MeanAbsoluteError { get; set; }
    public double LossFunction { get; set; }
}
