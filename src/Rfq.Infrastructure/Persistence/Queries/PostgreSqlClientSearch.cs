using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlClientSearch(RfqDbContext dbContext) : IClientSearch
{
    public async Task<IReadOnlyList<ClientSearchResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var input = query.Trim();
        return await dbContext.Clients
            .AsNoTracking()
            .Where(client =>
                EF.Functions.ILike(client.Code, $"%{input}%")
                || EF.Functions.ILike(client.Name, $"%{input}%"))
            .OrderBy(client => client.Code == input ? 0 : client.Code.StartsWith(input) ? 1 : 2)
            .ThenBy(client => client.Code)
            .Take(20)
            .Select(client => new ClientSearchResult(ClientId.Create(client.ClientId), client.Code, client.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientSearchResult?> ResolveAsync(
        ClientId clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientId);

        return await dbContext.Clients
            .AsNoTracking()
            .Where(client => client.ClientId == clientId.Value)
            .Select(client => new ClientSearchResult(ClientId.Create(client.ClientId), client.Code, client.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
