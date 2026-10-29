# fakeDb EF Core coverage

The fakeDb suites exercise the EF Core command pipeline without a real database:
model creation, LINQ translation, parameter binding, command execution, reader
materialization, change tracking, relationship fix-up, and affected-row handling.

## Core relational behavior

| Area | Tests |
|---|---|
| Query translation and scalar operators | `FakeDbEfCoreConformanceTests`, `FakeDbEfCoreSetAndAggregateTests` |
| Null semantics, joins, set operations, grouping | `FakeDbEfCoreConformanceTests`, `FakeDbEfCoreSetAndAggregateTests` |
| Tracking, no-tracking, identity resolution, `Find` | `FakeDbEfCoreExecutionTests`, `FakeDbEfCoreConformanceTests` |
| Includes, split queries, lazy loading, explicit loading | `FakeDbEfCoreAdvancedQueryTests`, `FakeDbEfCoreConformanceTests` |
| Inheritance and complex/owned types | `FakeDbEfCoreAdvancedQueryTests`, `FakeDbEfCoreConformanceTests` |
| Keyless entities, query filters, shadow properties | `FakeDbEfCoreRelationalFeatureTests` |
| Value converters, composite keys, query tags | `FakeDbEfCoreConformanceTests` |
| Raw SQL, interpolated SQL, scalar SQL, composed SQL | `FakeDbEfCoreCommandSurfaceTests`, `FakeDbEfCoreRelationalFeatureTests` |
| Execute update/delete and row counts | `FakeDbEfCoreCommandSurfaceTests` |
| Generated keys and concurrency | `SaveChangesTests`, `ReturningClauseTests`, `ProcedureCallTests` |
| Value generation, identity conventions, converters, and type mappings | `IdentityConventionTests`, `ConfigurationTests`, `ArrayTypeMappingTests`, `ParameterTests` |
| Transactions, ambient transactions, savepoints | `LifecycleTests`, `HistoryRepositoryTests` |
| Command and transaction interception | `FakeDbEfCoreRelationalFeatureTests`, `LifecycleTests`, `HistoryRepositoryTests` |
| SaveChanges interception and failure-state preservation | `FakeDbEfCoreSaveChangesPipelineTests` |
| Async queries and streaming | `FakeDbEfCoreAdvancedQueryTests`, `FakeDbEfCoreRelationalFeatureTests` |
| Database lifecycle (`Create`, `Delete`, existence, admin/data-source connections) | `DatabaseCreatorTests`, `LifecycleTests` |
| Migrations model differ, DDL generation, annotations, and history | `MigrationsSqlTests`, `HistoryRepositoryTests`, provider migration suites |
| Scaffolding/database-model metadata | `SchemaReaderTests`, provider scaffolding suites |
| Provider-owned EF annotations and extension APIs | `*UpstreamGapFakeDbTests`, provider SQL/integration suites |

## Provider execution behavior

- MySQL: `MySqlFunctionExecutionTests`, `MySqlUpstreamGapFakeDbTests`
- PostgreSQL: `PostgreSqlFakeDbExecutionTests`, `PostgreSqlUpstreamGapFakeDbTests`
- SQL Server: `SqlServerFakeDbExecutionTests`, `SqlServerUpstreamGapFakeDbTests`
- SQLite: `SqliteFunctionExecutionTests`, `SqliteSpatialExecutionTests`, `SqliteUpstreamGapFakeDbTests`
- DuckDB: `DuckDbExecutionTests`, `DuckDbListTests`, `DuckDbUpstreamGapFakeDbTests`
- Flatfile: `FlatFileFakeDbExecutionTests`

The `*UpstreamGapFakeDbTests` suites are retained as parity probes for
provider-specific behavior. Against fakeDb rc5, all current probes pass; they
must remain in the suite so future provider or fakeDb changes cannot silently
regress the covered upstream behaviors.

## Flatfile ISO conformance progress

The generated EF Core specification slice is also wired to the real flatfile
provider. The first targeted groups now establish a useful baseline:

- built-in data types: 28/30 passing after the interval seed-literal boundary;
- arithmetic operator translations: 5/5 passing;
- datetime translations: 14/19 passing after enabling the ISO current-date,
  current-timestamp, date, and time forms;
- ad-hoc complex-type queries: 13/14 passing.

The remaining failures are intentionally visible conformance work, including
flatfile engine type coercion, unsupported temporal operations, custom
non-ISO store-type aliases, and migration-lock/idempotent-script semantics.
