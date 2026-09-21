# EF Core and PostgreSQL implementation names

## Summary

Aligned Infrastructure implementation names with their direct provider dependency while
keeping the folder structure and Application ports introduced by Instruction 03.

## EF Core implementations

Renamed adapters that use only `DbContext`, LINQ, and normal EF Core APIs:

- `EfCoreActiveRfqQueries`
- `EfCoreQuoteExpiryQueries`
- `EfCoreCategoryRouting`
- `EfCoreBusinessDateResolver`
- `EfCoreSystemDateProvider`
- `EfCoreEventFeed`
- `EfCorePastRfqQueries`
- `EfCoreRfqHistoryQueries`
- `EfCoreEodQueries`
- `EfCoreGridConfigStore`

Provider-neutral entity configurations use the same `EfCore...` naming rule.

## PostgreSQL implementations retained

Kept `PostgreSql...` where the implementation directly relies on PostgreSQL behavior:

- `PostgreSqlUnitOfWork`: `FOR UPDATE`
- `PostgreSqlCaseIdGenerator`: sequence `nextval`
- `PostgreSqlDatabaseConfiguration`: Npgsql provider setup
- `PostgreSqlResetDevSafetyGuard`: Npgsql connection-string handling
- `PostgreSqlSecuritySearch` and `PostgreSqlClientSearch`: `ILIKE`
- `PostgreSqlUserDirectory`: PostgreSQL `text[]` role filtering
- configurations that specify `jsonb`, `text[]`, PostgreSQL timestamp types, filtered SQL,
  or the PostgreSQL sequence

## Compatibility

- Application interfaces and query semantics are unchanged.
- DI registrations now point to the renamed implementations.
- No schema or migration changes were introduced.
