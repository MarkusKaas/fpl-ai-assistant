using FplAiAssistant.Api.Models;
using FplAiAssistant.Api.Services;
using Xunit;

namespace FplAiAssistant.Tests;

public class SquadAnalysisServiceTests
{
    private readonly SquadAnalysisService _sut = new();

    private static Player MakePlayer(
        int id, string name, int teamId, Position position, decimal price, int totalPoints,
        decimal form, int minutes = 900) => new()
    {
        Id = id,
        WebName = name,
        FirstName = name,
        SecondName = name,
        TeamId = teamId,
        Team = new Team { Id = teamId, ShortName = "T" + teamId },
        Position = position,
        Price = price,
        TotalPoints = totalPoints,
        Form = form,
        MinutesPlayed = minutes,
    };

    private static FplEntryInfoDto MakeEntry(int bankTenths = 5, int valueTenths = 1000) => new()
    {
        Id = 1,
        Name = "Test FC",
        PlayerFirstName = "Test",
        PlayerLastName = "Manager",
        OverallPoints = 100,
        GameweekPoints = 50,
        BankTenths = bankTenths,
        TeamValueTenths = valueTenths,
    };

    [Fact]
    public void BuildDashboard_SuggestsCaptain_WithHighestScoreAmongStarters()
    {
        var weakStarter = MakePlayer(1, "WeakStarter", 10, Position.Midfielder, 6.0m, 50, 3.0m);
        var strongStarter = MakePlayer(2, "StrongStarter", 11, Position.Midfielder, 7.0m, 80, 8.0m);
        var strongBench = MakePlayer(3, "StrongBench", 12, Position.Forward, 9.0m, 120, 9.0m);

        var picks = new List<FplPickDto>
        {
            new() { Element = 1, Position = 1 },
            new() { Element = 2, Position = 2 },
            new() { Element = 3, Position = 12 }, // benched — should never win captaincy
        };

        var squad = new List<Player> { weakStarter, strongStarter, strongBench };
        var fixtures = new Dictionary<int, List<FixturePreview>>();

        var result = _sut.BuildDashboard(MakeEntry(), asOfGameweek: 4, picks, squad, squad, fixtures);

        Assert.NotNull(result.SuggestedCaptain);
        Assert.Equal("StrongStarter", result.SuggestedCaptain!.Name);
    }

    [Fact]
    public void BuildDashboard_SuggestsTransfer_ForWeakestStarter_WhenBetterAffordableReplacementExists()
    {
        var weakStarter = MakePlayer(1, "WeakStarter", 10, Position.Midfielder, 6.0m, 20, 2.0m);
        var okStarter = MakePlayer(2, "OkStarter", 11, Position.Defender, 5.0m, 60, 5.0m);
        var betterReplacement = MakePlayer(3, "BetterReplacement", 12, Position.Midfielder, 6.5m, 90, 8.0m);

        var picks = new List<FplPickDto>
        {
            new() { Element = 1, Position = 1 },
            new() { Element = 2, Position = 2 },
        };

        var squad = new List<Player> { weakStarter, okStarter };
        var fullPool = new List<Player> { weakStarter, okStarter, betterReplacement };
        var fixtures = new Dictionary<int, List<FixturePreview>>();

        var result = _sut.BuildDashboard(MakeEntry(bankTenths: 10), asOfGameweek: 4, picks, squad, fullPool, fixtures);

        var suggestion = Assert.Single(result.TransferSuggestions);
        Assert.Equal("WeakStarter", suggestion.Out.Name);
        Assert.Equal("BetterReplacement", suggestion.In.Name);
    }

