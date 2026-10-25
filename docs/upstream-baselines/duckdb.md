# DuckDB.EFCoreProvider (skuirrels fork, EF Core 10) — provider-specific API inventory

Source: `providers/duckdb-ef/src/DuckDB.EFCoreProvider` (+ `.NTS`), docs `docs/*.md`, README.
Scope: only what plain relational EF Core does not already provide. Namespaces: public extensions live in
`DuckDB.EFCoreProvider.Extensions` (note: `UseAutoIncrement()` is *not* in `Microsoft.EntityFrameworkCore`; roadmap says it moves next major).
Markers: **[Experimental]** = documented as experimental; **[Internal]** = not public API. No `[Obsolete]` members exist.
`[Experimental("EF9100")]` appears only on internal precompiled-query plumbing (`DuckDBQueryCompilationContext*`), inherited from EF.

Three backend "profiles" share one options builder: native DuckDB file/in-memory, **DuckLake** (attached lakehouse catalog: Parquet data + SQL metadata DB), and **Quack** **[Experimental]** (DuckDB 1.5 remote protocol). A fourth, encrypted-file, is a variant of native.

---

## 1. Options builder

Entry points on `DbContextOptionsBuilder` (+ generic `<TContext>` twins):

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseDuckDB` | `(Action<DuckDBDbContextOptionsBuilder>?)`, `(string? connectionString, …)`, `(DbConnection, …)`, `(DbConnection, bool contextOwnsConnection, …)` | Provider selection | Standard relational wiring over DuckDB.NET | |
| `UseDuckLake` | `(string metadataPath, Action<DuckLakeDbContextOptionsBuilder>?, Action<DuckDBDbContextOptionsBuilder>?)`, `(Action<DuckLake…> , …)` | Lakehouse catalog backend | Opens in-memory host, `INSTALL/LOAD ducklake`, `ATTACH 'ducklake:…' AS cat (…)`, `USE cat` | Disables migrations, store generation, physical constraints, SaveChanges batching, tiered storage |
| `UseEncryptedDuckDB` | `(string path, Func<string> keyProvider, Action<DuckDBEncryptedDatabaseOptionsBuilder>?, Action<DuckDBDbContextOptionsBuilder>?)` | Encryption at rest | In-memory host (`:memory:?cache=shared`) + `ATTACH path AS alias (ENCRYPTION_KEY …)` executed outside EF logging; key fingerprint-verified on reuse | AES-256-GCM DB + WAL; not combinable with DuckLake/Quack |
| `UseQuack` | `(string endpoint, string token, Action<QuackDbContextOptionsBuilder>?, Action<DuckDBDbContextOptionsBuilder>?)` | Remote server connection | Client `quack` extension; private local catalog for remote session | **[Experimental]** (DuckDB 1.5.x) |
| `AddDuckDB<TContext>` / `AddEntityFrameworkDuckDB` | `IServiceCollection` | DI registration | | |
| `UseNetTopologySuite` (NTS pkg) | `DuckDBDbContextOptionsBuilder` ext | Spatial types | `INSTALL spatial; LOAD spatial;` on open | `AddEntityFrameworkDuckDBNetTopologySuite` for DI |

`DuckDBDbContextOptionsBuilder` (the `UseDuckDB` action builder; inherits relational `CommandTimeout`, `MaxBatchSize`, `MigrationsAssembly`, etc.):

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseDuckLake` | `(string metadataPath, Action<DuckLakeDbContextOptionsBuilder>?)` / `(Action<…>)` | Lakehouse catalog backend | as above | |
| `UseQuack` | `(string endpoint, string token, Action<QuackDbContextOptionsBuilder>?)` | Remote server connection | token ≥ 4 chars | **[Experimental]** |
| `UseEncryptedDatabase` | `(string path, Func<string> keyProvider, Action<DuckDBEncryptedDatabaseOptionsBuilder>?)` | Encryption at rest | see above | key never reaches EF logs/interceptors |
| `UseCaseInsensitiveStringSearches` | `(bool enable = true)` | Case-insensitive string matching (per context) | `StartsWith/Contains/EndsWith` → `COALESCE(ilike_escape(x, pattern, '$'), false)` with escaped pattern | Off by default (default = case-sensitive `starts_with/contains/ends_with`) |
| `EnableBulkInsertBatching` | `(bool = true)` | Multi-row INSERT batching in SaveChanges | consecutive inserts → one `INSERT … VALUES (..),(..)` | Atomic per run even without transaction; off by default |
| `EnableBulkUpdateBatching` | `(bool = true)` | Multi-row UPDATE batching | `UPDATE … FROM (VALUES …)` for PK-only WHERE, no read-back | Concurrency-token / computed rows fall back |
| `EnableBulkDeleteBatching` | `(bool = true)` | Multi-row DELETE batching | `DELETE … WHERE key IN (…)` / composite `DELETE … USING (VALUES …)` | |
| `MemoryLimit` | `(string "4GB"/"75%")` | Engine memory cap | `SET memory_limit = '…'` on open | instance-global |
| `Threads` | `(int)` | Engine parallelism | `SET threads = n` | instance-global |
| `CheckpointThreshold` | `(string)` | WAL checkpoint threshold | `SET checkpoint_threshold = '…'` | instance-global |
| `FileSearchPath` | `(string)` | Base dir for relative external-file paths | `SET file_search_path = '…'` | used by `FromParquet` etc. |
| `MigrationLockTimeout` | `(TimeSpan)` | Migration lock wait timeout | default 5 min; `Timeout.InfiniteTimeSpan` allowed | `TimeoutException` explains `DELETE FROM "__EFMigrationsLock"` |
| `EnableMigrationTableRebuilds` | `(bool = true)` | Table-rebuild migrations for unsupported ALTERs | copy → recreate → copy back → reindex | off by default |
| `LoadExtension` | `(string name)`, `(string name, DuckDBExtensionLoadMode)` | Engine extension/plugin loading | `INSTALL x; LOAD x;` / `LOAD x;` / none on every provider-owned connection | name validated `[A-Za-z][A-Za-z0-9_]*` |
| `ConfigureConnection` | `(Action<DuckDBConnection>)` | Per-connection init hook (secrets) | runs after extensions; not logged by EF | for `CREATE SECRET` |
| `ReverseNullOrdering` | `(bool)` | NULL sort order emulation | appends `NULLS FIRST` (asc) / `NULLS LAST` (desc) | **[Internal]** test-only |
| `DuckDBExtensionLoadMode` | enum `InstallAndLoad`, `LoadOnly`, `CallerManaged` | Extension provisioning policy | | `DuckDBExtensionConfiguration(Name, Mode)` record |

