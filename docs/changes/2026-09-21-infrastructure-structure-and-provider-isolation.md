# Infrastructure structure and provider isolation

## Summary

Reorganized `Rfq.Infrastructure` by adapter and technical concern while preserving the
existing database schema, HTTP behavior, and Domain model.

## Structure

- Moved EF persistence concerns under `Persistence/`, including entities, configurations,
  repositories, queries, migrations, the DbContext, and PostgreSQL transaction utilities.
- Moved event adapters to `Events/`, time adapters to `Time/`, calculation infrastructure to
  `Calculation/`, and development database utilities to `Development/`.
- Kept the stable `Rfq.Infrastructure` namespace so the folder refactor does not leak into
  consuming projects.

## Persistence boundaries

- Reduced `RfqDbContext` to DbSet exposure and configuration discovery.
- Moved every entity mapping to an `IEntityTypeConfiguration<T>` implementation under
  `Persistence/Configurations/`.
- Kept EF entities primitive-valued and retained typed ID conversion at repository/query
  boundaries.
- Removed Sales/Trader list projections and expiry-candidate retrieval from
  `IRfqCaseRepository`.
- Added `IActiveRfqQueries` and `IQuoteExpiryQueries`, implemented by dedicated PostgreSQL
  query adapters.

## PostgreSQL isolation and DI

- Made provider-specific infrastructure explicit through `PostgreSql...` class names for
  database configuration, unit of work locking, sequence allocation, event feed, queries,
  time providers, and reset safety validation.
- Retained the correctness-critical `FOR UPDATE`, sequence `nextval`, PostgreSQL search
  operators, provider types, and migrations in localized infrastructure code.
- Kept raw SQL static; normal business reads continue to use EF Core/LINQ.
- Centralized all Application-port registrations, including `IEventFeed` and
  `IOperationalQueries`, in `AddRfqInfrastructure()`; `ICurrentUser` remains in the API.

## Schema impact

- No schema or migration behavior was changed.
- Existing migrations were relocated to `Persistence/Migrations/` without generating a new
  migration.
