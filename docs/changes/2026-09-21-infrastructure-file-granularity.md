# Infrastructure file granularity

## Summary

Refined the file granularity introduced by Instructions 03 and 03b without changing the
Domain model, database schema, migrations, API routes, or query semantics.

## Persistence model discovery

- Split `Persistence/Entities/RfqEntities.cs` and `MasterEntities.cs` into one file per EF
  persistence entity.
- Split grouped mapping files into one `IEntityTypeConfiguration<T>` per file.
- Kept the existing entity visibility, properties, relationships, column mappings, and
  `EfCore...` / `PostgreSql...` class-name distinction.
- Moved the PostgreSQL sequence registration into the standalone
  `PostgreSqlModelConfiguration.cs` file.

## Operational ports and implementations

Replaced the multi-responsibility `IOperationalQueries` / `EfCoreOperationalQueries` pair
with four focused port/implementation pairs:

- `IPastRfqQueries` / `EfCorePastRfqQueries`
- `IRfqHistoryQueries` / `EfCoreRfqHistoryQueries`
- `IEodQueries` / `EfCoreEodQueries`
- `IGridConfigStore` / `EfCoreGridConfigStore`

The Operations API now receives each focused port directly, and Infrastructure DI owns all
four registrations.

## GridConfig payload

- GridConfig content remains an opaque string at the Application and Infrastructure
  boundaries.
- `EfCoreGridConfigStore` does not parse or inspect the JSON content.
- The existing database column and migration history are unchanged.