`DuckLakeDbContextOptionsBuilder`:

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseLocalMetadata` | `(string metadataPath)` | Catalog metadata location (local) | `ATTACH 'ducklake:path'` | |
| `UseDefaultSecret` | `()` | Credentials via default secret | `ATTACH 'ducklake:'` w/ default `TYPE ducklake` secret | |
| `UseNamedSecret` | `(string secretName)` | Credentials via named secret | `ATTACH 'ducklake:secret_name'` | secret created in `ConfigureConnection` |
| `CatalogName` | `(string)` | Catalog alias | `AS "name"`; `USE "name"` | |
| `DataPath` | `(string dataPath, bool overrideForCurrentConnection = false)` | Data file storage root | `DATA_PATH '…'` (+ `OVERRIDE_DATA_PATH`) | |
| `ReadOnly` | `(bool = true)` | Read-only attachment | `READ_ONLY` | |
| `CreateIfNotExists` | `(bool = true)` | Auto-create catalog | ATTACH option | |
| `AutomaticMigration` | `(bool = true)` | DuckLake catalog format upgrade | `AUTOMATIC_MIGRATION` | not EF migrations |
| `AsOfSnapshot` | `(long snapshotId)` | Catalog-wide time travel (by version) | `SNAPSHOT_VERSION` attach option; read-only context | |
| `AsOfTimestamp` | `(DateTimeOffset)` | Catalog-wide time travel (by time) | `SNAPSHOT_TIME` | |
| `AlsoAttach` | `(string catalogName, string metadataPath, bool readOnly = true)` | Additional attached catalogs | extra `ATTACH`; raw/dynamic SQL only | EF mapping to non-primary catalog is roadmap |
| `AlsoAttachNamedSecret` | `(string catalogName, string secretName, bool readOnly = true)` | Additional catalogs via secret | | |

`QuackDbContextOptionsBuilder` **[Experimental]**: `DisableSsl(bool)`, `CatalogName(string)`, `EnableHttpConnectionCaching(bool)`, `ExtensionLoadMode(DuckDBExtensionLoadMode)`, `ExtensionPath(string)` — remote transport tuning.

`DuckDBEncryptedDatabaseOptionsBuilder`: `CatalogName(string)` (attach alias, default from file name), `ReadOnly(bool)`, `EncryptTemporaryFiles(bool = true)` (`SET temp_file_encryption`, instance-wide).

---

## 2. Model building API

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseAutoIncrement` | `PropertyBuilder` / `PropertyBuilder<T>` → same | Store-generated integer key (identity) | Sets `DuckDB:ValueGenerationStrategy = AutoIncrement`, `ValueGeneratedOnAdd`; migrations emit `CREATE SEQUENCE IF NOT EXISTS "{table}_{col}_seq"` + `DEFAULT nextval('"…"')` | Integral CLR types only (signed + unsigned) |
| `UseAutoIncrement` | `ColumnBuilder` (table-splitting/overrides) → `ColumnBuilder` | Per-table-column identity override | annotation on `IMutableRelationalPropertyOverrides` | |
| `GetValueGenerationStrategy` | `IReadOnlyProperty → DuckDBValueGenerationStrategy` | Read identity strategy | | |
| `SetValueGenerationStrategy` | `IMutableProperty`, `IConventionProperty` (+fromDataAnnotation), `IMutableRelationalPropertyOverrides` | Write identity strategy | | |
| `FromParquet<TEntity>` | `EntityTypeBuilder<T>.FromParquet(string pathOrGlob)` | Entity backed by external file (columnar) | table source becomes `read_parquet('path')` in queries | Query-only; no update pipeline |
| `FromCsv<TEntity>` | `(string pathOrGlob)` | Entity backed by external CSV | `read_csv('path')` (auto-detect) | Query-only |
| `FromJsonFile<TEntity>` | `(string pathOrGlob)` | Entity backed by external JSON file | `read_json('path')` | Query-only |
| `[FromParquet(path)]` / `[FromCsv(path)]` / `[FromJsonFile(path)]` | class attributes (base `DuckDBFileSourceAttribute{Function,Path}`) | Same, by annotation | `DuckDBFileSourceConvention` | |
| `GetDuckDBFileSourceFunction` / `GetDuckDBFileSourcePath` / `GetParquetPath` | `IEntityType → string?` | Read file-source metadata | | |
| `UseStructMapping` | `ComplexPropertyBuilder[<T>]` `()` / `(string? structColumnName)` | Complex type stored as one composite/row-typed column | Column `STRUCT(field TYPE, …)`; reads `"Col"."field"`; whole-struct null check `"Col" IS NULL` | Opt-in; default complex mapping is flattened columns. Also `[UseStructMapping]` attribute. Static non-extension aliases also on `DuckDBPropertyBuilderExtensions` |
| `HasStructFieldName` | `ComplexPropertyBuilder[<T>]`, `ComplexTypePropertyBuilder<T>`, `PropertyBuilder<T>` `(string fieldName)` | Physical field name inside composite column | renames STRUCT leaf/sub-struct | |
| `HasStructField` | `PropertyBuilder<T>` / `ComplexTypePropertyBuilder<T>` `(string structColumnName, params string[] nestedFieldNames)` | Map scalar property to a path inside a composite column | reads `"Col"."a"."b"` | Entity scalars into existing STRUCT (e.g. file sources) |
| `HasStructForeignKey` | `ReferenceCollectionBuilder<P,D>` `(Expression<Func<D,object?>> path)`; `ReferenceCollectionBuilder.HasStructForeignKey<D>(…)`; `ReferenceReferenceBuilder.HasStructForeignKey<D>(…)` | FK stored inside composite column | shadow FK property bound to STRUCT leaf; joins on `"Col"."leaf"` | requires `UseStructMapping` |
| `GetStructMapping` / `SetStructMapping` | `IReadOnlyComplexProperty` / `IConventionComplexProperty` | Read/write composite mapping metadata | `DuckDBStructMapping{StructColumnName, FieldName, Children, Fields}` | |
| `GetStructColumnMap` / `SetStructColumnMap` | `IReadOnlyEntityType` / `IConventionEntityType` | Column→field-path map | `DuckDBStructFieldInfo` | |
| `GetStructFieldInfo` | `IReadOnlyProperty → DuckDBStructFieldInfo?` | Read scalar→struct path | | |
| `ToTieredStore<TRoot>` | `ModelBuilder.ToTieredStore(Expression<Func<TRoot, DateTime / DateTime? / DateOnly / DateOnly?>> timestamp, string archivePath, TierGranularity = Month, string? controlKey = null)` → `TieredStoreBuilder<TRoot>` | Hot/cold data tiering (row archival to columnar files) | Rows older than cutoff → Hive-partitioned Parquet under `archivePath`; union view hot∪cold | Native DuckDB only; null lifecycle stays hot |
| `TieredStoreBuilder.WithReadModel<TReadModel>` | `()` | Read-model entity over tiered union | | |
| `TieredStoreBuilder.WithTieredView` | `(string? viewName = null)` | Provider-managed union view | provider-managed view = hot table UNION ALL cold Parquet (`read_parquet`, Hive partitions) | |
| `TieredStoreBuilder.MatchBy` | `(Expression<…> props, TierMatchKeyUniqueness)` | Archive match key | | `TierMatchKeyUniqueness {Model, ExternallyEnforced}` |
| `TieredStoreBuilder.PartitionBy` | `(params Expression<Func<TRoot,object?>>[])`, `(…, aliases)`, `(Action<TieredPartitionBuilder<TRoot>>)` | Archive partitioning | Hive `key=value/` dirs | |
| `TieredPartitionBuilder.By/ByYear/ByMonth/ByDay` | `(Expression<Func<TRoot,TProp>>[, string alias])` | Partition transforms | `TierPartitionTransform {Value, Year, Month, Day}` | |
| `TieredStoreBuilder.WithoutHotChildFilter` | `()` | Disable hot-child filtering | | |
| `TieredStoreBuilder.Including<TChild>` | `(navigation, Action<TieredChildBuilder<TChild>>?)` | Aggregate children archived with root | `TieredChildBuilder.{WithReadModel, WithTieredView, MatchBy, Including}` | |
| `ToTieredView<TEntity>` | `EntityTypeBuilder<T>.ToTieredView(string viewName, Action<TieredPartitionBuilder<T>>)` | Read-only mapping of tiered view with partition pruning | `HasNoKey().ToView(name)`; injects partition-pruning predicates + contract marker | separate reader context |
| `GetTieredStore*` (Role, ArchivePath, Timestamp, Granularity, PartitionProperties, View, ControlKey, Parent, Root, ParentNavigation, Bindings, HotChildFilter, MatchProperties, MatchKeyUniqueness) | `IReadOnlyEntityType` getters | Read tiering metadata | | `TierGranularity {Month, Day}`, `TieredStoreBinding` record |
| `IsDuckDB` | `MigrationBuilder → bool` | Provider check in migrations | | also on `DatabaseFacade` |
| Annotations | `DuckDB:ValueGenerationStrategy`, `DuckDB:FileSourceFunction/Path`, `DuckDB:StructMapping/StructField/StructFieldName/StructColumnName/UseStructMapping/LogicalStructForeignKey`, `DuckDB:TieredStore:*`, `DuckDB:TieredView:PartitionProperties` | Raw metadata keys | | `DuckDBAnnotationNames` is public |
| Conventions (implicit) | `DuckDBValueGenerationConvention`, `DuckDBFileSourceConvention`, `DuckDBStructFieldConvention`, `DuckDBOwnedValueConvention`, `DuckDBSharedTableConvention` | Identity-by-convention etc. | Integer `ValueGenerated.OnAdd` keys w/o converter/default get `AutoIncrement`; on DuckLake (no sequences) such keys become `ValueGenerated.Never` | |
| Not provided | — | — | No `HasMethod`/index method, no filtered/descending index, no collation API, no `IsMemoryOptimized`-style flags, no ENUM/MAP model API, no fillfactor/include | Engine limits (see §7) |

