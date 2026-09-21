using Microsoft.EntityFrameworkCore;

namespace Rfq.Infrastructure;

public sealed class DevelopmentDataSeeder(RfqDbContext dbContext, TimeProvider timeProvider)
{
    public const string FoundationSeedKey = "database-foundation-v1";

    public const string MasterDataSeedKey = "master-data-v1";

    public const string SystemDateSeedKey = "system-date-v1";

    public const string SystemDateKey = "business-today";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedKeys = await dbContext.SeedMarkers
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

        if (!seedKeys.Contains(SystemDateSeedKey, StringComparer.Ordinal))
        {
            dbContext.SystemDates.Add(new SystemDateEntity
            {
                Key = SystemDateKey,
                BusinessDate = new DateOnly(2026, 9, 21),
            });
            dbContext.SeedMarkers.Add(
                new SeedMarker(SystemDateSeedKey, timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
            },
            new MasterUserEntity
            {
                UserId = "trader-b",
                Name = "社債 トレーダー",
                DeskId = "jpy-credit",
                Roles = ["Trader"],
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
