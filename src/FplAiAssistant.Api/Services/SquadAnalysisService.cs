using FplAiAssistant.Api.Models;

namespace FplAiAssistant.Api.Services;

/// <summary>
/// Turns a manager's raw picks + the full player pool + upcoming fixtures into
/// a captain suggestion and transfer suggestions. Deliberately simple, explainable
/// scoring (no ML) — the same philosophy as <see cref="PlayerRetrievalService"/>:
/// fast, free, and easy to unit test without any external service.
/// </summary>
public class SquadAnalysisService : ISquadAnalysisService
{
    /// <summary>The 8 (Def, Mid, Fwd) shapes that are legal for an FPL starting XI (plus 1 GK = 11).</summary>
    private static readonly (int Def, int Mid, int Fwd)[] ValidFormations =
    {
        (3, 4, 3), (3, 5, 2), (4, 3, 3), (4, 4, 2), (4, 5, 1), (5, 2, 3), (5, 3, 2), (5, 4, 1),
    };

    public TeamDashboardResponse BuildDashboard(
        FplEntryInfoDto entryInfo,
        int asOfGameweek,
        IReadOnlyList<FplPickDto> picks,
        IReadOnlyList<Player> squadPlayers,
        IReadOnlyList<Player> fullPool,
        IReadOnlyDictionary<int, List<FixturePreview>> fixturesByTeam,
        int transferSuggestionCount = 3)
    {
        var playersById = squadPlayers.ToDictionary(p => p.Id);
        var squadIds = new HashSet<int>(squadPlayers.Select(p => p.Id));

        var squadViews = new List<SquadPlayerView>();
        foreach (var pick in picks)
        {
            if (!playersById.TryGetValue(pick.Element, out var player))
            {
                continue; // Not in the local DB yet — needs a POST /api/data/refresh.
            }

            var fixtures = fixturesByTeam.GetValueOrDefault(player.TeamId, new List<FixturePreview>());
            squadViews.Add(ToView(player, fixtures, isStarting: pick.Position <= 11, pick.IsCaptain, pick.IsViceCaptain));
        }

        squadViews = squadViews
            .OrderByDescending(v => v.IsStarting)
            .ThenBy(v => PositionSortOrder(v.Position))
            .ThenByDescending(v => v.Price)
            .ToList();

        var suggestedCaptain = squadViews
            .Where(v => v.IsStarting)
            .OrderByDescending(PlayScore)
            .FirstOrDefault();

        var bank = entryInfo.BankTenths / 10m;
        var weakestStarters = squadViews
            .Where(v => v.IsStarting)
            .OrderBy(PlayScore)
            .Take(transferSuggestionCount)
            .ToList();

        var transferSuggestions = new List<TransferSuggestion>();
        foreach (var weak in weakestStarters)
        {
            var budget = weak.Price + bank;

            var bestReplacement = fullPool
                .Where(p => !squadIds.Contains(p.Id))
                .Where(p => p.Position.ToString() == weak.Position)
                .Where(p => p.Price <= budget)
                .Where(p => p.MinutesPlayed >= 90) // skip fringe players who barely feature
                .Select(p => ToView(p, fixturesByTeam.GetValueOrDefault(p.TeamId, new List<FixturePreview>()), false, false, false))
                .OrderByDescending(PlayScore)
                .FirstOrDefault();

            if (bestReplacement is null)
            {
                continue;
            }

            var scoreGain = PlayScore(bestReplacement) - PlayScore(weak);
            if (scoreGain <= 0)
            {
                continue; // Never suggest a sideways or worse move.
            }

            transferSuggestions.Add(new TransferSuggestion(
                Out: weak,
                In: bestReplacement,
                ScoreImprovement: Math.Round(scoreGain, 1),
                PriceDifference: bestReplacement.Price - weak.Price,
                Reason: BuildReason(weak, bestReplacement)));
        }

        return new TeamDashboardResponse(
            TeamId: entryInfo.Id,
            ManagerName: $"{entryInfo.PlayerFirstName} {entryInfo.PlayerLastName}".Trim(),
            TeamName: entryInfo.Name,
            OverallPoints: entryInfo.OverallPoints,
            OverallRank: entryInfo.OverallRank,
            GameweekPoints: entryInfo.GameweekPoints,
            Bank: bank,
            TeamValue: entryInfo.TeamValueTenths / 10m,
            AsOfGameweek: asOfGameweek,
            Squad: squadViews,
            SuggestedCaptain: suggestedCaptain,
            TransferSuggestions: transferSuggestions,
            RecommendedLineup: BuildRecommendedLineup(squadViews));
    }

