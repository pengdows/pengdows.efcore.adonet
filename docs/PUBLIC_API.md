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
| `SqlTemplate.Condition` | Plugins: a template that is a search condition, not a bool value (SQL Server's `FREETEXT`/`CONTAINS`), written bare in a `WHERE` and as a `CASE` in a projection where bools are bit values. |
| `AdoNetMigrationsSqlGenerator`, `AdoNetHistoryRepository` | Plugins: subclassed in `ConfigureServices` for what a database's DDL or history table does differently. |
| `IAdoNetBulkLoader`, `AdoNetInsertBulkLoader`, `BulkLoad`, `BulkColumn` | Plugins: a native bulk loader replaces or extends the core's INSERT loader. |
| `AdoNetAnnotationNames` | Plugins and migrations: the annotations the core reads (identity columns). |
| `IdentityInsertSyntax`, `ISqlDialect.IdentityInsert` | Plugins: a database that refuses an explicit value for an identity column unless told (SQL Server's `IDENTITY_INSERT`); the core's update and seed-data SQL wrap such inserts. |
| `IDatabaseCapabilities.SupportsMultipleActiveReadersFor`, `ReadsBinaryAsStream` | Plugins: driver facts that depend on the connection string (SqlClient's `MultipleActiveResultSets`), or a driver that returns binary only as a stream (DuckDB.NET 1.5.6.1). Default implementations, so existing dialects are unchanged. |
| `ITypeMappingDialect.ParameterDbType` | Plugins: the `DbType` a parameter binds as, for drivers that convert EF's standard one wrongly where a statement doesn't fix the type (DuckDB.NET 1.5.6.1). |
| `JsonSyntax.ScalarValue`, `JsonSyntax.NullValue` | Plugins: how the JSON set function takes a scalar and JSON's null, so the core writes no database's JSON functions. |
| `SpatialBinary.Gaia` | Plugins: a database whose geometry columns hold the GAIA blob (SpatiaLite), which the NetTopologySuite package encodes with the coordinates the column type names. |
| `SpatialParameterBinding.UserDefinedType` | Plugins: a spatial value bound as the database's own geometry type (SQL Server's UDT); named for the mechanism, not the database. |
| `Scaffolding.SchemaReader` (base of `InformationSchemaReader`) | Plugins: a database whose catalog isn't `INFORMATION_SCHEMA` (SQLite) reads its schema without inheriting ISO's queries. |
| `ISqlDialect.GeneratesCompositeKeyValues` | Plugins: whether the database generates a column of a key of several columns (an owned collection's own key); SQLite generates only its rowid (efcore#26708). Its suites configure such models as Microsoft's SQLite tests do. `AnsiSqlDialect` says true. |
| `FunctionSyntax.TrimCharacters`, `TrimStartCharacters`, `TrimEndCharacters` | Plugins: `String.Trim(char[])` of several characters, a trim of a set; null where the database's trim takes one character (ISO) or one string (MySQL). |
| `FunctionSyntax.PadLeft`, `PadRight` | Plugins: `String.PadLeft`/`PadRight`, keeping a longer string whole as .NET does (SQL's LPAD cuts it); null where the database has no padding (ISO). |
| `ISqlDialect.KeysWithoutReturning`, `AdoNetAnnotationNames.UseReturningClause` | Plugins: a table that can't take the dialect's RETURNING (triggers, virtual tables) reads keys back with a SELECT after the statement, as Microsoft's SQLite `UseSqlReturningClause(false)`; the annotation is what a plugin's table option sets. `AnsiSqlDialect` says null. |
| `Storage.IArrayTypeMapping` | Plugins: a primitive collection stored as the database's own array or list type (DuckDB's LIST), not JSON; its mapping gives the elements table and, where the database has them, its contains, length and index functions. |
| `FunctionSyntax.DecimalDivide`, `FractionalModulo` | Plugins: a database that keeps whole decimals as integers (SQLite's NUMERIC affinity) divides two of them as integers, and one whose `%` truncates its operands to integers loses a fraction; null keeps the operators. |
| `ITypeMappingDialect.RowVersionStoreType` | Plugins: the store type of a row version (`[Timestamp]`) the database sets on every insert and update: SQL Server's `rowversion`, MySQL's `TIMESTAMP(6)` with `ON UPDATE`, PostgreSQL's `xid` (its `xmin` system column). Default implementation: none. |
| `SqlServerDialect.ForCompatibilityLevel`, `CompatibilityLevel` | Applications: a database at an older compatibility level gets only the T-SQL it has (Microsoft's `UseCompatibilityLevel`); one dialect instance per level. |
| `SqlServerDbFunctionsExtensions` (`FreeText`, `Contains`, `DateDiffWeek`, `DateDiffNanosecond`, `DateDiffHour` … `DateDiffMicrosecond` over `TimeSpan`/`TimeOnly`, `DateFromParts`, `DateTimeFromParts`, `DateTime2FromParts`, `DateTimeOffsetFromParts`, `SmallDateTimeFromParts`, `TimeFromParts`, `IsDate`, `IsNumeric`, `DataLength`, `AtTimeZone`, `PatIndex`, `StandardDeviationSample`, `StandardDeviationPopulation`, `VarianceSample`, `VariancePopulation`) | Applications moving from Microsoft's SQL Server provider: its `EF.Functions` of the same names and signatures, which the core has no shared form of. The *FROMPARTS functions need compatibility level 110, `AtTimeZone` 130. |
| `SqlServerIndexBuilderExtensions` (`IsClustered`, `IncludeProperties`, `HasFillFactor`, `IsCreatedOnline`, `SortInTempDb`, `UseDataCompression`), `DataCompressionType`, `SqlServerKeyBuilderExtensions` (`IsClustered`, `HasFillFactor`), `SqlServerPropertyBuilderExtensions` (`UseIdentityColumn(seed, increment)`, `UseHiLo`, `UseSequence`, `IsSparse`), `SqlServerModelBuilderExtensions` (`UseIdentityColumns`, `UseHiLo`, `UseKeySequences`), `SqlServerValueGenerationStrategy`, `SqlServerEntityTypeBuilderExtensions` (`IsMemoryOptimized`) | Applications moving from Microsoft's SQL Server provider: its model options of the same names, written into DDL by the plugin's migrations under Microsoft's annotation names, so its models and scaffolded migrations keep their meaning. |
| `SqlServerTableBuilderExtensions.IsTemporal`, `TemporalTableBuilder`, `TemporalTableBuilder<TEntity>`, `TemporalPeriodPropertyBuilder`, `SqlServerDbSetExtensions` (`TemporalAll`, `TemporalAsOf`, `TemporalFromTo`, `TemporalBetween`, `TemporalContainedIn`) | Applications moving from Microsoft's SQL Server provider: system-versioned temporal tables in the model and migrations (create and drop), and `FOR SYSTEM_TIME` queries, read through `FromSql` over the table's columns so no query root is needed in the core. Navigations read current rows; range queries are not tracked. |
| `DmlSyntax.Iso` | Plugins: ISO SQL:2003 DML, which has no `UPDATE ... FROM`: an update through a join is written with correlated `EXISTS` and scalar subqueries. |
| `ISqlDialect.SupportsStoredProcedures` | Plugins: a database without stored procedures (SQLite, DuckDB, FlatFile) has a model mapped to them refused when the context starts, as Microsoft's SQLite provider does. `AnsiSqlDialect` says true. |
| `ISqlDialect.SupportsOuterReferencesInDerivedTables` | Plugins: a database whose derived tables can't read the outer row (MariaDB, MySQL before 8.0.14) has queries EF translates to one refused, as Pomelo does on such servers. `AnsiSqlDialect` says true. |
| `ISqlDialect.SupportsWindowFunctions` | Plugins: a database without ISO window functions (an older server) has queries EF translates to `ROW_NUMBER() OVER` refused, as `SupportsOuterReferencesInDerivedTables` refuses its own. Default implementation and `AnsiSqlDialect`: true. |
| `JsonSyntax.Equality` | Plugins: a database whose JSON is text underneath (MariaDB) compares a JSON column with JSON text through its own function, not `=`. |
| `JsonSyntax.DateTimeOffsetFromText` | Plugins: a database whose conversion of JSON's date-time text ignores the offset (MariaDB) reads a DateTimeOffset through its own template, out of a document and out of a JSON array table. |
| `AdoNetDesignTimeServices(dialect, driverFactory)`, `CreateDatabaseModelFactory`; `AdoNetDatabaseModelFactory(dialect, driverFactory)`, `DialectFor` | Plugins: EF's tools scaffold from a connection string (`dotnet ef dbcontext scaffold`), opened through the driver the plugin names; a plugin may choose the dialect from the open connection (MySQL tells MariaDB and versions apart), as a design-time context has no options to choose one. |
| `AdoNetDbFunctionsExtensions.DateDiffYear` … `DateDiffMicrosecond`, `DateDiffQuarter`, `DateDiffWeek`, `DateDiffTick`, `DateDiffNanosecond`, `FunctionSyntax.DateDiff`, `DateDiffUnit` | Applications: date and time differences in whole units, as Microsoft's SQL Server and Pomelo's MySQL `EF.Functions.DateDiff*`; plugins give each unit's template, null where the database has none. |
| `PostgreSqlDbFunctionsExtensions.ILike` | Applications: PostgreSQL's case-insensitive `ILIKE`, with or without an escape character, as Npgsql's `EF.Functions.ILike`. |
| `SqliteDbFunctionsExtensions` (`Glob`, `Hex`, `Unhex`, `Substr`) | Applications: SQLite's GLOB, HEX, UNHEX and SUBSTR over a blob, as Microsoft's SQLite `EF.Functions` of the same names. |
| `SqliteModelOptionsExtensions` (`UseSqlReturningClause` on tables, split tables and owned tables, `IsSqlReturningClauseUsed`, `UseAutoincrement`, `HasSrid`, `GetSrid`, `IsSqlite`) | Applications: Microsoft's SQLite names. `HasSrid` gives a SpatiaLite geometry column the SRID its `AddGeometryColumn` declares. A table with triggers or a virtual table refuses RETURNING; `UseAutoincrement` is the core's `UseIdentityColumn`, kept under Microsoft's name for code moving from its provider; `IsSqlite` as `DatabaseFacade.IsSqlite`. |
| `DuckDbModelOptionsExtensions.UseAutoIncrement`, `UseList` | Applications: the DuckDB EF provider's name for a sequence-generated integer column (the core's `UseIdentityColumn`); `UseList` stores a `List<T>` as DuckDB's LIST (`INTEGER[]`) with list functions, where a collection is otherwise JSON. |
| `MySqlFullTextExtensions` (`IsFullText`, `Match`, `IsMatch`), `MySqlMatchSearchMode` | Applications: MySQL `FULLTEXT` indexes and `MATCH ... AGAINST` in its three search modes, as Pomelo's. |
| `MySqlModelOptionsExtensions` (`HasCharSet`, `HasPrefixLength`, `IsSpatial`) | Applications: a column's, table's or database's character set, index prefix lengths and `SPATIAL` indexes in migrations, as Pomelo's. |
| `MySqlJsonDbFunctionsExtensions` (`AsJson`, `JsonType`, `JsonQuote`, `JsonUnquote`, `JsonExtract`, `JsonOverlaps`, `JsonContains`, `JsonContainsPath`, `JsonContainsPathAny`, `JsonContainsPathAll`, `JsonSearchAny`) | Applications: MySQL's JSON functions in queries, as Pomelo's `MySqlJsonDbFunctionsExtensions`; the same on MariaDB, but for `AsJson`, which makes text JSON through the dialect (`JSON_COMPACT`: MariaDB has no JSON type to cast to). |
| `MySqlDbFunctionsExtensions` (`ConvertTimeZone`, `Like<T>`, `Hex<T>`, `Unhex`, `Degrees`, `Radians`), `MySqlFullTextExtensions.Match`/`IsMatch` over `string[]` | Applications: the rest of Pomelo's `MySqlDbFunctionsExtensions` (CONVERT_TZ, LIKE over any value, HEX, UNHEX, DEGREES, RADIANS, MATCH over several columns), the same on MariaDB. |
| `MySqlModelOptionsExtensions` (`HasTableOption`, entity `UseCollation`, `UseGuidCollation`, `HasSpatialReferenceSystem`, key `HasPrefixLength`, complex-property `HasCharSet`, `UseMySqlIdentityColumn`, `IsMySql`), `IsFullText(parser)` | Applications: the rest of Pomelo's model options in migrations (table options such as `ENGINE`, a table's collation, a collation for GUID columns, a geometry column's SRID (MariaDB's `REF_SYSTEM_ID`), a key's prefix lengths, a FULLTEXT parser), under Pomelo's annotation names so its snapshots read the same; `IsMySql` for code that branches on the database. |
| `MySqlOracleAliases` (`UseMySQL`, `AddMySQLServer`, `AddEntityFrameworkMySQL`, `EnableRetryOnFailure(int, TimeSpan, ICollection<int>)`, `ForMySQLHasCharset`, `ForMySQLHasCollation`, `ForMySQLHasDefaultValue`, `ForMySQLHasDefaultValueSql`, `UseMySQLAutoIncrementColumn`), `MySQLCharsetAttribute`, `MySQLCollationAttribute`, `MySQLMatchSearchMode` (and `Match` taking it), `MySQLValueGenerationStrategy` | Applications moving from Oracle's MySql.EntityFrameworkCore: its entry points, fluent methods, attributes and enums under its names (learned from its public signatures and dev.mysql.com, not its code), each forwarding to this provider's own; only the `using`s change. |
| `MySqlMetadataExtensions` (`GetCharSet`/`SetCharSet`, `GetCollation`/`SetCollation`, `GetTableOptions`/`SetTableOptions`, `IsFullText`/`SetIsFullText`, `FullTextParser`/`SetFullTextParser`, `IsSpatial`/`SetIsSpatial`, `PrefixLength`/`SetPrefixLength`, `GetValueGenerationStrategy`/`SetValueGenerationStrategy`, `IsCompatible*Column`, the `Get*ConfigurationSource` methods, and the convention builder's `HasCharSet`/`CanSetCharSet`) | Conventions and code working on the model: MySQL's options read and set on the metadata, under the names Oracle's (and Pomelo's) provider gives them. |
| `MySqlMigrationBuilderExtensions` (`IsMySql`, `DropPrimaryKey` and `DropUniqueConstraint` with `recreateForeignKeys`), `MySqlDropPrimaryKeyAndRecreateForeignKeysOperation`, `MySqlDropUniqueConstraintAndRecreateForeignKeysOperation`; `AutoIncrementColumns`; obsolete `HasCollation`, `ForMySqlIsFullText`, `ForMySqlIsSpatial` | Migrations written as Pomelo's: branching on MySQL in a migration (its active provider is this plugin's, for every MySqlDialect), dropping a key whose index a foreign key uses, and Pomelo's model-level and obsolete names (kept obsolete, as Pomelo's are). `UseMySqlIdentityColumn` on a date and time is `DEFAULT CURRENT_TIMESTAMP(n)`, as Pomelo's. |
| `pengdows.efcore.adonet.mysql.NetTopologySuite`: `UseMySqlNetTopologySuite`, `MySqlSpatialDbFunctionsExtensions` (`SpatialDistancePlanar`, `SpatialDistanceSphere`), `SpatialDistanceAlgorithm` | Applications: Pomelo's MySQL NetTopologySuite functions, in their own package so the MySQL plugin takes no NetTopologySuite dependency. `UseMySqlNetTopologySuite` turns on the shared `UseNetTopologySuite` too; Pomelo's name for it is already the shared package's. |
| `MySqlDialect.ForServerVersion`, `MySqlDialect.ServerVersion` | Applications on an older MySQL or MariaDB: a dialect that turns off what that server lacks, as Pomelo's `MySqlServerVersion`/`MariaDbServerVersion` do; `FromServerVersion` reads the version from the handshake. A current server gets `Instance` or `MariaDb`. |
| `MySqlCommonJsonChangeTrackingOptions`; `pengdows.efcore.adonet.mysql.Json.Microsoft` (`UseMicrosoftJson`, `UseJsonChangeTrackingOptions`); `pengdows.efcore.adonet.mysql.Json.Newtonsoft` (`UseNewtonsoftJson`, `UseJsonChangeTrackingOptions`) | Applications: Pomelo's two JSON packages, POCOs and System.Text.Json's or Newtonsoft.Json's document types on json columns, read inside in queries and change-tracked as the options say; each in its own package, so the MySQL plugin takes no serializer dependency. |
| `MySqlDesignTimeServices.ConfigureDesignTimeServices` (override) | EF's tools: registers the MySQL annotation code generator (internal), so scaffolded code and migration snapshots write MySQL's annotations as their fluent methods, as Pomelo's do. |
| `DelegationModes`; `HasCharSet` and `UseCollation` on a model or entity type with `DelegationModes?` or `explicitlyDelegateToChildren`; `UseMySqlComputedColumn` | Applications: Pomelo's control of where a character set or collation is written (the database by ALTER DATABASE, each table, each text column; no delegation is all three for a character set, as in Pomelo, while EF's own model `UseCollation(collation)` stays the database's only), and its server-set date and time (`DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP`). |
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

## Plugin extension points

A dialect plugin in another assembly compiles against the packed core with no `InternalsVisibleTo`. The types it derives from or
calls are public: `AdoNetProviderOptions` (its `UseAdoNetXxx` entry point), `DelegatingDataReader` (the reader base a provider
derives from; its `Inner` is the wrapped `DbDataReader`, which a subclass reads, as `DbDataReader` itself exposes), the annotation,
convention, migrations annotation and value generator selector bases, `AdoNetQuerySqlGenerator`, `IAdoNetSingletonOptions` and
`DriverMembers`. Protected members of these bases are public API.

## Documentation debt

The core generates its XML documentation file. The dialect contract (`IAdoNetDialect`, `ISqlDialect`), the syntax parts (`Syntax`,
`FunctionSyntax`, `SpatialSyntax`, `KeyRetrieval`, `DatabaseLifecycle`) and the plugin extension points are documented. About 700
other public members (mainly the literal type mappings, the reader and the scaffolding types) have no summary yet; their CS1591 warnings
are suppressed in the core project until they are written, rather than failing the build.

