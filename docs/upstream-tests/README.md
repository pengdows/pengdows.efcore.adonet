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

## EF's specification tests

`spec-bases.tsv` lists every EF Core 10.0.11 specification base at least one upstream provider runs
(`tools/spec-bases.py`): 224 bases, 8,727 test methods. All five run 142 of them (6,225 methods); the other 82 bases
(2,502 methods) only some do. Every one is generated for all six databases, not only the ones all five share; a
database skips a test only for a capability it lacks, with the reason.

The generator lists what it can't generate yet, so nothing is dropped silently: the precompiled-query bases
(`PrecompiledQueryRelational`, `PrecompiledSqlPregenerationQueryRelational`) and `TwoDatabases` need fixtures, and
`LoggingRelational`, `MaterializationInterception`, `OptimisticConcurrencyRelational` and `RelationalType` take type
parameters a hand-written class supplies.

Regenerate:

    python3 -I tools/spec-bases.py <efcore@v10.0.11> <efcore.pg@v10.0.3> <pomelo> <duckdb-ef> <dir>
    dotnet run --project tools/specreflect -- <dir>/union.json <dir>/reflected.json
    python3 -I tools/genspec.py <dir>/reflected.json test/pengdows.efcore.adonet.Specification.Tests

SQLite, PostgreSQL and SQL Server have one open-source EF provider each. MySQL and DuckDB have more; the behaviour
catalogue below reads them too.

## What the tests do, whatever they are called

`TESTS.tsv` describes every upstream test a provider wrote itself (not its overrides of EF's specification tests, which
already match across providers by name): 5,084 tests from eight implementations. Each row says what the test does and
asserts, in one fresh sentence (never copied from the source), and names its behaviour: a provider-neutral key,
`area.feature.behaviour`, given to every test that checks the same thing, whatever the test is called and whichever
provider wrote it. `BEHAVIOURS.tsv` has one row per behaviour: the implementations that test it, how many tests do, a
description, whether it exists only because of one database's own feature, and any EF specification test confirmed to
check the same thing.

| Implementation | Source | Own tests |
|---|---|---:|
| `sqlserver` | dotnet/efcore v10.0.11 | 1,663 |
| `postgresql` | npgsql/efcore.pg v10.0.3 | 1,551 |
| `mysql-pomelo` | PomeloFoundation 9.0.1 (14c2cd5) | 675 |
| `duckdb-skuirrels` | skuirrels/DuckDB.EFCoreProvider f49a8c3 | 663 |
| `sqlite` | dotnet/efcore v10.0.11 | 319 |
| `mysql-oracle` | mysql/mysql-connector-net 26.10.0, `EFCore/tests` (GPL: described, never copied) | 141 |
| `duckdb-energyexemplar` | EnergyExemplar/EnergyExemplar.EFCore.Duckdb.Extensions 2fc1843 | 60 |
| `duckdb-dbalashov` | DBalashov/EntityFrameworkCore.DuckDB e9ee608 | 12 |

(The SQL Server count here also takes in its HierarchyId and NetTopologySuite test projects; miserix13's
WebNet.EntityFrameworkCore.DuckDB, 19713b5, has no tests.)

The 5,084 tests check 3,742 behaviours. 429 behaviours are tested by more than one implementation (1,350 tests); the
other 3,313 by one, though 1,551 of those are general (any relational database could have them), so they are candidates
for the common suite all six databases run. 1,800 behaviours exist only because of one database's own feature (a
PostgreSQL range, a SQL Server temporal table, DuckDB's tiered storage). 1,342 tests repeat a behaviour already
tested, by the same provider or another.

| Area | Tests | Behaviours | Shared by 2+ implementations |
|---|---:|---:|---:|
| provider-types | 821 | 692 | 18 |
| migrations-sql | 572 | 500 | 25 |
| query-functions | 561 | 461 | 34 |
| json | 293 | 171 | 49 |
| other (mostly "every base test is overridden" checks) | 288 | 19 | 1 |
| scaffolding | 284 | 166 | 62 |
| type-mapping | 251 | 183 | 41 |
| model-conventions | 241 | 206 | 23 |
| options-and-facade | 238 | 165 | 36 |
| value-generation | 220 | 137 | 44 |
| collections-arrays | 209 | 139 | 5 |
| query-operators | 175 | 159 | 2 |
| full-text-search | 174 | 152 | 0 |
| update-pipeline | 131 | 116 | 11 |
| design-time-codegen | 129 | 99 | 20 |
| database-creation | 88 | 51 | 16 |
| sql-literals | 86 | 60 | 17 |
| connection | 85 | 72 | 9 |
| bulk-operations | 41 | 38 | 0 |
| logging-diagnostics | 40 | 38 | 1 |
| transactions-concurrency | 39 | 22 | 5 |
| raw-sql | 38 | 37 | 0 |
| spatial | 33 | 29 | 0 |
| execution-strategy | 27 | 17 | 9 |
| migrations-runtime | 20 | 13 | 1 |

How it was made (2026-10-26): every test body was read from the sources above. A token comparison (the calls, members,
SQL words and literals each body uses, provider names removed, each class's shared scaffolding discounted) proposed
candidate twins across implementations and against EF's 9,371 specification tests. Readers then described each test from
its body, gave it a behaviour key, and confirmed or rejected each candidate; the keys were then merged per area wherever
two named the same behaviour. A confirmed twin always shares its partner's key. Limits:

- An EF specification test is named as an equivalent only where the comparison proposed it (22 behaviours); a
  provider's test can still repeat a specification test under another name unnoticed.
- Some long bodies were cut at 5,000 characters, and a few descriptions (parts of SQL Server's type-mapping-source and
  model-differ tests, DuckDB's tiered-storage and upsert tests) rest on the test name and the visible part.
- 46 Pomelo type-mapping tests sit in a commented-out block (ports of Npgsql's, never compiled); they are listed with
  `active` = no.
- Upstream names that don't match their bodies are keyed by what the body does: Pomelo's `*_invariant` and `*_current`
  string-comparison tests use each other's culture; Npgsql's `PadRight_char_with_constant` passes no character, its
  NodaTime `Period` `Milliseconds_is_not_translated` and `FromTicks_is_not_translated` use nanoseconds, and its
  `TimestampTranslationsTest.TimeOnly_ToTimeSpan` has `TimeOnly_FromDateTime_with_timestamptz`'s body.

Regenerate a file with, for example:

    python3 -I tools/upstream-inventory.py "Pomelo.EntityFrameworkCore.MySql 9.0.1" <pomelo-checkout> docs/upstream-tests/mysql-pomelo.tsv \
        test/EFCore.MySql.FunctionalTests test/EFCore.MySql.Tests test/EFCore.MySql.IntegrationTests
