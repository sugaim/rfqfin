using Microsoft.EntityFrameworkCore;
using Rfq.Domain;

namespace Rfq.Infrastructure;

public sealed class DevelopmentDataSeeder(RfqDbContext dbContext, TimeProvider timeProvider)
{
    public const string FoundationSeedKey = "database-foundation-v1";

    public const string MasterDataSeedKey = "master-data-v1";

    public const string BusinessDateSeedKey = "business-date-v1";

    public const string BusinessDateKey = "business-today";
    public const string DemoHistorySeedKey = "demo-history-v1";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        List<string> seedKeys = await dbContext.SeedMarkers
            .Select(marker => marker.Key)
            .ToListAsync(cancellationToken);
        if (!seedKeys.Contains(FoundationSeedKey, StringComparer.Ordinal))
        {
            dbContext.SeedMarkers.Add(
                new SeedMarker(FoundationSeedKey, timeProvider.GetUtcNow()));
        }

        if (!seedKeys.Contains(MasterDataSeedKey, StringComparer.Ordinal))
        {
            AddMasterData();
            dbContext.SeedMarkers.Add(
                new SeedMarker(MasterDataSeedKey, timeProvider.GetUtcNow()));
        }

        if (!seedKeys.Contains(BusinessDateSeedKey, StringComparer.Ordinal))
        {
            dbContext.BusinessDates.Add(new BusinessDateEntity
            {
                Key = BusinessDateKey,
                BusinessDate = new DateOnly(2026, 9, 21),
            });
            dbContext.SeedMarkers.Add(
                new SeedMarker(BusinessDateSeedKey, timeProvider.GetUtcNow()));
        }

