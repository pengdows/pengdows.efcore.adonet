# pengdows.efcore.adonet

An EF Core 10 relational provider split in two: a **core** that knows only ADO.NET, EF Core and a plugin contract,
and **dialect plugins** that describe and implement what makes each database different.

```csharp
// Switching databases changes two arguments: the ADO.NET factory and the dialect.
options.UseAdoNet(NpgsqlFactory.Instance,          connectionString, PostgreSqlDialect.Instance);
options.UseAdoNet(SqlClientFactory.Instance,       connectionString, SqlServerDialect.Instance);
options.UseAdoNet(MySqlConnectorFactory.Instance,  connectionString, MySqlDialect.Instance);

// Or each plugin's shorthand.
options.UseAdoNetPostgreSql(connectionString);
```

The core never names a database. Adding one must not require changing it; if it does, that's a missing extension
point. A test enforces that the core references no driver and defines no dialect but ANSI.

## Layers

| Layer | Responsibility |
|---|---|
| EF Core | LINQ, mapping, change tracking |
| **pengdows.efcore.adonet** (core) | Connections and `DbDataSource`s, transactions and savepoints, parameter binding through `DbType`, batching, concurrency checks, the query pipeline, and ISO SQL defaults for everything a dialect may override |
| **Dialect plugin** | SQL words and grammar, type names, capabilities; optionally migrations, LINQ translations, bulk loading — and it may use its driver's own APIs |
| ADO.NET driver | The database access contract |

## The plugin contract

```csharp
public interface IAdoNetDialect
{
    string Name { get; }
    ISqlDialect Sql { get; }                    // required: words + grammar parts
    ITypeMappingDialect TypeMappings { get; }   // required: store type names
    IDatabaseCapabilities Capabilities { get; } // required: what the driver accepts
    void ConfigureServices(IServiceCollection services); // optional EF services; these win over the core's
}
```

`AdoNetDialect`, `AnsiSqlDialect` and `AnsiTypeMappings` supply ISO SQL throughout, so a dialect overrides only what
its database does differently. Grammar comes in parts a dialect picks, named for their shape rather than a database:

