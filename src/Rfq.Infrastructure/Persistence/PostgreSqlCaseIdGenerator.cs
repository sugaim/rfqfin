using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class PostgreSqlCaseIdGenerator(RfqDbContext dbContext) : ICaseIdGenerator
{
    public const string SequenceName = "rfq_case_id_seq";

    public async Task<CaseId> NextAsync(CancellationToken cancellationToken = default)
    {
        long value = await dbContext.Database
            .SqlQueryRaw<long>("SELECT nextval('rfq_case_id_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return new CaseId(value);
    }
}