    [Fact]
    public void BuildDashboard_SuggestsNoTransfer_WhenNoAffordableReplacementBeatsCurrentPlayer()
    {
        var starter = MakePlayer(1, "SolidStarter", 10, Position.Forward, 12.0m, 150, 9.0m);
        var worseOption = MakePlayer(2, "WorseOption", 11, Position.Forward, 5.0m, 10, 1.0m);

        var picks = new List<FplPickDto> { new() { Element = 1, Position = 1 } };
        var squad = new List<Player> { starter };
        var fullPool = new List<Player> { starter, worseOption };
        var fixtures = new Dictionary<int, List<FixturePreview>>();

        var result = _sut.BuildDashboard(MakeEntry(bankTenths: 0), asOfGameweek: 4, picks, squad, fullPool, fixtures);

        Assert.Empty(result.TransferSuggestions);
    }

    [Fact]
    public void BuildDashboard_MarksStartingBenchCaptainAndViceCaptain_FromPicks()
    {
        var captain = MakePlayer(1, "CaptainPlayer", 10, Position.Forward, 10.0m, 100, 6.0m);
        var vice = MakePlayer(2, "VicePlayer", 11, Position.Midfielder, 7.0m, 80, 5.0m);
        var benchPlayer = MakePlayer(3, "BenchPlayer", 12, Position.Defender, 4.5m, 20, 2.0m);

        var picks = new List<FplPickDto>
        {
            new() { Element = 1, Position = 1, IsCaptain = true },
            new() { Element = 2, Position = 2, IsViceCaptain = true },
            new() { Element = 3, Position = 12 },
        };

        var squad = new List<Player> { captain, vice, benchPlayer };
        var fixtures = new Dictionary<int, List<FixturePreview>>();

        var result = _sut.BuildDashboard(MakeEntry(), asOfGameweek: 4, picks, squad, squad, fixtures);

        var captainView = Assert.Single(result.Squad, p => p.Name == "CaptainPlayer");
        Assert.True(captainView.IsCaptain);
        Assert.True(captainView.IsStarting);

        var viceView = Assert.Single(result.Squad, p => p.Name == "VicePlayer");
        Assert.True(viceView.IsViceCaptain);

        var benchView = Assert.Single(result.Squad, p => p.Name == "BenchPlayer");
        Assert.False(benchView.IsStarting);
    }

    [Fact]
    public void BuildDashboard_UsesNeutralFixtureScore_WhenNoFixturesLoadedForTeam()
    {
        var player = MakePlayer(1, "NoFixtureData", 10, Position.Midfielder, 6.0m, 50, 4.0m);
        var picks = new List<FplPickDto> { new() { Element = 1, Position = 1 } };
        var squad = new List<Player> { player };
        var fixtures = new Dictionary<int, List<FixturePreview>>(); // nothing loaded for team 10

        var result = _sut.BuildDashboard(MakeEntry(), asOfGameweek: 4, picks, squad, squad, fixtures);

        Assert.Equal(3.0m, result.Squad[0].FixtureScore);
    }

