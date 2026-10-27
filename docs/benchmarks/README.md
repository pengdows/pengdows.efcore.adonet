# MySQL benchmark protocol

The provider comparison workload is intentionally the same workload used by the correctness test. Run the
BenchmarkDotNet project in Release mode against MySQL 8.4:

```text
dotnet run -c Release --project test/pengdows.efcore.adonet.mysql.Benchmarks
```

The baseline is Oracle `MySql.EntityFrameworkCore` 10.0.12 on EF Core 10. MySqlConnector and MySql.Data are measured
as separate pengdows benchmarks. BenchmarkDotNet controls warmup, iteration count, process isolation, and reports
variance. The Pomelo console project is deliberately separate and is labeled Pomelo 8.0.2 / EF Core 8 / net8.0;
its numbers must not be compared as if it used the same EF version or process.

Commit the generated BenchmarkDotNet summary only with the machine, server, package versions, and date recorded. A
single stopwatch inside an xUnit test is a smoke check, not evidence for a speed claim.
