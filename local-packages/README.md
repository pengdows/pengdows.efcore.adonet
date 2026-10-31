# local-packages

A restore source for packages built from the sibling pengdows.crud repository that are not on nuget.org yet:
`pengdows.stormgate` and `pengdows.stormgate.EntityFrameworkCore` 2.0.6 (MIT). The specification tests use them to cap the
connections one run opens against a server with a fixed connection limit.

Why vendored: nuget.org has `pengdows.stormgate` and `pengdows.stormgate.EntityFrameworkCore` only as `2.0.6-rc1` through
`2.0.6-rc6`, not the final `2.0.6` these packages are. Checked 2026-10-31.

`nuget.config` maps only `pengdows.stormgate*` to this folder. When the packages are published, delete this folder and the
mapping, and nothing else changes.
