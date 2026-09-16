namespace FplAiAssistant.Api.Services;

public interface IFplDataService
{
    /// <summary>
    /// Pulls the current player/team dataset from the official public FPL API
    /// and upserts it into the local database. Returns how many of each were written.
    /// </summary>
    Task<(int TeamsWritten, int PlayersWritten)> RefreshAsync(CancellationToken cancellationToken = default);
}
