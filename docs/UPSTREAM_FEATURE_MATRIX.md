# Provider feature matrix

The prioritized implementation and TDD backlog is in
[`EFCORE_TDD_PLAN.md`](EFCORE_TDD_PLAN.md).

Status: working audit, 2026-10-28. This is the requested implementation grid.
Rows are normalized feature families rather than every overload and every
translator method; the provider inventories linked in each section remain the
exact API-level source. `Upstream` means present in the checked-in provider
inventory. `pengdows` means present in the current pengdows source. `◐` means
partial, conditional, driver/version-dependent, or not fully proven by tests.

Legend: `✅` implemented, `◐` partial/conditional, `❌` absent, `—` not a
meaningful feature for that engine, `?` not audited/proven. `↑` means the pengdows
implementation is ahead of the checked-in upstream surface or offers a reusable
cross-provider form that upstream does not.

In this document, **pengdows** means the target repository. “Pomelo local
checkout” means the separate local Pomelo EF10 development checkout used for
comparison; it is not part of pengdows.

## Shared EF/ADO.NET feature families

| Feature | MySQL pengdows / Upstream | SQLite pengdows / Upstream | SQL Server pengdows / Upstream | PostgreSQL pengdows / Upstream | DuckDB pengdows / Upstream |
|---|---|---|---|---|---|
| Connection string options | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ |
| `DbConnection` overload | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ |
| `DbDataSource` overload | ✅ / ◐↑ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ? |
| Generic `DbContextOptionsBuilder<T>` overloads | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ |
| DI registration | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ |
| Retry execution strategy | ✅ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ / ? |
| Ambient transactions | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ◐ / ? |
| Savepoints | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ❌ / ❌ |
| Multiple active readers | ◐ / ✅ | ✅ / ✅ | ◐ / ✅ | ◐ / ✅ | ? / ? |
| Schema support | ❌ / ❌ | ❌ / ❌ | ✅ / ✅ | ✅ / ✅ | ◐ / ? |
| Stored procedures | ◐ / ✅ | ❌ / ❌ | ◐ / ✅ | ◐ / ✅ | ❌ / ❌ |
| Sequences | ❌ / ❌ | ❌ / ❌ | ✅ / ✅ | ✅ / ✅ | ◐ / ✅ |
| Generated key retrieval | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ◐ / ✅ |
| Identity insert / explicit generated values | ✅ / ✅ | ◐ / ✅ | ✅ / ✅ | ◐ / ✅ | ◐ / ? |
| Concurrency tokens | ◐ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ / ? |
| Rowversion / system-version token | ❌ / ✅ | — / — | ❌ / ✅ | ◐ `xmin` / ✅ | — / — |
| Null ordering | ◐ / ? | ✅ / ✅ | ◐ / ? | ✅ / ✅ | ✅ / ? |
| Migrations SQL generation | ✅ / ✅ | ◐ / ✅ | ✅ / ✅ | ◐ / ✅ | ◐ / ✅ |
| Migrations history repository | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ? |
| Database scaffolding/schema reader | ✅ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ / ✅ | ? / ? |
| Bulk insert/update/delete | ◐ / ✅ | ❌ / ❌ | ✅ / ✅ | ✅ / ✅ | ◐ / ? |
| JSON column mapping | ✅ / ✅ | ❌/◐ / ✅ | ❌/◐ / ✅ | ✅ / ✅ | ❌/◐ / ✅ |
| EF JSON complex/owned types | ◐ / ✅ | ❌ / ✅ | ❌ / ✅ | ◐ / ✅ | ❌ / ✅ |
| EF primitive collections | ✅ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ / ✅ | ◐ native LIST / ✅ |
| NTS/spatial base integration | ◐ / ✅ | ❌ / ✅ | ✅ / ✅ | ❌ / ✅ | ? / ✅ |
| Design-time services | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ✅ / ✅ | ◐ / ? |

