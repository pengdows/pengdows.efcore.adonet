# Upstream issues

Defects found in other projects' code while building this provider, kept for issues and pull requests upstream. Each
entry says what was confirmed and how, and what still needs checking before filing. Work-arounds in this repository are
noted so they can be removed once the fix ships.

## dotnet/efcore (EF Core 10.0.11)

### PostgreSQL JSON update fixture uses an unquoted mixed-case table name

- **Where:** `JsonUpdateTestBase.Add_and_update_top_level_optional_owned_collection_to_JSON` reads
  `JsonEntitiesBasic` through raw SQL without identifier delimiters.
- **Defect:** PostgreSQL folds the unquoted name to `jsonentitiesbasic`, while the relational model creates the
  quoted mixed-case table name `"JsonEntitiesBasic"`; the probe fails with `42P01` before the provider operation.
- **Confirmed:** PostgreSQL specification run; the same test succeeds on providers whose unquoted identifier rules
  resolve the mixed-case name.
- **Status:** not reported; candidate fixture fix is to use the provider's delimited identifier helper or configure
  the fixture table name consistently.
- **Our handling:** the PostgreSQL specification test is explicitly skipped and linked to this limitation.

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

### A transaction that fails to begin leaves the connection open

- **Defect:** `RelationalConnection.BeginTransaction` (and `BeginTransactionAsync`) calls `Open()`, then begins the
  transaction; when beginning throws (a transient failure, a refused isolation level, an interceptor), nothing closes what
  it opened. The open count stays raised, so the connection stays open for the context's life and the application's own
  `CloseConnection` no longer closes it. With a retrying execution strategy every failed attempt adds one more.
- **Confirmed:** `LifecycleTests.A_transaction_that_fails_to_begin_*` (fakeDb failing `BeginTransaction`), and on flatfile,
  whose write lock made the leaked connection block the next one (`ExecutionStrategyConformanceTests.Beginning_a_transaction_is_retried`).
- **Our fix:** `AdoNetRelationalConnection` closes what a failed begin opened. Not yet reported upstream.

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

### Relational: a multi-row VALUES as a set-operation operand changes the result

- **Defect:** `QuerySqlGenerator.GenerateValues` writes several rows as `SELECT ... UNION ALL VALUES (...), (...)`, and
  `GenerateSetOperationOperand` writes such an operand unparenthesized, so in `x UNION <values>` the inner `UNION ALL`
  applies to the whole union and keeps its duplicates (`c.Ints.Skip(1).Union(ints).Count()` counted 4, not 3, on MySQL).
- **Effect upstream:** none seen in Microsoft's providers: SQL Server writes a real `VALUES` and SQLite overrides
  `GenerateSetOperationOperand`. It reaches any provider that keeps the base `GenerateValues`.
- **Here:** a set-operation operand over a multi-row VALUES is parenthesized.
- **Status:** not reported.

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

## npgsql/npgsql (Npgsql 10; source at eb2cb791)

### `NpgsqlConnection` never raises `Disposed`

- **Defect:** `Dispose(bool)` and `DisposeAsync()` close the connection and set `_disposed`, but neither calls
  `base.Dispose(disposing)`, so `Component.Disposed` never fires. `DbConnection` derives from `Component`, whose event is how
  code observes disposal; SqlClient, MySqlConnector and Microsoft.Data.Sqlite raise it.
- **Found by:** `ConnectionConformanceTests.An_owned_connection_in_OnConfiguring_is_disposed_with_the_context`, which now
  accepts either the event or a refusal to reopen (Npgsql refuses), since some drivers reopen a disposed connection.
- **Status:** read in source, not yet reported; no pull request.

### An interval with negative months reads as `TimeSpan.Zero`

- **Defect:** reading an interval as a `TimeSpan` refuses positive months but returns `TimeSpan.Zero` for negative ones,
  silently. Months have no fixed length, so neither has a `TimeSpan`.
