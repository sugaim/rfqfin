using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface IBusinessDateResolver
{
    Task<DateOnly> ResolveAsync(
        DateTimeOffset instant,
        DeskId deskId,
        CancellationToken cancellationToken = default);
}
