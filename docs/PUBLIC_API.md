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
repository and native loader are `internal`; they are reached through `IAdoNetDialect`. `MySqlDialect.NoBackslashEscapes`
and `LegacySqliteDialect` are dialect variants for server or engine settings.

## Testing (`pengdows.efcore.adonet.Testing`)

A test kit: the conformance suites a plugin's test project subclasses, the fixtures it derives from, and the models and
migrations the suites run. All of it is public so a plugin outside this repository can run the same suites.
`ProviderFixture.Factory` returns a `DbProviderFactory` because a fixture is where a plugin names its provider.