The JSON rows deserve special care: many current specification failures occur
before SQL translation because the test model lacks the provider-specific JSON
store type. They are real local test-lane/configuration gaps, but should not be
interpreted as proof that every JSON translator is absent.

## MySQL feature grid

Source baseline: [`mysql.md`](upstream-baselines/mysql.md). pengdows source:
[`MySqlDialect.cs`](../src/pengdows.efcore.adonet.mysql/MySqlDialect.cs),
[`MySqlMigrations.cs`](../src/pengdows.efcore.adonet.mysql/MySqlMigrations.cs),
[`MySqlSchemaReader.cs`](../src/pengdows.efcore.adonet.mysql/MySqlSchemaReader.cs).

| Feature | pengdows | Upstream Oracle/Pomelo | Direction / note |
|---|---:|---:|---|
| MySQL connection/provider setup | ✅ | ✅ | pengdows uses shared dialect registration. |
| `DbDataSource` setup | ✅ | ◐ | pengdows surplus versus decompiled Oracle 10.0.12. |
| MySqlConnector driver | ✅ | — | pengdows driver lane. |
| Oracle MySql.Data driver | ✅ | ✅ | pengdows tests both drivers through one dialect. |
| No-backslash-escapes mode | ✅↑ | ◐ | Explicit pengdows dialect mode. |
| SingleStore mode | ✅↑ | ? | Explicit pengdows dialect mode. Connectivity and auto-detection verified against a real `singlestore/cluster-in-a-box` container (`SingleStoreAutoDetectTests`); the full conformance suite has not been run against it. |
| Multiple statements / batch limits | ✅ | ✅ | pengdows max batch/parameter limits are explicit. |
| JSON scalar functions | ✅ | ✅ | `JSON_EXTRACT`, `JSON_UNQUOTE`, `JSON_SET`, casts. |
| JSON primitive collections / `JSON_TABLE` | ✅ | ✅ | Current EF10 shared tests are strong. |
| JSON provider-specific extension API | ✅ | ✅ Pomelo (local checkout) | `MySqlJsonFunctionsTests.cs`/`MySqlFunctionExecutionTests.cs`: `JsonContains`, `JsonExtract<T>`, `JsonOverlaps`, `JsonContainsPath`, `JsonSearchAny`, etc. |
| Locking extensions | ✅ | ✅ Pomelo (local checkout) | `ForUpdate`/`ForShare`/`ForUpdateNoWait`/`ForUpdateSkipLocked` as query tags (`MySqlLocking.cs`); proven against a real conflicting lock, not just SQL text (`MySqlLockingExecutionTests`). |
| Upsert extensions | ✅ | ✅ Pomelo (local checkout) | `DbContext.UpsertAsync` (`MySqlUpsert.cs`), `INSERT ... ON DUPLICATE KEY UPDATE col = VALUES(col)` (the MariaDB-compatible form, not MySQL 8.0.19's row-alias form Pomelo's local branch uses). |
| Charset/collation model API | ✅ | ✅ Pomelo (local checkout) | `MySqlModelOptions.cs`: `HasCharSet`/`UseCollation` on `PropertyBuilder`, `EntityTypeBuilder`, `ComplexTypePropertyBuilder` and `ModelBuilder`, with `DelegationModes`, matching Pomelo's shape. |
| Full-text model/query API | ✅ | ✅ Pomelo (local checkout) | `MySqlFullTextTests.cs`: `IsFullText`, `EF.Functions.Match`/`IsMatch`. |
| Spatial core syntax | ◐ | ✅ | Checked against the engine, not just marked incomplete: `Relate`/`Boundary`/`PointOnSurface` are null for plain MySQL because MySQL 8 genuinely has no such function (MariaDB does, and already gets the real translation). `Covers` is not a MySQL-specific gap — it is not implemented for *any* dialect in this codebase (`SpatialSyntax.cs`'s `IsoFunctions` has no entry for it); closing it is a core-wide feature, not a MySQL fix. |
| Multi-table DML | ✅ | ✅ | pengdows dialect templates. |
| Auto-increment/generated columns | ✅ | ✅ | pengdows migration reorder/drop/add handling. |
| Expression defaults on TEXT/BLOB/JSON/GEOMETRY | ✅↑ | ✅ | pengdows migration-specific handling. |
| Schema reverse engineering | ✅ | ✅ | MySQL-specific information-schema reader. |
| Server-version feature gates | ◐ | ◐ | `MySqlDialect.AutoDetect` picks MySQL, MariaDB or SingleStore: the connection handshake alone (free) separates MySQL/MariaDB, as Pomelo's `ServerVersion.AutoDetect` does; SingleStore's handshake deliberately mimics MySQL, so one extra `SELECT @@version_comment` on the same connection tells it apart — confirmed against a real `singlestore/cluster-in-a-box` container. Not covered: feature-threshold gating *within* a detected server (e.g. by compatibility level) — crud's one-signal-many-flags pattern, not yet built here. |
| Char-array trim translation | ✅ | ✅ | Fixed: `REGEXP_REPLACE` over an escaped character class (`FunctionSyntax.TrimCharacterSet`), not MySQL's substring-only `TRIM`. Neither Pomelo (declines to translate) nor Oracle's own provider (silently drops the characters, trims whitespace only — see `UPSTREAM_ISSUES.md`) gets this right. |
| PadLeft/PadRight translation | ✅ | ✅ | `LPAD`/`RPAD`; Oracle baseline supports these too. |
| MySQL 8.4 shared EF10 suite | ✅ | — | 29,903 passed, 0 failed, 437 skipped. |

## SQLite feature grid

Source baseline: [`sqlite.md`](upstream-baselines/sqlite.md). pengdows source:
[`SqliteDialect.cs`](../src/pengdows.efcore.adonet.sqlite/SqliteDialect.cs),
[`SqliteMigrations.cs`](../src/pengdows.efcore.adonet.sqlite/SqliteMigrations.cs).

| Feature | pengdows | Upstream Microsoft SQLite | Direction / note |
|---|---:|---:|---|
| Current SQLite dialect | ✅ | ✅ | Uses Microsoft.Data.Sqlite. |
| Pre-3.35 legacy dialect | ✅↑ | ◐ | Explicit pengdows `LegacySqliteDialect`. |
| `RETURNING` capability/options | ◐ | ✅ | pengdows split exists; option-level parity is incomplete. |
| UDF/collation connection initialization | ◐ | ✅ | Needs focused registration and external-connection tests. |
| JSON1 scalar functions | ✅ | ✅ | SQL templates present. |
| `json_each` primitive collections | ✅ | ✅ | Requires SQLite JSON1/version support. |
| EF JSON owned/complex types | ❌/◐ | ✅ | Current ledger repeatedly fails on provider JSON store type. |
| Decimal-as-text arithmetic/UDFs | ❌/◐ | ✅ | Upstream has dedicated handling; local mapping needs parity audit. |
| SQLite-specific EF functions | ◐ | ✅ | Shared math/date coverage, not full extension API. |
| Table rebuild migrations | ✅ | ✅ | Needs interleaved-operation boundary coverage. |
| Spatial / SpatiaLite / NTS | ❌ | ✅ | No local SQLite spatial package/surface. |
| Schema reverse engineering | ◐ | ✅ | Local reader exists; metadata parity not complete. |
| Lateral/correlated subquery behavior | ❌ | ◐ | SQLite engine limitation; local skips/failures need classification. |

## SQL Server feature grid

Source baseline: [`sqlserver.md`](upstream-baselines/sqlserver.md). Local source:
[`SqlServerDialect.cs`](../src/pengdows.efcore.adonet.sqlserver/SqlServerDialect.cs),
[`SqlServerMigrations.cs`](../src/pengdows.efcore.adonet.sqlserver/SqlServerMigrations.cs).

| Feature | pengdows | Upstream Microsoft SQL Server | Direction / note |
|---|---:|---:|---|
| SQL Server connection/provider setup | ✅ | ✅ | Shared ADO.NET setup. |
| Compatibility-level option | ❌ | ✅ | Major gap; SQL feature gates are not modeled. |
| `APPLY` lateral joins | ✅ | ✅ | Local dialect. |
| `OPENJSON` primitive collections | ✅ | ✅ | Local SQL template; no compatibility-level gate. |
| JSON path/non-constant JSON support | ◐ | ✅ | Needs version-gated coverage. |
| Temporal tables/model API | ❌ | ✅ | No local temporal annotations/extensions. |
| Temporal query operators | ❌ | ✅ | Provider-owned gap. |
| `rowversion` mappings | ❌ | ✅ | Important concurrency gap. |
| Identity insert | ✅ | ✅ | Local SQL Server dialect. |
| Savepoints | ✅ | ✅ | `SAVE TRANSACTION` / rollback. |
| Spatial UDT/WKB integration | ✅ | ✅ | Local NTS package and dialect binding. |
| Full-text search | ❌ | ✅ | Provider-owned gap. |
| Vector search | ❌ | ✅ | Provider-owned/newer API gap. |
| HierarchyId | ❌ | ✅ | Provider-owned gap. |
| Bulk copy / bulk loader | ✅ | ✅ | Local reusable loader/conformance path. |
| SQL Server migrations | ✅ | ✅ | Core path present; provider metadata surface incomplete. |
| Table and column comments | ✅ | ✅ | Stored as `MS_Description` extended properties by migrations. |
| Schema reverse engineering | ◐ | ✅ | Needs full metadata comparison. |

## PostgreSQL feature grid

Source baseline: [`postgresql.md`](upstream-baselines/postgresql.md). Local source:
[`PostgreSqlDialect.cs`](../src/pengdows.efcore.adonet.postgresql/PostgreSqlDialect.cs),
[`PostgreSqlIntervalReader.cs`](../src/pengdows.efcore.adonet.postgresql/PostgreSqlIntervalReader.cs).

| Feature | pengdows | Upstream Npgsql | Direction / note |
|---|---:|---:|---|
| PostgreSQL connection/provider setup | ✅ | ✅ | Shared ADO.NET setup. |
| PostgreSQL version gates | ❌ | ✅ | Major gap for version-dependent SQL. |
| Schemas / current schema | ✅ | ✅ | Local dialect. |
| JSONB mapping and path queries | ✅ | ✅ | Core JSONB templates present. |
| JSON helper extension API | ❌ | ✅ | Provider-owned API gap. |
| PostgreSQL arrays | ❌/◐ | ✅ | No Npgsql array type surface. |
| Array conversions | ❌ | ✅ | Provider-owned gap. |
| Ranges | ❌ | ✅ | Provider-owned gap. |
| Multiranges | ❌ | ✅ | Provider-owned/versioned gap. |
| Network types (`inet`, `cidr`, `macaddr`) | ❌ | ✅ | Provider-owned gap. |
| Full-text search | ❌ | ✅ | Provider-owned gap. |
| Trigram/fuzzy matching | ❌ | ✅ | Provider-owned extension gap. |
| PostgreSQL extensions/migrations | ❌/◐ | ✅ | No equivalent `HasPostgresExtension` surface. |
| `xmin` concurrency | ◐ | ✅ | Generic concurrency exists; system-column mapping not proven. |
| Infinity DateTime conversions | ? | ✅ | Needs explicit mapping tests. |
| UTC-only DateTimeOffset | ◐ | ✅ | Boundary behavior needs proof; Npgsql rejects non-UTC offsets. |
| Interval materialization | ◐↑ | ✅ | Local interceptor is new/uncommitted; needs edge coverage. |
| PostgreSQL bulk loading | ✅ | ✅ | Local bulk loader. |
| Null ordering | ✅ | ✅ | Local dialect. |
| NTS/spatial | ❌ | ✅ | No PostgreSQL NTS package surface. |

## DuckDB feature grid

Source baseline: [`duckdb.md`](upstream-baselines/duckdb.md). Local source:
[`DuckDbDialect.cs`](../src/pengdows.efcore.adonet.duckdb/DuckDbDialect.cs),
[`DuckDbDbContextOptionsBuilderExtensions.cs`](../src/pengdows.efcore.adonet.duckdb/DuckDbDbContextOptionsBuilderExtensions.cs).

| Feature | pengdows | Upstream DuckDB fork | Direction / note |
|---|---:|---:|---|
| DuckDB connection/provider setup | ✅ | ✅ | Shared ADO.NET setup. |
| LIST / primitive collections | ◐ | ✅ | Native mapping exists; EF JSON/collection suite is not complete. |
| STRUCT / composite columns | ◐ | ✅ | Local capability is present but needs dedicated conformance. |
| JSON functions/mapping | ◐ | ✅ | Repeated generic JSON store-type failures remain. |
| Row-value/analytic features | ◐ | ✅ | Needs feature-specific tests. |
| Parquet query roots | ✅↑ | ✅/older inventory | Local source/inventory is ahead of stale baseline where applicable. |
| CSV query roots | ✅↑ | ✅/older inventory | Same qualification. |
| Extension load/install policy | ✅↑ | ✅/older inventory | Local explicit `InstallAndLoad`, `LoadOnly`, `CallerManaged`. |
| Secret-safe connection configuration | ✅↑ | ◐ | Local `ConfigureConnection` path. |
| DuckLake catalogs | ✅↑ | ✅/older inventory | Snapshot, attach, secret, and data-path options. |
| Encrypted databases | ✅↑ | ✅/older inventory | Key provider and temporary-file encryption options. |
| Quack remote connections | ✅↑ | ✅/older inventory | Experimental; requires remote integration tests. |
| Bulk insert/update/delete batching | ✅↑ | ◐ | Local explicit EF batching controls. |
| Migration table rebuilds | ✅↑ | ◐ | Local opt-in rebuild workflow. |
| Stored procedures | ❌ | ❌ | Explicit engine limitation. |
| Savepoints | ❌ | ❌ | Explicit local/engine limitation. |
| Sequences / auto-increment | ◐ | ✅ | Local sequence-based identity path needs full DDL evidence. |
| Spatial/NTS | ? | ✅ | Inventory says upstream exists; local package parity unverified. |
| Time travel query roots | ◐ | ✅ | DuckLake-specific, not ordinary DuckDB. |

## Local-surplus shortlist

These are the clearest cases where pengdows has something the corresponding
upstream provider does not expose in the checked-in baseline:

1. MySQL `DbDataSource` support and one dialect tested against both
   MySqlConnector and MySql.Data.
2. MySQL explicit `NoBackslashEscapes` and `SingleStore` dialect variants.
3. SQLite's explicit current/legacy dialect split for pre-3.35 engines.
4. PostgreSQL's reusable interval reader interceptor and shared dialect-level
   interval subtraction path, pending tests and commit.
5. DuckDB's explicit extension load modes, secret-safe connection hook,
   encrypted database options, DuckLake/Quack operational options, and EF bulk
   batching controls.
6. Across all providers, a single reusable ADO.NET implementation for
   connection/data-source setup, migrations, scaffolding, SQL generation, type
   mapping, retries, and conformance tests.

These surplus rows are implementation advantages, not proof that the associated
feature is fully production-verified. The next audit pass should turn every `◐`
and `?` into a test-backed `✅`, `❌`, or explicitly documented limitation.
