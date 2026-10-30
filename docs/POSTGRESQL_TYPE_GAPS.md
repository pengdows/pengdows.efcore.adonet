# PostgreSQL type gaps and where each belongs

Found by the shared EF Core 10 conformance suite against PostgreSQL 17 (`pengdows.efcore.conformance`, 1,274 cases; each native
type is a probe case). Counts are the pengdows-only failures against the Npgsql reference, as of 2026-10-30.

**Placement rule (maintainer, 2026-10-30):** a type with a generic meaning in SQL (shared or standard across databases) goes in
the core (`pengdows.efcore.adonet`). A type only one database has goes in that database's package
(`pengdows.efcore.adonet.postgresql`), not the core.

| Gap | Cases | Placement | Why |
|---|---:|---|---|
| Geometric types `point`, `lseg`, `path`, `box`, `line`, `circle`, `polygon`, and their arrays | 8 or 6 each | PostgreSQL package | PostgreSQL-only. The core's `Geometry` is a different, spatial type and can't hold these losslessly. |
| `tsvector`, `tsquery`, and their arrays | 8 or 6 each | PostgreSQL package | PostgreSQL-only text-search types. |
| `oid`, `regtype`, `regconfig` (read as `uint`), `tid`, `cid` | 6, 6, 4, 8, 6 | PostgreSQL package | PostgreSQL-only. Reads currently go through `Int64`, which Npgsql refuses for these. |
| `xid8`, `jsonpath` | 8, 6 | PostgreSQL package | PostgreSQL-only. Already bound by name (`PostgreSqlNamedTypeMapping`); still needs the reads and the cases to pass. |
| Multiranges `int4multirange`, `int8multirange`, `nummultirange`, `datemultirange`, `tsmultirange`, `tstzmultirange` | 6 each | PostgreSQL package | PostgreSQL 14+ only. |
| Arrays of ranges (`int4range[]` and the other scalar range arrays) | 6 each | Split | The array binding is generic, so it belongs in the core. The range element mapping is PostgreSQL-only, so it stays in the PostgreSQL package. EF's model finalization needs that element mapping. |
| `cidr[]` | 6 | Split | Same as the range arrays: the array is generic, the `cidr` element is PostgreSQL-only. |
| `bit varying` (`varbit`) and its arrays | 6 each | Core | Standard SQL (`BIT VARYING`, SQL:1992). DuckDB has `bitstring`, and the core already round-trips `BitArray` on every database. |

Types the core already has but the PostgreSQL package still fails on, from the pengdows.crud type catalogue: `bit`, `pg_lsn`,
`timetz`, `money`, the scalar ranges, `cidr`, `inet`, `macaddr`/`macaddr8`. The PostgreSQL package copies its binding rules from the
core for these, not from another source.

**Done here:** `macaddr`, `macaddr8`, `cidr`, `xid8`, and `jsonpath` are bound by name in
`PostgreSqlProviderTypeMappings.cs` (`PostgreSqlNamedTypeMapping`), taken from pengdows.crud's binding rules. That cleared the 34
write failures for these types.

## Status (2026-10-30)

Conformance against PostgreSQL 17 (1,274 cases; `pengdows.efcore.conformance`, reference Npgsql 10.0.3): pengdows-only failures
went from 305 to 3, and all 3 are `57P01 terminating connection` from the forced drop (`WITH (FORCE)`), not from any type.
Upstream-only failures: 4 (the two right-join cases, which pengdows now passes and Npgsql fails; and two `uint` parameter
queries Npgsql fails).

Done, in the provider:
- Named types, bound as their CLR value with the parameter typed as the column (`PostgreSqlNamedTypeMapping`): `macaddr`,
  `macaddr8`, `cidr`, `xid8`, `jsonpath`, the geometric types and their arrays, `tsvector`, `tsquery`, `tid`, `pg_lsn`, `oid`,
  `regtype`, `regconfig`, `cid`, `bit`, `bit varying` and their arrays, `money`, `time with time zone`, and the six multiranges.
- Default store type for a CLR type with exactly one PostgreSQL type, used when the model names none (a collection's element).
- Literals for the named types: quoted and cast to the store type; bit strings as `B'...'`; `inet` quoted.
- Core: a collection the provider maps as its own array type no longer needs the JSON text mapping; its element is mapped under
  the collection's store type when EF gives none.

**Closed (2026-10-30):** the three `57P01` failures. The forced drop ends sessions the pool still holds, and the next connection
taken from that pool failed on its first command. The core now clears the pool for the dropped database's connection string after
the drop as well as before (`AdoNetDatabaseCreator.ClearPoolAfterDrop`), for every server provider. Column comments (`COMMENT ON
COLUMN`) are now emitted for create, add and alter column on PostgreSQL, the case that had been hidden behind the `57P01`.

Result: pengdows-only failures 0; upstream-only 4 (two right-join cases, which pengdows passes; two `uint` parameter queries,
which Npgsql fails).