---

## 3. Value generation / key generation

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DuckDBValueGenerationStrategy.AutoIncrement` | enum (`None`, `AutoIncrement`) | Identity column emulation via sequence | `CREATE SEQUENCE IF NOT EXISTS "{t}_{c}_seq"`; column `DEFAULT nextval('"{t}_{c}_seq"')`; `INSERT … RETURNING` reads key | Default for integral keys by convention (native profile) |
| Sequences (`HasSequence`) | relational | Sequences | `CREATE/DROP SEQUENCE IF EXISTS`; rename/restart/increment-alter unsupported | rejected on DuckLake (`SupportsSequences=false`) |
| `Guid.NewGuid()` / `Guid.CreateVersion7()` in SQL | translation | Server-side UUID | `uuidv4()` / `uuidv7()` | client-side GUID generation is EF default |
| Store-generated read-back | update pipeline | Returning generated values | native: `RETURNING`; referenced rows (FK-inbound): `UPDATE` + keyed `SELECT` (DuckDB rejects `UPDATE … RETURNING` with dependants); DuckLake: no `RETURNING`, affected-row checks | |
| DuckLake | profile | — | store-generated values/defaults rejected; client keys required | |

---

## 4. EF.Functions extensions (`DuckDBDbFunctionsExtensions`)

Small surface; all throw if evaluated client-side.

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `SplitPart` | `(string value, string separator, int index) → string` | Split string and take Nth field (1-based) | `split_part(value, separator, index)` | |
| `StandardDeviationSample` | `(IEnumerable<int/long/float/double/decimal>) → double?` | Sample standard deviation aggregate | `stddev_samp(x)`; group predicate → `CASE WHEN p THEN x END`; `Distinct` → `stddev_samp(DISTINCT x)` | aggregate (used on group/collection) |
| `ArgMax<TValue,TOrder>` | `(IEnumerable<(TValue Value, TOrder Order)>) → TValue?` | Value at max ordering key | `arg_max(value, order)` (predicate → CASE-wrapped) | non-deterministic on ties; DISTINCT not translated |
| `ArgMin<TValue,TOrder>` | same | Value at min ordering key | `arg_min(value, order)` | |
| `GreaterThan` | `(ITuple a, ITuple b) → bool` | Row-value (tuple) comparison | `(a1, a2, …) > (b1, b2, …)` | keyset pagination; operands from `ValueTuple.Create`/tuple literals |
| `LessThan` | `(ITuple, ITuple) → bool` | Row-value comparison | `(…) < (…)` | |
| `GreaterThanOrEqual` | `(ITuple, ITuple) → bool` | Row-value comparison | `(…) >= (…)` | |
| `LessThanOrEqual` | `(ITuple, ITuple) → bool` | Row-value comparison | `(…) <= (…)` | |
| `EF.Functions.Random()` (EF core) | `() → double` | Random number | `random()` | EF-defined, provider-translated |
| `EF.Functions.Like` (EF relational) | | Pattern match | `LIKE` | standard |
| Not provided | — | — | No regex-replace/extract, list/map/struct functions, JSON functions, `read_parquet`/`read_csv` as functions, date_trunc/date_part, string_agg, median/quantile, full-text, collate, hashing | raw SQL only (CAPABILITY-MAP §4) |

---

## 5. LINQ translations beyond relational core

### 5a. String / char
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `string.StartsWith(string/char)` | instance | Prefix match | `COALESCE(starts_with(s, p), false)`; CI mode → `ilike_escape(s, esc(p)||'%', '$')` | |
| `string.Contains(string/char)` | instance | Substring match | `COALESCE(contains(s, p), false)`; CI → `ilike_escape` | |
| `string.EndsWith(string/char)` | instance | Suffix match | `COALESCE(ends_with(s, p), false)`; CI → `ilike_escape` | |
| `string.Substring(int[, int])` | instance | Substring (0-based→1-based) | `substring(s, i + 1[, len])` | |
| `string.ToUpper()` / `ToLower()` | instance | Case conversion | `upper(s)` / `lower(s)` | |
| `string.Trim()` / `Trim(char)` / `Trim(char[] const)` | instance | Trim | `trim(s[, chars])` | |
| `string.TrimStart(...)` / `TrimEnd(...)` (same 3 overloads) | instance | Left/right trim | `ltrim` / `rtrim` | |
| `string.IndexOf(string/char)` | instance | Find position (0-based) | `instr(s, x) - 1` | |
| `string.IndexOf(string/char, int start)` | instance | Find from position | `instr(substring(s, start+1), x) - 1 + start` | |
| `string.Replace(string,string)` / `(char,char)` | instance | Replace | `replace(s, a, b)` | |
| `string.IsNullOrEmpty` | static | Null/empty test | `s IS NULL OR length(CAST(s AS VARCHAR)) = 0` | |
| `string.IsNullOrWhiteSpace` | static | Null/blank test | `s IS NULL OR length(trim(s)) = 0` | |
| `string.Length` | member | Length | `length(s)` | |
| `Enumerable.FirstOrDefault(string)` / `LastOrDefault(string)` | static | First/last char | `left(s, 1)` / `right(s, 1)` | |
| `string.Join(sep, new[]{…})` (string/char sep; string[]/object[]) | static, inline array | Concatenate with separator | `concat_ws(sep, COALESCE(a,''), …)` | inline array only |
| string `+` | operator | Concatenation | `a \|\| b` | |
| `(int)ch` etc. (char → numeric cast) | convert | Char code point | `CAST(unicode(c) AS T)` | |
| `x.ToString()` | instance, 19 CLR types | To text | `CAST(x AS VARCHAR)`; bool → `CASE WHEN x = true THEN 'True' WHEN x = false THEN 'False' END` | |
| `Convert.ToBoolean/ToByte/ToDecimal/ToDouble/ToInt16/ToInt32/ToInt64/ToString(x)` | static | Type cast | `CAST(x AS T)` | |
| `Regex.IsMatch(string input, string pattern)` | static | Regex match | `regexp_matches(input, pattern)` | no options overload, no Replace/Match |

### 5b. Math / numeric
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Math/MathF.Abs` | static | Absolute | `abs` | all numeric overloads |
| `Acos, Acosh, Asin, Asinh, Atan, Atanh, Atan2, Cbrt, Cos, Cosh, Sin, Sinh, Tan, Tanh, Exp, Sqrt` | static (Math + MathF) | Trig/transcendental | same-named function | `Math.Atanh(double)` special-cased |
| `Ceiling` / `Floor` | static | Rounding | `ceiling` / `floor` | |
| `Log(x)` / `Log10` / `Log2` / `Log(x, base)` | static | Logarithms | `ln` / `log10` / `log2` / `ln(x)/ln(base)` | |
| `Pow` | static | Power | `pow(a, b)` | |
| `Round(x)` / `Round(x, digits)` | static | Rounding | `round(x[, d])` | |
| `Truncate` | static | Truncation | `trunc` | |
| `Sign` | static | Sign | `sign` | |
| `double/float.DegreesToRadians` / `RadiansToDegrees` | static | Angle conversion | `radians` / `degrees` | |
| `double.IsNaN` etc. | static | NaN test | `isnan(x)` | |
| inline-collection `Max()/Min()` (e.g. `new[]{a,b}.Max()`) | EF relational hook | N-ary max/min | `greatest(…)` / `least(…)` | via `GenerateGreatest/Least` overrides |
| `a << b` / `a >> b` | operator | Bit shift | `CASE WHEN (b >= 0) THEN a << b ELSE NULL END` | |
| `a ^ b` | operator | XOR | bool: `(a AND NOT b) OR (NOT a AND b)`; int: `xor(a, b)` | |

