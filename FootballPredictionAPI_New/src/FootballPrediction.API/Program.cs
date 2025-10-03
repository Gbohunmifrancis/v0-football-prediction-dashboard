using FootballPrediction.Infrastructure.Data;
using FootballPrediction.Infrastructure.Services;
using FootballPrediction.Infrastructure.Services.MLModels;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<FplDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient for services that need it
builder.Services.AddHttpClient<FplDataScrapingService>();

// Register ML services
builder.Services.AddScoped<RealMLPredictionService>();
builder.Services.AddScoped<RealMLTrainingService>();
builder.Services.AddScoped<RealXGBoostPredictionService>();
builder.Services.AddScoped<RealLSTMPredictionService>();
builder.Services.AddScoped<SquadSelectionService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapGet("/health", () => "API is healthy");

app.MapPost("/ml/train", async (RealMLTrainingService trainingService) =>
{
    try
    {
        await trainingService.TrainModelAsync();
        return Results.Ok("Training completed");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/squad/{gameweek}", async (int gameweek, SquadSelectionService squadService) =>
{
    try
    {
        var squad = await squadService.SelectOptimalSquadAsync(gameweek, SquadSelectionStrategy.Balanced);
        return Results.Ok(squad);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapControllers();
app.Run();
