# local-packages

A restore source for packages built from the sibling pengdows.crud repository that are not on nuget.org yet:
`pengdows.stormgate` and `pengdows.stormgate.EntityFrameworkCore` 2.0.6 (MIT). The specification tests use them to cap the
connections one run opens against a server with a fixed connection limit.

Why vendored: nuget.org has `pengdows.stormgate` and `pengdows.stormgate.EntityFrameworkCore` only as `2.0.6-rc1` through
`2.0.6-rc6`, not the final `2.0.6` these packages are. Checked 2026-10-31.

`nuget.config` maps only `pengdows.stormgate*` to this folder. When the packages are published, delete this folder and the
mapping, and nothing else changes.

## DuckDB.NET

`DuckDB.NET.Data` and `DuckDB.NET.Bindings` at `0.0.0-local.4240e4d`, packed from Giorgi's `Giorgi-DuckDB.NET` develop checkout
(commit `4240e4d`). The DuckDB plugin uses them until upstream publishes a release that reports `DataSourceInformation` and
`DataTypes`. The native `libduckdb` still comes from `DuckDB.NET.Bindings.Full` on nuget.org; its managed assembly is excluded so
the checkout's build is the one that runs. `nuget.config` maps only these two names to this folder. To rebuild: pack
`DuckDB.NET.Bindings/Bindings.csproj` and `DuckDB.NET.Data/Data.csproj` from the checkout with `-p:Version=0.0.0-local.<commit>`.

