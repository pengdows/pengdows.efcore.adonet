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

### SQL Server: double constants below 2.2250738585072014E-308 become 0

- **Where:** `src/EFCore.SqlServer/Storage/Internal/SqlServerDoubleTypeMapping.cs` at v10.0.11,
  `GenerateNonNullSqlLiteral`: a subnormal double is written as its round-trip text, e.g. `4.9406564584124654E-324`.
- **Defect:** SQL Server parses a float literal below its documented range (2.23E-308) as 0, silently, though its
  arithmetic and parameters keep subnormal values. `EF.Constant(double.Epsilon)` (or any inlined subnormal constant)
  compares with 0: wrong rows, no error.
- **Confirmed:** on SQL Server 2022, `SELECT CAST(4.9406564584124654E-324 AS float)` returns 0 and `SELECT
  CAST(2.2250738585072014E-308 AS float) / CAST(4503599627370496 AS float)` returns 4.94E-324;
  `LiteralConformanceTests.Floating_point_constants_at_the_edges_of_their_types` failed on `double.Epsilon` with the
  same literal.
- **Before filing:** reproduce with Microsoft's provider (`EF.Constant(double.Epsilon)` against a row saved through a
  parameter).
- **Our fix:** `SqlServerTypeMappings` writes a subnormal as `(value * 2^52 / 4503599627370496E0)`, both exact.

### SQLite (suspected): float constants don't match the floats SQLite stored

- **Where:** `src/EFCore.Sqlite.Core/Storage/Internal/SqliteTypeMappingSource.cs` at v10.0.11 maps `float` to EF's base
  `FloatTypeMapping`, whose literal is the float's shortest text (`0.1f` → `0.1`).
- **Defect (suspected):** SQLite keeps a REAL in 8 bytes, so a float saved through a parameter is stored as
  `0.10000000149011612`; the literal `0.1` never equals it. `EF.Constant(0.1f)` finds nothing.
- **Confirmed:** only in this provider before its fix (the same base literal failed on SQLite, PostgreSQL and DuckDB);
  not yet run against Microsoft's SQLite provider.
- **Our fix:** the core writes a float constant as its exact double value.

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

## pengdows.flatfile (0.2.1-preview.9)

Open, recorded as skips in `test/pengdows.efcore.adonet.flatfile.Tests/Fixtures.cs`, each probed against preview.9 with
plain ADO.NET:

- Comparing a `DOUBLE PRECISION` value with a numeric literal goes through `decimal`: `WHERE "D" = 0E0` throws
  `OverflowException` when any row holds a double beyond decimal's range (1E+29, 1.7976931348623157E+308), and a literal
  beyond it (`"D" < 1.7976931348623157E+308`) throws for every row. ISO: an approximate numeric literal (one with an
  exponent) is a floating-point value, and comparing approximate with exact numerics is done as approximate. Found by
  `LiteralConformanceTests.Floating_point_constants_at_the_edges_of_their_types`.
- No `POWER` ("Unsupported scalar function 'POWER'"). Found by `TranslationConformanceTests.Math_on_doubles`.
- Interval literals take one field and no precision (`SqlParser.ParseIntervalLiteral`): ISO's
  `INTERVAL '0 01:02:03.5' DAY TO SECOND`, `INTERVAL '01:02:03' HOUR TO SECOND` and `DAY(8) TO SECOND` are rejected.
  And a one-field literal can't be compared with an `INTERVAL` column: `WHERE "S" = INTERVAL '3723' SECOND` throws
  `ArgumentException: Object must be of type TimeSpan` (the literal is an `IntervalValue`, the column a `TimeSpan`).
  Found by `LiteralConformanceTests.Time_span_constants`.
- Renaming a table leaves foreign keys that reference it naming the old table: after
  `ALTER TABLE "Authors" RENAME TO "Writers"`, an insert into `Shelves` fails with "Referenced table 'Authors' was not
  found for foreign key 'FK_Shelves_Authors'". Found by `MigrationsConformanceTests.Migrate_applies_records_and_reverts_migrations`
  and `Alter_column_changes_size_nullability_and_default`.
