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
| SQL Server | T-SQL, `[name]`, `TOP`/`OFFSET FETCH`, `SCOPE_IDENTITY()`, `APPLY`, `IDENTITY_INSERT` for explicit keys | SqlClient, ambient transactions, multiple readers only with `MultipleActiveResultSets` (per connection string), transient error numbers | Migrations (`sp_rename`, computed `AS`), conventions (filtered unique indexes, one cascade path), query string, bulk copy |
| SQLite | `LIMIT`, `RETURNING` (or `last_insert_rowid()` before 3.35), JSON as text | Microsoft.Data.Sqlite, multiple readers | Migrations (constraints folded into `CREATE TABLE`, table rebuilds), `sqlite_master`/PRAGMA schema reader |
| DuckDB | ISO with sequences, no cascades | DuckDB.NET 1.5.6.1: binary read as a stream, parameter DbTypes it converts correctly | Migrations (sequences; foreign keys only in `CREATE TABLE`) |

Every provider-specific difference must be represented in one of these existing
boundaries. A new boolean belongs in a capability object only when it describes a
real driver/database fact; a new SQL template belongs in `ISqlDialect`; a new EF
service belongs in `ConfigureServices`.

## Capability matrix: settings the core reads from a dialect

The executable record is `DialectCapabilityMatrixTests` (a change to a dialect that moves a cell fails there until this table
and the reason beside it are updated on purpose). Each difference is classified by where it comes from.

| Provider | `NULLS FIRST/LAST` (`ISqlDialect.SupportsNullOrderingClauses`) | `DateTimeOffset` only at offset zero (`WritesDateTimeOffsetAsUtc`) | JSON document is plain text (`StoresDocumentAsText`) | JSON is a binary document (`UsesBinaryDocument`) |
|---|---|---|---|---|
| SQL Server | no (engine) | no | yes (engine: `nvarchar(max)`) | no |
| PostgreSQL | yes | yes (driver: Npgsql) | no | yes (engine: `jsonb`) |
| MySQL | no (engine) | no | no | no |
| SQLite | yes | no | no | no |
| DuckDB | yes | no | no | no |
| FlatFile | yes | no | no | no |

### Null ordering (`ReverseNullOrdering`)

EF's specification tests, .NET and SQL Server sort nulls first. PostgreSQL, DuckDB and the FlatFile engine sort them last by
default; SQLite, MySQL and SQL Server sort them first. The option writes `NULLS FIRST` (ascending) and `NULLS LAST`
(descending) on every ORDER BY so the first group matches the second. It is internal, as in Npgsql and the DuckDB providers it
follows, and a dialect whose engine takes no such clause (`SupportsNullOrderingClauses` false) refuses it when the context
starts. A null-first order also needs indexes created with the matching order to be used, which is why it is not a public
setting; the test hosts for PostgreSQL, DuckDB and FlatFile turn it on.

### `DateTimeOffset` on PostgreSQL

Npgsql refuses to write a `timestamptz` value whose offset is not zero, where every other driver stores the instant.
PostgreSQL itself stores only the instant. The PostgreSQL dialect converts a non-zero offset to UTC (the same instant) before
it binds, so EF's contract, an instant that round-trips, holds as on the other providers; the offset is not kept and reads
back as zero. Npgsql's own provider chooses to throw instead and overrides the corresponding EF tests; this provider follows
EF's contract first (behaviour-precedence rule). DB impact: PostgreSQL only; a write that used to fail now succeeds and
loses its offset, never its instant.

### JSON column compared with text

A JSON column compared with a constant or parameter is written as `column = <text converted to JSON>` (the dialect's
`FromText`), because a bare string is a JSON string scalar to MySQL and equals no document. A column that stores its document
as plain text (SQL Server) compares as text and takes no conversion. DB impact: MySQL (`CAST(x AS JSON)`) and PostgreSQL
(`CAST(x AS jsonb)`); covered by `JsonEqualityTests` and the specification suites.

## Where a difference goes: cases from the SQL Server and SQLite providers

