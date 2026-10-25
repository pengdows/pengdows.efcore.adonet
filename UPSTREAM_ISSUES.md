# Upstream issues

Defects found in other projects' code while building this provider, kept for issues and pull requests upstream. Each
entry says what was confirmed and how, and what still needs checking before filing. Work-arounds in this repository are
noted so they can be removed once the fix ships.

## dotnet/efcore (EF Core 10.0.11)

### SQL Server: `string.Length` and `Substring(start)` ignore trailing spaces

- **Where:** the SQL Server provider's string translations (`SqlServerStringMemberTranslator`,
  `SqlServerStringMethodTranslator`): `Length` is `CAST(LEN(x) AS int)`, `Substring(start)` is
  `SUBSTRING(x, start + 1, LEN(x))`.
- **Defect:** `LEN` doesn't count trailing spaces, so `"ab  ".Length` is 2 on the server and 4 in .NET; `Substring` to the
  end drops them.
- **Confirmed:** in Microsoft's own baseline, `NorthwindWhereQuerySqlServerTest.Filter_non_nullable_value_after_FirstOrDefault_on_empty_collection`
  expects `CAST(LEN([o].[CustomerID]) AS int)`; our `TranslationConformanceTests.Length` and `Substring` fail on SQL
  Server 2022 with that translation.
- **Before filing:** search existing issues (this may be a known, by-design decision tied to SQL Server's comparison
  semantics, which also ignore trailing spaces).
- **Our fix:** `CAST(LEN(REPLACE(x, ' ', '_')) AS int)` and `SUBSTRING(x, start, DATALENGTH(x))` in
  `SqlServerFunctions`; one copied baseline adapted and marked `pengdows:`.

### Migrator: a migration that fails while its SQL is generated leaves a statement-released lock held

- **Where:** `src/EFCore.Relational/Migrations/Internal/Migrator.cs` at v10.0.11, `MigrateImplementation` and
  `MigrateImplementationAsync`: the migration transaction begins (line 160) before the commands are generated (185), and
  the `finally` disposes `state.DatabaseLock` (207, 349) before `state.Transaction` (209, 355).