### 5c. Date / time
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DateTime/DateTimeOffset/DateOnly .Year/.Month/.Day` | member | Date part | `year(x)` / `month(x)` / `day(x)` | |
| `.Hour/.Minute/.Second/.Millisecond` (DateTime, DTO, TimeOnly, TimeSpan.Hours/Minutes/Seconds) | member | Time part | `hour` / `minute` / `second` / `millisecond` | |
| `TimeSpan.Microseconds` | member | Microsecond part | `microsecond(x)` | TimeSpan stored as TIME |
| `DateTime.DayOfYear`, `DateOnly.DayOfYear` | member | Day of year | `dayofyear(x)` | |
| `DateOnly.DayOfWeek` | member | Day of week | `dayofweek(x)` | |
| `DateOnly.DayNumber` | member | Days since 0001-01-01 | `date_diff('day', DATE '0001-01-01', x)` | |
| `DateTime.Date` | member | Truncate to date | `CAST(CAST(x AS DATE) AS TIMESTAMP)` | |
| `DateTime.Now` | static | Local now | `current_localtimestamp()` | |
| `DateTime.UtcNow` | static | UTC now | `CAST(now() AS TIMESTAMP)` | |
| `DateTime.Today` | static | Today | `CAST(today() AS TIMESTAMP)` | |
| `AddYears(n)` / `AddMonths(n)` (DateTime, DTO, DateOnly) | instance | Date arithmetic | `date_add(x, to_years(n))` / `to_months(n)` | DateOnly result re-cast to DATE |
| `AddDays(n)` | instance | Date arithmetic | DateOnly: `date_add(x, to_days(n))`; DateTime/DTO (double): `date_add(x, n * INTERVAL '1 day')` | |
| `DateOnly.FromDateTime(dt)` | static | Date from timestamp | `CAST(dt AS DATE)` | |
| `(a - b).TotalDays/TotalHours/TotalMinutes/TotalSeconds/TotalMilliseconds/TotalMicroseconds/TotalNanoseconds` | member on DateTime/DTO subtraction | Date difference | `date_diff('day'/'hour'/…, a, b)` (argument order per factory) | integer-unit diff |
| DTO non-zero offsets | — | — | limited; spec tests skipped | |

### 5d. Guid / random
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Guid.NewGuid()` | static | Server UUIDv4 | `uuidv4()` | |
| `Guid.CreateVersion7()` | static | Server UUIDv7 | `uuidv7()` | |
| `EF.Functions.Random()` | | Random | `random()` | |