| Difference | Kind | Boundary |
|---|---|---|
| SQL Server refuses an explicit value for an `IDENTITY` column unless `IDENTITY_INSERT` is on | SQL | `ISqlDialect.IdentityInsert`; the core's update and seed-data SQL wrap such an insert, one table at a time |
| SqlClient holds several readers only with `MultipleActiveResultSets` | Driver, per connection | `IDatabaseCapabilities.SupportsMultipleActiveReadersFor(connectionString)`; the compiled-query cache is keyed by the answer |
| DuckDB.NET returns a BLOB only as a stream | Driver | `IDatabaseCapabilities.ReadsBinaryAsStream`; the core wraps readers and copies each value while its row is current |
| DuckDB.NET converts some untyped parameters wrongly by DbType | Driver | `ITypeMappingDialect.ParameterDbType` |
| Npgsql misreads an interval with negative months | Driver | A PostgreSQL reader interceptor (`ConfigureServices`): a `TimeSpan` read from an interval column goes through `NpgsqlInterval`; in the reader, not the mapping, because a `TimeSpan` may also come from a `time` column and EF buffers through the mapping's reader call |
| SQLite has no lateral join | Engine SQL | `ISqlDialect.LateralJoins` null; the core refuses such a query at translation; a capability skip in the suites |
| SQLite reuses the highest rowid after a delete unless the key is `AUTOINCREMENT` | Engine DDL | SQLite's migrations generator writes a generated key of one column inline as `PRIMARY KEY AUTOINCREMENT`, as Microsoft's provider does; EF's fix-up confused a new row with the deleted one it replaced |
| SQLite can't generate a column of a key of several columns | Engine | `ISqlDialect.GeneratesCompositeKeyValues`; the suites configure owned collections' keys as Microsoft's SQLite tests do (efcore#26708) |
| MySql.Data casts its own value types (a JSON integer's `BIGINT UNSIGNED` to a byte) and has no `DateOnly`/`TimeOnly` | Driver | A MySQL reader interceptor wraps MySql.Data's reader only (named, not referenced: GPL) and converts as MySqlConnector does |
| A database's `CURRENT_TIMESTAMP` is the session's zone | SQL | `FunctionSyntax.UtcNow` names UTC for every dialect: ISO's `CAST(CURRENT_TIMESTAMP AT TIME ZONE INTERVAL '00:00' HOUR TO MINUTE AS TIMESTAMP)` by default, `NOW() AT TIME ZONE 'UTC'` (PostgreSQL), `CURRENT_TIMESTAMP AT TIME ZONE 'UTC'` (DuckDB), `SYSUTCDATETIME()`, `UTC_TIMESTAMP(6)` |
| ISO has no `UPDATE ... FROM` | SQL | `DmlSyntax.Iso` (FlatFile): correlated subqueries |
| Each server sets a row version its own way | Type mapping and DDL | `ITypeMappingDialect.RowVersionStoreType`: SQL Server `rowversion`; MySQL `TIMESTAMP(6)` with `DEFAULT`/`ON UPDATE CURRENT_TIMESTAMP(6)` in its migrations; PostgreSQL `xid`, read and bound as such by its plugin, the column named `xmin` by its convention and never declared by its migrations |
| A SQL Server database's compatibility level limits its T-SQL | SQL | `SqlServerDialect.ForCompatibilityLevel`: trims and JSON functions gated at 130, 140 and 160 |
| PostgreSQL's dialect needs PostgreSQL 13 (jsonpath, `DROP DATABASE ... WITH (FORCE)`), as Npgsql 10 does | SQL | No version option: nothing it translates differs between supported versions |
| FlatFile reads generated keys back the ISO way | SQL | `KeyRetrieval.DeltaTable` (`SELECT ... FROM FINAL TABLE (INSERT ...)`) over ISO identity columns |
| SQLite, DuckDB and FlatFile have no stored procedures | Engine | `ISqlDialect.SupportsStoredProcedures`; the core's model validator refuses a model mapped to them; a capability skip in the suites |
| SQLite has no ALTER for constraints or columns | Engine DDL | SQLite's migrations generator: fold into a same-migration `CREATE TABLE`, otherwise rebuild from the target model |
| DuckDB can't declare a foreign key after `CREATE TABLE` | Engine DDL | DuckDB's migrations generator refuses by name; a capability skip in the suites |
| SQL Server refuses two cascade paths to a table | Engine DDL | SQL Server's convention set: the second path cascades in EF (`ClientCascade`) |
| MariaDB speaks MySQL but has no `LATERAL`, no `ROW` in a table value constructor, no JSON type to `CAST` to, and other spatial and regex functions | SQL | `MySqlDialect.MariaDb`, chosen with `UseDialect`: lateral joins refused, `VALUES (...)`, `JSON_COMPACT`, `ST_NumInteriorRings`, PCRE `REGEXP` with `(?-i)`; its sessions take the database's collation |
| MariaDB's derived tables can't read the outer row (MySQL's can since 8.0.14) | Engine SQL | `ISqlDialect.SupportsOuterReferencesInDerivedTables`; the core refuses such a query at translation, `VALUES` included; a capability skip in the suites |
| MariaDB's JSON is `LONGTEXT`, so `=` compares spellings | SQL | `JsonSyntax.Equality`: `JSON_EQUALS` for a JSON column compared with JSON text |
| MariaDB reports integer display widths (`BIGINT(20)`) | Catalog | MySQL's schema reader drops them but for `TINYINT(1)` and `ZEROFILL`, as MySQL 8.0.19 does |
| Parallel migrators can deadlock inserting the lock row | Engine | The history repository waits out a refusal the dialect calls transient (`IDatabaseCapabilities.IsTransient`) like a held lock |
| A catalog other than `INFORMATION_SCHEMA` | Engine catalog | `Scaffolding.SchemaReader`; `InformationSchemaReader` is ISO's |
| A driver reports a cancel as its own exception | Every driver | The core's exception detector: a failure after the token is canceled is the cancellation |

A failure seen on three or more databases with the same signature belongs to the core or the shared test hosts, not to a
provider: read the specification ledger by signature across databases before starting provider work.

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
