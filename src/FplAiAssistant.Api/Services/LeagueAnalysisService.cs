using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Turns a classic league's standings plus each manager's entry info/picks
/// into a friend-comparison view: not just who's ahead, but squad value,
/// money in the bank, and who each manager captained. Pure, unit-tested
/// logic — no external calls — same philosophy as <see cref="SquadAnalysisService"/>.
/// </summary>
public class LeagueAnalysisService : ILeagueAnalysisService
{
    public LeagueDashboardResponse BuildLeagueDashboard(
        FplLeagueStandingsDto standings,
        IReadOnlyDictionary<int, FplEntryInfoDto> entryInfoByTeam,
        IReadOnlyDictionary<int, IReadOnlyList<FplPickDto>> picksByTeam,
        IReadOnlyList<Player> playerPool,
        int asOfGameweek,
        int? viewerTeamId = null)
    {
        var playersById = playerPool.ToDictionary(p => p.Id);

        var entries = new List<LeagueEntryView>();
        foreach (var result in standings.Standings.Results)
        {
            if (!entryInfoByTeam.TryGetValue(result.Entry, out var info))
            {
                continue; // Couldn't load this team's data (private profile, transient error) — skip rather than fail the whole league.
            }

            string? captainName = null;
            if (picksByTeam.TryGetValue(result.Entry, out var picks))
            {
                var captainPick = picks.FirstOrDefault(p => p.IsCaptain);
                if (captainPick is not null && playersById.TryGetValue(captainPick.Element, out var captainPlayer))
                {
                    captainName = captainPlayer.WebName;
                }
            }

            var rankChange = result.LastRank > 0 ? result.LastRank - result.Rank : 0;

            entries.Add(new LeagueEntryView(
                TeamId: result.Entry,
                ManagerName: result.PlayerName,
                TeamName: result.EntryName,
                Rank: result.Rank,
                RankChange: rankChange,
                Total: result.Total,
                GameweekPoints: result.EventTotal,
                TeamValue: info.TeamValueTenths / 10m,
                Bank: info.BankTenths / 10m,
                CaptainName: captainName));
        }

        entries = entries.OrderBy(e => e.Rank).ToList();

        var topGameweekScorer = entries.OrderByDescending(e => e.GameweekPoints).FirstOrDefault();
        var mostValuableSquad = entries.OrderByDescending(e => e.TeamValue).FirstOrDefault();
        var biggestClimber = entries.Where(e => e.RankChange > 0).OrderByDescending(e => e.RankChange).FirstOrDefault();

        return new LeagueDashboardResponse(
            LeagueId: standings.League.Id,
            LeagueName: standings.League.Name,
            AsOfGameweek: asOfGameweek,
            ViewerTeamId: viewerTeamId,
            Entries: entries,
            TopGameweekScorer: topGameweekScorer,
            MostValuableSquad: mostValuableSquad,
            BiggestClimber: biggestClimber);
    }

    public LeagueHistoryResponse BuildLeagueHistory(
        FplLeagueStandingsDto standings,
        IReadOnlyDictionary<int, FplEntryInfoDto> entryInfoByTeam,
        IReadOnlyDictionary<int, IReadOnlyList<FplGameweekHistoryDto>> historyByTeam,
        IReadOnlyDictionary<int, string> crestUrlsByTeamId)
    {
        var series = new List<ManagerGameweekSeries>();

        foreach (var result in standings.Standings.Results)
        {
            if (!historyByTeam.TryGetValue(result.Entry, out var history) || history.Count == 0)
            {
                continue; // Couldn't load this team's history (private profile, transient error) — leave it out of the chart.
            }

            string? crestUrl = null;
            if (entryInfoByTeam.TryGetValue(result.Entry, out var info) && info.FavouriteTeamId is int favouriteTeamId)
            {
                crestUrlsByTeamId.TryGetValue(favouriteTeamId, out crestUrl);
            }

            var points = history
                .OrderBy(h => h.Event)
                .Select(h => new GameweekHistoryPoint(
                    Gameweek: h.Event,
                    Points: h.Points,
                    TotalPoints: h.TotalPoints,
                    OverallRank: h.OverallRank,
                    TeamValue: h.TeamValueTenths / 10m,
                    Bank: h.BankTenths / 10m,
                    Transfers: h.Transfers,
                    TransferCost: h.TransferCost,
                    PointsOnBench: h.PointsOnBench))
                .ToList();

            series.Add(new ManagerGameweekSeries(
                TeamId: result.Entry,
                ManagerName: result.PlayerName,
                TeamName: result.EntryName,
                CrestUrl: crestUrl,
                History: points));
        }

        return new LeagueHistoryResponse(
            LeagueId: standings.League.Id,
            LeagueName: standings.League.Name,
            Series: series);
    }
}