- A `DELETE` target takes no correlation name: `DELETE FROM "T" AS "t" WHERE "t"."Id" = 1` and `DELETE FROM "T" "t" ...`
  fail ("Expected token 'EndOfInput' ... but found 'As'"), though `UPDATE "T" AS "t"` and `UPDATE "T" "t"` work. ISO
  (SQL:2011) allows `DELETE FROM <target table> [ [ AS ] <correlation name> ]`. Found by running `ExecuteDelete` with EF's
  standard DML. No work-around: every `ExecuteDelete` fails, and the five tests that use it are skipped.
- Found by EF's Select specification suite on preview.9 (each skipped in the base suite's flatfile Select class):
  - Joins of several derived tables don't finish: three `LEFT JOIN (SELECT ... FROM "OrderDetails" INNER JOIN "Products"
    ...)` on `"Orders"`, one with `ROW_NUMBER()`, filtered `"o"."OrderID" < 10350` (`Do_not_erase_projection_mapping_when_adding_single_projection`),
    run in milliseconds elsewhere and were unfinished after 90 seconds. Likely nested-loop joins of derived tables with
    the filter applied last.
  - An outer column inside a lateral subquery isn't resolved: `LEFT JOIN LATERAL (SELECT ... WHERE "o"."CustomerID" =
    "c"."CustomerID") AS "o1" ON TRUE` throws "No row is available for table 'c0'" / "for table 'Customers'".
  - A window function's `ORDER BY` takes only column references: `ROW_NUMBER() OVER (ORDER BY (SELECT 1))` throws
    "ORDER BY only supports column references, not 'ScalarSubqueryExpression'". ISO allows any value expression.
  - `ROW_NUMBER() OVER (PARTITION BY "o0"."OrderID" ORDER BY "p"."ProductName")`, ordering by a column of a table joined
    into the window's query, numbers the rows differently from every other database (wrong first row per partition).
  - An integer literal is read back as a `decimal`: `CASE WHEN ... THEN 1 ELSE 2 END` fails EF's `GetByte` ("expected
    'Nullable<Byte>' but the actual value was of type 'System.Decimal'"). An exact numeric literal without a decimal point
    could be an integer, and `GetByte` could convert an in-range decimal.
  - A predicate as a CASE result is rejected: `CASE WHEN "o"."CustomerID" IS NULL THEN TRUE ELSE "o"."OrderID" < 100 END`
    ("Expected token 'End' ... but found 'LessThan'"). Valid ISO (a boolean value expression).
- A connection holds the database's write lock from `Open`, even one that only reads, so two contexts on one database
  can't both be open: the second waits, then times out ("Timed out after 5000ms waiting for another write connection to
  close"). EF opens a connection per query, so parallel work on one database (EF's specification test classes in
  parallel, or an application's concurrent requests) serializes or fails. Taking the lock on the first write, as SQLite
  does, would let readers run together.
- Errors that are not `DbException`s: the three above surface as `OverflowException`, `ArgumentException` and
  `InvalidOperationException`; provider-agnostic code (EF's `DbUpdateException` wrapping included) catches database errors
  as `DbException`. `DROP TABLE` of a missing table is fixed: it now throws `FlatFileException`, a `DbException`.

Fixed in preview.9 (our work-arounds removed, each by its test): `LIKE ... ESCAPE`; directional `TRIM`; `REPLACE`,
`FLOOR`, `CEILING`, `ROUND`; zoned timestamp literals; `FETCH FIRST`/`OFFSET` in derived tables and `EXISTS`; parameters
in `OFFSET`/`FETCH`; boolean `UPDATE` values; outer references in joined and nested subqueries; `ALTER SEQUENCE ...
RENAME TO`; `ALTER COLUMN`; `VARBINARY`; `INTERVAL` columns; self-referencing foreign keys in `CREATE TABLE`; self-joins;
a parameter as a comparison's left operand or a whole search condition; CASE as a search condition; a predicate as a
value. EF's Where specification suite now passes 408 of 408 (36 failed on preview.8).
