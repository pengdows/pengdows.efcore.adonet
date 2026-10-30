# Feature gaps: what is still to fix or implement

Status as of 2026-10-30. Each item says where it came from, because the sources differ in strength:

- **Measured:** a failing case in the shared EF Core 10 conformance suite (`pengdows.efcore.conformance`), with the reference
  provider passing it or the reference failing it too.
- **Matrix:** a cell in [`UPSTREAM_FEATURE_MATRIX.md`](UPSTREAM_FEATURE_MATRIX.md) (dated 2026-10-28). Those cells have not all been
  re-checked against current code, so some may already be closed.
- **Known limit:** a database or driver restriction, not a provider defect.

## 1. Measured by the conformance suite

| # | Item | Engine | Status |
|---|---|---|---|
| C1 | Serializable isolation: `BeginTransaction(IsolationLevel.Serializable)` throws. DuckDB.NET accepts only Unspecified and Snapshot. | DuckDB | Open. Decision taken: leave failing and documented. The fix is a shared-test gate change, which is off the table. Known limit. |
| C2 | Savepoints are declared unsupported on DuckDB. The skuirrels reference declares them supported and fails. | DuckDB | Reference-side error, not a pengdows gap. Pengdows is correct. |
| C3 | Collection-mode capability: pengdows declares `parameterized-collection-modes` supported, the skuirrels reference declares it unsupported. | DuckDB | Reference declaration to align. The two sides run different case sets until it is. |
| C4 | Native-type probes need the driver's `DataTypes` and `DataSourceInformation` metadata. Released DuckDB.NET 1.5.6.1 has neither; Giorgi's `develop` checkout has both. The skuirrels reference therefore runs 478 cases to pengdows's 596. | DuckDB | Driver dependency. Pengdows uses the develop checkout; the references use their own packages. |
| C5 | DuckDB `ElementAt` and `ElementAtOrDefault` with a negative index: DuckDB rejects the negative LIMIT/OFFSET. SQLite fails the same tests. | DuckDB | Open. The expected behaviour is the spec's, and a fix may need translation work. |
| C6 | Entity type configuration test (generic): the shared test runs its own raw ADO.NET command with `@id`, and DuckDB needs `$`. | DuckDB (test defect) | Shared-test defect. Not a pengdows fix. |
| C7 | TPT `SaveChanges` affected-count: pengdows returns 4 where EF expects 2. Microsoft's SQLite provider fails the same test. | DuckDB, SQLite (shared) | Unresolved. Possibly EF's commands-versus-entries count. Not changed. |
| C8 | Migration `ALTER TABLE` with dependent indexes or foreign keys. | DuckDB | Known limit. Already documented in the DuckDB fixture's known gaps. |
| C9 | Foreign-key cascade and delete behaviour when rows are deleted earlier in the same transaction. | DuckDB | Known limit, documented in the DuckDB fixture. |
| C10 | `Index`, `Take(range)`, and `ElementAt(Index from-end)` are untranslatable for relational providers. | All | EF limit, shared by the reference. |
| C11 | The `DROP DATABASE ... WITH (FORCE)` leak: pooled connections killed by a forced drop were handed out dead. | PostgreSQL | **Closed** (2026-10-30). The core clears the pool after a drop as well as before. |
| C12 | Column comments: `COMMENT ON COLUMN` was not emitted. | PostgreSQL | **Closed** (2026-10-30). |

## 2. Matrix gaps (shared EF/ADO.NET families, from the 2026-10-28 matrix)

Each row is a feature the upstream provider has and pengdows is partial or missing for, per that matrix. Re-check before work starts.

| # | Feature | Engines (matrix status) | Notes |
|---|---|---|---|
| M1 | EF JSON complex and owned types | SQLite ❌, SQL Server ❌, DuckDB ❌, MySQL ◐, PostgreSQL ◐ | Largest gap. The matrix says many current spec failures happen before SQL translation because the test model lacks the provider's JSON store type. That means part of this may be test-lane configuration, not translation. Needs a cell-by-cell check. |
| M2 | Retry execution strategy | SQLite ◐, SQL Server ◐, PostgreSQL ◐ | Partial. |
| M3 | Multiple active readers | MySQL ◐, SQL Server ◐, PostgreSQL ◐ | Partial. |
| M4 | Stored procedures | MySQL ◐, SQL Server ◐, PostgreSQL ◐ | Partial. |
| M5 | Sequences and generated keys | DuckDB ◐ | Partial. |
| M6 | Identity insert and explicit generated values | SQLite ◐, PostgreSQL ◐ | Partial. |
| M7 | Concurrency tokens | MySQL ◐, SQLite ◐, SQL Server ◐, PostgreSQL ◐ | Partial. Check each provider's token types. |
| M8 | Rowversion / system-version token | MySQL ❌ (out of date: `MySqlRowVersionTests` exists), SQL Server ❌, PostgreSQL ◐ (`xmin`) | Re-check MySQL and SQL Server. |
| M9 | Migrations SQL generation | SQLite ◐, PostgreSQL ◐, DuckDB ◐ | Partial. PostgreSQL's table and column comments are closed (C12); the rest is open. |
| M10 | Database scaffolding and schema reader | SQLite ◐, SQL Server ◐, PostgreSQL ◐, DuckDB ? | Partial. |
| M11 | Bulk insert, update, and delete | MySQL ◐, DuckDB ◐ | Partial. |
| M12 | EF primitive collections | SQLite ◐, SQL Server ◐, PostgreSQL ◐ (improved 2026-10-30, see below), DuckDB ◐ native LIST | Partial. |
| M13 | NTS and spatial base integration | MySQL ◐, SQLite ❌, PostgreSQL ❌ | Missing on two engines. |
| M14 | Retry, and other items marked `?` in the matrix | Various | Not audited. |

