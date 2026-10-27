# MySQL benchmark protocol

The provider comparison workload is intentionally the same workload used by the correctness test. Run the
BenchmarkDotNet project in Release mode against MySQL 8.4:

```text
dotnet run -c Release --project test/pengdows.efcore.adonet.mysql.Benchmarks
```

The four measured stacks are Oracle `MySql.EntityFrameworkCore` 10.0.12 with `MySql.Data`, pengdows EF with
`MySqlConnector`, pengdows EF with `MySql.Data`, and Pomelo EF with `MySqlConnector`. Pomelo is built from the local checkout at
`../Pomelo.EntityFrameworkCore.MySql` (EF Core 10 / net10.0). All four lanes use the same process protocol,
MySQL 8.4 container, schema, seed, queries, driver family where applicable, and workload. BenchmarkDotNet controls
warmup, iteration count, process isolation, memory diagnostics, and reports variance.

The older `mysql.PomeloComparison` console project remains a historical EF 8 lane and must not be used for current
performance claims.

Commit the generated BenchmarkDotNet summary only with the machine, server, package versions, and date recorded. A
single stopwatch inside an xUnit test is a smoke check, not evidence for a speed claim.
