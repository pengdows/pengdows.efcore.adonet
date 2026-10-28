# Public API

Every public or protected member is declared in its project's `PublicAPI.Unshipped.txt` (nothing has shipped yet, so
`PublicAPI.Shipped.txt` is empty); `Microsoft.CodeAnalysis.PublicApiAnalyzers` fails the build on anything undeclared.
This file records why each public type has a consumer that cannot do without it. Everything else is `internal`: EF's
service implementations (connection, type mapping source, SQL generators, translators, update and transaction
machinery, conventions, type mappings) are reached through EF's interfaces, never by type.

## Core (`pengdows.efcore.adonet`)

| Types | Consumer and reason |
| --- | --- |
| `UseAdoNet` overloads (`AdoNetDbContextOptionsBuilderExtensions`), `AdoNetDbContextOptionsBuilder` | Applications: how a context is configured. |
| `AdoNetOptionsExtension` | EF: the provider's options extension; applications reading their configuration (`Dialect`). Its factory and data source are `internal`: raw ADO.NET objects don't leave the library. |
| `AddEntityFrameworkAdoNet` (`AdoNetServiceCollectionExtensions`) | Applications using `UseInternalServiceProvider`, and EF's specification tests. |
| `AdoNetBulkExtensions` | Applications: `BulkInsert`. |
| `IAdoNetDialect`, `AdoNetDialect`, `AdoNetDialects` | Plugins: the dialect contract, its ISO base, and the registry `UseAdoNet(factory, ...)` resolves a dialect from. |
| `ISqlDialect`, `AnsiSqlDialect`, `ITypeMappingDialect`, `AnsiTypeMappings`, `StoreTypeFacets`, `IDatabaseCapabilities`, `DatabaseCapabilities` | Plugins: the parts of a dialect, with ISO defaults to override. |
| `PagingSyntax`, `BooleanSyntax`, `KeyRetrieval` (and `ReturningKeyRetrieval`, `SelectAfterKeyRetrieval`, `SequenceKeyRetrieval`), `DmlSyntax`, `FunctionSyntax`, `SavepointSyntax`, `IQuerySqlWriter` | Plugins: grammar parts a dialect chooses or extends; `IQuerySqlWriter` is what a `DmlSyntax` writes through. |
| `SqlTemplate` (`Call`, `TryGetTemplate`) | Plugins with translators of their own, writing SQL the way `FunctionSyntax` does. `Write` is `internal`. |
| `AdoNetMigrationsSqlGenerator`, `AdoNetHistoryRepository` | Plugins: subclassed in `ConfigureServices` for what a database's DDL or history table does differently. |
| `IAdoNetBulkLoader`, `AdoNetInsertBulkLoader`, `BulkLoad`, `BulkColumn` | Plugins: a native bulk loader replaces or extends the core's INSERT loader. |
| `AdoNetAnnotationNames` | Plugins and migrations: the annotations the core reads (identity columns). |
| `IdentityInsertSyntax`, `ISqlDialect.IdentityInsert` | Plugins: a database that refuses an explicit value for an identity column unless told (SQL Server's `IDENTITY_INSERT`); the core's update and seed-data SQL wrap such inserts. |
| `IDatabaseCapabilities.SupportsMultipleActiveReadersFor`, `ReadsBinaryAsStream` | Plugins: driver facts that depend on the connection string (SqlClient's `MultipleActiveResultSets`), or a driver that returns binary only as a stream (DuckDB.NET 1.5.6.1). Default implementations, so existing dialects are unchanged. |
| `ITypeMappingDialect.ParameterDbType` | Plugins: the `DbType` a parameter binds as, for drivers that convert EF's standard one wrongly where a statement doesn't fix the type (DuckDB.NET 1.5.6.1). |
| `JsonSyntax.ScalarValue`, `JsonSyntax.NullValue` | Plugins: how the JSON set function takes a scalar and JSON's null, so the core writes no database's JSON functions. |
| `SpatialParameterBinding.UserDefinedType` | Plugins: a spatial value bound as the database's own geometry type (SQL Server's UDT); named for the mechanism, not the database. |
| `Scaffolding.SchemaReader` (base of `InformationSchemaReader`) | Plugins: a database whose catalog isn't `INFORMATION_SCHEMA` (SQLite) reads its schema without inheriting ISO's queries. |
| `ISqlDialect.GeneratesCompositeKeyValues` | Plugins: whether the database generates a column of a key of several columns (an owned collection's own key); SQLite generates only its rowid (efcore#26708). Its suites configure such models as Microsoft's SQLite tests do. `AnsiSqlDialect` says true. |
| `FunctionSyntax.TrimCharacters`, `TrimStartCharacters`, `TrimEndCharacters` | Plugins: `String.Trim(char[])` of several characters, a trim of a set; null where the database's trim takes one character (ISO) or one string (MySQL). |
| `FunctionSyntax.PadLeft`, `PadRight` | Plugins: `String.PadLeft`/`PadRight`, keeping a longer string whole as .NET does (SQL's LPAD cuts it); null where the database has no padding (ISO). |
| `ISqlDialect.KeysWithoutReturning`, `AdoNetAnnotationNames.UseReturningClause` | Plugins: a table that can't take the dialect's RETURNING (triggers, virtual tables) reads keys back with a SELECT after the statement, as Microsoft's SQLite `UseSqlReturningClause(false)`; the annotation is what a plugin's table option sets. `AnsiSqlDialect` says null. |
| `FunctionSyntax.DecimalDivide`, `FractionalModulo` | Plugins: a database that keeps whole decimals as integers (SQLite's NUMERIC affinity) divides two of them as integers, and one whose `%` truncates its operands to integers loses a fraction; null keeps the operators. |
| `ITypeMappingDialect.RowVersionStoreType` | Plugins: the store type of a row version (`[Timestamp]`) the database sets on every insert and update: SQL Server's `rowversion`, MySQL's `TIMESTAMP(6)` with `ON UPDATE`, PostgreSQL's `xid` (its `xmin` system column). Default implementation: none. |
| `SqlServerDialect.ForCompatibilityLevel`, `CompatibilityLevel` | Applications: a database at an older compatibility level gets only the T-SQL it has (Microsoft's `UseCompatibilityLevel`); one dialect instance per level. |
| `DmlSyntax.Iso` | Plugins: ISO SQL:2003 DML, which has no `UPDATE ... FROM`: an update through a join is written with correlated `EXISTS` and scalar subqueries. |
| `ISqlDialect.SupportsStoredProcedures` | Plugins: a database without stored procedures (SQLite, DuckDB, FlatFile) has a model mapped to them refused when the context starts, as Microsoft's SQLite provider does. `AnsiSqlDialect` says true. |
| `ISqlDialect.SupportsOuterReferencesInDerivedTables` | Plugins: a database whose derived tables can't read the outer row (MariaDB, MySQL before 8.0.14) has queries EF translates to one refused, as Pomelo does on such servers. `AnsiSqlDialect` says true. |
| `JsonSyntax.Equality` | Plugins: a database whose JSON is text underneath (MariaDB) compares a JSON column with JSON text through its own function, not `=`. |
| `AdoNetDbFunctionsExtensions.DateDiffYear` … `DateDiffMicrosecond`, `FunctionSyntax.DateDiff`, `DateDiffUnit` | Applications: date and time differences in whole units, as Microsoft's SQL Server and Pomelo's MySQL `EF.Functions.DateDiff*`; plugins give each unit's template, null where the database has none. |
| `PostgreSqlDbFunctionsExtensions.ILike` | Applications: PostgreSQL's case-insensitive `ILIKE`, with or without an escape character, as Npgsql's `EF.Functions.ILike`. |
| `SqliteDbFunctionsExtensions` (`Glob`, `Hex`, `Unhex`, `Substr`) | Applications: SQLite's GLOB, HEX, UNHEX and SUBSTR over a blob, as Microsoft's SQLite `EF.Functions` of the same names. |
| `MySqlFullTextExtensions` (`IsFullText`, `Match`, `IsMatch`), `MySqlMatchSearchMode` | Applications: MySQL `FULLTEXT` indexes and `MATCH ... AGAINST` in its three search modes, as Pomelo's. |
| `MySqlModelOptionsExtensions` (`HasCharSet`, `HasPrefixLength`, `IsSpatial`) | Applications: a column's, table's or database's character set, index prefix lengths and `SPATIAL` indexes in migrations, as Pomelo's. |
| `MySqlDialect.MariaDb` | Applications: MariaDB, which has no `LATERAL` (queries needing one are refused), writes table value constructor rows without `ROW`, and has no JSON type to `CAST` to. Chosen with `UseDialect`, as `SingleStore` is. |

### Raw ADO.NET objects handed out

Guarded by `DialectContractTests.Public_api_hands_out_no_raw_ado_net_objects_but_the_recorded_ones`.

- `BulkLoad.Connection`, `BulkLoad.Transaction`: a native loader's API needs the driver's own connection and transaction
  (`SqlBulkCopy`, Npgsql's binary `COPY`). The core opens the connection and owns the transaction for the length of
  `Load`; the loader must not keep either.
- EF's own `Database.GetDbConnection()` reaches the connection, as for every EF provider.

Accepted as inputs: a `DbProviderFactory`, `DbDataSource` or `DbConnection` in `UseAdoNet`, which is how a context is
configured.

### Rules off

`RS0026`/`RS0027` (optional parameters across overloads) are disabled, as in EF Core's own repository: the `UseAdoNet...`
entry points follow EF's `Use...` overload conventions, and overrides of EF virtuals keep EF's optional parameters.

## Plugins

Each plugin exposes its dialect (`XxxDialect.Instance`, constructed nowhere else) and its `UseAdoNetXxx` entry points:
a connection string, a connection, a data source, or one set later. The dialect's parts, migrations generator, history
repository and native loader are `internal`; they are reached through `IAdoNetDialect`. `MySqlDialect.NoBackslashEscapes`,
`MySqlDialect.SingleStore`, `MySqlDialect.MariaDb` and `LegacySqliteDialect` are dialect variants for server or engine settings.

## Testing (`pengdows.efcore.adonet.Testing`)

A test kit: the conformance suites a plugin's test project subclasses, the fixtures it derives from, and the models and
migrations the suites run. All of it is public so a plugin outside this repository can run the same suites.
`ProviderFixture.Factory` and `AdoNetEntryPoints.Factory` return a `DbProviderFactory` because a fixture is where a plugin
names its provider.