        if (!seedKeys.Contains(DemoHistorySeedKey, StringComparer.Ordinal))
        {
            AddSyntheticHistory(string.Equals(
                Environment.GetEnvironmentVariable("RFQ_SEED_PROFILE"),
                "large",
                StringComparison.OrdinalIgnoreCase) ? 5000 : 750);
            dbContext.SeedMarkers.Add(
                new SeedMarker(DemoHistorySeedKey, timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddSyntheticHistory(int count)
    {
        string[] clients = ["client-001", "client-002", "client-003", "client-004"];
        (string, string, string)[] securities =
        [
            ("sec-jgb-375", "JGB", "trader-a"),
            ("sec-toyota-1", "CORP", "trader-b"),
            ("sec-toyota-2", "CORP", "trader-b"),
            ("sec-other-1", "OTHER", "trader-a"),
        ];
        string payload = QuotePayloadPersistence.Serialize(new CalculatedQuotePayload(
            CalculationDriver.Price, 100m, 100m, .8m, .81m, 0m, .81m, .82m, 10m, 13m));
        for (int index = 0; index < count; index++)
        {
            long caseId = 1000L + index;
            (string, string, string) security = securities[index % securities.Length];
            DateTimeOffset created = new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero)
                .AddMinutes(-(index * 37L));
            Guid revisionId = DeterministicGuid(caseId, 1);
            Guid quoteId = DeterministicGuid(caseId, 2);
            int bucket = index % 8;
            bool isDraft = bucket == 0;
            bool isQuoted = bucket is 2 or 3 or 6 or 7;
            RfqLifecycleKind lifecycle = bucket switch
            {
                0 => RfqLifecycleKind.Draft,
                4 => RfqLifecycleKind.Cancelled,
                6 or 7 => RfqLifecycleKind.Closed,
                _ => RfqLifecycleKind.Open,
            };
            RfqStatus status = bucket switch
            {
                0 => RfqStatus.Draft,
                3 => RfqStatus.Presented,
                4 => RfqStatus.Cancelled,
                6 => RfqStatus.Hit,
                7 => RfqStatus.Away,
                _ => RfqStatus.Active,
            };
            string createdBy = index == 1
                ? security.Item3
                : index % 3 == 0 ? "sales-a" : "sales-dev";
            var revision = new RfqRevisionEntity
            {
                RevisionId = revisionId,
                CaseId = caseId,
                Status = isDraft ? RevisionStatus.Draft : RevisionStatus.Confirmed,
                Version = isDraft ? 1 : 2,
                CreatedAt = created,
                CreatedBy = createdBy,
                SettlementDate = new DateOnly(2026, 9, 23).AddDays(index % 20),
                StandardSettlementDate = new DateOnly(2026, 9, 23),
                Notional = (10 + index % 190) * 1_000_000m,
                SalesAndTradingMessage = $"Demo RFQ {caseId}",
                ConfirmedAt = isDraft ? null : created.AddMinutes(2),
                ConfirmedBy = isDraft ? null : createdBy,
            };
            var entity = new RfqCaseEntity
            {
                CaseId = caseId,
                ClientId = clients[index % clients.Length],
                SecurityId = security.Item1,
                CategorySnapshot = security.Item2,
                CreatedAt = created,
                CreatedBusinessDate = isDraft ? null : new DateOnly(2026, 9, 21),
                CreatedBy = revision.CreatedBy,
                SalesId = index == 1 ? null : revision.CreatedBy,
                Revisions = [revision],
                SalesMemo = new SalesMemoEntity
                {
                    CaseId = caseId,
                    Value = index % 10 == 0 ? "Follow up with client" : string.Empty,
                    Version = 1,
                },
                TraderMemo = new TraderMemoEntity
                {
                    CaseId = caseId,
                    Value = index % 13 == 0 ? "Watch liquidity" : string.Empty,
                    Version = 1,
                },
                Current = new CaseCurrentEntity
                {
                    CaseId = caseId,
                    Lifecycle = lifecycle,
                    RfqStatus = status,
                    QuoteStatus = lifecycle == RfqLifecycleKind.Open
                        ? isQuoted ? QuoteStatus.Quoted : QuoteStatus.Requested
                        : null,
                    QuoteRequestReason = lifecycle == RfqLifecycleKind.Open && !isQuoted
                        ? QuoteRequestReason.Initial : null,
                    CurrentRevisionId = revisionId,
                    CurrentRevision = revision,
                    CurrentQuoteId = lifecycle == RfqLifecycleKind.Open && isQuoted ? quoteId : null,
                    ClosedQuoteId = lifecycle == RfqLifecycleKind.Closed ? quoteId : null,
                    ClosedBusinessDate = lifecycle == RfqLifecycleKind.Closed
                        ? new DateOnly(2026, 9, 21)
                        : null,
                    Version = 2,
                    ContactOwnerId = revision.CreatedBy,
                    AssignedTraderId = security.Item3,
                    Owned = lifecycle == RfqLifecycleKind.Open && index % 2 == 0,
                },
            };
            dbContext.RfqCases.Add(entity);
            if (!isDraft)
            {
                dbContext.WorkingQuotes.Add(new WorkingQuoteEntity
                {
                    RevisionId = revisionId,
                    Version = 1,
                    Mode = WorkingQuoteMode.Calculated,
                    CalculatedPayloadJson = payload,
                    CreatedAt = created.AddMinutes(3),
                    CreatedBy = security.Item3,
                    UpdatedAt = created.AddMinutes(3),
                    UpdatedBy = security.Item3,
                });
            }
            if (isQuoted)
            {
                dbContext.ConfirmedQuotes.Add(new ConfirmedQuoteEntity
                {
                    QuoteId = quoteId,
                    RevisionId = revisionId,
                    SecurityId = security.Item1,
                    SettlementDate = revision.SettlementDate!.Value,
                    ConfirmedBy = security.Item3,
                    ConfirmedAt = created.AddMinutes(5),
                    Mode = WorkingQuoteMode.Calculated,
                    CalculatedPayloadJson = payload,
                    RequestReasonAnswered = QuoteRequestReason.Initial,
                });
            }
        }
    }

    private static Guid DeterministicGuid(long value, byte suffix)
    {
        byte[] bytes = new byte[16];
        BitConverter.GetBytes(value).CopyTo(bytes, 0);
        bytes[15] = suffix;
        return new Guid(bytes);
    }

    private void AddMasterData()
    {
        dbContext.Desks.Add(new DeskEntity
        {
            DeskId = "jpy-credit",
            Name = "JPY Credit",
            TimeZoneId = "Asia/Tokyo",
        });

        dbContext.MasterUsers.AddRange(
            new MasterUserEntity
            {
                UserId = "sales-dev",
                Name = "開発 営業",
                DeskId = "jpy-credit",
                Roles = ["Sales"],
            },
            new MasterUserEntity
            {
                UserId = "sales-a",
                Name = "営業 一郎",
                DeskId = "jpy-credit",
                Roles = ["Sales"],
            },
            new MasterUserEntity
            {
                UserId = "trader-a",
                Name = "国債 トレーダー",
                DeskId = "jpy-credit",
                Roles = ["Trader"],
                DefaultQuoteExpiryMinutes = 15,
            },
            new MasterUserEntity
            {
                UserId = "trader-b",
                Name = "社債 トレーダー",
                DeskId = "jpy-credit",
                Roles = ["Trader"],
                DefaultQuoteExpiryMinutes = null,
            });

        dbContext.Categories.AddRange(
            new CategoryEntity { CategoryId = "JGB", Name = "日本国債" },
            new CategoryEntity { CategoryId = "CORP", Name = "国内社債" },
            new CategoryEntity { CategoryId = "OTHER", Name = "その他" });

        dbContext.CategoryRoutings.AddRange(
            new CategoryRoutingEntity { CategoryId = "JGB", DefaultTraderId = "trader-a" },
            new CategoryRoutingEntity { CategoryId = "CORP", DefaultTraderId = "trader-b" },
            new CategoryRoutingEntity { CategoryId = "OTHER", DefaultTraderId = "trader-a" });

        dbContext.Clients.AddRange(
            new ClientEntity { ClientId = "client-001", Code = "C001", Name = "青空銀行" },
            new ClientEntity { ClientId = "client-002", Code = "C002", Name = "みらい生命" },
            new ClientEntity { ClientId = "client-003", Code = "C003", Name = "東都証券" },
            new ClientEntity { ClientId = "client-004", Code = "C004", Name = "さくらアセット" });

        dbContext.Securities.AddRange(
            Security(
                "sec-jgb-375",
                "利付国債 第375回",
                "JGB 0.5 03/20/2030 #375",
                "0-02-0375-00001",
                "JP1103751P43",
                "JGB"),
            Security(
                "sec-toyota-1",
                "トヨタ自動車 第1回社債",
                "TOYOTA 0.5 03/20/2030 #1",
                "0-02-1234-00001",
                "JP363460AG12",
                "CORP"),
            Security(
                "sec-toyota-2",
                "トヨタ自動車 第2回社債",
                "TOYOTA 0.75 09/20/2031 #2",
                "0-02-1234-00002",
                "JP363460BH20",
                "CORP"),
            Security(
                "sec-other-1",
                "サンプル債券",
                "SAMPLE 1 12/20/2032 #1",
                "0-02-9999-00001",
                "JP0000000001",
                "OTHER"));
    }

    private static SecurityEntity Security(
        string securityId,
        string japaneseName,
        string bbgDisplay,
        string internalCode,
        string isin,
        string categoryId) =>
        new()
        {
            SecurityId = securityId,
            JapaneseName = japaneseName,
            BbgDisplay = bbgDisplay,
            BbgSearchText = Rfq.Application.SecuritySearchNormalizer.NormalizeBbgText(bbgDisplay),
            InternalCode = internalCode,
            Isin = isin,
            CategoryId = categoryId,
        };
}
