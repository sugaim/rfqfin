# Instruction 03 — Infrastructure Structure and Provider Isolation

## Goal

`Rfq.Infrastructure` を整理し、Application の interface / port と実装の対応を追いやすくする。
同時に、EF Core を通常の ORM 境界として使い、PostgreSQL 固有処理を必要な箇所だけに局所化する。

## Target structure

Infrastructure のトップレベルは adapter / technical concern で整理する。

```text
Rfq.Infrastructure/
  Persistence/
    RfqDbContext.cs
    Entities/
    Configurations/
    Repositories/
    Queries/
    Migrations/

  Events/
  Time/
  Calculation/
  Development/

  DependencyInjection.cs
```

Application interface と実装クラスの対応が名前と配置から追えるようにする。

例:

- `IRfqCaseRepository` → `Persistence/Repositories/RfqCaseRepository.cs`
- `IOperationalQueries` → `Persistence/Queries/PostgreSqlOperationalQueries.cs`
- `IEventFeed` → `Events/PostgreSqlEventFeed.cs`
- `IBusinessDateResolver` → `Time/PostgreSqlBusinessDateResolver.cs`
- `ICalculationClient` → `Calculation/...`

将来 DB 実装を API 実装へ差し替える場合も、Application interface は変更せず別 adapter を追加できる構造にする。

## Persistence

- `RfqDbContext` から entity configuration を分離する。
- 各 entity mapping は `IEntityTypeConfiguration<T>` を使い、`Configurations/` に配置する。
- EF entity は persistence model として primitive 型のままでよい。
- Domain / Application typed ID との変換は repository / query / mapper 境界で行う。
- EF Core の上に独自 ORM や generic repository abstraction を追加しない。

## Repository / Query separation

- `RfqCaseRepository` は aggregate の load / add / update / revision persistence に集中させる。
- Sales / Trader active RFQ list などの read projection は query 実装へ分離する。
- expiry candidate retrieval も read/query 側へ分離する。
- Application 側の interface も、command persistence と read query の責務が混在しない形に整理する。
- full CQRS framework や別 project は追加しない。

## Database provider isolation

PostgreSQL 固有処理を洗い出し、必要な箇所に限定する。

対象例:

- `FOR UPDATE`
- sequence / `nextval`
- PostgreSQL 固有 operator / function
- provider-specific migration

方針:

- EF Core / LINQ で自然に表現できる query / update は provider-neutral に保つ。
- correctness / concurrency のため PostgreSQL 固有機能が必要な処理は残す。
- 固有機能を RDBMS 非依存化のためだけに劣化実装へ置き換えない。
- raw SQL は static / parameterized とし、通常の業務 query に拡散させない。
- PostgreSQL 固有クラスは `PostgreSql...` と命名し、provider dependency が見えるようにする。

## Dependency injection

Infrastructure implementation の登録は `AddRfqInfrastructure()` に集約する。

少なくとも以下を Infrastructure 側で登録する。

- repositories
- query implementations
- `IEventFeed`
- `IOperationalQueries`
- business/system date providers
- calculation client
- unit of work
- development-only infrastructure where appropriate

transport / authentication に依存する `ICurrentUser` 等は API 側に残す。

## Constraints

- Domain の business model は変更しない。
- Application interface は DB / EF / PostgreSQL / HTTP 型を公開しない。
- API / Web の behavior は変更しない。
- JSON payload / GridConfig の責務整理は今回行わない。別 instruction で扱う。
- project 分割や architecture framework の追加は行わない。

## Done When

- Infrastructure のファイル配置から Application interface の実装先を追える。
- `RfqDbContext` の mapping 定義が `IEntityTypeConfiguration<T>` に分離されている。
- command persistence と read query の責務が分離されている。
- PostgreSQL 固有コードの所在が限定されている。
- 通常の DB access は EF Core / LINQ を通している。
- Infrastructure DI が `AddRfqInfrastructure()` に集約されている。
- 既存テストが通る。
