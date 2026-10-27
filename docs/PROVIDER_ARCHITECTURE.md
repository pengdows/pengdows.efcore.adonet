# Provider architecture

The provider packages share one EF Core implementation. A database package supplies
one `IAdoNetDialect` composition root; it does not replace the query, update, or
ADO.NET lifetime pipeline.

## Boundaries

| Boundary | Owns | Must not own |
|---|---|---|
| `IAdoNetDialect` | Composition of SQL, type mapping, driver capabilities, optional EF services, and schema reading | EF query semantics duplicated per database |
| `ISqlDialect` | Identifier/parameter/literal spelling, paging, booleans, JSON, spatial, DML, key retrieval, and SQL feature shape | Connection or transaction lifetime |
| `ITypeMappingDialect` | Store type names, CLR/store conversions, literal and cast formatting | Query execution or model policy |
| `IDatabaseCapabilities` | Driver/runtime facts: batching, parameter limits, readers, ambient transactions, transient errors | SQL text decisions |
| Core EF adapter | Shared translation, null semantics, batching, lifecycle, migrations defaults, and ADO.NET contract | Database-specific SQL or driver APIs |
| Provider package | A real database boundary: driver registration, migrations/scaffolding overrides, and dialect exceptions | Copying a complete EF provider implementation |
| Flatfile engine | ISO parser, binder, executor, PSM, triggers, persistence, and spill execution | EF-specific translation policy |

The split between `ISqlDialect` and `IDatabaseCapabilities` is intentional. SQL
syntax is a property of the database language; enlistment, reader multiplexing,
parameter ceilings, and retry behavior are properties of the driver/runtime.

## Current provider matrix

| Provider | SQL/key boundary | Driver/runtime boundary | Provider-only services |
|---|---|---|---|
| MySQL | MySQL syntax, JSON, `LIMIT`, `LAST_INSERT_ID()` | MySqlConnector or MySql.Data, ambient transactions, affected-row semantics | MySQL migrations, schema reader, optional driver setup |
| PostgreSQL | PostgreSQL syntax, JSON, `LIMIT`, `RETURNING`/row values | Npgsql, ambient transactions, savepoints | PostgreSQL migrations and schema reader |
| Flatfile | ISO SQL, `:name`, sequences, delta-table/generated-key strategy | File locks, single writer, multiple readers, no ambient enlistment | Directory lifecycle and flatfile schema reader |

Every provider-specific difference must be represented in one of these existing
boundaries. A new boolean belongs in a capability object only when it describes a
real driver/database fact; a new SQL template belongs in `ISqlDialect`; a new EF
service belongs in `ConfigureServices`.

## Migration rules

1. Add or strengthen a shared contract test before changing a provider.
2. If two providers need the same behavior, implement it in the core first and
   express the difference through an existing dialect capability.
3. Keep provider adapters thin and test them against the shared conformance
   suites.
4. Do not add vendor SQL to the ISO flatfile path. Use an ISO construct or record
   an explicit capability boundary.
5. Do not introduce a second capability registry. Extend the existing dialect
   contract only when the feature cannot be represented by its current parts.

This document is an architectural contract, not a claim that every provider has
identical feature coverage. The provider-specific capability matrix and current
test results remain the evidence for coverage.
