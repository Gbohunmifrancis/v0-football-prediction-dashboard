using Microsoft.EntityFrameworkCore;
using FootballPrediction.Core.Entities;

namespace FootballPrediction.Infrastructure.Data;

public class FplDbContext : DbContext
{
    public FplDbContext(DbContextOptions<FplDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players { get; set; }
    public DbSet<Team> Teams { get; set; }
    public DbSet<GameweekData> GameweekData { get; set; }
    public DbSet<PlayerGameweekPerformance> PlayerGameweekPerformances { get; set; }
    public DbSet<PlayerPrediction> PlayerPredictions { get; set; }
    public DbSet<HistoricalPlayerPerformance> HistoricalPlayerPerformances { get; set; }
    public DbSet<HistoricalTeamStrength> HistoricalTeamStrengths { get; set; }
    public DbSet<InjuryUpdate> InjuryUpdates { get; set; }
    public DbSet<TransferNews> TransferNews { get; set; }
    public DbSet<Fixture> Fixtures { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Player entity
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.SelectedByPercent).HasPrecision(18, 2);
        });

        // Configure Team entity
        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Strength).HasPrecision(18, 2);
        });

        // Configure GameweekData entity
        modelBuilder.Entity<GameweekData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Gameweek).IsRequired();
            entity.Property(e => e.StartDate).IsRequired();
            entity.Property(e => e.EndDate).IsRequired();
        });

        // Configure PlayerGameweekPerformance entity
        modelBuilder.Entity<PlayerGameweekPerformance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.Gameweek).IsRequired();
        });

        // Configure PlayerPrediction entity
        modelBuilder.Entity<PlayerPrediction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.PredictedPoints).HasPrecision(18, 2);
        });

        // Configure HistoricalPlayerPerformance entity
        modelBuilder.Entity<HistoricalPlayerPerformance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Position).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Points).HasPrecision(18, 2);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.WasHome).IsRequired();
        });

        // Configure HistoricalTeamStrength entity
        modelBuilder.Entity<HistoricalTeamStrength>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TeamName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AttackStrengthHome).HasPrecision(18, 2);
            entity.Property(e => e.AttackStrengthAway).HasPrecision(18, 2);
            entity.Property(e => e.DefenseStrengthHome).HasPrecision(18, 2);
            entity.Property(e => e.DefenseStrengthAway).HasPrecision(18, 2);
        });

        // Configure InjuryUpdate entity
        modelBuilder.Entity<InjuryUpdate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.InjuryType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExpectedReturnDate);
        });

        // Configure TransferNews entity
        modelBuilder.Entity<TransferNews>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerId).IsRequired();
            entity.Property(e => e.FromTeamId);
            entity.Property(e => e.ToTeamId);
            entity.Property(e => e.TransferFee).HasPrecision(18, 2);
        });

        // Configure Fixture entity
        modelBuilder.Entity<Fixture>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TeamHomeId).IsRequired();
            entity.Property(e => e.TeamAwayId).IsRequired();
            entity.Property(e => e.Gameweek).IsRequired();
            entity.Property(e => e.KickoffTime).IsRequired();
            
            // Configure relationship for TeamHome
            entity.HasOne(f => f.TeamHome)
                .WithMany(t => t.HomeFixtures)
                .HasForeignKey(f => f.TeamHomeId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Configure relationship for TeamAway
            entity.HasOne(f => f.TeamAway)
                .WithMany(t => t.AwayFixtures)
                .HasForeignKey(f => f.TeamAwayId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
