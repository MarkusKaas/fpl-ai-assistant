using FplAiAssistant.Api.Models;
using FplAiAssistant.Api.Services;
using Xunit;

namespace FplAiAssistant.Tests;

public class LeagueAnalysisServiceTests
{
    private readonly LeagueAnalysisService _sut = new();

    private static Player MakePlayer(int id, string name) => new()
    {
        Id = id,
        WebName = name,
        FirstName = name,
        SecondName = name,
        TeamId = 1,
        Team = new Team { Id = 1, ShortName = "T1" },
        Position = Position.Forward,
        Price = 8.0m,
        TotalPoints = 100,
        Form = 5.0m,
        MinutesPlayed = 900,
    };

    private static FplEntryInfoDto MakeEntryInfo(int bankTenths, int valueTenths) => new()
    {
        BankTenths = bankTenths,
        TeamValueTenths = valueTenths,
    };

    private static FplLeagueStandingsDto MakeStandings(params FplLeagueStandingEntryDto[] results) => new()
    {
        League = new FplLeagueInfoDto { Id = 275094, Name = "Friends League" },
        Standings = new FplLeagueStandingsResultsDto { Results = results.ToList() },
    };

    [Fact]
    public void BuildLeagueDashboard_BuildsEntryView_WithRankValueBankAndCaptain()
    {
        var striker = MakePlayer(1, "StrikerName");
        var standings = MakeStandings(new FplLeagueStandingEntryDto
        {
            Entry = 101, EntryName = "Team A", PlayerName = "Manager A", Rank = 1, LastRank = 1, Total = 250, EventTotal = 66,
        });
        var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto> { [101] = MakeEntryInfo(bankTenths: 12, valueTenths: 1005) };
        var picksByTeam = new Dictionary<int, IReadOnlyList<FplPickDto>>
        {
            [101] = new List<FplPickDto> { new() { Element = 1, Position = 1, IsCaptain = true } },
        };

        var result = _sut.BuildLeagueDashboard(standings, entryInfoByTeam, picksByTeam, new List<Player> { striker }, asOfGameweek: 4);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(101, entry.TeamId);
        Assert.Equal("Manager A", entry.ManagerName);
        Assert.Equal("Team A", entry.TeamName);
        Assert.Equal(1, entry.Rank);
        Assert.Equal(250, entry.Total);
        Assert.Equal(66, entry.GameweekPoints);
        Assert.Equal(100.5m, entry.TeamValue);
        Assert.Equal(1.2m, entry.Bank);
        Assert.Equal("StrikerName", entry.CaptainName);
    }

    [Fact]
    public void BuildLeagueDashboard_ComputesRankChange_FromLastRank()
    {
        var standings = MakeStandings(
            new FplLeagueStandingEntryDto { Entry = 1, Rank = 1, LastRank = 3, Total = 100, EventTotal = 50 }, // climbed 2 spots
            new FplLeagueStandingEntryDto { Entry = 2, Rank = 2, LastRank = 0, Total = 90, EventTotal = 40 }); // no previous rank yet

        var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto>
        {
            [1] = MakeEntryInfo(0, 1000),
            [2] = MakeEntryInfo(0, 1000),
        };
        var picksByTeam = new Dictionary<int, IReadOnlyList<FplPickDto>>();

        var result = _sut.BuildLeagueDashboard(standings, entryInfoByTeam, picksByTeam, new List<Player>(), asOfGameweek: 4);

        Assert.Equal(2, result.Entries.Single(e => e.TeamId == 1).RankChange);
        Assert.Equal(0, result.Entries.Single(e => e.TeamId == 2).RankChange);
    }

    [Fact]
    public void BuildLeagueDashboard_SkipsEntry_WhenEntryInfoCouldNotBeLoaded()
    {
        var standings = MakeStandings(
            new FplLeagueStandingEntryDto { Entry = 1, Rank = 1, Total = 100 },
            new FplLeagueStandingEntryDto { Entry = 2, Rank = 2, Total = 90 }); // team 2's data failed to load

        var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto> { [1] = MakeEntryInfo(0, 1000) };
        var picksByTeam = new Dictionary<int, IReadOnlyList<FplPickDto>>();

        var result = _sut.BuildLeagueDashboard(standings, entryInfoByTeam, picksByTeam, new List<Player>(), asOfGameweek: 4);

        var entry = Assert.Single(result.Entries);
        Assert.Equal(1, entry.TeamId);
    }