### 5e. Primitive collections (LIST / arrays, `T[]`, `List<T>`)
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| collection as queryable source | `e.Tags.Where(...)` etc. | Unnest array to rows | `unnest(arr) AS "t"("value")`; ordered: `(SELECT unnest(arr) AS "value", generate_subscripts(arr, 1) AS "ordinality") AS "t"` | `WITH ORDINALITY` emulated |
| `arr.Contains(x)` | | Array membership | `array_contains(arr, x)` when element or item non-nullable; else EXISTS over unnest | NULL search not supported by DuckDB |
| `arr.Any()` | | Non-empty | `array_length(arr) > 0` | |
| `arr.Count()` / `.Length` / `.Count` | | Array length | `array_length(arr)`; JSON collections → `json_array_length(j)` | |
| `arr[i]` / `ElementAt(i)` | | Element access (0→1-based) | `arr[i + 1]` | |
| `arr.Skip(n)` / `Take(n)` / `Skip().Take()` | | Slice | `arr[a:b]` then unnest | |
| `arr.Distinct()` | | Dedupe | `list_distinct(arr)` then unnest | |
| `arr.Where(x => x != v)` | | Remove element | `array_remove(arr, v)` | simple-predicate pattern only |
| `arr.Append(x)` / `Prepend(x)` | | Push | `array_push_back(arr, x)` / `array_push_front(arr, x)` | |
| `new[] { a, b }` inline | | Array literal | `[a, b]` / `list_value(a, b)` (`::T[]` cast) | |
| parameterized `IN` | | Membership vs param array | `x = ANY(@p)` (`DuckDBAnyExpression`) | |
| `string.Join` over list | — | — | not translated (`DuckDBStringAggregateMethodTranslator` returns null) | |
| Multidimensional arrays | — | — | `NotSupportedException` | |

