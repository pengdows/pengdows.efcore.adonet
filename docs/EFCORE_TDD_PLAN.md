# EF Core provider-parity TDD plan

This is the working backlog for closing EF Core feature gaps across the
providers. The shared EF Core pipeline is tested with fakeDb; behavior that
depends on a real engine is verified with the provider integration suite.

## Test policy

- Start with a focused failing test or convert an existing parity probe into a
  failing test.
- Use fakeDb when the behavior is EF Core model building, SQL generation,
  parameter binding, command execution, result materialization, tracking,
  SaveChanges, or provider-service wiring.
- Use a real database when correctness depends on engine parsing, execution,
  data types, indexes, migrations, locking, or server-version behavior.
- Keep both tests when fakeDb proves the EF pipeline and the real database
  proves engine semantics.
- Do not mark a feature complete until the test passes and the feature matrix
  records the implementation and test evidence.

## Work order

### 1. Flatfile migration and conformance failures

- [ ] Built-in data type failures.
- [ ] Temporal/date arithmetic failures.
- [ ] Remaining ad-hoc complex-type query failure.
- [ ] Idempotent migration script generation.
- [ ] Migration lock/history concurrency behavior.
- [ ] Historical model-diff failures.
- [ ] Verify generated-value INSERT/UPDATE uses Pengdows `INCLUDE`; retain
      `DEFAULT VALUES` coverage.
- [x] Flatfile date arithmetic using parameterized ISO intervals.
- [x] Flatfile `TimeOnly` plus/minus interval arithmetic and component reads.

The remaining flatfile temporal skip is `TimeSpan.TotalDays`: the engine now
returns date/time subtraction as `TimeSpan`, but `EXTRACT` does not yet accept
interval values. Keep that capability unadvertised until the engine exposes an
ISO interval-to-days operation.

Current result: the adapter's generated-value and migration baseline is green
in the checked-in flatfile slice. Against engine commit `24db35d` (verified
with a locally packed package), the date arithmetic and `TimeOnly` interval
cases pass. The repository now consumes preview.20, which includes that
engine work. The remaining temporal skip is the
interval-to-`TotalDays` case: date subtraction returns `TimeSpan`, but the
engine does not yet allow `EXTRACT` over interval values. Do not add
MySQL-style date functions to the ISO adapter; the remaining case requires an
engine-level ISO interval-to-days operation or an explicit capability skip.

Evidence: flatfile specification/conformance tests, `DeltaTableTests`, and
flatfile integration tests.

### 2. SQLite EF Core behavior

- [ ] JSON owned and complex types with SQLite storage mappings.
- [ ] Decimal exact arithmetic and text-backed decimal mappings.
- [ ] Schema/scaffolding metadata parity.
- [ ] Spatial/SpatiaLite model, migration, and execution behavior.
- [ ] Current versus legacy SQLite capability gates.
- [ ] `RETURNING` opt-out and re-enabled paths through SaveChanges.
- [x] SQLite date differences / `TimeSpan.TotalDays` through `JULIANDAY`.

Evidence: FakeDb probes first; Microsoft.Data.Sqlite and SpatiaLite lanes where
the engine must execute the SQL.

### 3. PostgreSQL EF Core behavior

- [ ] Array mappings, parameter binding, and array predicates.
- [ ] Range and multirange mappings and operators.
- [ ] Network types (`inet`, `cidr`, and MAC types).
- [ ] `xmin` concurrency mapping and SaveChanges behavior.
- [ ] Infinity date/time conversions.
- [ ] UTC-only `DateTimeOffset` enforcement and boundary cases.
- [ ] Extension annotations and migration generation.
- [ ] JSONB owned/complex-type materialization and updates.

Evidence: `PostgreSqlUpstreamGapFakeDbTests` for EF translation/mapping and
PostgreSQL Testcontainers for engine execution.

### 4. SQL Server EF Core behavior

- [ ] `rowversion` mapping, generated values, and concurrency conflicts.
- [ ] Temporal-table annotations and migration DDL.
- [ ] Temporal query APIs and period-column behavior.
- [ ] Compatibility-level feature gates.
- [ ] HierarchyId mapping and query translation.
- [ ] Vector mapping/query APIs.
- [ ] Full-text EF function surface.

Evidence: `SqlServerUpstreamGapFakeDbTests` for model/SQL contracts and SQL
Server Testcontainers for execution.

### 5. MySQL EF Core behavior

- [x] Provider JSON extension methods and execution. Evidence: `MySqlJsonFunctionsTests.cs`, `MySqlFunctionExecutionTests.cs`.
- [x] Charset/collation annotations, DDL, and scaffolding round-trips. Evidence: `MySqlDelegationTests.cs`,
      `MySqlModelOptionsDdlTests.cs`, `MySqlCharSetAndIndexTests.cs`, `MySqlScaffoldingAnnotationTests.cs`.
- [x] Full-text model/query APIs. Evidence: `MySqlFullTextTests.cs`.
- [x] Locking extensions: `ForUpdate`, `ForShare`, `ForUpdateNoWait`, `ForUpdateSkipLocked` as query tags, read back by
      `MySqlQuerySqlGenerator` (`MySqlLocking.cs`), as Pomelo's are. Evidence: `MySqlLockingTests` (SQL shape) and
      `MySqlLockingExecutionTests` (a real second connection genuinely fails on `NOWAIT` and is genuinely excluded by
      `SKIP LOCKED`, against MySQL 8.4).
