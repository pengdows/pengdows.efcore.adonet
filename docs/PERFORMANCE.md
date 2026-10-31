# Performance measurements behind the code review's P2 findings

The code review asks for measurement before optimization. Each finding below records what was measured, on which build, and
the decision it led to. Figures come from Debug builds of the core test project (the test project's default configuration)
on Linux, so read them as relative costs, not as release-build throughput.

## Bulk insert materializes the input more than once

Measured with `BulkInsert` against fakeDb, which records each command and runs no server:

| Rows  | Allocated bytes | Per row | Elapsed |
|------:|----------------:|--------:|--------:|
| 2,000 | 57,088,616      | 28,544  | 726 ms  |
| 20,000| 511,117,208     | 25,555  | 1,229 ms|

Per-row allocation is nearly flat as the input grows, so the cost is linear in rows and dominated by per-row command work, not
by the list copies. The copies themselves are small by comparison: the entity list holds references, and each row adds one
`object[]` of its column values, a few hundred bytes at most.

**Decision:** no change on the fakeDb path; the cost there is per-row command work.

SQL Server's native loader (`SqlBulkCopy`) is measured on a real server: about 875 bytes per row at 20,000 rows, 862 at
100,000, with the one-off setup dominating at 2,000 rows. The loader used to build one `DataTable` for every row. It now
writes 10,000-row batches over the same copy, so only one batch's table is alive at a time. Total allocation per row is
unchanged (the batches are allocated in turn), so the saving is peak live memory, which this measurement does not capture.
Covered by `SqlServerBulkBatchingTests`, which checks every row arrives, past the batch boundary.

## The data-source cache grows with connection strings

The cache holds one `DbDataSource` per (factory, connection string) pair, and one augmented connection string per pair. A
test measures the growth: 500 distinct strings give 500 retained entries, reuse keeps the count, disposal releases every
data source, and eviction removes exactly one (`AdoNetDataSourcesTests`).

**Decision:** no automatic eviction. A data source may back an open connection, so evicting on age or size needs an ownership
rule first. The lifetime and the growth are documented on `AdoNetDataSources`. An application with dynamically built
connection strings should dispose its service provider, or evict, when a database is retired.

## JSON binding parses the document and reflects the driver's type

Measured by binding a 100-byte JSON document through the JSON string mapping, against a plain string (200,000 calls each):

| Value              | Per call | Allocated per call |
|--------------------|---------:|-------------------:|
| JSON document      | 2,943 ns | 168 bytes          |
| Plain string       | 192 ns   | 96 bytes           |

The extra cost is the document check (`JsonDocument.Parse`) on each JSON-typed string. The PostgreSQL driver-type lookup is
a reflection call per parameter on top of that; it was not measured separately.

**Decision:** no change. About 3 µs per JSON parameter is small beside a database round trip. Removing the parse would need
the mapping to know the value came from a JSON source, a change in the shared binding code that the measurement does not
justify.
