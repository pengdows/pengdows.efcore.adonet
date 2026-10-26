# Upstream provider tests

The denominator for this repository's goal: all six databases pass the equivalent of every test the upstream EF
providers run, rewritten to the unified API where needed, without duplicates, and common to all six wherever the
behaviour applies.

Each `.tsv` lists one provider's test methods, read from its source by `tools/upstream-inventory.py` (theory rows not
expanded; nothing run):

| File | Provider | Source | Methods | Its own | Overriding EF specification tests |
|---|---|---|---:|---:|---:|
| `sqlserver.tsv` | Microsoft.EntityFrameworkCore.SqlServer | dotnet/efcore v10.0.11 | 13,837 | 1,539 | 12,298 |
| `postgresql.tsv` | Npgsql.EntityFrameworkCore.PostgreSQL | npgsql/efcore.pg v10.0.3 | 4,394 | 1,550 | 2,844 |
| `mysql-pomelo.tsv` | Pomelo.EntityFrameworkCore.MySql | PomeloFoundation 9.0.1 (14c2cd5) | 3,763 | 674 | 3,089 |
| `duckdb-skuirrels.tsv` | DuckDB.EFCoreProvider | skuirrels f49a8c3 | 5,109 | 662 | 4,447 |
| `sqlite.tsv` | Microsoft.EntityFrameworkCore.Sqlite | dotnet/efcore v10.0.11 | 2,799 | 310 | 2,489 |

The providers' own tests total 4,735 methods under 3,754 distinct names (339 names appear in more than one provider).
"Overriding" rows replace an inherited EF specification test, usually to assert the provider's SQL; the behaviour itself
is EF's, run here through the generated specification suite (`tools/genspec.py`).

Other implementations to add: Oracle's MySql.EntityFrameworkCore (GPL: its tests may be read for behaviours, never
copied), and the DuckDB.EFCore and EnergyExemplar.EntityFrameworkCore.DuckDb packages if their sources are public.
SQLite, PostgreSQL and SQL Server have one open-source EF provider each.

Regenerate a file with, for example:

    python3 -I tools/upstream-inventory.py "Pomelo.EntityFrameworkCore.MySql 9.0.1" <pomelo-checkout> docs/upstream-tests/mysql-pomelo.tsv \
        test/EFCore.MySql.FunctionalTests test/EFCore.MySql.Tests test/EFCore.MySql.IntegrationTests
