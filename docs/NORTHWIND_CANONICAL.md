# Canonical Northwind test database

Northwind tests use Microsoft's EF Core Northwind contract as the source of truth:

- [NorthwindData](https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Specification.Tests/TestModels/Northwind/NorthwindData.cs) supplies the canonical rows.
- [NorthwindRelationalContext](https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Relational.Specification.Tests/TestModels/Northwind/NorthwindRelationalContext.cs) supplies the canonical relational table names and raw-query objects.

The EF query model intentionally ignores some Northwind properties because upstream providers normally provision a fuller
database script. That distinction matters here: `EnsureCreated` creates only the properties present in the active model. The
shared `NorthwindCanonicalSchema` fixture customization therefore restores the omitted columns and the canonical `Order Details`
table name before the test store is created. It is used by ordinary query, bulk-update, and query-filter Northwind fixtures.

Provider differences remain at the boundary. Each host supplies identifier quoting, store types, views, procedures, collation,
and capability-specific skips. No provider may remove a canonical table or column merely because its ordinary query model does
not use it.

The long-term provisioning path is a dedicated migration-based Northwind context generated from this same contract. The current
specification fixture still uses `EnsureCreated` because that is how EF's upstream specification tests initialize their ephemeral
stores; the schema customization keeps that path equivalent to the canonical database while migration provisioning is added and
verified separately.
