using Rfq.Domain;

namespace Rfq.Application;

public interface IUserCandidateQueries
{
    Task<IReadOnlyList<UserCandidate>> GetAssignableTradersAsync(
        DeskId deskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserCandidate>> GetContactOwnersAsync(
        DeskId deskId, CancellationToken cancellationToken = default);
}

public sealed record UserCandidate(UserId UserId, string Name);