| Part | Variants |
|---|---|
| `PagingSyntax` | `OffsetFetch`, `TopOrOffsetFetch`, `LimitOffset(noLimitValue)`, any `.WithLiteralValues()` |
| `BooleanSyntax` | `Native`, `BitValues` (values aren't predicates: `x = TRUE` / `CASE WHEN`) |
| `KeyRetrieval` | `Returning`, `SelectAfter(identityFunction, rowsAffectedFunction)`, `Sequence(...)` |
| `DmlSyntax` (`ExecuteUpdate`/`ExecuteDelete`) | `Standard`, `UnaliasedTarget`, `TargetAliasFrom`, `MultiTable` |
| `FunctionSyntax` | SQL templates (`"CHAR_LENGTH({0})"`) for what .NET members translate to; ISO by default, `null` = unsupported; checked when the dialect is configured |
| `SavepointSyntax` | savepoints as SQL where the driver's transactions have none: ISO `SAVEPOINT` by default, SQL Server's `SAVE TRANSACTION`, `null` = none |

Each part is an abstract class: a database with its own form (Firebird's `FIRST`/`SKIP`, Oracle's `ROWNUM`) subclasses it.
String constants go through `ISqlDialect.StringLiteral` (SQL Server prefixes `N`, MySQL doubles backslashes unless the
server has `NO_BACKSLASH_ESCAPES`: `MySqlDialect.NoBackslashEscapes`); other constants through
`ITypeMappingDialect.FormatLiteral`, which must write what a parameter of the same value would store. Identifiers are
quoted where `RequiresQuoting` says (always, by default; PostgreSQL leaves lower-case names bare). A few single
words too: `DefaultValues` (an INSERT with no columns: MySQL's `() VALUES ()`) and `CurrentSchema`. Store types come
from `GetStoreType(clrType, StoreTypeFacets)`: size, precision, scale, fixed length, Unicode.

### Translations

The core maps .NET members to `FunctionSyntax` templates once; a plugin overrides only the spellings its database
uses, e.g. SQL Server's `"DATEPART(year, {0})"` for `EXTRACT(YEAR FROM {0})`:

| .NET | ISO template |
|---|---|
| `a + b` on strings | `{0} \|\| {1}` |
| `ToUpper`, `ToLower`, `Trim`, `TrimStart`, `TrimEnd` | `UPPER`, `LOWER`, `TRIM`, `TRIM(LEADING FROM ...)`, `TRIM(TRAILING FROM ...)` |
| `Length`, `Substring`, `IndexOf`, `Replace` | `CHAR_LENGTH`, `SUBSTRING(... FROM ... FOR ...)`, `POSITION(... IN ...)`, `REPLACE` |
| `StartsWith`, `EndsWith`, `Contains` | constants and parameters: `LIKE` with wildcards escaped (a parameter's pattern is computed at run time, so an index serves it); columns, or anything where LIKE has no `ESCAPE`: compared by position |
| `string.IsNullOrWhiteSpace` | `x IS NULL OR TRIM(x) = ''` |
| `Math.Abs`, `Floor`, `Ceiling`, `Round`, `Pow`, `Sqrt` | `ABS`, `FLOOR`, `CEILING`, `ROUND`, `POWER`, `SQRT` |
| `tuple.Equals(other)` (`Tuple`, `ValueTuple`, `.Create`) | ISO row values `(a, b) = (x, y)`, nullable pairs compared on their own; `ISqlDialect.RowValues = false` (SQL Server) leaves them untranslated |
| `Year` ... `Second` of `DateTime`, `DateTimeOffset`, `DateOnly` | `EXTRACT(YEAR FROM ...)` ... (whole seconds); a `DateTimeOffset` first through `OffsetDateTime`, so parts are those of the value as it reads back |

Anything else a plugin translates registers EF's own `IMethodCallTranslatorPlugin`/`IMemberTranslatorPlugin` in
`ConfigureServices`; `SqlTemplate.Call` builds a templated function for them too.

Optional services a dialect may register in `ConfigureServices`, each with an ISO default in the core:

| Service | Core default | Plugins overriding it |
|---|---|---|
| `IMigrationsSqlGenerator` | `AdoNetMigrationsSqlGenerator`: ISO DDL, identity as `GENERATED BY DEFAULT AS IDENTITY` | SQL Server, MySQL, SQLite, DuckDB |
| `IHistoryRepository` | `AdoNetHistoryRepository`: `INFORMATION_SCHEMA`, a lock row in `__EFMigrationsLock` | MySQL, SQLite |
| `IAdoNetBulkLoader` | `AdoNetInsertBulkLoader`: multi-row `INSERT ... VALUES` | SQL Server (`SqlBulkCopy`), PostgreSQL (binary `COPY`) |
| `IMethodCallTranslatorProvider`, `IMemberTranslatorProvider` | EF's relational translators plus the core's, through `FunctionSyntax` | — (plugins add `IMethodCallTranslatorPlugin`s) |
| anything else EF resolves | EF's relational defaults | — |

EF builds one internal service provider per distinct dialect, as decided by the dialect's `Equals`/`GetHashCode`.
Share instances (`SqlServerDialect.Instance`), or give a configurable dialect value equality.

Without `UseDialect`, the dialect is looked up in `AdoNetDialects`, where plugins register through
`XxxDbContextOptionsBuilderExtensions.RegisterDialect()`. A factory with no dialect registered and none chosen is refused
when the context is created; ISO SQL is chosen like any other dialect, with `UseDialect(AdoNetDialect.Ansi)`.

## Databases

| Plugin | Driver | Queries, saves, bulk DML, bulk insert | Migrations | Known gaps |
|---|---|---|---|---|
| `postgresql` | Npgsql | ✅ (bulk insert: binary `COPY`) | ✅ | — |
| `sqlserver` | Microsoft.Data.SqlClient | ✅ (bulk insert: `SqlBulkCopy`, checking constraints and firing triggers) | ✅ | — |
| `mysql` | MySqlConnector or MySql.Data | ✅ | ✅ | — |
| `sqlite` (+ pre-3.35) | Microsoft.Data.Sqlite | ✅ | ✅ except `ALTER COLUMN` | Needs a table rebuild, not implemented |
| `duckdb` | DuckDB.NET | ✅ (no savepoints) | `CreateTables` only | DuckDB can't `ALTER` a table with indexes or foreign keys |
| `flatfile` | pengdows.flatfile | ✅ except below | ✅ except below | Engine (0.2.1-preview.9): literals with an exponent, no `POWER`, interval literals of one field only, renaming a table leaves foreign keys naming the old one; strict ISO has no `UPDATE ... FROM` |

Gaps are only what a database lacks, recorded per fixture in `KnownEngineGaps` and reported as skipped tests,
attributed `[SKIP:<dialect>]`. Defects found in EF Core, drivers and engines while building this are listed in
[UPSTREAM_ISSUES.md](UPSTREAM_ISSUES.md) for issues and pull requests.

MySQL identifiers are quoted the ISO way, `"name"`: each session the provider uses gets `ANSI_QUOTES` added to its
`sql_mode` as it opens (`ISqlDialect.SessionSetup`), including a connection the application opened itself. Raw SQL sent
through the context must then quote strings with single quotes only; backtick identifiers still work.

`BulkInsert` doesn't read back keys the database generates (sequence keys are drawn first and set on the entities),
writes every mapped column (database defaults don't apply), and doesn't support owned or complex types.

## Size

Code lines (no blanks or comments) and Release DLL size, against the official providers on the same EF Core:

| Package | Lines | DLL |
|---|---|---|
| core | 3,477 | 148 KB |
| sqlserver | 476 | 28 KB |
| postgresql | 198 | 16 KB |
| mysql | 195 | 14 KB |
| sqlite | 160 | 13 KB |
| duckdb | 189 | 13 KB |
| flatfile | 96 | 9 KB |
| Microsoft.EntityFrameworkCore.SqlServer 10.0.11 | 23,474 | 627 KB |
| Microsoft.EntityFrameworkCore.Sqlite.Core 10.0.11 | 8,677 | 304 KB |
| Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 | — | 663 KB |

Not a feature-for-feature comparison: the official providers also scaffold, translate hundreds of LINQ methods, and
support spatial, JSON, temporal tables and more. The number to watch is how large a plugin grows as it reaches parity
on what it needs, while the core stays put. About 45 lines of each plugin are its `UseAdoNetXxx` entry points (a
connection string, a connection, a data source, or one set later, each also generic).

## Tests

```bash
dotnet test     # every project; server databases run in Docker (Testcontainers), and fail without it
dotnet test test/pengdows.efcore.adonet.Tests   # the core alone: fakeDb and an in-memory SQLite connection, no servers
```

- **Core** (`pengdows.efcore.adonet.Tests`): every grammar part, key strategy, DML form, DDL form, the history lock,
  bulk loading, connection lifecycle and failure paths, against dialects composed from parts the way an outside plugin
  would, on `pengdows.crud.fakeDb`. SQL is read from EF's own command log. 99.2% line, 91.8% branch coverage of the core
  from these tests alone.
- **Testing package** (`pengdows.efcore.adonet.Testing`): the conformance suites a plugin runs —
  `ProviderConformanceTests`, `MigrationsConformanceTests`, `BulkConformanceTests`, `TranslationConformanceTests` (each
  translated member run on the server and compared with .NET), `LiteralConformanceTests` (each type's constant matched
  against the same value saved through a parameter). Write a `ProviderFixture` (factory, connection string,
  dialect, DDL) and a one-line subclass of each suite.
- **Plugins** (`pengdows.efcore.adonet.<db>.Tests`): their fixture, the suites, their entry points and SQL shapes.
- **EF Core's specification tests** (`pengdows.efcore.adonet.Specification.Tests`), run unchanged on all six databases
  through one database-neutral test store (harness: `pengdows.efcore.adonet.FunctionalTests.Shared`,
  `test/FunctionalTests.props`). Results only: no provider's SQL baselines. Where suite: 408 of 408 on all six (SQL Server,
  PostgreSQL, MySQL, SQLite, DuckDB and pengdows.flatfile 0.2.1-preview.9).

### Coverage against the other MySQL providers

Each provider's own tests, run against MySQL 8.4 under coverlet 10.1.0 and measured on the provider's own assemblies (for
us, the core plus the MySQL plugin, since a MySQL user runs both). Measured 2026-10-26, on commit d238404.

| Provider | Code (lines / coverable) | Line | Branch | Method | Tests run (passed / failed / skipped) |
|---|---|---|---|---|---|
| pengdows.efcore.adonet + mysql | 6,591 / 3,300 | **95.8%** | **89.0%** | **97.2%** | 27,927 / 1,692 / 225 |
| Pomelo.EntityFrameworkCore.MySql 9.0.1 (EF Core 9) | 30,595 / 14,381 | 73.3% | 65.6% | 64.7% | 29,601 / 10 / 1,288 |
| MySql.EntityFrameworkCore 26.10.0 (Oracle) | 18,085 / 6,859 | 58.9% | 42.0% | 53.6% | 137 / 3 / 2 |

- **Ours:** the core's unit tests on fakeDb, the MySQL plugin's conformance suites, and EF Core 10.0.11's specification
  suites on MySQL. The 1,692 failures are specification tests MySQL doesn't pass yet; the suites still run the code they
  reach.
- **Pomelo:** its unit tests and its functional tests (EF Core 9's specification suites with Pomelo's MySQL baselines). It
  targets .NET 9 and was run there. Its test discoverer queries the server, so the run pins `ServerVersion` rather than
  `auto`, and its 200-connection pool needs the server's `max_connections` raised.
- **Oracle:** the three test projects in its source release (basic, design-time, migrations), run as a test-only tool
  (GPL: read and run, never copied). It ships no specification-suite run.

A percentage is of each provider's own code, so the numbers say how much of what each ships its tests reach, not how much
each does. Pomelo's tests reach more lines than ours in total (about 10,500 against 3,200), and do so over a much
larger codebase.

Public API is declared in each project's `PublicAPI.*.txt` and enforced by PublicApiAnalyzers; why each public type is
public is in [docs/PUBLIC_API.md](docs/PUBLIC_API.md). Reviews follow [REVIEW_POLICY.md](REVIEW_POLICY.md).

## Writing a plugin

1. Reference `pengdows.efcore.adonet` (and your driver, if you use its APIs).
2. Subclass `AdoNetDialect`; override `Sql`, `TypeMappings`, `Capabilities` with what differs from ISO.
3. Add a `UseAdoNetXxx` extension and a `RegisterDialect()`.
4. In a test project, reference `pengdows.efcore.adonet.Testing`, write a fixture, subclass the five suites.
5. Where a suite fails, override a grammar part or register a service in `ConfigureServices` (migrations,
   history, bulk loader, translators); record what the engine itself can't do in `KnownEngineGaps`.

The SQL Server plugin is the fullest example: words, `TopOrOffsetFetch`, `BitValues`, `SelectAfter`,
`TargetAliasFrom`, savepoint SQL, a migrations generator, a native bulk loader and a `ToQueryString` that writes
runnable T-SQL, in 476 lines.
