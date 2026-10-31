# Source review: pengdows.efcore.adonet

**Reviewed:** 2026-10-31  
**Source snapshot:** `main` at `3daadc1430887b8936cec0fb39115f6b25cbd267`  
**Scope:** Repository-wide architectural and source review, **not** a formal PR-diff review under [REVIEW_POLICY.md](../REVIEW_POLICY.md).  
**Evidence:** Source files, tests, and project documentation read through GitHub. I did **not** independently compile the repository, execute its integration tests, or benchmark it. Findings about runtime outcomes that require a reproduction are marked accordingly. Some project-reported test counts come from different commits/dates.

## Executive assessment

**Architecture: retain.** Shared EF Core/ADO.NET plumbing plus independent dialects is a sound direction. Developing six different providers concurrently tests the separation between common EF semantics and database-specific behavior; these providers act as architectural cross-checks, not merely feature-count targets. The system is under two weeks old at the time of review. Assess it as a rapidly validated new implementation, **not** a mature production substitute for every upstream EF Core provider.

**Scope boundary:** This project does **not** use `pengdows.crud` at runtime, and it does **not** provide CRUD's PoolGovernor, pool splitting, intent routing, connection modes, pool protection, backpressure, tenant-per-database orchestration, SQL builder/TableGateway, or automatic auditing. See the [README](../README.md#what-this-project-is-not) and [llms.txt](../llms.txt). The goal is to make **EF Core provider implementations more correct, less fragile, and more maintainable**.

### Five Rings assessment

| Ring | Provisional score | Basis |
| --- | ---: | --- |
| Carmack — reasoning | 9/10 | A single core, explicit dialect contract, and six independent implementations challenge assumptions early. |
| Abrash — runtime | 7/10 | Bulk and connection paths warrant measured cost/latency characterization. |
| Holub — ownership | 7/10 | Resource ownership is generally explicit, but migration lock ownership and connection-session initialization need attention. |
| Martin — structure | 9/10 | Clear plugin boundaries with common EF infrastructure in the core. |
| Schneier — adversary | 6/10 | Lock takeover, session behavior, and failure-path tests need further adversarial validation. |

These are engineering judgments from source inspection, **not** statistically derived quality metrics. Confidence: architecture **0.88**; runtime/release readiness **0.70**.

## Findings

### P0 — Generic migrations lock can be taken over while its owner is active

**Source:** [AdoNetHistoryRepository.cs](../src/pengdows.efcore.adonet/Migrations/AdoNetHistoryRepository.cs)

The generic migration lock uses a row with a timestamp and a two-minute timeout. After that timeout, a contender can delete the presumed-stale row and acquire the lock without establishing whether its original owner is still migrating. The release SQL deletes the row by fixed ID rather than by an owner identity.

**Risk:** A migration that takes longer than the timeout could overlap a second migrator; an old owner could also release a new owner's lock. This is a **source-level concurrency defect/risk**, not a demonstrated corrupt database in this review. Actual provider transaction and command behavior still needs a two-migrator reproduction.

**Minimal direction:** Use database-managed session/advisory locks where viable, or an ownership token plus lease renewal/fencing and ownership-checked release. Test simultaneous migrators, long migrations, takeover, cancellation, and loss of connection.

### P1 — PostgreSQL-specific behavior in the shared core

**Sources:** [AdoNetQuerySqlGenerator.cs](../src/pengdows.efcore.adonet/Query/AdoNetQuerySqlGenerator.cs), [AdoNetStringTypeMapping.cs](../src/pengdows.efcore.adonet/Storage/AdoNetStringTypeMapping.cs), [AdoNetJsonTypeMapping.cs](../src/pengdows.efcore.adonet/Storage/AdoNetJsonTypeMapping.cs)

The shared SQL generator emits `::inet` for an `inet` mapping; core type-mapping code also recognizes Npgsql-specific `NpgsqlDbType`/`Jsonb` through reflection. Reflection avoids a direct Npgsql assembly reference, but the knowledge remains provider-specific. The reflected parameter customization can be harmless for other drivers; the `::inet` SQL is explicitly PostgreSQL syntax.

**Direction:** Move SQL syntax and provider-specific parameter configuration behind an existing dialect/plugin extension point. Avoid a large refactor unless another provider actually exposes the need.

### P1 — MySQL migrations lock is not scoped to the current database

**Source:** [MySqlMigrations.cs](../src/pengdows.efcore.adonet.mysql/MySqlMigrations.cs)

`GET_LOCK('__EFMigrationsLock', 120)` and `RELEASE_LOCK('__EFMigrationsLock')` use a fixed server-level name. Distinct databases on the same server therefore serialize migrations unnecessarily.

