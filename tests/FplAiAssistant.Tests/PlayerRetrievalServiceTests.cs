using FplAiAssistant.Api.Models;
using FplAiAssistant.Api.Services;
using Xunit;

namespace FplAiAssistant.Tests;

public class PlayerRetrievalServiceTests
{
    private readonly PlayerRetrievalService _sut = new();

    private static Player MakePlayer(
        int id, string name, Position position, decimal price, int totalPoints,
        decimal form, decimal selectedByPercent, int minutes = 900) => new()
    {
        Id = id,
        WebName = name,
        FirstName = name,
        SecondName = name,
        Position = position,
        Price = price,
        TotalPoints = totalPoints,
        Form = form,
        SelectedByPercent = selectedByPercent,
        MinutesPlayed = minutes,
    };

    [Fact]
    public void Retrieve_FiltersByPosition_WhenQuestionNamesOne()
    {
        var pool = new List<Player>
        {
            MakePlayer(1, "KeeperOne", Position.Goalkeeper, 5.0m, 100, 6.0m, 20m),
            MakePlayer(2, "StrikerOne", Position.Forward, 9.0m, 150, 8.0m, 30m),
        };

        var result = _sut.Retrieve("who is the best goalkeeper right now?", pool);

        Assert.Single(result);
        Assert.Equal("KeeperOne", result[0].Name);
    }

    [Fact]
    public void Retrieve_FavoursForm_OverRawTotalPoints()
    {
        var pool = new List<Player>
        {
            MakePlayer(1, "HighTotalLowForm", Position.Midfielder, 8.0m, 200, 2.0m, 15m),
            MakePlayer(2, "LowTotalHighForm", Position.Midfielder, 6.0m, 60, 9.0m, 15m),
        };

        var result = _sut.Retrieve("who should I captain this week?", pool);

        Assert.Equal("LowTotalHighForm", result[0].Name);
    }

    [Fact]
    public void Retrieve_DifferentialQuestion_ExcludesHighlyOwnedPlayers()
    {
        var pool = new List<Player>
        {
            MakePlayer(1, "Template", Position.Midfielder, 8.0m, 150, 7.0m, 55m),
            MakePlayer(2, "Differential", Position.Midfielder, 6.5m, 90, 6.5m, 4m),
        };

        var result = _sut.Retrieve("give me a good differential midfielder", pool);

        Assert.Contains(result, p => p.Name == "Differential");
        Assert.DoesNotContain(result, p => p.Name == "Template");
    }

    [Fact]
    public void Retrieve_ExcludesPlayersWithNoMeaningfulMinutes()
    {
        var pool = new List<Player>
        {
            MakePlayer(1, "BenchWarmer", Position.Forward, 4.5m, 3, 9.9m, 30m, minutes: 12),
            MakePlayer(2, "RegularStarter", Position.Forward, 7.0m, 80, 5.0m, 25m, minutes: 1500),
        };

        var result = _sut.Retrieve("best forward", pool);

        Assert.DoesNotContain(result, p => p.Name == "BenchWarmer");
        Assert.Contains(result, p => p.Name == "RegularStarter");
    }

    [Fact]
    public void Retrieve_FallsBackToUnfilteredPool_WhenFiltersMatchNothing()
    {
        // No goalkeeper in the pool at all — a "best goalkeeper" question
        // should still return *something* rather than an empty list.
        var pool = new List<Player>
        {
            MakePlayer(1, "OnlyMidfielder", Position.Midfielder, 7.0m, 120, 6.0m, 20m),
        };

        var result = _sut.Retrieve("who is the best goalkeeper?", pool);

        Assert.NotEmpty(result);
    }

    [Fact]
    public void Retrieve_RespectsTakeLimit()
    {
        var pool = Enumerable.Range(1, 20)
            .Select(i => MakePlayer(i, $"Player{i}", Position.Midfielder, 6.0m, i * 5, i, 10m))
            .ToList();

        var result = _sut.Retrieve("top midfielders", pool, take: 3);

        Assert.Equal(3, result.Count);
    }
}