### 5f. STRUCT (composite) columns
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| member access on struct-mapped complex property | `c.Location.City` | Composite field access | `"c"."Location"."City"` (nested path delimited per segment) | |
| `c.Location == null` / `!= null` | | Whole-composite null test | single `"c"."Location" IS [NOT] NULL` (rewrites EF's per-leaf checks) | |
| projection of whole complex | `select c.Location` | Composite materialization | projects struct column; materialized client-side | |
| struct FK joins | `HasStructForeignKey` | Join on composite leaf | `ON "p"."Id" = "d"."Rel"."ParentId"` | |
| updates of struct leaves | SaveChanges | Partial composite update | STRUCT root rewritten as one column write planned by `DuckDBStructMutationPlan` (leaf-path collision checks) | internal |

### 5g. JSON
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| owned/complex `ToJson()` property path | `e.Json.Prop.Sub` | JSON path extraction | `CAST(col ->> '$.Prop.Sub' AS T)`; array index `->> '$[n]'` | |
| JSON collection as queryable | `e.Json.Items.Where(...)` | JSON array to rows | `json_each(col[, '$.path'])` with `key`/`value` columns | |
| JSON collection count | | JSON array length | `json_array_length(col)` | |
| JSON literal building | | Build JSON array text | `'[' \|\| … \|\| ']'` | |
| `JsonDocument` / `JsonElement` / `string` mapped to `JSON` | type | JSON scalar column | `JSON` | no JSON query functions on these |

### 5h. Aggregates / analytic / row values
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Average` | aggregate | Mean | `AVG(x)` with casts to `double`/`decimal` (int → CAST AS DOUBLE) | |
| `Sum` | aggregate | Sum | `SUM(x)` cast back to CLR type (DuckDB widens to HUGEINT/DOUBLE) | |
| `Count` / `LongCount` | aggregate | Count | `COUNT(*)` / `COUNT(x)` | |
| `Min` / `Max` | aggregate | Min/max | `MIN` / `MAX` | |
| `EF.Functions.StandardDeviationSample/ArgMax/ArgMin` | aggregate | see §4 | `stddev_samp`, `arg_max`, `arg_min` | |
| `ValueTuple.Create(a,b)` / tuple literal | expression | Row value | `(a, b)` | used by row comparisons & ArgMax/ArgMin |
| Window functions | — | — | only EF's generic support; no `QUALIFY`, `PIVOT`, `ASOF`, `SAMPLE`, `ROLLUP/CUBE` | raw SQL |
| Spatial aggregates (NTS) | `GeometryCombiner.Combine`, `UnaryUnionOp.Union`, `ConvexHull`, `EnvelopeCombiner.CombineAsGeometry` | Spatial aggregates | `ST_Collect`, `ST_Union_Agg`, `ST_ConvexHull(ST_Collect…)`, `ST_Envelope_Agg` | |

### 5i. Query roots / paging / misc
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DbSet<T>.AsOfSnapshot(long)` | `DuckLakeQueryableExtensions` → `IQueryable<T>` | Per-table time travel by version | `FromSqlRaw("SELECT * FROM cat.tbl AT (VERSION => {0})")` | DuckLake only; uncomposed root |
| `DbSet<T>.AsOfTimestamp(DateTimeOffset)` | → `IQueryable<T>` | Per-table time travel by time | `SELECT * FROM cat.tbl AT (TIMESTAMP => {0})` | DuckLake only |
| file-source entity query | `[FromParquet]` etc. | External-file table | `FROM read_parquet('glob') AS "e"` | |
| tiered view query | `ToTieredView` | Partition pruning | adds derived hive-partition predicates | |
| `Skip/Take` | | Paging | `LIMIT n OFFSET m` (no correlated LIMIT) | |
| NULL ordering | | — | DuckDB default is NULLS LAST for ASC (differs from SQL Server); emulation internal only | homogenization risk |

### 5j. Spatial (DuckDB.EFCoreProvider.NTS)
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Geometry.Area/Boundary/Centroid/Dimension/Envelope/GeometryType/OgcGeometryType/InteriorPoint/PointOnSurface/IsEmpty/IsSimple/IsValid/Length/NumGeometries/NumPoints/SRID` | members | Spatial properties | `ST_Area`, `ST_Boundary`, `ST_Centroid`, `ST_Dimension`, `ST_Envelope`, `ST_GeometryType`, `ST_PointOnSurface`, `ST_IsEmpty`, `ST_IsSimple`, `ST_IsValid`, `ST_Length`, `ST_NumGeometries`, `ST_NPoints`, `ST_SRID` | |
| `Geometry.AsBinary/ToBinary/AsText/ToText/Buffer/Contains/ConvexHull/CoveredBy/Covers/Crosses/Difference/Disjoint/Distance/EqualsTopologically/GetGeometryN/Intersection/Intersects/IsWithinDistance/Normalized/Overlaps/Reverse/SymmetricDifference/Touches/Union/Within` | methods | Spatial predicates/ops | `ST_AsBinary`, `ST_AsText`, `ST_Buffer`, `ST_Contains`, …, `ST_DWithin`, `ST_GeometryN(g, n+1)`, `ST_Union`, `ST_Within` | |
| `Point.X/Y/Z/M` | members | Coordinates | `ST_X/ST_Y/ST_Z/ST_M` | |
| `LineString.Count/StartPoint/EndPoint/IsClosed/IsRing/GetPointN`, `MultiLineString.IsClosed` | | Line ops | `ST_NumPoints`, `ST_StartPoint`, `ST_EndPoint`, `ST_IsClosed`, `ST_IsRing`, `ST_PointN` | |
| `Polygon.ExteriorRing/NumInteriorRings/GetInteriorRingN`, `GeometryCollection.Count`/indexer | | Polygon/collection ops | `ST_ExteriorRing`, `ST_NumInteriorRings`, `ST_InteriorRingN`, `ST_NumGeometries`, `ST_CollectionExtract` | |

---

## 6. Store types / type mappings

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `bool` | `BOOLEAN` (aliases BOOL, LOGICAL) | Boolean | | |
| `sbyte` / `short` / `int` / `long` | `TINYINT` / `SMALLINT` / `INTEGER` / `BIGINT` (+ INT1/2/4/8, INT16/32/64, SHORT, INT, SIGNED, LONG) | Signed ints | | |
| `byte` / `ushort` / `uint` / `ulong` | `UTINYINT` / `USMALLINT` / `UINTEGER` / `UBIGINT` (+ UINT8…UINT64) | Native unsigned ints | | `byte` is UNSIGNED (≠ SQL Server tinyint semantics differ) |
| `float` / `double` | `FLOAT` (FLOAT4, REAL) / `DOUBLE` (FLOAT8) | Floats | | |
| `decimal` | `DECIMAL(18,3)` default (NUMERIC) | Fixed decimal | | max precision 38 |
| `string` | `VARCHAR` (CHAR, BPCHAR, TEXT, STRING) | Text (no length enforcement) | | `char` → fixed char mapping |
| `byte[]` | `BLOB` (BYTEA, BINARY, VARBINARY) | Binary | `octet_length` for `.Length` | |
| `Guid` | `UUID` | Native UUID | | |
| `DateOnly` | `DATE` | Date | | |
| `DateTime` | `TIMESTAMP` (DATETIME); explicit `TIMESTAMP_NS`, `TIMESTAMP_MS`, `TIMESTAMP_S` | Timestamp (µs) | | |
| `DateTimeOffset` | `TIMESTAMPTZ` | Timestamp with TZ (stored as UTC instant) | | offset not preserved |
| `TimeOnly` | `TIME`; explicit `TIMETZ`, `TIME_NS` | Time of day | | |
| `TimeSpan` | `TIME` via `DuckDBTimeSpanToTimeOnlyValueConverter` | Duration (lossy) | | **no INTERVAL mapping** |
| `JsonDocument` / `JsonElement` / `string`(store `JSON`) / owned `ToJson()` | `JSON` | JSON document | | |
| `T[]` / `List<T>` (1-D, mapped element) | `T[]` e.g. `INTEGER[]`, `VARCHAR[]` (LIST) | Array/list column | `DuckDBArrayTypeMapping<,,>` + `DuckDBArrayConverter` | fixed `T[n]` ARRAY raw-reader-only; multidim unsupported |
| complex property + `UseStructMapping` | `STRUCT(name TYPE, …)` | Composite/row type | | |
| NTS `Geometry` & subtypes | `GEOMETRY` (+ Z/M/ZM, typed variants) | Spatial | WKT wire format (`ST_GeomFromText`/`ST_AsWKT`) | NTS package |
| `MAP`, `UNION`, `VARIANT`, `HUGEINT`/`UHUGEINT` (Int128), `ENUM`, `BIT`, `INTERVAL` | — | — | **No EF mapping**; raw reader only (`BigInteger`, `Dictionary<K,V>`, …) | CLR enums → underlying int/string, not DuckDB ENUM |
| `GetDuckDBStoreTypeMapping` | `DatabaseFacade.(string storeType) → DuckDBStoreTypeMappingInfo(StoreType, Support, ClrType, TypeMapping, ElementType)` | Type-mapping introspection | `DuckDBStoreTypeSupport {ScalarProperty, ComplexProperty, RawReaderOnly, Unsupported}` | tooling |

---

## 7. Migrations

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| Identity via sequence | annotation | Identity column | `CREATE SEQUENCE IF NOT EXISTS …; … DEFAULT nextval('…')` on CreateTable/AddColumn | |
| `CreateTable` FKs | | FK constraints | emitted inline; `CASCADE/SET NULL/SET DEFAULT` downgraded to `NO ACTION` + warning | DuckDB only supports NO ACTION/RESTRICT |
| `AddForeignKey` / `DropForeignKey`, add/drop PK/unique/check | ops | Constraint alteration | default `NotSupportedException` (`RebuildRequired`); with `EnableMigrationTableRebuilds()` → backup copy, recreate from target model, copy back, recreate indexes | constraint changes and column-shape changes must be in separate migrations |
| `AlterColumn` | op | Column alteration | `ALTER TABLE … ALTER COLUMN … TYPE/SET/DROP NOT NULL/DEFAULT` subset; some shapes fail | |
| `AddColumn` w/ constraint/default/required, computed | op | — | engine rejects (`Adding columns with constraints not yet supported`); only VIRTUAL generated columns | |
| `RenameTable` / `RenameColumn` | ops | Rename | `ALTER TABLE … RENAME …`; fails if table has PK/indexes/deps in engine | |
| `RenameIndex`, filtered index, DESC index | — | — | not supported (desc direction discarded; `HasFilter` not emitted) | |
| `RenameSequence`, schema move, sequence RESTART/INCREMENT alter | — | — | not supported | |
| `EnsureSchema` | op | Schema | `CREATE SCHEMA IF NOT EXISTS` | |
| STRUCT schema planning | `DuckDBStructSchemaPlanner` | Composite column DDL | flattened EF columns → one `STRUCT(...)` column | internal |
| History table | `__EFMigrationsHistory` | History | `CREATE TABLE IF NOT EXISTS`; exists via `information_schema.tables … database_name = current_database()` | |
| Migration lock | `__EFMigrationsLock` row, `LockReleaseBehavior.Explicit` | Cross-process migration lock | timeout default 5 min (`MigrationLockTimeout`) then `TimeoutException` with stale-lock instructions | |
| Idempotent scripts | — | — | **not supported** (no procedural IF blocks) | |
| `IsDuckDB(MigrationBuilder)` | | Provider check | | |
| DuckLake | profile | — | EF migrations explicitly rejected; `EnsureCreated` only (constraints/indexes omitted) | |
| Savepoints | — | — | `SupportsSavepoints = false` | |

---

## 8. Database-level features

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DuckDBDatabaseCreator` | `EnsureCreated/Deleted/Exists/HasTables` | Database lifecycle | file-based; in-memory always exists; DuckLake `EnsureDeleted` rejected | |
| `BulkInsert` / `BulkInsertAsync` | `DbContext.(IEnumerable<T>[, ct]) → int` | Bulk load | DuckDB `Appender` (bypasses change tracking, store-generated values) | works on DuckLake |
| `Upsert` / `UpsertAsync` | `DbContext.([DuckDBUpsertInputMode,] IEnumerable<T>[, Expression conflictTarget][, DuckDBUpsertMatchMode][, int batchSize][, ct]) → int` | Bulk upsert / merge | appender staging → `INSERT … ON CONFLICT (key) DO UPDATE/DO NOTHING` (native) or `MERGE INTO` (DuckLake / `LogicalKeyMerge`) | `InputMode {MayContainDuplicates, DistinctConflictTargets}`; `MatchMode {UniqueConflictTarget, LogicalKeyMerge}` |
| `ExportToParquet` / `ExportToParquetAsync` | `DatabaseFacade.(IQueryable<T>, string path, Action<DuckDBParquetExportOptions<T>>?)` | Query result export to columnar file | `COPY (sql) TO 'path' (FORMAT PARQUET, PARTITION_BY (…), OVERWRITE_OR_IGNORE, COMPRESSION …)` | options `PartitionBy`, `OverwriteOrIgnore`, `Compression(Snappy/Zstd/Gzip/…)` |
| `SqlQueryDynamicRawAsync` / `SqlQueryDynamicAsync` / `SqlQueryDynamicCommandAsync` | `DatabaseFacade` → `DuckDBDynamicQueryResult{Columns, ReadRowsAsync}` | Unknown-shape query streaming | raw DuckDB.NET values + column metadata | |
| `GetDuckDBCommandPlan` / `Get…Count/LongCount/Any/Min/Max/Sum/AverageCommandPlan` | `DatabaseFacade.(IQueryable<T>) → DuckDBCommandPlan{CommandText, Parameters}` | SQL extraction without execution | | replay via `SqlQueryDynamicCommandAsync(plan)` / `ReplayQuackCommandAsync` |
| `DuckLake()` | `DatabaseFacade → DuckLakeMaintenanceFacade` | Lakehouse maintenance | `GetSnapshotsAsync` (`ducklake_snapshots`), `SetCommitMessageAsync`, `ExpireSnapshotsAsync`, `CleanupOldFilesAsync`, `DeleteOrphanedFilesAsync`, `FlushInlinedDataAsync`, `MergeAdjacentFilesAsync`, `RewriteDataFilesAsync` (`ducklake_*` table functions) | destructive ops default dry-run |
| Tiered storage lifecycle | `DatabaseFacade.EnsureTieredStoresCreated`, `ArchiveTier[Async]<TRoot>(cutoff)`, `BootstrapArchiveTier[Async]`, `ReconcileArchiveTier[Async]`, `CompactArchiveTier[Async]`, `RestoreArchiveTier[Async]`, `PurgeArchiveOlderThan`, `PlanArchiveRetention`/`PublishArchiveRetention`, `Capture/Plan/ApplyArchiveRecovery`, `GetArchiveGenerationInventory`, `PlanArchiveGenerationCleanup`/`Revalidate…`, `Inspect/Plan/RewriteArchiveContract`, `GetArchiveConflicts`/`Stream…`, `GetArchiveDetachedDescendants`/`Stream…`, `PreflightTieredStorageAsync` | Hot→cold archival to Parquet | `COPY … TO … (PARTITION_BY …)` + delete hot rows + control table + views | native profile only; large surface (~40 methods) |
| `StartQuackServerAsync` / `GetQuackDiagnosticsAsync` | `DatabaseFacade` | Serve DB remotely / probe | | **[Experimental]** |
| Extensions / secrets / settings | see §1 | Connection initialization | `INSTALL/LOAD`, `ConfigureConnection`, `SET …`, `ATTACH`/`USE` | applied to provider-owned and caller-owned connections |
| Concurrency model | — | — | single-writer embedded file; no retrying execution strategy | |
| Scaffolding | `DuckDBDatabaseModelFactory` | Reverse engineering | tables, columns, keys, indexes, sequences, FKs; DuckLake via `ducklake:/path` source | computed columns not detected |
| `IsDuckDB(DatabaseFacade)` | | Provider check | | |

---

## 9. Diagnostics / event IDs (`DuckDBEventId`, base `CoreEventId.ProviderBaseId`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `BulkInsertStarting/Completed/Failed` | EventId (command) | Bulk op telemetry | | |
| `UpsertStarting/Completed/Failed` | EventId | Bulk upsert telemetry | | |
| `ParquetExportStarting/Completed/Failed` | EventId | Export telemetry | | |
| `TieredStorageOperationStarting/Completed/Failed` | EventId | Archival telemetry | | |
| `QuackDiagnosticsStarting/Completed/Failed`, `QuackServerStarting/Completed/Failed` | EventId | Remote server telemetry | | Experimental |
| `ExtensionLoadStarting/Completed/Failed` | EventId (infrastructure, +100) | Extension load | | |
| `DuckLakeAttachmentStarting/Completed/Failed` | EventId | Catalog attach | | |
| `EncryptedDatabaseAttachmentStarting/Completed/Failed` | EventId | Encrypted attach | key never logged | |
| `SaveChangesBatch` | EventId (+200) | Batch flushed/rejected by batching guard | | |
| FK action downgrade warning | migrations log warning | Unsupported cascade → NO ACTION | | |
| `DuckDBOperationEventData` | DiagnosticSource payload | Structured event data | | |
