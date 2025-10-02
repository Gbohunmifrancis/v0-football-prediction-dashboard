using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FootballPrediction.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameweekData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Gameweek = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsFinished = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeadlineTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HighestScore = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageScore = table.Column<decimal>(type: "TEXT", nullable: true),
                    TransfersMade = table.Column<int>(type: "INTEGER", nullable: true),
                    ChipPlays = table.Column<int>(type: "INTEGER", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameweekData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalTeamStrengths",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TeamName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AttackStrengthHome = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    AttackStrengthAway = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DefenseStrengthHome = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DefenseStrengthAway = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalTeamStrengths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FplId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", nullable: false),
                    Code = table.Column<int>(type: "INTEGER", nullable: false),
                    Strength = table.Column<int>(type: "INTEGER", precision: 18, scale: 2, nullable: false),
                    StrengthOverallHome = table.Column<int>(type: "INTEGER", nullable: false),
                    StrengthOverallAway = table.Column<int>(type: "INTEGER", nullable: false),
                    StrengthAttackHome = table.Column<int>(type: "INTEGER", nullable: false),
                    StrengthAttackAway = table.Column<int>(type: "INTEGER", nullable: false),
                    StrengthDefenceHome = table.Column<int>(type: "INTEGER", nullable: false),
                    StrengthDefenceAway = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Played = table.Column<int>(type: "INTEGER", nullable: false),
                    Win = table.Column<int>(type: "INTEGER", nullable: false),
                    Draw = table.Column<int>(type: "INTEGER", nullable: false),
                    Loss = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsFor = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalDifference = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fixtures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FplId = table.Column<int>(type: "INTEGER", nullable: false),
                    Gameweek = table.Column<int>(type: "INTEGER", nullable: false),
                    GameweekDataId = table.Column<int>(type: "INTEGER", nullable: true),
                    KickoffTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TeamHomeId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamAwayId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamHomeScore = table.Column<int>(type: "INTEGER", nullable: true),
                    TeamAwayScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Finished = table.Column<bool>(type: "INTEGER", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ProvisionalStartTime = table.Column<bool>(type: "INTEGER", nullable: false),
                    Started = table.Column<bool>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fixtures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fixtures_GameweekData_GameweekDataId",
                        column: x => x.GameweekDataId,
                        principalTable: "GameweekData",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Fixtures_Teams_TeamAwayId",
                        column: x => x.TeamAwayId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fixtures_Teams_TeamHomeId",
                        column: x => x.TeamHomeId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FplId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    SecondName = table.Column<string>(type: "TEXT", nullable: false),
                    WebName = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SelectedByPercent = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: false),
                    TotalPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsPerGame = table.Column<decimal>(type: "TEXT", nullable: false),
                    Form = table.Column<decimal>(type: "TEXT", nullable: false),
                    TransfersIn = table.Column<int>(type: "INTEGER", nullable: false),
                    TransfersOut = table.Column<int>(type: "INTEGER", nullable: false),
                    ValueForm = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValueSeason = table.Column<decimal>(type: "TEXT", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    CleanSheets = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsConceded = table.Column<int>(type: "INTEGER", nullable: false),
                    YellowCards = table.Column<int>(type: "INTEGER", nullable: false),
                    RedCards = table.Column<int>(type: "INTEGER", nullable: false),
                    Saves = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Bps = table.Column<int>(type: "INTEGER", nullable: false),
                    Influence = table.Column<decimal>(type: "TEXT", nullable: false),
                    Creativity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Threat = table.Column<decimal>(type: "TEXT", nullable: false),
                    IctIndex = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    News = table.Column<string>(type: "TEXT", nullable: false),
                    NewsAdded = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChanceOfPlayingNextRound = table.Column<int>(type: "INTEGER", nullable: true),
                    ChanceOfPlayingThisRound = table.Column<int>(type: "INTEGER", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoricalPlayerPerformances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FplPlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Season = table.Column<string>(type: "TEXT", nullable: false),
                    Gameweek = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", precision: 18, scale: 2, nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    CleanSheets = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsConceded = table.Column<int>(type: "INTEGER", nullable: false),
                    YellowCards = table.Column<int>(type: "INTEGER", nullable: false),
                    RedCards = table.Column<int>(type: "INTEGER", nullable: false),
                    Saves = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Bps = table.Column<int>(type: "INTEGER", nullable: false),
                    Influence = table.Column<decimal>(type: "TEXT", nullable: false),
                    Creativity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Threat = table.Column<decimal>(type: "TEXT", nullable: false),
                    IctIndex = table.Column<decimal>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    TeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamName = table.Column<string>(type: "TEXT", nullable: false),
                    WasHome = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpponentTeam = table.Column<string>(type: "TEXT", nullable: false),
                    OpponentStrength = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamScore = table.Column<int>(type: "INTEGER", nullable: false),
                    OpponentScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    OwnershipPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    Form5Games = table.Column<decimal>(type: "TEXT", nullable: false),
                    HomeAwayForm = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinutesPerGame = table.Column<decimal>(type: "TEXT", nullable: false),
                    GoalsPerGame = table.Column<decimal>(type: "TEXT", nullable: false),
                    AssistsPerGame = table.Column<decimal>(type: "TEXT", nullable: false),
                    PointsPerMillion = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsPlayingNextWeek = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalPlayerPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricalPlayerPerformances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoricalPlayerPerformances_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InjuryUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    InjuryType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ReportedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InjuryUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InjuryUpdates_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerFixture",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    FixtureId = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHome = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFixture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerFixture_Fixtures_FixtureId",
                        column: x => x.FixtureId,
                        principalTable: "Fixtures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerFixture_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerGameweekPerformances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Gameweek = table.Column<int>(type: "INTEGER", nullable: false),
                    GameweekDataId = table.Column<int>(type: "INTEGER", nullable: true),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Goals = table.Column<int>(type: "INTEGER", nullable: false),
                    Assists = table.Column<int>(type: "INTEGER", nullable: false),
                    CleanSheets = table.Column<int>(type: "INTEGER", nullable: false),
                    GoalsConceded = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnGoals = table.Column<int>(type: "INTEGER", nullable: false),
                    PenaltiesSaved = table.Column<int>(type: "INTEGER", nullable: false),
                    PenaltiesMissed = table.Column<int>(type: "INTEGER", nullable: false),
                    YellowCards = table.Column<int>(type: "INTEGER", nullable: false),
                    RedCards = table.Column<int>(type: "INTEGER", nullable: false),
                    Saves = table.Column<int>(type: "INTEGER", nullable: false),
                    BonusPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    Bps = table.Column<int>(type: "INTEGER", nullable: false),
                    Influence = table.Column<decimal>(type: "TEXT", nullable: false),
                    Creativity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Threat = table.Column<decimal>(type: "TEXT", nullable: false),
                    IctIndex = table.Column<decimal>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    WasHome = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpponentTeamId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamScore = table.Column<int>(type: "INTEGER", nullable: false),
                    OpponentScore = table.Column<int>(type: "INTEGER", nullable: false),
                    KickoffTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGameweekPerformances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerGameweekPerformances_GameweekData_GameweekDataId",
                        column: x => x.GameweekDataId,
                        principalTable: "GameweekData",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PlayerGameweekPerformances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerPredictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Gameweek = table.Column<int>(type: "INTEGER", nullable: false),
                    PredictedPoints = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    MinutesLikelihood = table.Column<decimal>(type: "TEXT", nullable: false),
                    GoalsPrediction = table.Column<decimal>(type: "TEXT", nullable: false),
                    AssistsPrediction = table.Column<decimal>(type: "TEXT", nullable: false),
                    CleanSheetChance = table.Column<decimal>(type: "TEXT", nullable: false),
                    BonusPrediction = table.Column<decimal>(type: "TEXT", nullable: false),
                    FormAnalysis = table.Column<string>(type: "TEXT", nullable: false),
                    FixtureDifficulty = table.Column<string>(type: "TEXT", nullable: false),
                    InjuryRisk = table.Column<decimal>(type: "TEXT", nullable: false),
                    RotationRisk = table.Column<decimal>(type: "TEXT", nullable: false),
                    Confidence = table.Column<decimal>(type: "TEXT", nullable: false),
                    PredictionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModelVersion = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPredictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerPredictions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferNews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<int>(type: "INTEGER", nullable: false),
                    NewsType = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    PublishedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Reliability = table.Column<string>(type: "TEXT", nullable: false),
                    FromTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    ToTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    TransferFee = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    IsConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferNews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferNews_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_GameweekDataId",
                table: "Fixtures",
                column: "GameweekDataId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_TeamAwayId",
                table: "Fixtures",
                column: "TeamAwayId");

            migrationBuilder.CreateIndex(
                name: "IX_Fixtures_TeamHomeId",
                table: "Fixtures",
                column: "TeamHomeId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlayerPerformances_PlayerId",
                table: "HistoricalPlayerPerformances",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricalPlayerPerformances_TeamId",
                table: "HistoricalPlayerPerformances",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_InjuryUpdates_PlayerId",
                table: "InjuryUpdates",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerFixture_FixtureId",
                table: "PlayerFixture",
                column: "FixtureId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerFixture_PlayerId",
                table: "PlayerFixture",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameweekPerformances_GameweekDataId",
                table: "PlayerGameweekPerformances",
                column: "GameweekDataId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGameweekPerformances_PlayerId",
                table: "PlayerGameweekPerformances",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPredictions_PlayerId",
                table: "PlayerPredictions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferNews_PlayerId",
                table: "TransferNews",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricalPlayerPerformances");

            migrationBuilder.DropTable(
                name: "HistoricalTeamStrengths");

            migrationBuilder.DropTable(
                name: "InjuryUpdates");

            migrationBuilder.DropTable(
                name: "PlayerFixture");

            migrationBuilder.DropTable(
                name: "PlayerGameweekPerformances");

            migrationBuilder.DropTable(
                name: "PlayerPredictions");

            migrationBuilder.DropTable(
                name: "TransferNews");

            migrationBuilder.DropTable(
                name: "Fixtures");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "GameweekData");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
