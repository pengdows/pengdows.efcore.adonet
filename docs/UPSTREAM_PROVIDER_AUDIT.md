# Upstream provider completeness audit

Status: working audit, 2026-10-28.  This document records a comparison of
`pengdows.efcore.adonet` with the provider-specific EF Core surfaces in this
repository's upstream inventories and with the local MySQL comparison lanes.
It is intentionally broader than a PR review: the repository had unrelated
uncommitted changes when the audit began, and those changes were preserved.

The row-by-row implementation grid is in
[`UPSTREAM_FEATURE_MATRIX.md`](UPSTREAM_FEATURE_MATRIX.md).

## Scope and evidence

The comparison separates three questions that are easy to conflate:

1. Does the dialect implement the shared EF Core relational/ADO.NET contract?
2. Does it match behavior that the database and driver actually support?
3. Does it reproduce every provider-owned extension API?

The third question is not automatically a defect. Provider-owned features such
as PostgreSQL ranges, SQL Server temporal tables, or DuckDB external-file query
roots do not belong in the common ADO.NET abstraction unless they are designed
as an explicit provider extension. They are nevertheless recorded here so the
direction of the gap is visible.

Evidence used:

- Provider inventories in [`docs/upstream-baselines`](upstream-baselines/README.md)
  and the source under `src/pengdows.efcore.adonet.*`.
- EF Core 10.0.11 / Npgsql 10.0.3 / DuckDB.NET 1.5.6.1 package metadata in
  [`Directory.Packages.props`](../Directory.Packages.props).
- The MySQL comparison and specification records, including
  [`mysql-full-review.tsv`](specification-results/mysql-full-review.tsv).
- The historical provider result ledgers. Their totals are evidence of current
  harness behavior only; they are not normalized provider-quality scores. In
  particular, JSON fixtures without a provider-specific store type, unsupported
  upstream tests, and intentionally skipped database features must not be read
  as local regressions.

## Executive result

| Provider | Shared-contract position | Main upstream-side gap | Main local-side gap / risk | Audit confidence |
|---|---|---|---|---|
| MySQL | Strong for MySQL 8.4 + MySqlConnector | Oracle/Pomelo-owned APIs are not reproduced | No server-version capability object; some provider semantics remain incomplete | High |
| SQLite | Broad core dialect, but not complete | SQLite provider options, UDF/collation plumbing, RETURNING conventions, and NTS surface are richer | JSON model validation currently rejects the provider store type; old/new SQLite capability split needs coverage | Medium |
| SQL Server | Broad core dialect with bulk/spatial/JSON foundations | Temporal, graph, hierarchyid, vector, full-text, compatibility-level and many metadata APIs | No compatibility-level gating; rowversion/temporal/provider-owned surface appears materially incomplete | Medium |
| PostgreSQL | Broad core dialect with JSON and interval work | Arrays, ranges/multiranges, network types, full-text, extensions, Npgsql-specific version gates and mappings | No PostgreSQL server-version capability surface; DateTimeOffset/interval boundary behavior needs proof | Medium |
| DuckDB | Deliberately extended beyond ordinary relational support | The local source is richer in DuckDB-specific features than the older upstream inventory | External-file/lakehouse/extension features need a dedicated conformance lane; shared JSON/complex mapping remains incomplete | Medium |

## MySQL: “complete” is contract-complete, not provider-surface-complete

The current MySQL full review records **29,903 passed, 0 failed, 437 skipped
of 30,340** on MySQL 8.4 with MySqlConnector 2.6.2. The focused MySQL test
project also passed **415/418** tests, with three skips. This supports the claim
that the common EF Core 10 relational surface is in good shape for that server
and driver combination.

The comparison still found directional gaps:

- Local pengdows supports connection strings, `DbConnection`, and `DbDataSource`
  plus two ADO.NET drivers; Oracle's EF provider exposes a narrower connection
  overload set, while Pomelo exposes a much larger MySQL/MariaDB-specific model,
  JSON, locking, upsert, charset/collation, full-text, spatial, and scaffolding
  API surface.