    [Fact]
    public void BuildLeagueDashboard_ComputesHighlights_TopScorerMostValuableAndBiggestClimber()
    {
        var standings = MakeStandings(
            new FplLeagueStandingEntryDto { Entry = 1, EntryName = "Low", PlayerName = "Low", Rank = 3, LastRank = 3, Total = 200, EventTotal = 40 },
            new FplLeagueStandingEntryDto { Entry = 2, EntryName = "TopScorer", PlayerName = "TopScorer", Rank = 2, LastRank = 5, Total = 210, EventTotal = 90 },
            new FplLeagueStandingEntryDto { Entry = 3, EntryName = "Rich", PlayerName = "Rich", Rank = 1, LastRank = 1, Total = 220, EventTotal = 50 });

        var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto>
        {
            [1] = MakeEntryInfo(0, 950),
            [2] = MakeEntryInfo(0, 1000),
            [3] = MakeEntryInfo(0, 1100), // most valuable squad
        };
        var picksByTeam = new Dictionary<int, IReadOnlyList<FplPickDto>>();

        var result = _sut.BuildLeagueDashboard(standings, entryInfoByTeam, picksByTeam, new List<Player>(), asOfGameweek: 4);

        Assert.Equal("TopScorer", result.TopGameweekScorer?.TeamName);
        Assert.Equal("Rich", result.MostValuableSquad?.TeamName);
        Assert.Equal("TopScorer", result.BiggestClimber?.TeamName); // jumped from 5th to 2nd = +3
    }

    [Fact]
    public void BuildLeagueHistory_MapsGameweekPoints_SortedByGameweek()
    {
        var standings = MakeStandings(new FplLeagueStandingEntryDto { Entry = 1, EntryName = "Team A", PlayerName = "Manager A", Rank = 1 });
        var historyByTeam = new Dictionary<int, IReadOnlyList<FplGameweekHistoryDto>>
        {
            [1] = new List<FplGameweekHistoryDto>
            {
                new() { Event = 2, Points = 55, TotalPoints = 120, OverallRank = 200_000 },
                new() { Event = 1, Points = 65, TotalPoints = 65, OverallRank = 500_000 }, // out of order on purpose
            },
        };

        var result = _sut.BuildLeagueHistory(
            standings,
            entryInfoByTeam: new Dictionary<int, FplEntryInfoDto>(),
            historyByTeam,
            crestUrlsByTeamId: new Dictionary<int, string>());

        var series = Assert.Single(result.Series);
        Assert.Equal(2, series.History.Count);
        Assert.Equal(1, series.History[0].Gameweek);
        Assert.Equal(65, series.History[0].Points);
        Assert.Equal(2, series.History[1].Gameweek);
        Assert.Equal(55, series.History[1].Points);
    }

    [Fact]
    public void BuildLeagueHistory_SkipsEntry_WhenHistoryCouldNotBeLoaded()
    {
        var standings = MakeStandings(
            new FplLeagueStandingEntryDto { Entry = 1, EntryName = "HasHistory", PlayerName = "A", Rank = 1 },
            new FplLeagueStandingEntryDto { Entry = 2, EntryName = "NoHistory", PlayerName = "B", Rank = 2 });

        var historyByTeam = new Dictionary<int, IReadOnlyList<FplGameweekHistoryDto>>
        {
            [1] = new List<FplGameweekHistoryDto> { new() { Event = 1, Points = 50 } },
        };

        var result = _sut.BuildLeagueHistory(
            standings,
            entryInfoByTeam: new Dictionary<int, FplEntryInfoDto>(),
            historyByTeam,
            crestUrlsByTeamId: new Dictionary<int, string>());

        var series = Assert.Single(result.Series);
        Assert.Equal("HasHistory", series.TeamName);
    }

    [Fact]
    public void BuildLeagueHistory_ResolvesCrestUrl_FromManagersFavouriteTeam()
    {
        var standings = MakeStandings(new FplLeagueStandingEntryDto { Entry = 1, EntryName = "Team A", PlayerName = "Manager A", Rank = 1 });
        var entryInfoByTeam = new Dictionary<int, FplEntryInfoDto> { [1] = new() { FavouriteTeamId = 3 } };
        var historyByTeam = new Dictionary<int, IReadOnlyList<FplGameweekHistoryDto>>
        {
            [1] = new List<FplGameweekHistoryDto> { new() { Event = 1, Points = 50 } },
        };
        var crestUrlsByTeamId = new Dictionary<int, string> { [3] = "https://example.com/badges/t3.png" };

        var result = _sut.BuildLeagueHistory(standings, entryInfoByTeam, historyByTeam, crestUrlsByTeamId);

        Assert.Equal("https://example.com/badges/t3.png", Assert.Single(result.Series).CrestUrl);
    }
}