    /// <summary>
    /// The best valid starting XI/formation from the manager's actual 15, independent of
    /// any transfers — brute-forces the 8 legal FPL formations and picks whichever maximises
    /// total <see cref="PlayScore"/>. Returns null if the squad is too small/unbalanced to
    /// fill any legal formation (e.g. in a unit test using a handful of players).
    /// </summary>
    private static LineupSuggestion? BuildRecommendedLineup(IReadOnlyList<SquadPlayerView> squad)
    {
        var goalkeepers = squad.Where(p => p.Position == nameof(Position.Goalkeeper)).OrderByDescending(PlayScore).ToList();
        if (goalkeepers.Count == 0)
        {
            return null;
        }

        var defenders = squad.Where(p => p.Position == nameof(Position.Defender)).OrderByDescending(PlayScore).ToList();
        var midfielders = squad.Where(p => p.Position == nameof(Position.Midfielder)).OrderByDescending(PlayScore).ToList();
        var forwards = squad.Where(p => p.Position == nameof(Position.Forward)).OrderByDescending(PlayScore).ToList();

        var startingGk = goalkeepers[0];

        (int Def, int Mid, int Fwd)? bestFormation = null;
        var bestScore = decimal.MinValue;

        foreach (var formation in ValidFormations)
        {
            if (defenders.Count < formation.Def || midfielders.Count < formation.Mid || forwards.Count < formation.Fwd)
            {
                continue; // Not enough players in this position to fill the shape.
            }

            var score = PlayScore(startingGk)
                + defenders.Take(formation.Def).Sum(PlayScore)
                + midfielders.Take(formation.Mid).Sum(PlayScore)
                + forwards.Take(formation.Fwd).Sum(PlayScore);

            if (score > bestScore)
            {
                bestScore = score;
                bestFormation = formation;
            }
        }

        if (bestFormation is null)
        {
            return null; // No legal formation fits this squad (too few players in some position).
        }

        var (def, mid, fwd) = bestFormation.Value;

        var starters = new List<SquadPlayerView> { startingGk };
        starters.AddRange(defenders.Take(def));
        starters.AddRange(midfielders.Take(mid));
        starters.AddRange(forwards.Take(fwd));
        var startingIds = new HashSet<int>(starters.Select(p => p.Id));

        var bench = squad
            .Where(p => !startingIds.Contains(p.Id))
            .OrderBy(p => p.Position == nameof(Position.Goalkeeper) ? 0 : 1) // bench GK listed first, matching FPL convention
            .ThenByDescending(PlayScore)
            .ToList();

        starters = starters
            .OrderBy(v => PositionSortOrder(v.Position))
            .ThenByDescending(PlayScore)
            .ToList();

        var changes = new List<string>();
        foreach (var player in squad)
        {
            var willStart = startingIds.Contains(player.Id);
            if (willStart && !player.IsStarting)
            {
                changes.Add($"Start {player.Name} ({player.Position})");
            }
            else if (!willStart && player.IsStarting)
            {
                changes.Add($"Bench {player.Name} ({player.Position})");
            }
        }

        return new LineupSuggestion(
            Formation: $"{def}-{mid}-{fwd}",
            Starters: starters,
            Bench: bench,
            ChangesFromCurrent: changes);
    }

    /// <summary>
    /// Blended score: recent form weighted highest (same idea as the advice
    /// retrieval service — "who's good right now" is mostly a recency question),
    /// a season-long points component, and fixture ease over the lookahead
    /// window (FPL difficulty 1=easiest..5=hardest, inverted to a 1..5 "ease"
    /// score so easier fixtures push the score up).
    /// </summary>
    private static decimal PlayScore(SquadPlayerView v) =>
        (v.Form * 2m) + (v.TotalPoints / 10m) + v.FixtureScore;

    private static SquadPlayerView ToView(Player p, List<FixturePreview> fixtures, bool isStarting, bool isCaptain, bool isViceCaptain)
    {
        var fixtureScore = fixtures.Count == 0
            ? 3m // Neutral when fixtures haven't loaded — don't penalise or reward blindly.
            : fixtures.Average(f => 6m - f.Difficulty);

        return new SquadPlayerView(
            Id: p.Id,
            Name: p.WebName,
            Team: p.Team?.ShortName ?? "?",
            Position: p.Position.ToString(),
            Price: p.Price,
            Form: p.Form,
            TotalPoints: p.TotalPoints,
            IsStarting: isStarting,
            IsCaptain: isCaptain,
            IsViceCaptain: isViceCaptain,
            FixtureScore: Math.Round(fixtureScore, 1),
            NextFixtures: fixtures);
    }

    private static string BuildReason(SquadPlayerView outPlayer, SquadPlayerView inPlayer)
    {
        var priceDiff = inPlayer.Price - outPlayer.Price;
        var priceText = priceDiff switch
        {
            > 0 => $"costs £{priceDiff:0.0}m more",
            < 0 => $"frees up £{Math.Abs(priceDiff):0.0}m",
            _ => "same price",
        };

        return $"{inPlayer.Name} is in better form ({inPlayer.Form} vs {outPlayer.Form}) with easier upcoming fixtures " +
               $"(ease {inPlayer.FixtureScore} vs {outPlayer.FixtureScore}) and {priceText}.";
    }

    private static int PositionSortOrder(string position) => position switch
    {
        nameof(Position.Goalkeeper) => 1,
        nameof(Position.Defender) => 2,
        nameof(Position.Midfielder) => 3,
        nameof(Position.Forward) => 4,
        _ => 5,
    };
}