**Direction:** Derive a stable advisory-lock name from the database identity, within the MySQL lock-name length constraint. Verify acquire and release use exactly the same name. Also verify that the same physical session owns the lock for the full migration: MySQL advisory locks are session-scoped.

### P1 — Asynchronous connection opening can execute synchronous session SQL

**Source:** [AdoNetRelationalConnection.cs](../src/pengdows.efcore.adonet/Storage/AdoNetRelationalConnection.cs)

The connection-state handler runs dialect session setup through `ExecuteNonQuery()`. A connection opened asynchronously can trigger that handler, causing blocking provider I/O on an async path or failure with drivers that reject synchronous operations.

**Direction:** Test `OpenAsync` with a driver that rejects synchronous execution; arrange async initialization where the library controls the open. Preserve correct behavior for externally opened connections. Also examine whether caching session setup by connection object can preserve a prior dialect's setup when an external connection is reused.

### P1 — No GitHub Actions pipeline visible in the reviewed tree

The reviewed snapshot did not contain `.github/workflows`, and no workflow runs were available in the query. This **does not establish that no external CI exists**.

**Direction:** Pin commit-level build and regression evidence; run core tests without Docker, then integration/specification suites with the required databases.

### P2 — Bulk insert materializes data more than once

**Sources:** [AdoNetBulkExtensions.cs](../src/pengdows.efcore.adonet/AdoNetBulkExtensions.cs), [SqlServerBulkLoader.cs](../src/pengdows.efcore.adonet.sqlserver/SqlServerBulkLoader.cs), [MySqlBulkLoader.cs](../src/pengdows.efcore.adonet.mysql/MySqlBulkLoader.cs)

Bulk paths materialize entity/row lists and some native loaders build a complete `DataTable`. This increases transient memory use with input size. It is **not** proof of unacceptable throughput.

**Direction:** Benchmark large inputs, allocation rates, and memory peaks before choosing bounded batches or streaming readers.

### P2 — Long-lived data-source cache can grow with connection-string cardinality

**Source:** [AdoNetDataSources.cs](../src/pengdows.efcore.adonet/Storage/AdoNetDataSources.cs)

A concurrent cache retains factory/connection-string keyed `DbDataSource` instances until explicit removal or service-provider disposal. For a stable set of databases this is expected; dynamic tenant/database churn could cause growth.

**Direction:** Document cache lifetime and measure behavior under dynamic connection strings. Do not add automatic eviction without first resolving ownership of active data sources.

### P2 — JSON mapping has repeated parsing/reflection work

**Sources:** [AdoNetStringTypeMapping.cs](../src/pengdows.efcore.adonet/Storage/AdoNetStringTypeMapping.cs), [AdoNetJsonTypeMapping.cs](../src/pengdows.efcore.adonet/Storage/AdoNetJsonTypeMapping.cs)

The JSON binding path may parse strings and inspect reflected provider members. Measure actual hot-path cost before changing it.

## Strengths worth preserving

- Real `DbProviderFactory`, `DbConnection`, and `DbDataSource` substitutability; FakeDb enables useful behavioral testing without a live server.
- A single query/update and resource-management core rather than six separate implementations.
- Dialect contracts for grammar, type mappings, and capability differences, with EF-specific service overrides when needed.
- Cross-provider conformance and imported EF specification tests, including server-executed type/translation behavior.
- Public API baselines and a documented review policy.
- Interaction with the larger conformance feedback loop: a driver defect identified here can become an ADO.NET regression test, an upstream PR, and a narrower or removable downstream workaround.

## Recommended sequence

1. Prove/fix migration lock ownership and lifetime under concurrent migrators.
2. Scope MySQL lock names and verify session pinning during migration.
3. Remove or consciously justify provider-specific knowledge in the common core.
4. Exercise async session setup with providers rejecting synchronous I/O.
5. Establish commit-specific CI evidence.
6. Keep a versioned, provider-specific feature/capability matrix.
7. Benchmark bulk and dynamic data-source lifetimes before optimizing.

## Release interpretation

**Suitable for continued development and integration testing.** A limited preview is reasonable with documented scope and supported configurations. This source review does **not** independently certify general production readiness or complete EF Core provider parity. The recent repository-reported MySQL specification result (29,903 passed / 0 failed / 437 skipped) is useful evidence, but not a test run performed as part of this review.

The primary success criterion is whether another database or feature can be added without making the shared core know about that vendor or duplicating EF infrastructure. That is precisely why the six-provider concurrent-development strategy matters.
