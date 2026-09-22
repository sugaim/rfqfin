using Rfq.Domain;

namespace Rfq.Application;

public interface IDeskLocalDateResolver
{
    Task<DateOnly> ResolveAsync(
        DateTimeOffset instant,
        DeskId deskId,
        CancellationToken cancellationToken = default);
}
