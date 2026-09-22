using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rfq.Application;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class EfCoreSalesRecentRevisionQueries(RfqDbContext dbContext)
    : ISalesRecentRevisionQueries
{
    public async Task<IReadOnlyList<SalesRecentRevisionItem>> GetAsync(
        UserId salesUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(salesUserId);
        if (limit is < 1 or > 100)
        {
            throw new RfqRequestValidationException("Recent revisions limit must be between 1 and 100.");
        }

        List<CaseRow> cases = await dbContext.RfqCases.AsNoTracking()
            .Where(entity => entity.SalesId == salesUserId.Value
                || entity.Current.ContactOwnerId == salesUserId.Value)
            .Select(entity => new CaseRow(
                entity.CaseId,
                entity.ClientId,
                dbContext.Clients.Where(client => client.ClientId == entity.ClientId)
                    .Select(client => client.Name).FirstOrDefault() ?? entity.ClientId,
                entity.SecurityId,
                dbContext.Securities.Where(security => security.SecurityId == entity.SecurityId)
                    .Select(security => security.JapaneseName).FirstOrDefault() ?? entity.SecurityId))
            .ToListAsync(cancellationToken);
        if (cases.Count == 0)
        {
            return [];
        }

        long[] caseIds = [.. cases.Select(item => item.CaseId)];
        var casesById = cases.ToDictionary(item => item.CaseId);
        List<RevisionRow> revisions = await dbContext.RfqRevisions.AsNoTracking()
            .Where(item => caseIds.Contains(item.CaseId) && item.ConfirmedAt != null)
            .Select(item => new RevisionRow(
                item.CaseId,
                item.ConfirmedAt!.Value,
                item.Notional,
                item.SettlementDate,
                item.SalesAndTradingMessage))
            .ToListAsync(cancellationToken);
        List<QuoteRow> quotes = await dbContext.ConfirmedQuotes.AsNoTracking()
            .Where(item => caseIds.Contains(item.Revision.CaseId))
            .Select(item => new QuoteRow(
                item.Revision.CaseId,
                item.ConfirmedAt,
                item.Mode,
                item.CalculatedPayloadJson,
                item.ManualPayloadJson))
            .ToListAsync(cancellationToken);

        var result = new List<SalesRecentRevisionItem>();
        foreach (IGrouping<long, RevisionRow> group in revisions.GroupBy(item => item.CaseId))
        {
            RevisionRow[] ordered = [.. group.OrderBy(item => item.ConfirmedAt)];
            for (int index = 1; index < ordered.Length; index++)
            {
                List<SalesRecentRevisionChange> changes = RevisionChanges(ordered[index - 1], ordered[index]);
                if (changes.Count > 0)
                {
                    result.Add(ToItem(
                        SalesRecentRevisionKind.Rfq,
                        ordered[index].ConfirmedAt,
                        casesById[group.Key],
                        changes));
                }
            }
        }

        foreach (IGrouping<long, QuoteRow> group in quotes.GroupBy(item => item.CaseId))
        {
            QuoteValues[] ordered = [.. group.OrderBy(item => item.ConfirmedAt).Select(ToQuoteValues)];
            for (int index = 1; index < ordered.Length; index++)
            {
                List<SalesRecentRevisionChange> changes = QuoteChanges(ordered[index - 1], ordered[index]);
                if (changes.Count > 0)
                {
                    result.Add(ToItem(
                        SalesRecentRevisionKind.Quote,
                        ordered[index].ConfirmedAt,
                        casesById[group.Key],
                        changes));
                }
            }
        }

        return [.. result.OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.CaseId.Value)
            .Take(limit)];
    }

    private static SalesRecentRevisionItem ToItem(
        SalesRecentRevisionKind kind,
        DateTimeOffset occurredAt,
        CaseRow rfqCase,
        IReadOnlyList<SalesRecentRevisionChange> changes) => new(
            kind,
            occurredAt,
            new CaseId(rfqCase.CaseId),
            ClientId.Create(rfqCase.ClientId),
            rfqCase.ClientName,
            SecurityId.Create(rfqCase.SecurityId),
            rfqCase.SecurityName,
            changes);

    private static List<SalesRecentRevisionChange> RevisionChanges(
        RevisionRow before, RevisionRow after)
    {
        var changes = new List<SalesRecentRevisionChange>();
        Add(
            changes,
            SalesRecentRevisionField.Notional,
            FormatNotional(before.Notional),
            FormatNotional(after.Notional));
        Add(
            changes,
            SalesRecentRevisionField.Settlement,
            before.SettlementDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            after.SettlementDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(
            changes,
            SalesRecentRevisionField.Message,
            before.Message,
            after.Message);
        return changes;
    }

    private static List<SalesRecentRevisionChange> QuoteChanges(
        QuoteValues before, QuoteValues after)
    {
        var changes = new List<SalesRecentRevisionChange>();
        Add(changes, SalesRecentRevisionField.Price, Format(before.Price), Format(after.Price));
        Add(changes, SalesRecentRevisionField.Yield, Format(before.BbgYield), Format(after.BbgYield));
        Add(
            changes,
            SalesRecentRevisionField.Simple,
            Format(before.FinalSimpleYield),
            Format(after.FinalSimpleYield));
        Add(
            changes,
            SalesRecentRevisionField.GSpread,
            Format(before.GSpread),
            Format(after.GSpread));
        return changes;
    }

    private static QuoteValues ToQuoteValues(QuoteRow row)
    {
        if (row.Mode == WorkingQuoteMode.Calculated)
        {
            CalculatedQuotePayload value = QuotePayloadPersistence.DeserializeCalculated(row.CalculatedPayloadJson
                ?? throw new DomainInvariantException("Calculated confirmed quote payload is missing."));
            return new QuoteValues(
                row.CaseId,
                row.ConfirmedAt,
                value.Price,
                value.BbgYield,
                value.FinalSimpleYield,
                value.GSpread);
        }

        ManualQuotePayload manual = QuotePayloadPersistence.DeserializeManual(row.ManualPayloadJson
            ?? throw new DomainInvariantException("Manual confirmed quote payload is missing."));
        return new QuoteValues(
            row.CaseId,
            row.ConfirmedAt,
            manual.Price,
            null,
            manual.FinalSimpleYield,
            null);
    }

    private static void Add(
        List<SalesRecentRevisionChange> changes,
        SalesRecentRevisionField field,
        string? before,
        string? after)
    {
        if (!string.Equals(before, after, StringComparison.Ordinal))
        {
            changes.Add(new SalesRecentRevisionChange(field, before, after));
        }
    }

    private static string? Format(decimal? value) =>
        value?.ToString("0.########", CultureInfo.InvariantCulture);

    private static string? FormatNotional(decimal? value) => value is null
        ? null
        : $"{(value.Value / 1_000_000m).ToString("0.##", CultureInfo.InvariantCulture)} MM";

    private sealed record CaseRow(
        long CaseId,
        string ClientId,
        string ClientName,
        string SecurityId,
        string SecurityName);

    private sealed record RevisionRow(
        long CaseId,
        DateTimeOffset ConfirmedAt,
        decimal? Notional,
        DateOnly? SettlementDate,
        string Message);

    private sealed record QuoteRow(
        long CaseId,
        DateTimeOffset ConfirmedAt,
        WorkingQuoteMode Mode,
        string? CalculatedPayloadJson,
        string? ManualPayloadJson);

    private sealed record QuoteValues(
        long CaseId,
        DateTimeOffset ConfirmedAt,
        decimal? Price,
        decimal? BbgYield,
        decimal? FinalSimpleYield,
        decimal? GSpread);
}