- **Status:** [#6682](https://github.com/npgsql/npgsql/pull/6682) open.
- **Our work-around:** the PostgreSQL dialect maps `TimeSpan` over `interval` to `PostgreSqlIntervalTypeMapping`, which reads
  `NpgsqlInterval` and refuses any months. Covered by `PostgreSqlIntervalTests`, buffered and streamed.

## MySQL Server (8.4.11)

### JSON_TABLE over an outer column reads it as empty in a semijoin or a merged lateral table

- **Defect:** `WHERE x IN (SELECT ... FROM JSON_TABLE(t.col, ...))`, the same under `EXISTS`, and `CROSS JOIN LATERAL
  (SELECT ... FROM JSON_TABLE(t.col, ...) WHERE ...)` fail with "Invalid JSON text in argument 1 to function json_table:
  The document is empty", although every row holds a valid array; the same JSON_TABLE in a scalar subquery or a plain join
  works. The optimizer's semijoin and derived-table merge evaluate the argument without the outer row.
- **Status:** the EXISTS case is [Bug #114897](https://bugs.mysql.com/bug.php?id=114897), fixed in 9.3.0; 8.4 (LTS) has it.
- **Here:** a query block that reads a JSON array table carries `/*+ NO_SEMIJOIN() NO_MERGE() */`
  (`JsonSyntax.ArrayElementsHint`).

## mysql-net/MySqlConnector (2.6.2)

### `MySqlTransaction.SupportsSavepoints` is false although its savepoints work

- **Defect:** `MySqlTransaction` implements `Save`, `Rollback(string)` and `Release`, which work, but inherits
  `DbTransaction.SupportsSavepoints == false`.
- **Status: accepted upstream.** Reported as [#1676](https://github.com/mysql-net/MySqlConnector/issues/1676), closed as
  completed ("an oversight when the .NET 5 DbTransaction interface was added"); a later release reports true.
- **Effect here:** none. Whether there are savepoints is the dialect's to say; MySQL's dialect has ISO savepoints, used
  through the driver's methods where it reports them and as SQL until then.

### A prepared NULL `DbType.Object` parameter is rejected (not reached)

- **Defect:** `DbType.Object` maps to `MySqlDbType.Vector`, so MySQL 8.4 rejects a prepared command with a NULL
  `DbType.Object` parameter. Issue [#1679](https://github.com/mysql-net/MySqlConnector/issues/1679); our
  [#1680](https://github.com/mysql-net/MySqlConnector/pull/1680) was closed for the maintainer's
  [#1681](https://github.com/mysql-net/MySqlConnector/pull/1681), merged 2026-10-25, unreleased after 2.6.2.
- **Not reached:** neither EF nor this provider calls `DbCommand.Prepare()`, and the MySQL dialect binds no parameter as
  `DbType.Object`. Only a user's own interceptor that prepares commands could meet it.

### SingleStore: savepoints succeed and do nothing (declined)

- **Defect:** on SingleStore, `Save` and `Rollback(savepoint)` return normally and roll nothing back, so work rolled back to
  a savepoint is committed.
- **Status: declined.** [#1677](https://github.com/mysql-net/MySqlConnector/issues/1677), closed as not planned: the
  maintainer reads `SupportsSavepoints` as "the driver implements the methods", not "the server supports them". Once
  #1676 ships, MySqlConnector reports savepoints on SingleStore too.
- **Our handling:** savepoint support is the dialect's (`ISqlDialect.Savepoints`), never the driver's flag; where the
  dialect has none, all three savepoint methods throw `NotSupportedException`. `MySqlDialect.SingleStore` has none.

### Output parameters only for `CommandType.StoredProcedure` (by design)

- **Limitation:** an `Output` or `InputOutput` parameter on a text command (EF's `CALL p(@p0, @p1)`) throws; the driver
  returns outputs only when it builds the call itself, from the procedure's metadata. MySQL's protocol has no output
  parameters for a text query. As read in Pomelo's update batch, which works around it; not confirmed here separately, and
  MySql.Data not checked.
- **Status:** by design; not a defect to report.
- **Our handling:** as Pomelo's: each output goes through a user variable (`ISqlDialect.ProcedureOutputVariable`), set
  before the call, passed in the parameter's place and selected back after it. Both drivers read `@name` as a parameter
  unless the connection allows user variables, so the dialect's `ConnectionOptions` adds `Allow User Variables=true` to
  connection strings and closed connections, and refuses an open connection or a data source without it.

### JSON path members that aren't identifiers

- **Defect (1):** `RelationalSqlGenerationHelper.DelimitJsonPathElement` leaves a name of letters and digits bare, so a
  member named `1CustomEnum` is written `$.CustomOwnedReferenceRoot.1CustomEnum`, which no database's path grammar reads. The
  SQL Server provider overrides it to quote a name whose first character isn't a letter; the relational default doesn't.
- **Defect (2):** the path in a `SaveChanges` JSON update (`IColumnModification.JsonPath`, a string in 10.0.11) is built
  by EF with the member names unquoted: a member named `Full name` is patched at `$.Full name`. EF's `main` replaces the
  string with `StructuredJsonPath`, which a provider can delimit; the shipped string can't be repaired by a provider.
- **Confirmed:** (1) `JsonQueryMySqlTest.Json_projection_enum_with_custom_conversion` fails with "Invalid JSON path
  expression" under the default helper and passes under ours; (2) `JsonTests` writes `JSON_SET("Detail", '$.Full name', ...)`
  from a `SaveChanges` update, while the `ExecuteUpdate` path (ours) writes `'$."Full name"'`.
- **Before filing:** (2) against the 10.0.11 source, and whether `main`'s `StructuredJsonPath` already closes it.
- **Our fix:** (1) `AdoNetSqlGenerationHelper.DelimitJsonPathElement`. (2) none yet: a `SaveChanges` patch of a member whose
  name isn't an identifier writes an unquoted path.

## mysql/mysql-connector-net (MySql.Data 26.10.0)

Found by running the shared MySQL suites through Oracle's driver as well as MySqlConnector (the dialect is
driver-neutral). Checked against the user's open pull requests there (#95-#115) and fork (pengdows/mysql-connector-net
#1-#5): neither is covered.

### `DbType.DateTime2` maps to `MySqlDbType.Blob`

- **Defect:** `new MySqlParameter { DbType = DbType.DateTime2, Value = DateTime }` becomes `MySqlDbType.Blob`, and the value is
  sent as current-culture text (`'1/2/2026 3:04:05\u202FAM'`), which MySQL rejects ("Incorrect datetime value"). `DateTime`,
  `Date`, `Time` and `DateTimeOffset` map to date/time types; `DateTime2` should map to `MySqlDbType.DateTime`.
- **Confirmed:** directly, reading `MySqlParameter.MySqlDbType` after setting each `DbType`.
- **Effect here:** none now. Oracle's own EF provider binds DateTime as `DbType.DateTime`, and so does the MySQL dialect
  (`ITypeMappingDialect.DateTimeDbType`); before that, 78 of 113 shared tests failed through MySql.Data.
- **Status:** not reported; a pull request candidate.

### `MySqlConnection.DisposeAsync` doesn't dispose

- **Defect:** `Dispose()` raises `Component.Disposed` and leaves the connection disposed; `DisposeAsync()` does neither (the
  event doesn't fire and the connection opens again). EF disposes a connection the context owns asynchronously.
- **Confirmed:** directly (the `Disposed` event, sync and async).
- **Effect here:** none now. The core closes an owned connection asynchronously and then disposes it synchronously
  (`AdoNetRelationalConnection.DisposeDbConnectionAsync`), which every driver gets right; this also covers Npgsql, whose
  connection never raises `Disposed`.
- **Status:** not reported; a pull request candidate.

## PomeloFoundation/Pomelo.EntityFrameworkCore.MySql (9.0.1; source at 14c2cd55)

Found while writing the MySQL dialect's translations against Pomelo's as the behaviour reference, and confirmed by running
Pomelo 9.0.1 against MySQL 8.4. Searched Pomelo's issues: neither is reported.

### `AddHours`, `AddMinutes`, `AddSeconds`, `AddMilliseconds` and `AddDays` drop the fraction

- **Defect:** the amount is cast to an integer before `DATE_ADD`, and MySQL's cast rounds it:
  `DATE_ADD(at, INTERVAL CAST(1.5 AS signed) hour)` adds 2 hours where `DateTime.AddHours(1.5)` adds 1 hour 30 minutes
  (10:00 became 12:00, not 11:30).
- **Here:** a fractional amount is added as microseconds (`FunctionSyntax.AddMicroseconds`), so the fraction is kept.

### `TimeSpan.Hours` returns the total hours past a day

- **Defect:** `EXTRACT(hour FROM span)` on a MySQL TIME counts every hour, so a span of 1 day 2 hours gives 26, where
  `TimeSpan.Hours` is 2.
- **Here:** `(EXTRACT(HOUR FROM span) % 24)`.

## Giorgi/DuckDB.NET (DuckDB.NET.Data 1.5.6.1)

### `DuckDBConnection.Close()` throws on a connection that isn't open

- **Defect:** `Close()` on a connection already closed, or never opened, throws `InvalidOperationException: Connection is
  already closed.` ADO.NET's contract is that `Close` may be called more than once without an exception (SqlClient,
  Npgsql, MySqlConnector and Microsoft.Data.Sqlite all return quietly), and provider-agnostic code relies on it.
- **Confirmed:** directly, outside EF: `Open(); Close(); Close();` throws on the second call, and `Close()` on a new
  connection throws. In the base test suite, the test store's `CloseConnection()` after setup failed every DuckDB fixture
  (20,810 tests).
- **History:** deliberate upstream since 2021 (`863a426` "Throw exception if already Closed"; `c0ea769` kept it).
- **Status: fixed upstream.** Our pull request (from `pengdows/DuckDB.NET`, commit `a16b0af` "Allow Close to be called more
  than once") is merged; the next DuckDB.NET release after 1.5.6.1 includes it.
- **Our work-around:** the provider's connection (`AdoNetRelationalConnection`) and the test store close a connection only
  when it is open. Remove the test store's with the update to that release.

### `GetFieldValue<byte[]>` on a BLOB column throws

- **Defect:** `GetFieldValue<byte[]>` on a `BLOB` column throws `InvalidCastException: Unable to cast object of type
  'System.IO.UnmanagedMemoryStream' to type 'System.Byte[]'`, and `GetValue` returns that stream. ADO.NET drivers return a
  binary column's value as `byte[]` (SqlClient, Npgsql, MySqlConnector, Microsoft.Data.Sqlite), with `GetStream` for
  streaming; EF reads a `byte[]` property through `GetFieldValue<byte[]>`.
- **Confirmed:** directly, outside EF: `SELECT '\x01\x02'::BLOB`, then `GetFieldValue<byte[]>(0)`. Still in
  `StringVectorDataReader.GetValidValue` on main after #377 (which added `GetBytes`).
- **Found by:** `MigrationsConformanceTests.Unsized_key_and_index_columns_are_created_and_hold_long_values`, skipped on
  DuckDB (and 18 tests of EF's specification suites: value converters, binary keys, many-to-many joins).
- **Status: fixed upstream.** [#384](https://github.com/Giorgi/DuckDB.NET/pull/384) "Return byte arrays from typed BLOB
  reads", merged 2026-10-26 (f86315d): `GetFieldValue<byte[]>` returns the bytes, also for a BLOB inside a struct, map or
  list; `GetValue` and `GetStream` keep returning a stream. In the next release after 1.5.6.1.
- **Our work-around:** the DuckDB dialect declares `IDatabaseCapabilities.ReadsBinaryAsStream`; its readers are wrapped
  (`BinaryAsStreamReader`) so each binary value is copied out of the driver's stream while its row is current. The
  key-column test runs on DuckDB again. Drop the capability with the update to that release.

### Parameters whose type the statement doesn't fix are converted by DbType, wrongly for some

- **Defect:** where DuckDB can't infer a parameter's type (a raw query's argument, a projection), 1.5.6.1 converts the value
  by its `DbType`. It has no conversion for `Decimal` or `DateTime2`, so it sends the current culture's text (`1234,5`
  under de-DE); a `Time`-typed `TimeSpan` goes through `TimeOnly.FromTimeSpan`, which throws for a negative span or one of a
  day or more.
- **Status:** [#382](https://github.com/Giorgi/DuckDB.NET/pull/382) open.
- **Our work-around:** the DuckDB dialect's `ParameterDbType` binds `decimal` as `Currency`, `DateTime` as `DateTime`, and
  `TimeSpan` (an `INTERVAL` here) as `Object`, all of which 1.5.6.1 converts correctly. Covered by
  `ProviderConformanceTests.Raw_query_arguments_read_back_under_a_comma_decimal_culture` and
  `DuckDbIntervalParameterTests`.

### Foreign keys are checked against rows deleted earlier in the same transaction (engine, documented)

- **Behavior:** deleting a parent after its child in one transaction fails with "Violates foreign key constraint because key
  ... is still referenced", DuckDB's documented foreign key limitation. EF deletes children first, in one SaveChanges.
- **Found by:** `DatabaseCreatorConformanceTests.Ensure_created_creates_a_table_reached_by_two_cascade_paths_and_deletes_cascade`,
  a `[SKIP:DuckDB]` capability skip.

### Driver fixes merged upstream, not yet released (we pin 1.5.6.1)

Merged into Giorgi/DuckDB.NET from our ADO.NET conformance work; each ships in the next release. Until we update, a DuckDB
failure in the specification suite that matches one of these is the driver's, not the provider's.

| PR | Change | Where it shows in EF's specification suites |
|---|---|---|
| [#373](https://github.com/Giorgi/DuckDB.NET/pull/373), [#379](https://github.com/Giorgi/DuckDB.NET/pull/379) | `Close` may be called more than once, also on a never-opened connection | The test store's close-only-if-open check (above) |
| [#374](https://github.com/Giorgi/DuckDB.NET/pull/374) | `RecordsAffected` is -1 for statements other than INSERT, UPDATE and DELETE | Update and concurrency suites (affected-row checks) |
| [#359](https://github.com/Giorgi/DuckDB.NET/pull/359), [#360](https://github.com/Giorgi/DuckDB.NET/pull/360) | Negative intervals; an untyped `TimeSpan` parameter binds as an interval | Built-in data types, temporal translations, value converters |
| [#375](https://github.com/Giorgi/DuckDB.NET/pull/375) | `GetOrdinal`: case-insensitive fallback, `IndexOutOfRangeException` | Raw SQL (`FromSql`, `SqlQuery`), which reads columns by name |
| [#376](https://github.com/Giorgi/DuckDB.NET/pull/376), [#377](https://github.com/Giorgi/DuckDB.NET/pull/377) | `GetValues` copies only what the array holds; `GetBytes`, `GetChars` | Reader-level tests: built-in data types, large binary and text |
| [#372](https://github.com/Giorgi/DuckDB.NET/pull/372) | `CommandType` defaults to Text, parameter `Direction` to Input | Interception and command tests that inspect commands |
| [#378](https://github.com/Giorgi/DuckDB.NET/pull/378) | `Prepare()` requires an open connection | Command lifecycle |
| [#380](https://github.com/Giorgi/DuckDB.NET/pull/380) | Unsupported savepoint operations fail consistently | Transaction suite (DuckDB has no savepoints; the dialect declares none) |
| [#364](https://github.com/Giorgi/DuckDB.NET/pull/364) | ADO.NET schema data types and metadata | Scaffolding, once design-time services exist |
| [#384](https://github.com/Giorgi/DuckDB.NET/pull/384) | `GetFieldValue<byte[]>` on a BLOB, also inside a struct, map or list | Value converters, binary keys, many-to-many (18 tests); the shared key-column test |
| [#357](https://github.com/Giorgi/DuckDB.NET/pull/357) | Regression test for `CommandBehavior.CloseConnection` | (test only) |

When the release ships: update `DuckDB.NET.Data` and `DuckDB.NET.Data.Full`, remove the test store's close-only-if-open
check, and rerun the DuckDB specification suite.

## pengdows.crud.fakeDb (2.0.5; source at 2.0.6)

Gaps that keep core paths out of reach of fakeDb tests (REVIEW_POLICY: a gap is added to fakeDb, never mocked around):

- No savepoints: `fakeDbTransaction.SupportsSavepoints` is false and `Save`/`Rollback(name)` throw, so the core's use of a
  driver's own savepoints is only covered by the integration suites (SQLite, PostgreSQL, flatfile). A switch to emulate
  them would cover it.
- No `System.Transactions` enlistment: `fakeDbConnection` doesn't override `DbConnection.EnlistTransaction`, so a context
  inside a `TransactionScope` on a dialect whose driver enlists fails with `NotSupportedException`. Ambient transactions
  are only covered by EF's Transaction specification suite on the server databases.
- 2.0.5 always reports `RecordsAffected = 0` for a batch's reader, so a successful plain-statement write (sequence-key
  dialects) can't be scripted; 2.0.6's `RecordsAffectedOverride` should allow it once published.
- 2.0.5's `CreateDataSource` returns .NET's default data source, and the factory doesn't list the data sources it made,
  so two core tests keep a hand-written factory: `ConnectionSourceTests.CountingFactory` (one data source per connection
  string) and `ConfigurationRulesTests.TrackingFactory` (data sources disposed with the service provider). fakeDb's source
  (built locally as 2.0.6.1) has `SupportsNativeDataSource`, `CreatedDataSources` and `FakeDbDataSource.WasDisposed`, which
  replace both once published; nuget.org's latest is 2.0.5.
- 2.0.5's `fakeDbConnection` names no `DbProviderFactory` (`DbProviderFactories.GetFactory(connection)` is null); the
  source reports the factory that created it. The core's database creator therefore prefers the factory the context was
  configured with, and only then asks the connection.
- A dialect is registered by factory type and `fakeDbFactory` is sealed, so a test that registers one needs a factory type
  of its own (`ProbedFactory`, `UnknownFactory`); both hand out fakeDb connections.

## pengdows.flatfile (0.2.1-preview.10)

Open, recorded as skips in `test/pengdows.efcore.adonet.flatfile.Tests/Fixtures.cs`, each probed against preview.10 with
plain ADO.NET:

- Comparing a `DOUBLE PRECISION` value with a numeric literal goes through `decimal`: `WHERE "D" = 0E0` throws
  `OverflowException` when any row holds a double beyond decimal's range (1E+29, 1.7976931348623157E+308), and a literal
  beyond it (`"D" < 1.7976931348623157E+308`) throws for every row. ISO: an approximate numeric literal (one with an
  exponent) is a floating-point value, and comparing approximate with exact numerics is done as approximate. Found by
  `LiteralConformanceTests.Floating_point_constants_at_the_edges_of_their_types`. **Fixed** in pengdows.flatfile 37a379d
  ("Preserve approximate numeric semantics"), shipped in preview.17. The preview.16 package was built from 00f2ee9, three
  minutes before that fix, though its tag is later, so preview.16 still negated a double through `decimal`.
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
- A `DATE` value can't be read as a `DateTime`: `GetDateTime` and `GetFieldValue<DateTime>` on a `DATE` column throw
  `InvalidCastException` ("Unable to cast object of type 'System.DateOnly' to type 'System.DateTime'"). Other drivers
  return a `DATE` through `GetDateTime` (the ADO.NET mapping of a date before `DateOnly`), and EF reads a `DateTime`
  property through it. Found by `BulkConformanceTests.Bulk_insert_writes_values_as_the_column_type`.
- An unqualified table name resolves to a table in any schema: after `CREATE SCHEMA "tenant"` and
  `CREATE TABLE "tenant"."H" (...)`, `CREATE TABLE "H" (...)` fails with "Table 'H' already exists", and
  `SELECT COUNT(*) FROM "H"` says "Table 'H' must be schema-qualified". ISO: an unqualified name is in the default schema
  (`CURRENT_SCHEMA`, which the engine also lacks: "Column 'CURRENT_SCHEMA' was not found"). Found by
  `MigrationsConformanceTests.Another_schemas_history_table_is_not_this_ones`.
- A lookup through an index on a `VARBINARY` column fails: with `CREATE INDEX "IX_K_Hash" ON "K" ("Hash")`,
  `SELECT * FROM "K" WHERE "Hash" = :c` (a `byte[]` parameter) throws `ArgumentException: Object must be of type String`
  from `FlatFileIndexKeyComparer.CompareValue` (it compares the index's key as a string). Found by
  `MigrationsConformanceTests.Unsized_key_and_index_columns_are_created_and_hold_long_values`.
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
  - Correlated scalar subqueries inside a lateral join don't finish: `FROM "Customers" AS "c" LEFT JOIN LATERAL (SELECT
    ... FROM "OrderDetails" AS "o" WHERE (SELECT "o0"."OrderID" FROM "Orders" AS "o0" WHERE "c"."CustomerID" =
    "o0"."CustomerID" ORDER BY ... FETCH FIRST 1 ROWS ONLY) = "o"."OrderID" ...) ON TRUE WHERE "c"."CustomerID" LIKE 'F%'`
    (`NorthwindAggregateOperatorsQuery...Multiple_collection_navigation_with_FirstOrDefault_chained`) ran past 5 minutes;
    other databases answer at once. Likely each subquery is re-evaluated per row of the lateral subquery.
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
