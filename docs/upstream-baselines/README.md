# Upstream baselines

What the existing EF Core providers implement and what their own test suites verify, recorded on 2026-10-25 **before**
any of their tests were adapted to run on pengdows.efcore.adonet. Later claims about running their suites are measured
against this.

| Provider | Version | Source inventory | Test-suite record |
|---|---|---|---|
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.11 | [sqlserver.md](sqlserver.md) | [sqlserver-tests.md](sqlserver-tests.md) |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.11 | [sqlite.md](sqlite.md) | [sqlite-tests.md](sqlite-tests.md) |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | [postgresql.md](postgresql.md) | [postgresql-tests.md](postgresql-tests.md) |
| Pomelo.EntityFrameworkCore.MySql | 9.0.1 (EF Core 9) | [mysql.md](mysql.md) | [mysql-tests.md](mysql-tests.md) |
| DuckDB.EFCoreProvider (skuirrels) | main, EF Core 10 | [duckdb.md](duckdb.md) | [duckdb-tests.md](duckdb-tests.md) |

Method: the providers' source and tests were read statically (nothing was run), by one reader per provider and per
suite. Counts are approximate: theory rows are not expanded, and inherited counts were measured against EF Core
10.0.11's specification assemblies (EF Core 9.0.1's for Pomelo). Each record notes where it inferred rather than read.

Each test-suite record has: a suite map (inherited from EF's specification suites, overridden, provider-only, skipped),
the provider-only behaviours, the divergences from the shared specification (capability gaps, switches), the conditions
that gate tests, and the test infrastructure. Behaviours are marked by how they are asserted: SQL baseline, results,
both, or exception.