- **Defect:** if generating the commands throws (a migration's `Up` throws, or the SQL generator refuses an operation), a
  history repository with `LockReleaseBehavior.Explicit` that releases its lock with a statement runs that statement in
  the still-open transaction, which is then rolled back: the lock stays held and every later migration times out.
  A failing *command* is not affected: `MigrationCommandExecutor` disposes the transaction itself before rethrowing.
- **Confirmed:** `MigrationsConformanceTests.A_migration_that_fails_to_generate_leaves_no_lock_behind` waited out the
  2-minute lock on SQLite and PostgreSQL with this provider's earlier lock.
- **Before filing:** check EF's own SQLite provider (lock table, `Explicit`), which looks affected the same way; SQL
  Server's `sp_getapplock` session lock likely is not. A fix: dispose the transaction before the lock.
- **Our fix:** `AdoNetHistoryRepository`'s lock rolls back an open transaction before releasing.

### RelationalTypeMappingSource.FindCollectionMapping null-forgives the JSON string mapping

- **Where:** `src/EFCore.Relational/Storage/RelationalTypeMappingSource.cs` at v10.0.11, `FindCollectionMapping`:
  `FindMapping(info.WithConverter(new ValueConverterInfo(modelType, typeof(string), ...)))!.WithComposedConverter(...)`.
- **Defect:** when the provider has no string mapping for the column's store type, the `!` turns a "no mapping" result into a
  `NullReferenceException` during model building. A string property with `HasColumnType("INTEGER")` triggers it: `string`
  is an `IEnumerable<char>`, so EF tries it as a JSON collection.
- **Confirmed:** `ConfigurationTests.A_column_type_that_cannot_hold_the_property_is_reported_not_crashed_on` threw the NRE from
  inside `FindCollectionMapping` before our guard.
- **Before filing:** reproduce with an EF provider whose `FindMapping` returns null for a CLR type its store type doesn't
  hold (EF's own providers may return a mismatched mapping instead, hiding it).
- **Our fix:** `AdoNetTypeMappingSource.FindCollectionMapping` returns null when that string mapping doesn't exist.

### SQL Server: `AzureSynapseDbContextOptionsBuilder.UseCompatibilityLevel` is ignored

- **Where:** `src/EFCore.SqlServer/Infrastructure/Internal/SqlServerOptionsExtension.cs` at v10.0.11, line 226,
  `WithAzureSynapseCompatibilityLevel`: `clone._azureSynapseCompatibilityLevel = _azureSynapseCompatibilityLevel;`
  assigns the extension's old value instead of the `azureSynapseCompatibilityLevel` parameter.
- **Defect:** the compatibility level set through `UseAzureSynapse(..., b => b.UseCompatibilityLevel(n))` never takes
  effect.
- **Confirmed:** by reading the source (found during the provider API inventory); not yet reproduced in a test.
- **Before filing:** check main for a fix; a one-line PR with a test asserting the option's value.

## dotnet/SqlClient (Microsoft.Data.SqlClient 7.1.1)

### `SqlTransaction.SupportsSavepoints` is false although it has savepoints

- **Defect:** `SqlTransaction` implements `Save(string)` and `Rollback(string)` (SQL Server's `SAVE TRANSACTION`), but
  reports `DbTransaction.SupportsSavepoints == false`, so provider-agnostic code (EF's relational savepoints included) skips
  them.
- **Confirmed:** `ProviderConformanceTests.Savepoints_undo_part_of_a_transaction` against SQL Server 2022 read
  `GetDbTransaction().SupportsSavepoints == false`.
- **Before filing:** check whether `Save`/`Rollback(string)` override the `DbTransaction` virtuals (or only hide them) and
  `Release` (SQL Server has no release; it should be a no-op).
- **Our work-around:** `SqlServerSql.Savepoints` writes `SAVE TRANSACTION` / `ROLLBACK TRANSACTION` as SQL.

## mysql-net/MySqlConnector (2.6.2)

### `MySqlTransaction.SupportsSavepoints` reads false in our runs

- **Defect (suspected):** MySQL has `SAVEPOINT`, `ROLLBACK TO SAVEPOINT` and `RELEASE SAVEPOINT`, but
  `GetDbTransaction().SupportsSavepoints` was false in the same conformance test against MySQL 8.4.
- **Before filing:** confirm on a bare `MySqlTransaction` (outside EF) and read MySqlConnector's source; it may be
  deliberate or configuration-dependent. Unverified as a bug.
- **Our work-around:** the core's ISO `SavepointSyntax` applies to MySQL.

## pengdows.crud.fakeDb (2.0.5; source at 2.0.6)

Gaps that keep core paths out of reach of fakeDb tests (REVIEW_POLICY: a gap is added to fakeDb, never mocked around):

- No savepoints: `fakeDbTransaction.SupportsSavepoints` is false and `Save`/`Rollback(name)` throw, so the core's use of a
  driver's own savepoints is only covered by the integration suites (SQLite, PostgreSQL, flatfile). A switch to emulate
  them would cover it.
- 2.0.5 always reports `RecordsAffected = 0` for a batch's reader, so a successful plain-statement write (sequence-key
  dialects) can't be scripted; 2.0.6's `RecordsAffectedOverride` should allow it once published.

## pengdows.flatfile (0.2.1-preview.8)

Recorded as skips in `test/pengdows.efcore.adonet.flatfile.Tests/Fixtures.cs`, each probed directly:

- `LIKE ... ESCAPE` is ignored: an escaped pattern never matches.
- `TRIM(LEADING FROM x)` and `TRIM(TRAILING FROM x)` trim both ends.
- No `REPLACE`, `FLOOR`, `CEILING` or `ROUND`.
- `TIMESTAMP '... +02:00'` (ISO: a TIMESTAMP WITH TIME ZONE literal) is typed without its zone, then compared with a
  `DateTimeOffset` value, which throws.
- `FETCH FIRST` is rejected inside a derived table; a boolean expression is rejected as an `UPDATE` value; a column of an
  outer table is resolved against the wrong table inside a joined `IN` subquery; no `ALTER SEQUENCE ... RENAME TO`; no
  `ALTER COLUMN`.
- `VARBINARY` is rejected as a vendor type (it is in SQL:2016; `BLOB` is accepted). Arguable, not a clear bug.
- No `INTERVAL` type.
- `DROP TABLE` of a missing table throws `InvalidOperationException`, not a `DbException`: provider-agnostic code catching
  `DbException` for database errors misses it.
- A table can't reference itself in `CREATE TABLE`: `ForeignKeyEvaluator.ValidateDefinition` (called from
  `DefaultFlatFileQueryExecutor` at the CREATE TABLE path) looks the referenced table up in the catalog before the new
  table is in it, and throws "Referenced table 'X' was not found for foreign key ... on table 'X'". Valid ISO (a tree,
  EF's Northwind `Employees.ReportsTo`). Fix: resolve a reference to the table being created to its own definition.
  Found by `MigrationsConformanceTests.Create_tables_builds_a_table_that_references_itself`. Our work-around:
  `FlatFileMigrationsSqlGenerator` adds such keys with `ALTER TABLE ... ADD CONSTRAINT` after the table.
- A table joined to itself returns wrong rows. Probed directly on a two-row tree (`C(Id, Name, ParentId)`, rows
  `(1, 'Fiction', NULL)` and `(2, 'SF', 1)`): `SELECT c.Id, c0.Name FROM C AS c LEFT JOIN C AS c0 ON c.ParentId = c0.Id`
  returns two rows of NULLs (expected `1|NULL`, `2|Fiction`), and the same `INNER JOIN` returns no rows (expected
  `2|Fiction`). Likely the two aliases of one table resolve to the same row source. No work-around: a provider can't
  rewrite every self-join.
