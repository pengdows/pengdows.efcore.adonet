# Upstream bug matrix

Bugs reported against the upstream EF providers (and EF Core itself) are behavioural requirements their test suites
missed. Each is reproduced here as a test on every database it applies to, and classified:

- **Avoided**: the reproduction passes here, while the reported upstream version fails it.
- **Inherited**: it fails here too, usually because the bug is in EF Core above the provider's extension points.
- **Not applicable**: the behaviour belongs to another engine, feature or provider.
- **To reproduce**: listed, not yet tested.

Each row: the issue (title and state as of 2026-10-26, checked with `gh`), the layer it touches, and the result. A test
proving a row names the row's issue in its doc comment.

| Issue | Title | State | Layer | Result |
|---|---|---|---|---|
| [dotnet/efcore#39206](https://github.com/dotnet/efcore/issues/39206) | Split query + projection with Skip/Take: paging subquery has no key ordering, child collections come back empty | open | query (split queries) | to reproduce |
| [dotnet/efcore#39327](https://github.com/dotnet/efcore/issues/39327) | Connection open count survives the reset of a pooled DbContext | open | connection lifecycle | to reproduce |
| [dotnet/efcore#39222](https://github.com/dotnet/efcore/issues/39222) | Invalid translation for constant negative TimeSpan (SQL Server) | closed | literals | to reproduce |
| [dotnet/efcore#39328](https://github.com/dotnet/efcore/issues/39328) | SQLite: JSON property names that need quoting give a path that never matches | closed | JSON paths | to reproduce |
| [dotnet/efcore#39329](https://github.com/dotnet/efcore/issues/39329) | Collection `Contains` normalization rewrites any `Contains` overload and guesses the element type | open | query (EF Core) | to reproduce |
| [dotnet/efcore#39333](https://github.com/dotnet/efcore/issues/39333) | SQL Server: quadratic parameter-name generation across split queries | open | query performance | to reproduce |
| [dotnet/efcore#39291](https://github.com/dotnet/efcore/issues/39291) | Compiled model writes `NullableValueComparer<TComparer>` (CS0453) | open | compiled models (EF Core) | to reproduce |
| [Pomelo#2045](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2045) | HasColumnType("boolean") generates invalid boolean(1) in migrations | open | type mapping, migrations | **avoided** (MySQL): `MySqlNamedColumnTypeTests` |
| [Pomelo#2029](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2029) | Migration adding a [Timestamp] concurrency column fails | open | migrations | waits on row-version support (EF's OptimisticConcurrency bases, not yet generated) |
| [Pomelo#2028](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2028) | Collation ignored in migration | open | migrations | to reproduce |
| [Pomelo#2004](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2004) | SET AUTOCOMMIT = 1 causes session pinning with RDS Proxy | open | connection/session | to reproduce |
| [npgsql/efcore.pg#4084](https://github.com/npgsql/efcore.pg/issues/4084) | Translations declare the wrong nullability, so `!=`, `!(==)`, `== null` return the wrong rows | open | expression nullability | to reproduce |
| [npgsql/efcore.pg#4095](https://github.com/npgsql/efcore.pg/issues/4095) | Numeric casts round instead of truncating; `(byte)` doesn't wrap; `BigInteger` division | open | numeric conversion | to reproduce |
| [npgsql/efcore.pg#4090](https://github.com/npgsql/efcore.pg/issues/4090) | `.Second` rounds, `TimeOnly.IsBetween` across midnight, `TimeOnly - TimeOnly` | open | temporal translation | to reproduce |
| [npgsql/efcore.pg#4086](https://github.com/npgsql/efcore.pg/issues/4086) | Negative TimeSpan constants with a day part; float constants lose their type | open | literals | to reproduce |
| [npgsql/efcore.pg#4096](https://github.com/npgsql/efcore.pg/issues/4096) | Text conversions: char space lost, `Convert.ToString(bool)`, `string.Join` null separator | open | string translation | to reproduce |
| [npgsql/efcore.pg#4098](https://github.com/npgsql/efcore.pg/issues/4098) | Array `Skip`/`Take` chains, `int.MaxValue` bounds, `Intersect().Any()` with NULLs | open | collections | to reproduce |
| [npgsql/efcore.pg#4097](https://github.com/npgsql/efcore.pg/issues/4097) | `ExecuteUpdate` copying an out-of-range JSON array element nulls the column | open | JSON update | to reproduce |
| [npgsql/efcore.pg#4099](https://github.com/npgsql/efcore.pg/issues/4099) | `Contains` on a JSON primitive collection compares JSON text; DateTime/TimeOnly never match | open | JSON collections | to reproduce |

Engine limitations are recorded separately from provider bugs (UPSTREAM_ISSUES.md and the dialects' capabilities): a
provider bug is something to avoid; an engine limitation is something to report accurately. skuirrels' DuckDB provider
keeps such a map: https://github.com/skuirrels/DuckDB.EFCoreProvider/blob/main/docs/CAPABILITY-MAP.md