- [x] Upsert extensions: `DbContext.UpsertAsync` builds `INSERT ... ON DUPLICATE KEY UPDATE col = VALUES(col)`
      (`MySqlUpsert.cs`) — the `VALUES(col)` form, not MySQL 8.0.19's row-alias form, so the same statement runs on
      MariaDB and older MySQL too. Bypasses the change tracker and `SaveChanges`, as Pomelo's does; unlike Pomelo, values
      are bound through EF's own raw-SQL parameterization (driver-agnostic) and identifiers through `ISqlGenerationHelper`,
      not hand-rolled MySqlConnector-specific code. Evidence: `MySqlUpsertTests` (SQL shape) and
      `MySqlUpsertExecutionTests` (a real conflicting key updates the row; MySQL's `Use Affected Rows=false` semantics
      for a no-op update are explicit, not assumed).
- [x] MySQL/MariaDB/SingleStore detection: `MySqlDialect.AutoDetect`/`FromServerVersion` (`MySqlDialect.cs`) read the
      server's own `DbConnection.ServerVersion` handshake string first (free, no query — the same signal Pomelo's
      `ServerVersion.AutoDetect` uses, not copied) to tell MySQL from MariaDB. SingleStore's handshake deliberately
      reports a MySQL-compatible version to stay compatible with version-gated clients (confirmed against a real
      server: `VERSION()` reads `5.7.32`), so `AutoDetect` also runs `SELECT @@version_comment` once on the same
      already-open connection — confirmed against a real server to read
      `SingleStoreDB source distribution (compatible; MySQL Enterprise & MySQL Commercial)`. An explicit, app-called
      selection helper (matches the ecosystem's own convention), not an automatic per-connection probe baked into the
      dialect: the dialect stays a static, shared singleton, so EF's internal-service-provider-per-dialect caching is
      untouched. Evidence: `MySqlAutoDetectTests` (fakeDb, including MariaDB's `5.5.5-` legacy-compatibility version
      prefix), `MySqlAutoDetectExecutionTests`/`MariaDbAutoDetectExecutionTests` (real MySQL 8.4 and MariaDB 11.4
      containers), and `SingleStoreAutoDetectTests` (a real `singlestore/cluster-in-a-box` container — no
      `Testcontainers.SingleStore` package exists, so this builds the container directly).
      Still open: compatibility-level/feature-threshold gating *within* a detected server (crud's one-signal→many-flags
      pattern) — this only solves "which of the three static dialects," not "does this specific MySQL 8.0 vs 8.4
      support X." Also still open: full conformance-suite verification against a real SingleStore server (connectivity
      and detection are now proven; the broader test suite, which assumes MySQL/MariaDB DDL and transaction semantics
      SingleStore's distributed engine may not share, has not been run against it).
- [x] `Trim(char[])`, `TrimStart(char[])` and `TrimEnd(char[])` translate to a regex character class via a
      regex-replace function (`FunctionSyntax.TrimCharacterSet`), not MySQL's substring-only `TRIM`. Neither Pomelo nor
      Oracle's own provider gets this right (Pomelo declines to translate; Oracle silently drops the characters and
      trims whitespace only). Evidence: `MySqlUpstreamGapFakeDbTests.Trim_character_array_translates_to_a_regex_character_class`,
      `TranslationConformanceTests.Trimming_a_set_of_characters` (MySqlConnector, MySql.Data, MariaDB).
- [x] `PadLeft` and `PadRight` translations (`LPAD`/`RPAD`). Evidence: `MySqlUpstreamGapFakeDbTests.Pad_left_and_right_translate_to_lpad_and_rpad`.

Evidence: FakeDb parity probes plus MySQL, MariaDB, and SingleStore integration
lanes where syntax or version behavior differs.

### 6. DuckDB EF Core behavior

- [ ] Native `LIST` mapping, parameters, and collection queries.
- [ ] `STRUCT` mapping and materialization.
- [ ] JSON owned/complex types.
- [ ] Sequence and identity behavior.
- [ ] Spatial mapping and execution.
- [ ] DuckLake/time-travel query roots.
- [ ] Extension loading and secret configuration through EF connection setup.

Evidence: `DuckDbUpstreamGapFakeDbTests` for EF contracts and DuckDB
integration tests for native types and extensions.

## Shared completion gate

Before closing a work item:

- [ ] A focused test demonstrates the behavior.
- [ ] FakeDb coverage exists for the EF-facing pipeline where applicable.
- [ ] Integration coverage exists where the real engine is part of correctness.
- [ ] The provider test project passes.
- [ ] The feature matrix has evidence and no stale `?`/`◐` status.
- [ ] Public API files and provider documentation are updated when needed.

## Current baseline

- Core FakeDb tests: 454 passing.
- Flatfile tests: 122 passing, 10 skipped.
- The provider-specific items above remain the implementation backlog; passing
  parity probes are not treated as complete until they exercise the intended
  EF Core behavior and have the required real-engine evidence.