    private static List<Player> MakeFullSquad(out List<FplPickDto> picks)
    {
        // A standard 15-man squad (2 GK / 5 DEF / 5 MID / 3 FWD) with a starting XI
        // already picked (4-4-2-ish), so BuildRecommendedLineup always has a full
        // choice of legal formations to brute-force over.
        var squad = new List<Player>
        {
            MakePlayer(1, "GK1", 1, Position.Goalkeeper, 5.0m, 50, 4.0m),
            MakePlayer(2, "GK2", 2, Position.Goalkeeper, 4.0m, 20, 2.0m),
            MakePlayer(3, "Def1", 3, Position.Defender, 5.0m, 50, 4.0m),
            MakePlayer(4, "Def2", 4, Position.Defender, 5.0m, 50, 4.0m),
            MakePlayer(5, "Def3", 5, Position.Defender, 5.0m, 50, 4.0m),
            MakePlayer(6, "Def4", 6, Position.Defender, 4.5m, 30, 3.0m),
            MakePlayer(7, "Def5", 7, Position.Defender, 4.0m, 10, 1.0m),
            MakePlayer(8, "Mid1", 8, Position.Midfielder, 7.0m, 80, 6.0m),
            MakePlayer(9, "Mid2", 9, Position.Midfielder, 7.0m, 80, 6.0m),
            MakePlayer(10, "Mid3", 10, Position.Midfielder, 6.5m, 60, 5.0m),
            MakePlayer(11, "Mid4", 11, Position.Midfielder, 5.0m, 30, 2.0m),
            MakePlayer(12, "Mid5", 12, Position.Midfielder, 4.5m, 10, 1.0m),
            MakePlayer(13, "Fwd1", 13, Position.Forward, 8.0m, 100, 7.0m),
            MakePlayer(14, "Fwd2", 14, Position.Forward, 7.0m, 70, 5.0m),
            MakePlayer(15, "Fwd3", 15, Position.Forward, 5.0m, 20, 2.0m),
        };

        picks = new List<FplPickDto>
        {
            new() { Element = 1, Position = 1, IsCaptain = true },
            new() { Element = 3, Position = 2 },
            new() { Element = 4, Position = 3 },
            new() { Element = 5, Position = 4 },
            new() { Element = 8, Position = 5 },
            new() { Element = 9, Position = 6 },
            new() { Element = 10, Position = 7 },
            new() { Element = 11, Position = 8 },
            new() { Element = 13, Position = 9 },
            new() { Element = 14, Position = 10 },
            new() { Element = 6, Position = 11 }, // Def4 starts too (4 DEF / 4 MID / 2 FWD)
            new() { Element = 2, Position = 12 }, // bench GK
            new() { Element = 7, Position = 13 }, // bench
            new() { Element = 12, Position = 14 }, // bench
            new() { Element = 15, Position = 15 }, // bench
        };

        return squad;
    }

    [Fact]
    public void BuildDashboard_RecommendsLineup_WithValidFormationShape()
    {
        var squad = MakeFullSquad(out var picks);
        var fixtures = new Dictionary<int, List<FixturePreview>>();

        var result = _sut.BuildDashboard(MakeEntry(), asOfGameweek: 4, picks, squad, squad, fixtures);

        var lineup = result.RecommendedLineup;
        Assert.NotNull(lineup);
        Assert.Equal(11, lineup!.Starters.Count);
        Assert.Equal(4, lineup.Bench.Count);
        Assert.Single(lineup.Starters, p => p.Position == nameof(Position.Goalkeeper));

        var defCount = lineup.Starters.Count(p => p.Position == nameof(Position.Defender));
        var midCount = lineup.Starters.Count(p => p.Position == nameof(Position.Midfielder));
        var fwdCount = lineup.Starters.Count(p => p.Position == nameof(Position.Forward));

        Assert.InRange(defCount, 3, 5);
        Assert.InRange(midCount, 2, 5);
        Assert.InRange(fwdCount, 1, 3);
        Assert.Equal(10, defCount + midCount + fwdCount);
        Assert.Equal($"{defCount}-{midCount}-{fwdCount}", lineup.Formation);
    }

    [Fact]
    public void BuildDashboard_RecommendsStartingBenchedPlayer_WhenClearlyStrongest()
    {
        var squad = MakeFullSquad(out var picks);
        // Swap in a clearly-best benched defender in place of the weak bench defender (id 7).
        squad[6] = MakePlayer(7, "StarDef", 7, Position.Defender, 6.0m, 200, 9.9m);
        var fixtures = new Dictionary<int, List<FixturePreview>>();

        var result = _sut.BuildDashboard(MakeEntry(), asOfGameweek: 4, picks, squad, squad, fixtures);

        var lineup = result.RecommendedLineup;
        Assert.NotNull(lineup);
        Assert.Contains(lineup!.Starters, p => p.Name == "StarDef");
        Assert.Contains(lineup.ChangesFromCurrent, c => c.Contains("Start StarDef"));
    }
}