**PostgreSQL primitive collections (M12), partly closed 2026-10-30:** arrays of named types and ranges now build a model and
round-trip without a store type (`PostgreSqlNativeTypeTests`). Still open: a model that names no store type for a collection whose
element has more than one PostgreSQL type (for example `uint`, `decimal`, and `DateTime` ranges) fails, by design.

## 3. Feature families pengdows already has that the matrix marks ahead of upstream

These are not gaps. Listed so the matrix can record them: `DbDataSource` overloads (MySQL), and reusable cross-provider forms that
upstream does not offer.

## 4. Things this document does not cover

- Features outside the shared conformance suite and the matrix: for example scaffolding output fidelity, performance, and the
  translation of every LINQ operator on each engine. Those need their own audits.
- The `pengdows.crud` type gaps (TYPE-023 to TYPE-029 in its `FUTURE_WORK.md`) are a separate library and are tracked there.
- SQL Server's two savepoint-release cases are upstream-only failures (the reference fails them, pengdows passes). They are not
  gaps here.

## 5. Next steps

1. Re-audit the matrix cell by cell against current code, and mark each M-item closed, open, or not applicable.
2. Start with M1 (EF JSON complex and owned types): check how much of it is test-lane configuration, then how much is translation.
3. Decide C3 with the skuirrels maintainers, or align the reference declaration ourselves.
4. Decide C5 (negative `ElementAt`) and C7 (TPT count) against the spec, not against any provider's behaviour.

## 6. Re-audit against the code (2026-10-30)

First pass, by grep for implementation and tests. "Verified" means the code exists and a test covers it; it does not mean every
matrix cell is correct. Items not listed here still need a per-feature check.

| # | Finding | Evidence |
|---|---|---|
| M2 | Retry strategy is implemented in core. The matrix's partial status is out of date; a per-provider retry test is still needed. | `AdoNetRetryingExecutionStrategy.cs`, `ExecutionStrategyConformanceTests.cs` |
| M3 | Multiple active readers: the dialect flag exists, set for FlatFile and per-connection-string for SQL Server. MySQL and PostgreSQL don't set it. | `AdoNetDialect.cs`, `SqlServerCapabilities.cs`, `FlatFileDialect.cs` |
| M4 | Stored procedures: the flag is `false` for FlatFile and DuckDB, and no MySQL, SQL Server or PostgreSQL stored-procedure support is visible. | `ProviderConformanceTests.cs` gates on it |
| M5 | Sequences: DuckDB has migration and sequence code; MySQL has a schema reader entry. | `DuckDbMigrations.cs`, `MySqlSchemaReader.cs` |
| M6 | Identity insert: SQL Server implements `IdentityInsertSyntax`. No SQLite or PostgreSQL implementation. | `SqlServerDialect.cs:176` |
| M8 | Rowversion: **verified as present**, with tests on MySQL and SQL Server. The matrix row is out of date. | `MySqlRowVersionTests.cs`, `SqlServerRowVersionTests.cs` |
| M1 | JSON complex and owned types: tests exist on SQLite, DuckDB, PostgreSQL, MySQL and the core. The matrix's ❌ on SQLite, SQL Server and DuckDB needs checking against these tests. | `JsonTests.cs`, `DuckDbJsonTests.cs`, `PostgreSqlJsonTests.cs`, `MySqlModelOptionsDdlTests.cs` |

**Result:** of the 14 matrix items, M2 and M8 are shown to be implemented, and M1 has tests the matrix doesn't credit. M3, M4, M5 and M6
are partial and need per-engine work. M7, M9 to M14 haven't been checked yet.

**Not done in this pass:** implementing any remaining item. Each needs its own test-first change in the matching provider, and
several (M3 MySQL and PostgreSQL, M4 across three engines, M6 on SQLite and PostgreSQL) are real multi-day work. Doing them in one
pass would not meet the repo's test-first rules.

## 7. Bulk operations on the native driver tools (2026-10-30)

- **MySQL bulk insert:** now uses MySqlConnector's `MySqlBulkCopy` (`MySqlBulkLoader`), as SQL Server uses `SqlBulkCopy` and
  PostgreSQL uses binary `COPY`. The native copy needs `AllowLoadLocalInfile=true` on the client and `local_infile` on the server;
  without the client option the loader falls back to multi-row INSERT. It isn't on by default, because `LOAD DATA LOCAL` lets the
  server ask the client for local files. Tests: `MySqlBulkLoaderTests` (4), including a round trip through the native path.
- **Still open:** DuckDB bulk insert uses multi-row INSERT; DuckDB.NET's Appender is the native tool and is not yet wired in.
  Bulk update and delete have no native driver tool in any provider here, so they stay SQL; `BulkDelete` isn't implemented
  (matrix M11).
