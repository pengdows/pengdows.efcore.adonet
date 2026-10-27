# FlatFile: specification-suite boundaries

The EF Core specification suite is evaluated against `pengdows.flatfile` as an ISO SQL provider. Vendor syntax is not added
to make a test pass. The original 14,016-test run below is historical and must not be presented as current parity evidence.
The current engine source passes 2,373 provider tests per target framework (12 authorization skips) and 2,018/2,027 SQL
conformance cases per target framework. The adapter consumes the locally built `pengdows.flatfile` `0.2.1-preview.16`
package while that release is being published.

## 0. Engine backlog status

The engine-side items listed in the earlier audit are fixed and covered by regression tests. The adapter pin is now
`0.2.1-preview.16`, and the obsolete `KnownEngineGaps` entries were removed. Keep the remaining entries below as explicit
ISO/provider boundaries, not as requests to add vendor SQL.

## 1. Engine work still needed

None from the historical table has been reproduced against the current engine build. Re-open an item only with a focused
reproduction and a regression test.

| Failures | Problem | Where | ISO expectation |
|---:|---|---|---|
| 170 | `Expected token 'Select' ... found 'With'`: `WITH` inside a derived table | `pengdows.sql/SqlParser.cs:5938` | A table subquery is a query expression, which may have a `WITH` clause |
| 40 | `Unsupported predicate expression 'BoundScalarSubqueryExpression'` | `BoundPredicateEvaluator.cs:143` | A scalar subquery is a value expression and valid in a predicate |
| 48 | `ORDER BY ... must appear in the grouped projection` | `SqlBinder.cs:846`, `BoundSelectExecutor.cs:889` | A grouped query's sort key may be any expression of grouping columns and aggregates |
| 8 | `ORDER BY only supports column references` | `SqlBinder.cs:2135` | A sort key is any value expression |
| 4 | `Unsupported value expression 'BoundAggregateExpression'` | `BoundPredicateEvaluator.cs:288` | An aggregate is a value expression |
| 28 | `No row is available for table 'Gears'` | `BoundPredicateEvaluator.cs:473` | An outer reference in a correlated or group-join `DefaultIfEmpty` shape cannot resolve its row |
| 16 | `Table alias or name 'u' was not found` | `SqlBinder.cs:2939` | A nested correlated subquery sees every enclosing scope's aliases |
| 20 | `Column 'CountryId' was not found on table 'Countries'` | `SqlBinder.cs:2856` | Column resolution in bulk updates through an inheritance hierarchy |
| 6 | `Unexpected token 'EXISTS'` in an `ORDER BY` subquery | `SqlParser.cs:4438` | `EXISTS` is a valid predicate in a `CASE` sort key |
| 4 | `Expected a SQL statement ... found 'LeftParen'` | `SqlParser.cs:194` | A query primary may be parenthesized: `(SELECT ...) UNION (SELECT ...)` |

## 2. Provider-side work (this repository)

- **JSON (625).** `FlatFileDialect` sets no `Json`; the core has `JsonSyntax.Iso`. The engine has `JSON_VALUE`,
  `JSON_QUERY`, `JSON_EXISTS` and `JSON_TABLE`. Find out whether it has a `JSON` column type, then wire up the dialect. ISO
  has no `JSON_SET`, so partial JSON updates need a decision.
- **Generated-value readback (224).** ISO has no `RETURNING`, but it has feature T495, data change delta tables:
  `SELECT ... FROM FINAL TABLE (INSERT INTO ...)`. The engine already parses and executes them (`DataChangeDeltaTable` in
  `pengdows.sql/SqlAst.cs`, `ExecuteDataChangeDeltaTable` in `DefaultFlatFileQueryExecutor.cs`). Use that to read generated
  values back, not a second select. Matching returned rows to the entities of a batched insert is the correlation problem
  that the [pengdows/sql-spec INCLUDE proposal](https://github.com/pengdows/sql-spec/blob/main/proposal.md) addresses;
  treat that proposal as an explicit Pengdows extension until the standards-alignment research is complete. Until then, key
  the match on the primary key or insert one row per statement.
- **`@name` parameters (about 34).** The engine rejects `@` as non-ISO, correctly. The failing SqlExecutor and SqlQuery
  tests hardcode SQL Server syntax; override them in `Overrides/FlatFile/` with `:name`.
- **Stored procedures (62).** The engine has ISO SQL procedures with `CALL`, but no `OUT`/`INOUT` parameters, which the
  `StoredProcedureUpdate` tests need. `FlatFileHost` has no procedures either.
- **Ambient transactions (12).** Unsupported by design in the engine; skip by capability.
- **LINQ translation gaps (about 100).** Map `TimeOnly.Nanosecond`, string functions and the like to the engine's ISO scalar
  functions where it has them.
- **`OFFSET requires a non-negative integer literal or parameter marker` (11).** ISO `OFFSET` takes a literal or a
  parameter only; EF's column-based offset needs rewriting in the translator. The message text is not in the engine source.
- **Spatial (173).** SQL/MM is not ISO SQL; a documented skip.

## 3. To investigate

- **The hang (confirmed).** Both the parallel run and a run with parallelism off stopped at exactly 14,016 tests, in
  `NorthwindBulkUpdatesFlatFileTest.Delete_Where_predicate_with_GroupBy_aggregate_2`, with the test host at 99% CPU until
  the 3-minute hang timeout killed it. Parallelism makes no difference, so it is not writer contention: a `DELETE` whose
  predicate contains a grouped aggregate loops in the engine. Reproduce it against the engine directly. Until it is
  fixed, the rest of the suite (the tests after it) cannot be measured; skip that test with a `[SKIP:FlatFile]` reason to
  get a full run.
- **`Column 'X' contains null` (24)**, `FlatFileDataReader.cs:385`: join semantics or the reader contract.
- **Write-lock timeouts (10)**, `ConnectionWriteLock.cs:130`: a connection we leave open, or a lock taken at open.
- **535 value mismatches.** Not characterized. Null ordering was the first guess, and it is only a small part: with
  `ReverseNullOrdering()` on the FlatFile host, 8 of the 148 tests that had failed in `TPCManyToManyNoTrackingQuery` and
  `TPTManyToManyQuery` now pass and 140 still fail. The option stays on for the host; the real cause is elsewhere.
