# pengdows.efcore.adonet

A provider-agnostic EF Core 10 relational provider. It reaches the database only through the ADO.NET abstractions:
`DbProviderFactory`, `DbDataSource`, `DbConnection` and `DbTransaction`. An `AdoNetSqlDialect` supplies the
SQL details that ADO.NET can't describe.

```csharp
// Any registered ADO.NET provider; the dialect is detected from the factory, or falls back to ANSI.
options.UseAdoNet(SqliteFactory.Instance, "Data Source=app.db");
options.UseAdoNet("DuckDB.NET.Data", connectionString);              // via DbProviderFactories
options.UseAdoNet(dataSource);                                        // caller owns the DbDataSource
options.UseAdoNet(connection, contextOwnsConnection: false);
options.UseAdoNet(factory, cs, o => o.UseDialect(MyDialect.Instance).MaxBatchSize(50));
```

## How it stays provider-neutral

| Concern | Mechanism |
|---|---|
| Connections | `DbDataSource.CreateConnection()` or `DbProviderFactory.CreateConnection()`. EF opens late and closes early, so pooling stays with the provider. |
| Parameters | EF's stock type mappings bind via `DbType`/`Size`/`Precision`, never provider parameter types. The dialect controls the marker (`@`, `:`, `$`) and whether `ParameterName` includes it. |
| Savepoints | `DbTransaction.Save/Rollback(name)/Release`, gated on `DbTransaction.SupportsSavepoints`. No SQL text. |
| Close | Calls `Close()` only on open connections; some providers (DuckDB.NET) throw on a second `Close()`. |
| Paging | `OFFSET … FETCH` (ANSI) or `LIMIT … OFFSET`, plus an optional "no limit" value for OFFSET-only queries. For engines that only accept literals there, values are inlined once known, and EF doesn't cache that SQL. |
| Generated values | `RETURNING`; or `INSERT …; SELECT … WHERE {rows-affected}=1 AND key={identity-fn}`; or **RecordsAffected**: plain one-statement commands checked through `DbDataReader.RecordsAffected`, with integer keys taken from `NEXT VALUE FOR "{table}_{column}_seq"` before the insert. |
| Batching | Several commands are batched into one `DbCommand` only if the dialect allows multiple statements; otherwise each command runs on its own. |

Dialects are cached per **instance** in EF's internal service provider. Use shared static instances
(`AdoNetSqlDialect.Ansi`, `SqliteSqlDialect.Instance`, …) and don't create a new one per context.

Built-in dialects: `Ansi`, `SqliteSqlDialect`, `LegacySqliteSqlDialect` (pre-3.35, no RETURNING), `DuckDbSqlDialect`,
`FlatFileSqlDialect` (pengdows.flatfile: strict ISO SQL, `:name` parameters, literal-only paging, sequence keys).
To support another database, subclass `AdoNetSqlDialect` and override what differs. Add it to
`AdoNetSqlDialect.Detect` if you want it auto-detected.

## Not supported (by design, for now)

- Migrations, `EnsureCreated`, database create/delete. Manage the schema outside EF; `Exists`/`HasTables` work.
- Positional (`?`) parameters, as used by ODBC/OleDb. EF reuses named parameters within one statement.
- Provider-specific LINQ translations (`string.Contains`, date parts, …). Only EF's relational defaults are
  available; register an `IMethodCallTranslatorPlugin` to add more.
- SQL Server needs `TOP` and bool-predicate rewriting; databases without a boolean type need `1/0` predicates.
  Neither is expressible in the dialect yet.

## Build / test

```bash
dotnet test                                   # Sqlite (in-memory), legacy Sqlite, DuckDB and pengdows.flatfile (temp dirs); no servers
dotnet test --filter "FullyQualifiedName~DuckDb"
```

`ProviderConformanceTests<TFixture>` is a single suite written only against EF and ADO.NET abstractions. To check a new
provider, add a `ProviderFixture` (factory, connection string, DDL) and a one-line subclass of the suite. Failures caused
by the database engine itself go in the fixture's `KnownEngineGaps` and are reported as skipped with the reason.

Known engine gaps:
- pengdows.flatfile 0.2.1-preview.8 rejects `FETCH FIRST` inside a derived table, which EF emits for
  `Include(...)` combined with `Single`/`Take`.