- Fixed since this audit was written: char-array `Trim`/`TrimStart`/`TrimEnd` now translate to a regex character class
  (`FunctionSyntax.TrimCharacterSet`, MySQL's dialect using a regex-replace function), proven against MySqlConnector,
  MySql.Data and MariaDB. This claim was itself inaccurate: Oracle's provider does not correctly translate a
  multi-character trim set either — it silently drops the trim-character argument and trims whitespace only, producing
  wrong results with no error. Pomelo declines to translate the call at all rather than risk a wrong answer. Neither
  upstream provider was a correctness bar worth matching here.
- MySQL has no local server-version capability object comparable to Npgsql's
  `SetPostgresVersion` or SQL Server's compatibility-level option. This matters
  for MariaDB, SingleStore, older MySQL, and feature-dependent JSON/spatial SQL.
- MySql.Data has a known targeted JSON materialization difference: two cases
  return `MySqlUInt64` where EF requests `byte`; MySqlConnector passes those
  cases. This is driver behavior, not evidence that the dialect is driver
  neutral.

The local Pomelo checkout is an EF10 development branch, not a released upstream
EF10 baseline. The public Pomelo release line remains EF9-compatible, and EF10
support is tracked in [issue #2007](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2007)
and [issue #2050](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2050).
Therefore the Pomelo comparison is useful for API and translation discovery but
is not a stable compatibility oracle.

## SQLite

The upstream inventory in [`sqlite.md`](upstream-baselines/sqlite.md) is much
broader than the local dialect in provider-owned behavior: it includes connection
initialization for functions/collations, `UseSqlReturningClause`, SQLite-specific
`DbFunctions`, decimal-as-text handling, JSON/primitive collections, and an
explicit SpatiaLite/NTS surface. Local source has the important shared pieces:
limit/offset, JSON1 `json_each`, date/time and math templates, schema absence,
file lifecycle, and migrations with a pre-3.35 legacy dialect.

The strongest local evidence of a gap is the recorded specification result:
**15,825 passed, 11,965 failed, 220 skipped of 28,010**. Many failures are
expected harness or unsupported-feature cases, but the repeated JSON error
(`provider-specific JSON store type`), lateral-join failures, and several
provider-validation mismatches should not be accepted as “complete” without a
fresh SQLite 3.38+ run that configures the provider-specific JSON store type.

Priority follow-up:

- Add a SQLite capability/options object covering SQLite version, JSON1,
  RETURNING, and UDF/collation initialization; test both current and legacy
  dialects.
- Add focused JSON owned/complex/primitive-collection tests with the exact store
  type expected by Microsoft.Data.Sqlite.
- Compare the migration rebuild and RETURNING paths against
  `SqliteMigrations.cs`, including interleaved operations and generated keys.
- Decide explicitly whether SpatiaLite/NTS is supported, unsupported, or a
  separate package; do not leave the upstream surface implied.

## SQL Server

The local dialect already has substantial SQL Server-specific machinery:
`APPLY` lateral joins, `OPENJSON`, spatial UDT binding, identity insert,
savepoints, bulk loading, and SQL Server migrations. That is a strong foundation.

The upstream inventory in [`sqlserver.md`](upstream-baselines/sqlserver.md)
shows the missing direction: compatibility-level gating, temporal tables and
history metadata, rowversion mappings, temporal query operators, full-text,
vector, hierarchyid, richer JSON operations, and provider-specific model APIs.
The local dialect has no equivalent compatibility-level setting, so SQL generated
for `OPENJSON`, JSON paths, string trimming, or newer functions cannot be
validated against a declared server feature level.

The recorded ledger is **12,326 passed, 15,462 failed, 221 skipped of 28,009**.
The high failure count is not itself a diagnosis, but it makes a clean SQL
Server container run and per-feature triage mandatory before calling this
provider complete. In particular, rowversion/concurrency, temporal DDL/querying,
JSON, spatial, and bulk update/delete need separate evidence.

## PostgreSQL

The local PostgreSQL dialect has solid shared foundations: schemas, null ordering,
JSONB, `jsonb_array_elements`, PostgreSQL date arithmetic, bulk loading, and an
interval reader interceptor. The recent interval source changes are uncommitted,
so interval behavior is not yet a settled audit result.

The upstream inventory in [`postgresql.md`](upstream-baselines/postgresql.md)
shows a very large provider-owned surface absent from the local package:
arrays and array conversions, ranges/multiranges, `inet`/`cidr`/`macaddr`,
hstore/citext/ltree/cube extensions, PostgreSQL full-text, trigram and fuzzy
matching, Npgsql JSON helpers, generated-column/identity conventions, and
server-version gates.

The recorded ledger is **16,956 passed, 10,834 failed, 220 skipped of 28,010**.
One concrete boundary risk is DateTimeOffset: Npgsql requires UTC offsets for
`timestamptz`, while the shared provider surface can accept arbitrary offsets.
The audit must prove whether the local parameter/type mapping normalizes, rejects,
or silently changes such values. The interval reader also needs tests for null,
negative, sub-microsecond, month-bearing, and async reader paths.

Priority follow-up:

- Add a PostgreSQL version/capability option and gate identity, multirange,
  `regexp_count`, `jsonb_set_lax`, and UUID functions accordingly.
- Add explicit array/range/network/extension decisions. If unsupported, mark
  them as provider-owned gaps rather than accidental translation failures.
- Add conformance for UTC-only DateTimeOffset, infinity conversions, interval
  materialization, and `xmin` concurrency.

## DuckDB

DuckDB is the least comparable provider because its local source intentionally
contains an extended feature set: LIST/array and STRUCT mappings, JSON and row
values, extension loading, Parquet/CSV query roots, DuckLake snapshots, encrypted
databases, bulk DML batching, and Quack/remote options. These are not represented
by the ordinary relational provider inventory and should not be judged as missing
just because another provider does not expose them.

The local DuckDB ledger is **15,219 passed, 12,563 failed, 224 skipped of
28,006**. The repeated JSON store-type failures indicate that shared EF JSON
model setup is still a real gap in the test lane. The provider also explicitly
disables stored procedures and savepoints, so those limitations must remain
visible in capability metadata and conformance skips.

The main audit concern is not lack of features but evidence quality and ownership:

- The local source is ahead of the older `duckdb.md` inventory in DuckLake,
  encryption, Quack, extension policy, and external-file query roots. Refresh the
  inventory from the current source before comparing completeness.
- Add a dedicated DuckDB feature matrix for native LIST/STRUCT/JSON versus EF
  primitive collections/complex types; do not use generic relational JSON tests
  as the only oracle.
- Test extension installation/load modes, secrets, encrypted temporary files,
  remote connections, and snapshot reads with failure-path cleanup.
- Keep migrations, physical constraints, and SaveChanges batching claims
  separately measured from query-only external-file features.

## Cross-provider findings

1. **Capability/version modeling is the largest common gap.** MySQL, SQLite,
   SQL Server, PostgreSQL, and DuckDB all have server/engine/version-dependent
   behavior, but only the provider-specific upstreams consistently expose that
   choice. A dialect singleton is insufficient for feature-gated SQL.
2. **JSON test failures need classification before fixes.** Many current results
   fail before SQL translation because the model has no provider-specific JSON
   store type. This is a configuration/metadata gap, not proof that every JSON
   translation is wrong.
3. **Provider-owned APIs are asymmetric by design.** Local pengdows is broader
   than Oracle in shared ADO.NET connection and dialect reuse, while Pomelo,
   Npgsql, Microsoft SQL Server, and the DuckDB fork are broader in database-owned
   APIs. The correct response is selective extension packages and capability
   reporting, not indiscriminate API cloning.
4. **The historical result ledgers are not release gates.** Before declaring any
   provider complete, rerun its focused tests against a pinned engine/container,
   record the exact commit and package versions, and split pass/fail/skip totals
   into shared contract, provider-owned feature, expected engine limitation, and
   harness/infrastructure categories.

## Where pengdows is ahead of the upstream provider surfaces

This is the deliberately positive half of the audit. “Ahead” here means either
an observable capability not present in the corresponding inventory/API, or a
cross-provider design that the upstream provider does not offer as one reusable
surface. It does not mean every feature below is production-complete.

| Area | Confirmed local surplus | Evidence and qualification |
|---|---|---|
| All providers | One dialect abstraction over ADO.NET connections, data sources, retries, migrations, scaffolding, type mappings, SQL generation, and conformance fixtures | [`AdoNetDialect.cs`](../src/pengdows.efcore.adonet/AdoNetDialect.cs), [`AdoNetDbContextOptionsBuilderExtensions.cs`](../src/pengdows.efcore.adonet/AdoNetDbContextOptionsBuilderExtensions.cs). This is architectural surplus, not a claim that every provider-owned API is unified. |
| MySQL | Same EF10 pengdows dialect can run through both MySqlConnector and Oracle's MySql.Data driver; includes `DbDataSource` overloads and shared driver-neutral tests | [`MySqlProviderComparisonTests.cs`](../test/pengdows.efcore.adonet.mysql.Tests/MySqlProviderComparisonTests.cs), [`MySqlDbContextOptionsBuilderExtensions.cs`](../src/pengdows.efcore.adonet.mysql/MySqlDbContextOptionsBuilderExtensions.cs). Oracle's decompiled 10.0.12 API did not expose the same `DbDataSource` overload. |
| MySQL | Explicit `NoBackslashEscapes` and `SingleStore` dialect modes, multi-statement/batch limits, JSON primitive-collection SQL, migration fixes for generated columns/expression defaults, and schema reconstruction | [`MySqlDialect.cs`](../src/pengdows.efcore.adonet.mysql/MySqlDialect.cs), [`MySqlMigrations.cs`](../src/pengdows.efcore.adonet.mysql/MySqlMigrations.cs), [`MySqlSchemaReader.cs`](../src/pengdows.efcore.adonet.mysql/MySqlSchemaReader.cs). These are local implementation features; they still need server/version matrix coverage. |
| MySQL | Correct `Trim(char[])`/`TrimStart(char[])`/`TrimEnd(char[])` (a character set, trimmed in any order), via a regex character class and a regex-replace function. Decompiled: Oracle's `MySql.EntityFrameworkCore` 10.0.12 silently drops the trim-character argument for more than one character and trims whitespace only; Pomelo declines to translate the call at all. | [`MySqlDialect.cs`](../src/pengdows.efcore.adonet.mysql/MySqlDialect.cs) (`TrimCharacterSet`), [`MySqlUpstreamGapFakeDbTests.cs`](../test/pengdows.efcore.adonet.mysql.Tests/MySqlUpstreamGapFakeDbTests.cs). This is a correctness fix, not just a feature surplus: both upstream providers get this wrong. |
| SQLite | A first-class legacy pre-3.35 dialect alongside current SQLite behavior | [`SqliteDialect.cs`](../src/pengdows.efcore.adonet.sqlite/SqliteDialect.cs). The upstream inventory describes current RETURNING behavior but does not present the same explicit two-dialect selection in the local abstraction. |
| PostgreSQL | A provider-neutral interval reader interceptor and bulk-loader integration, with PostgreSQL interval subtraction represented through the shared dialect contracts | [`PostgreSqlIntervalReader.cs`](../src/pengdows.efcore.adonet.postgresql/PostgreSqlIntervalReader.cs), [`PostgreSqlBulkLoader.cs`](../src/pengdows.efcore.adonet.postgresql/PostgreSqlBulkLoader.cs). This is currently uncommitted and therefore not release evidence yet. |
| SQL Server | A reusable ADO.NET bulk-loader/conformance path and a local convention-set layer for SQL Server-specific model finalization | [`SqlServerBulkLoader.cs`](../src/pengdows.efcore.adonet.sqlserver/SqlServerBulkLoader.cs), [`SqlServerConventionSetBuilder.cs`](../src/pengdows.efcore.adonet.sqlserver/SqlServerConventionSetBuilder.cs). The upstream provider has related capabilities, so the surplus is the shared/reusable implementation shape rather than raw feature count. |
| DuckDB | The local provider is substantially ahead of the older checked-in inventory: encrypted databases, DuckLake snapshot/catalog operations, Quack remote options, extension load policy, external Parquet/CSV query roots, secret-safe connection configuration, and optional migration table rebuilds | [`DuckDbDialect.cs`](../src/pengdows.efcore.adonet.duckdb/DuckDbDialect.cs) and the current DuckDB inventory in [`duckdb.md`](upstream-baselines/duckdb.md). Because the inventory itself contains many of these newer features, this row is a warning to refresh the source/version comparison rather than a claim against the current DuckDB fork. |

The most meaningful surplus is therefore architectural: pengdows can provide a
single, testable EF Core surface over multiple ADO.NET drivers and engines while
still allowing dialect-specific SQL, migrations, scaffolding, and capability
limits. The most meaningful feature surplus is currently MySQL's two-driver
coverage, SQLite's explicit legacy/current split, PostgreSQL's interval-reader
work, and DuckDB's operational extensions. Those advantages should be preserved
when filling the upstream parity gaps.

## Review disposition

**MERGE: NO completeness claim yet.** No new P0 defect was established by this
read-only audit, but all four non-MySQL providers have material unverified or
known gaps, and MySQL is complete only for the tested shared EF Core contract.

**P1 follow-up:** capability/version modeling; SQLite JSON/RETURNING; SQL Server
rowversion/temporal/compatibility levels; PostgreSQL DateTimeOffset/interval and
server-version gates; DuckDB refreshed inventory and feature-specific tests.

**P2 follow-up:** provider-owned API parity, documentation of intentional
limitations, and more complete negative/driver-specific coverage.

## References

- Microsoft, [EF Core database providers](https://learn.microsoft.com/en-us/ef/core/providers/)
  and [provider version compatibility](https://learn.microsoft.com/en-us/ef/core/get-started/overview/install).
- [Pomelo.EntityFrameworkCore.MySql](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql),
  [EF10 tracking issue](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2007),
  and [maintenance discussion](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2051).
- [MySQL Connector/NET](https://github.com/mysql/mysql-connector-net), whose current
  line advertises EF Core 10/.NET 10 support.
