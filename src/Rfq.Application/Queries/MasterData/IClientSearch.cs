using System.Globalization;
using System.Text.RegularExpressions;
using Rfq.Domain;

namespace Rfq.Application;

public interface IClientSearch
{
    Task<IReadOnlyList<ClientSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<ClientSearchResult?> ResolveAsync(
        ClientId clientId,
        CancellationToken cancellationToken = default);
}
