using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Rfq.Infrastructure;

internal static class PostgreSqlModelConfiguration
{
    public static void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.HasSequence<long>(PostgreSqlCaseIdGenerator.SequenceName);
}

