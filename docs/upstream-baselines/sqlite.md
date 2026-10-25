# Microsoft.EntityFrameworkCore.Sqlite (Core + NTS) 10.0.11: provider-specific surface

Source read: `src/EFCore.Sqlite.Core`, `src/EFCore.Sqlite.NTS`. Everything below is something plain relational EF Core does not already provide, or a place where SQLite changes relational behaviour.

Conventions: `PB` = `PropertyBuilder`, `CTPB` = `ComplexTypePropertyBuilder`, `ICPB` = `IConventionPropertyBuilder`. "fromDataAnnotation" overloads follow the usual EF convention pattern.

Markers: **[Experimental]** = `[Experimental]` attribute. **[Obsolete]** = `[Obsolete]`. None of the public user APIs carry either. The only markers are listed in the last section.

---

## 1. Options builder

`SqliteDbContextOptionsBuilder : RelationalDbContextOptionsBuilder<...>` declares **no SQLite-specific methods of its own**. It inherits only the relational ones (CommandTimeout, MaxBatchSize, MigrationsAssembly, MigrationsHistoryTable, UseQuerySplittingBehavior, UseRelationalNulls, ExecutionStrategy, TranslateParameterizedCollectionsTo..., and so on). Note that SQLite ignores MaxBatchSize because it always uses singular batches (see section 8).

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseSqlite` | `DbContextOptionsBuilder.UseSqlite(Action<SqliteDbContextOptionsBuilder>? = null)` | Select provider (connection supplied later) | Registers `SqliteOptionsExtension`, applies the relational default warning configuration | Generic `<TContext>` overloads exist for every form |
| `UseSqlite` | `(string? connectionString, Action<…>? = null)` | Select provider + connection string | Creates a `SqliteConnection` lazily | |
| `UseSqlite` | `(DbConnection connection, Action<…>? = null)` | Select provider + external connection | Same as `contextOwnsConnection: false` | Initialization (functions, timeout) is applied to the passed connection too |
| `UseSqlite` | `(DbConnection connection, bool contextOwnsConnection, Action<…>? = null)` | External connection with ownership flag | The context disposes the connection when true | |
| `UseNetTopologySuite` (NTS pkg) | `SqliteDbContextOptionsBuilder.UseNetTopologySuite()` | Enable spatial types (NTS) | Adds the NTS extension **and** sets `SqliteOptionsExtension.WithLoadSpatialite(true)`, so `mod_spatialite` is loaded on every connection | The only way to turn LoadSpatialite on through public API |
| `AddSqlite<TContext>` | `IServiceCollection.AddSqlite<TContext>(string? cs, Action<SqliteDbContextOptionsBuilder>?, Action<DbContextOptionsBuilder>?)` | DI one-liner | `AddDbContext` + `UseSqlite` | |
| `AddEntityFrameworkSqlite` | `IServiceCollection` | Register provider services | | `[EditorBrowsable(Never)]` |
| `AddEntityFrameworkSqliteNetTopologySuite` | `IServiceCollection` | Register spatial services | | NTS pkg |
| `IsSqlite` | `DatabaseFacade.IsSqlite()` | Runtime provider check | Compares the provider name | |
| `SqliteOptionsExtension.LoadSpatialite` / `WithLoadSpatialite(bool)` | Internal-ish extension property | Load spatial extension on open | Calls `SpatialiteLoader.Load(conn)` | `.Internal` namespace |

## 2. Model building API

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseAutoincrement` | `PB UseAutoincrement()`, `PB<T> UseAutoincrement<T>()` | Identity / auto-increment key, strict monotonic | Column DDL becomes `INTEGER … PRIMARY KEY AUTOINCREMENT` (inline PK) | Sets `SqliteValueGenerationStrategy.Autoincrement` |
| `UseAutoincrement` | `ColumnBuilder UseAutoincrement()` | Per-table override (TPT/entity splitting) of identity | Sets the strategy on the `RelationalPropertyOverrides` for that table | |
| `UseAutoincrement` | `CTPB UseAutoincrement()`, `CTPB<T> UseAutoincrement<T>()` | Identity on a complex-type property | as above | |
| `HasValueGenerationStrategy` | `ICPB? HasValueGenerationStrategy(SqliteValueGenerationStrategy?, bool fromDataAnnotation=false)` | Convention-level identity strategy | | |
| `CanSetValueGenerationStrategy` | `bool ICPB.CanSetValueGenerationStrategy(SqliteValueGenerationStrategy?, bool)` | | | |
| `HasSrid` | `PB HasSrid(int srid)`, `PB<T> HasSrid<T>(int)` | Spatial reference system ID of a geometry column | Migrations: `SELECT AddGeometryColumn('T','C', srid, 'POINT', -1, notNull)` | Model validation: columns shared across entity types must agree (`DuplicateColumnNameSridMismatch`) |
| `HasSrid` | `CTPB HasSrid(int)`, `CTPB<T> HasSrid<T>(int)` | SRID on a complex property | | |
| `HasSrid` / `CanSetSrid` | `ICPB? HasSrid(int?, bool)`, `bool CanSetSrid(int?, bool)` | Convention-level SRID | | |
| `UseSqlReturningClause` | `TableBuilder[<T>] UseSqlReturningClause(bool useSqlReturningClause = true)` | Toggle RETURNING on DML (needed `false` for tables with triggers or virtual tables) | `true` gives `INSERT … RETURNING "Id"`; `false` gives `INSERT …; SELECT "Id" FROM t WHERE changes() = 1 AND "rowid" = last_insert_rowid();` | Default is true. Same overload set on `SplitTableBuilder[<T>]` and `OwnedNavigationTableBuilder[<TOwner,TDep>]` |
| `UseSqlReturningClause` | `IConventionEntityTypeBuilder? UseSqlReturningClause(bool?, bool)` plus a `StoreObjectIdentifier` overload | Convention version | | `CanUseSqlReturningClause(...)` pair |
| `GetValueGenerationStrategy` | `IReadOnlyProperty` (plus `StoreObjectIdentifier` and `IReadOnlyRelationalPropertyOverrides` overloads) | Read the identity strategy | | |
| `GetDefaultValueGenerationStrategy` | `IReadOnlyProperty` | Convention default | `Autoincrement` iff: single-column PK, integer CLR type and integer provider type, not an FK, no default value, default SQL or computed SQL, `ValueGenerated != Never`, not TPC | So `AUTOINCREMENT` is emitted by default for int PKs |
| `SetValueGenerationStrategy` | `IMutableProperty`, `IConventionProperty`, per-`StoreObjectIdentifier`, per-overrides | Write the strategy | | `GetValueGenerationStrategyConfigurationSource` (3 overloads) |
| `GetSrid` / `SetSrid` / `GetSridConfigurationSource` | `IReadOnlyProperty` (+ `StoreObjectIdentifier`) / `IMutableProperty` / `IConventionProperty` | Read/write SRID | | |
| `IsSqlReturningClauseUsed` | `IReadOnlyEntityType` (+ `StoreObjectIdentifier`), `ITable`, `IReadOnlyEntityTypeMappingFragment` | Read the RETURNING flag | Inherited from the root type or the owner principal | |
| `UseSqlReturningClause` (metadata) | `IMutableEntityType`, `IConventionEntityType`, `IMutableEntityTypeMappingFragment`, `IConventionEntityTypeMappingFragment` (+ store-object overloads) | Write the RETURNING flag | | `GetUseSqlReturningClauseConfigurationSource` |
| `SqliteValueGenerationStrategy` enum | `None`, `Autoincrement` | Identity strategy enum | | `Microsoft.EntityFrameworkCore.Metadata` |
| Annotations | `Sqlite:Autoincrement` (legacy `Autoincrement`), `Sqlite:ValueGenerationStrategy`, `Sqlite:Srid`, `Sqlite:UseSqlReturningClause`, `Sqlite:InitSpatialMetaData`, `Sqlite:InlinePrimaryKey`, `Sqlite:InlinePrimaryKeyName` | Persisted metadata keys | | `.Internal` constants |
| Relational APIs that SQLite ignores or rejects | `HasDefaultSchema`/`ToTable(schema)` → warning `SchemaConfiguredWarning` (schema ignored); `HasSequence` → `SequenceConfiguredWarning`; `InsertUsingStoredProcedure` and friends → throws `StoredProceduresNotSupported` | Capability limits | | Check-constraint names need not be unique across tables (`SqliteSharedTableConvention`) |

There are no SQLite-specific IndexBuilder or ModelBuilder extensions. No sparse, filegroup, clustered, include, fillfactor or collation-at-database APIs exist beyond relational ones.

## 3. Value generation and key generation

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `SqliteValueGenerationStrategy.Autoincrement` | enum | Store identity (rowid alias with AUTOINCREMENT) | `"Id" INTEGER NOT NULL CONSTRAINT "PK_T" PRIMARY KEY AUTOINCREMENT` (the PK is always inlined for single-column PKs) | Default for single-column integer PKs (see section 2) |
| `SqliteValueGenerationStrategy.None` | enum | No store generation | A single integer PK without AUTOINCREMENT is still a rowid alias, so SQLite generates values; EF then treats it as not generated | |
| Generated-key retrieval | update SQL | Fetch generated values | `RETURNING` (SQLite ≥ 3.35). Legacy/fallback: `SELECT … WHERE changes() = 1 AND "rowid" = last_insert_rowid()` | Version-detected; `UseSqlReturningClause(false)` forces fallback |
| Concurrency checks | update SQL | Rows-affected check | `RETURNING 1`, or `SELECT changes()` in fallback mode | |
| Sequences | n/a | — | Not supported: model warning, migrations throw `SequencesNotSupported`, `GenerateNextSequenceValueOperation` throws | |
| Composite key validation | `CompositeKeyWithValueGeneration` warning | Composite PK containing a generated integer column cannot be AUTOINCREMENT | | |
| `ConflictingValueGenerationStrategiesWarning` | warning + `ConflictingValueGenerationStrategiesEventData` | Strategy conflicts with default value or computed SQL | | |
| GUID keys | client | Client-side `Guid.NewGuid()` (core) | Stored as uppercase TEXT | No sequential-GUID generator |

## 4. EF.Functions extensions (`SqliteDbFunctionsExtensions`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Glob` | `bool Glob(this DbFunctions, string matchExpression, string pattern)` | Case-sensitive Unix-glob match | `match GLOB pattern` (`NOT … GLOB` when negated) | Custom `GlobExpression` |
| `Hex` | `string Hex(this DbFunctions, byte[] bytes)` | Binary → uppercase hex text | `hex(bytes)` | |
| `Unhex` | `byte[]? Unhex(this DbFunctions, string value)` | Hex text → binary | `unhex(value)`; NULL on invalid input | Needs SQLite ≥ 3.41 |
| `Unhex` | `byte[]? Unhex(this DbFunctions, string value, string ignoreChars)` | Hex → binary, skipping separator chars | `unhex(value, ignoreChars)` | |
| `Substr` | `byte[] Substr(this DbFunctions, byte[] bytes, int startIndex)` | Binary substring (1-based) | `substr(bytes, startIndex)` | No +1 adjustment because the argument is already 1-based |
| `Substr` | `byte[] Substr(this DbFunctions, byte[] bytes, int startIndex, int length)` | Binary substring | `substr(bytes, startIndex, length)` | |
| `Random` (relational core API, SQLite translation) | `double EF.Functions.Random()` | Uniform random 0..1 | `abs(random() / 9223372036854780000.0)` | |
| `Like` / `Collate` (relational core) | | | `LIKE` (ASCII case-insensitive by default in SQLite) / `COLLATE x` | Not SQLite-specific, listed for completeness |

## 5. LINQ translations beyond relational core

### 5a. String / char

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `string.Length` | member | Character length | `length(s)` | |
| `string.IndexOf(string\|char)` | method | 0-based position | `instr(s, x) - 1` | |
| `string.IndexOf(string\|char, int start)` | method | Position from offset | `(instr(substr(s, start + 1), x) - 1) + start` | |
| `string.Replace(string,string)` / `(char,char)` | method | Replace all | `replace(s, a, b)` | |
| `string.ToLower()` / `ToUpper()` | method | Case fold | `lower(s)` / `upper(s)` | ASCII-only unless ICU is present |
| `string.Substring(int)` / `(int,int)` | method | Substring | `substr(s, i + 1[, len])` | |
| `string.Trim/TrimStart/TrimEnd()` and `(char)` / `(char[])` | method | Trim whitespace or a char set | `trim(s[, 'chars'])`, `ltrim`, `rtrim` | The char argument must be a constant |
| `string.Contains(string\|char)` | method | Substring test | `instr(s, x) > 0` | Case-sensitive (unlike LIKE) |
| `string.StartsWith/EndsWith(string\|char)` | method | Prefix / suffix | Constant: `s LIKE 'x%'`, escaped with `ESCAPE '\'`. Parameter: runtime-escaped `@p_startswith` with `LIKE … ESCAPE '\'`. Column: `s IS NOT NULL AND p IS NOT NULL AND (substr(s, 1, length(p)) = p OR p = '')` (EndsWith uses `substr(s, -length(p))`) | `""` pattern becomes `LIKE '%'` |
| `string.IsNullOrWhiteSpace(s)` | static | Blank test | `s IS NULL OR trim(s) = ''` | |
| `Enumerable.FirstOrDefault(string)` / `LastOrDefault(string)` | method on string-as-char-seq | First/last char | `substr(s, 1, 1)` / `substr(s, length(s), 1)` | |
| `string + string` | operator | Concatenation | `a \|\| b` | |
| `char.ToLower(c)` / `char.ToUpper(c)` | static | Char case fold | `lower(c)` / `upper(c)` | |
| `(int)c` / `(char)i` | cast | Codepoint conversion | `unicode(c)` / `char(i)` | Done in the SQL generator |
| `x.ToString()` | method | Value → text | `COALESCE(CAST(x AS TEXT), '')` for bool, byte, byte[], char, DateOnly, DateTime, DateTimeOffset, decimal, double, float, Guid, int, long, sbyte, short, TimeOnly, TimeSpan, uint, ushort. bool: `CASE x WHEN 0 THEN 'False' WHEN 1 THEN 'True' ELSE '' END` (non-null column: `CASE WHEN x THEN 'True' ELSE 'False' END`) | Enums are handled by the core enum translator |
| `string.Join(sep, IEnumerable<string>)` (aggregate) | GroupBy aggregate | Delimited string aggregation | `COALESCE(group_concat(COALESCE(x, ''), sep), '')`; predicate becomes `CASE WHEN pred THEN … END`; Distinct becomes `group_concat(DISTINCT …)` | **Ordered input (`OrderBy` inside the aggregate) is NOT translated** |
| `string.Concat(IEnumerable<string>)` (aggregate) | GroupBy aggregate | Concatenation aggregate | `COALESCE(group_concat(COALESCE(x,''), ''), '')` | Same ordering limitation |
| `Regex.IsMatch(string input, string pattern)` | static | Regular expression match | `input REGEXP pattern` (`NOT … REGEXP`) | `regexp(pattern, input)` is registered on each connection as a .NET `Regex.IsMatch` UDF. Options/overloads are not translated |

### 5b. Math / numeric

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Math.Abs` (double, float, int, long, sbyte, short) | static | Absolute value | `abs(x)` | |
| `Math/MathF.Acos, Acosh, Asin, Asinh, Atan, Atanh, Cos, Cosh, Sin, Sinh, Tan, Tanh` | static | Trigonometry | `acos`, `acosh`, `asin`, `asinh`, `atan`, `atanh`, `cos`, `cosh`, `sin`, `sinh`, `tan`, `tanh` | These need SQLite built with math functions (≥ 3.35, SQLITE_ENABLE_MATH_FUNCTIONS; the bundled e_sqlite3 has them) |
| `Math/MathF.Atan2(y,x)` | static | | `atan2(y, x)` | |
| `Math/MathF.Ceiling`, `Floor` | static | Rounding | `ceiling(x)`, `floor(x)` | |
| `Math/MathF.Exp` | static | | `exp(x)` | |
| `Math/MathF.Log(x)` | static | Natural log | `ln(x)` | |
| `Math/MathF.Log(x, base)` | static | Log with base | `log(base, x)` | |
| `Math/MathF.Log10`, `Log2` | static | | `log10(x)`, `log2(x)` | |
| `Math/MathF.Pow` | static | Power | `pow(x, y)` | |
| `Math/MathF.Round(x)` | static | Round | `round(x)` | Away from zero (SQLite) versus banker's rounding (.NET) |
| `Math/MathF.Round(x, int digits)` | static | Round to digits | `round(x, d)` | |
| `Math.Sign` (double, float, long, sbyte, short) | static | Sign | `sign(x)` | `Math.Sign(int)` is not in the list |
| `Math/MathF.Sqrt` | static | | `sqrt(x)` | |
| `Math/MathF.Truncate` | static | | `trunc(x)` | |
| `double/float.DegreesToRadians`, `RadiansToDegrees` | static | | `radians(x)`, `degrees(x)` | |
| `Math.Max/Min(a,b)` and Greatest/Least | static | Scalar greatest/least | `max(a, b, …)` / `min(a, b, …)` (multi-argument scalar forms; NULL if any argument is NULL) | Via `GenerateGreatest`/`GenerateLeast` |
| `%` on double/float | operator | Float modulo | `mod(a, b)` | |
| `^` (XOR) | operator | — | Not translated | |
| `byte[].Length` | ArrayLength | Binary length | `length(b)` | |
| `byte[].Contains(byte)` | `Enumerable.Contains` | Byte search | `instr(b, X'..') > 0` for a constant, `instr(b, char(v)) > 0` otherwise | |

### 5c. Decimal handling (decimal is stored as TEXT; arithmetic goes through UDFs registered on the connection)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `decimal + - * /` | operators | Exact decimal arithmetic | `ef_add(a,b)`, `ef_add(a, ef_negate(b))`, `ef_multiply(a,b)`, `ef_divide(a,b)` (NULL on /0) | Only when both operands are decimal by provider type |
| `decimal %` | operator | | `ef_mod(a,b)` | |
| `-decimal` | unary | | `ef_negate(x)` | |
| `decimal < <= > >=` | comparison | Exact compare | `ef_compare(a,b) > 0` and so on | `=` / `<>` use text equality |
| `OrderBy/ThenBy(decimal)` | ordering | Numeric sort of TEXT | `ORDER BY x COLLATE EF_DECIMAL` | Collation is registered per connection |
| `Sum/Average/Min/Max(decimal)` | aggregates | Exact aggregates | `ef_sum`, `ef_avg`, `ef_min`, `ef_max` (UDF aggregates; predicate becomes CASE, distinct becomes DISTINCT) | |

### 5d. Date/time (stored as TEXT)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DateTime.Year/Month/Day/DayOfYear/Hour/Minute/Second/DayOfWeek` | members | Date part | `CAST(strftime('%Y'\|'%m'\|'%d'\|'%j'\|'%H'\|'%M'\|'%S'\|'%w', d) AS INTEGER)` | |
| `DateTime.Millisecond` | member | | `(CAST(strftime('%f', d) AS REAL) * 1000) % 1000` | |
| `DateTime.Ticks` | member | | `CAST((julianday(d) - 1721425.5) * 864000000000 AS INTEGER)` | |
| `DateTime.Microsecond`, `Nanosecond` | member | — | Not translated | |
| `DateTime.Now` | static | Local now | `rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', 'now', 'localtime'), '0'), '.')` | |
| `DateTime.UtcNow` | static | UTC now | same as above with `'now'` only | |
| `DateTime.Today` | static | | `… strftime(fmt, 'now', 'localtime', 'start of day') …` | |
| `DateTime.Date` | member | Truncate to day | `… strftime(fmt, d, 'start of day') …` | |
| `DateTime.TimeOfDay` | member | | `rtrim(rtrim(strftime('%H:%M:%f', d), '0'), '.')` | |
| `DateTime.AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds` | methods | Date arithmetic | `rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f', d, CAST(n AS TEXT) \|\| ' days'), '0'), '.')` with the matching unit | |
| `DateTime.AddMilliseconds(ms)` / `AddTicks(t)` | methods | | modifier `CAST(ms / 1000.0 AS TEXT) \|\| ' seconds'` / `CAST(t / 10000000.0 AS TEXT) \|\| ' seconds'` | |
| `DateOnly.Year/Month/Day/DayOfYear/DayOfWeek` | members | | `CAST(strftime('%Y', d) AS INTEGER)` and so on | |
| `DateOnly.DayNumber` | member | | `CAST(julianday(d) - julianday('0001-01-01') AS INTEGER)` | |
| `DateOnly.AddYears/AddMonths/AddDays` | methods | | `date(d, CAST(n AS TEXT) \|\| ' years')` | |
| `DateOnly.FromDateTime(dt)` | static | | `date(dt)` | |
| DateTime/DateOnly/TimeOnly/DateTimeOffset/TimeSpan `+` / `-` | operators | — | **Not translated** (restricted binary expressions) | |
| DateTimeOffset, TimeSpan, ulong `< <= > >=` | comparisons | — | **Not translated** | |
| TimeOnly/TimeSpan/ulong `* /`, ulong `%` | operators | — | Not translated | |
| `OrderBy/ThenBy` on DateTimeOffset, TimeSpan, ulong | ordering | — | **Throws** `OrderByNotSupported` | |
| `Min/Max` on DateTimeOffset, TimeSpan, ulong | aggregate | — | **Throws** `AggregateOperationNotSupported` | |
| Negation of TimeOnly/TimeSpan | unary | — | Not translated | |
| DateTimeOffset, TimeOnly, TimeSpan members | — | — | No SQLite member translators | Gap compared with other providers |

### 5e. Primitive collections / JSON (needs SQLite ≥ 3.38 for json_each)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| Querying a primitive-collection column or parameter (`Where`, `Select`, `Contains` and so on) | queryable over `List<T>`/`T[]` | Table-valued JSON array expansion | `FROM json_each(col_or_@p) AS "x"` projecting `"x"."value"`, ordered by `"x"."key"` | Error `QueryingIntoJsonCollectionsNotSupported` below 3.38 |
| `.Count()` on a collection | | Array length | `json_array_length(col)` | |
| `.Any()` (no predicate) | | Non-empty | `json_array_length(col) > 0` | |
| `coll[i]` / `ElementAt(i)` | | Array index | `col ->> i` (constant paths: `col ->> '$[0]'`) | For parameter collections the VALUES form is rewritten to JSON |
| Owned/complex JSON scalar access | `JsonScalarExpression` | JSON path extraction | `col ->> 'Prop'`, `col ->> '$.A.B[0]'`, dynamic index `col ->> '$.A' ->> (@i)` | Uses the `->>` operator, not json_extract |
| Querying JSON collections of objects | `TransformJsonQueryToTable` | Shred a JSON array of objects | `(SELECT value ->> 'a' AS a, … FROM json_each(col, '$.Path'))` | |
| `ExecuteUpdate` on JSON property | partial update | In-place JSON patch | `json_set(col, '$.path', value)`; objects wrapped in `json(value)`; bool via `json('true'/'false')` | Throws `ExecuteUpdateJsonPartialUpdateDoesNotSupportUlong` |
| SaveChanges JSON partial update | update SQL | | `"col" = json_set("col", '$.path', json(@p))` | |
| byte[] inside JSON | read | | Stored as hex; read back with `unhex(value)` | |
| `APPLY`-requiring queries | | — | **Throws** `ApplyNotSupported` (no CROSS/OUTER APPLY or LATERAL) | |
| Nested set operations | | | Wrapped as `SELECT * FROM (…)` because SQLite rejects parenthesised compound selects | |
| Limit/offset | | Paging | `LIMIT n OFFSET m`; offset alone uses `LIMIT -1 OFFSET m` | |

### 5f. Spatial (NTS + SpatiaLite; `UseNetTopologySuite()`)

Boolean predicates are emitted as `CASE WHEN a IS NOT NULL AND b IS NOT NULL THEN Fn(a, b) END`. Indexes on N-th element accessors are shifted +1.

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Geometry.Area` | member | | `Area(g)` | |
| `Geometry.Boundary` | member | | `Boundary(g)` | |
| `Geometry.Centroid` | member | | `Centroid(g)` | |
| `Geometry.Dimension` | member | | `Dimension(g)` | |
| `Geometry.Envelope` | member | | `Envelope(g)` | |
| `Geometry.InteriorPoint`, `PointOnSurface` | member | | `PointOnSurface(g)` | |
| `Geometry.IsEmpty`, `IsSimple`, `IsValid` | member (bool) | | `IsEmpty(g)`, `IsSimple(g)`, `IsValid(g)` (CASE-guarded) | |
| `Geometry.Length` | member | | `GLength(g)` | |
| `Geometry.NumGeometries`, `NumPoints` | member | | `NumGeometries(g)`, `NumPoints(g)` | |
| `Geometry.SRID` | member | | `SRID(g)` | |
| `Geometry.GeometryType` | member | | `CASE rtrim(GeometryType(g), ' ZM') WHEN 'POINT' THEN 'Point' … END` | |
| `Geometry.OgcGeometryType` | member | | Same CASE, mapped to enum ints | |
| `Geometry.AsBinary()`, `ToBinary()` | method | WKB | `AsBinary(g)` | |
| `Geometry.AsText()`, `ToText()` | method | WKT | `AsText(g)` | |
| `Geometry.Buffer(d)`, `Buffer(d, int)` | method | | `Buffer(g, d[, q])` | |
| `Geometry.Contains/Crosses/CoveredBy/Covers/Disjoint/Intersects/Overlaps/Touches/Within(Geometry)` | method (bool) | Spatial predicates | `Contains(a,b)` and so on (same names) | |
| `Geometry.EqualsTopologically(g)` | method | | `Equals(a, b)` | |
| `Geometry.Relate(g, string)` | method | DE-9IM | `Relate(a, b, pattern)` | |
| `Geometry.ConvexHull()` | method | | `ConvexHull(g)` | |
| `Geometry.Difference/Intersection(g)` | method | | `Difference(a,b)` / `Intersection(a,b)` | |
| `Geometry.SymmetricDifference(g)` | method | | `SymDifference(a,b)` | |
| `Geometry.Union()` / `Union(g)` | method | | `UnaryUnion(g)` / `GUnion(a,b)` | |
| `Geometry.Distance(g)` | method | | `Distance(a,b)` | |
| `Geometry.IsWithinDistance(g, d)` | method | | `Distance(a,b) <= d` | |
| `Geometry.Reverse()` | method | | `ST_Reverse(g)` | |
| `Geometry.GetGeometryN(i)` / `GeometryCollection[i]` | method/indexer | | `GeometryN(g, i + 1)` | |
| `GeometryCollection.Count` | member | | `NumGeometries(g)` | |
| `LineString.Count` | member | | `NumPoints(g)` | |
| `LineString.StartPoint`, `EndPoint` | member | | `StartPoint(g)`, `EndPoint(g)` | |
| `LineString.IsClosed`, `IsRing` | member (bool) | | `IsClosed(g)`, `IsRing(g)` | |
| `LineString.GetPointN(i)` | method | | `PointN(g, i + 1)` | |
| `MultiLineString.IsClosed` | member | | `IsClosed(g)` (CASE-guarded) | |
| `Point.X/Y/Z/M` | member | | `X(g)`, `Y(g)`, `Z(g)`, `M(g)` | |
| `Polygon.ExteriorRing` | member | | `ExteriorRing(g)` | |
| `Polygon.NumInteriorRings` | member | | `NumInteriorRing(g)` | |
| `Polygon.GetInteriorRingN(i)` | method | | `InteriorRingN(g, i + 1)` | |
| `GeometryCombiner.Combine(IEnumerable<Geometry>)` | aggregate | Collect | `Collect(g)` | |
| `ConvexHull.Create(IEnumerable<Geometry>)` | aggregate | | `ConvexHull(Collect(g))` | |
| `UnaryUnionOp.Union(IEnumerable<Geometry>)` | aggregate | | `GUnion(g)` | |
| `EnvelopeCombiner.CombineAsGeometry(IEnumerable<Geometry>)` | aggregate | | `Extent(g)` | |
| Geometry literal | constant | | `GeomFromText('WKT', srid)` | |

## 6. Store types / type mappings

SQLite uses **type affinity**. Unknown store types resolve by substring rules in this order: `INT` → INTEGER (long), `CHAR`/`CLOB`/`TEXT` → TEXT, `BLOB` → BLOB, `REAL`/`FLOA`/`DOUB` → REAL, otherwise BLOB. Facets (size, precision, scale) are dropped from the store type (`WithStoreTypeAndSize(name, null)`). There are no length or precision semantics.

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| string | TEXT | | `'…'` literal; CR/LF emitted as `CHAR(13)`/`CHAR(10)` concatenated with `\|\|` | |
| byte[] | BLOB | | `X'…'` | In JSON: hex |
| bool, byte, sbyte, short, ushort, int, uint, long | INTEGER | | | bool is 0/1 |
| ulong | INTEGER | | Literal is `(long)value` (wraps) | No ordering, comparison, min/max or arithmetic |
| char | TEXT | | | |
| double, float | REAL | | | |
| decimal | TEXT | Exact decimal as text | Literal `'1.5'` | UDF arithmetic (section 5c); JSON stored as string |
| DateTime | TEXT | | `'yyyy-MM-dd HH:mm:ss.FFFFFFF'` | Space separator, not `T` |
| DateTimeOffset | TEXT | | `'yyyy-MM-dd HH:mm:ss.FFFFFFFzzz'` | Not orderable or comparable server-side |
| DateOnly | TEXT | | `'yyyy-MM-dd'` | |
| TimeOnly | TEXT | | `'HH:mm:ss[.fffffff]'` | |
| TimeSpan | TEXT | | | Not orderable server-side |
| Guid | TEXT | | Uppercase `'XXXXXXXX-…'` | A BLOB guid store type is possible via converter |
| JSON (owned/complex `ToJson`, primitive collections) | TEXT | | `SqliteJsonTypeMapping` | |
| NTS `Geometry` and subtypes | `GEOMETRY`, `POINT`, `LINESTRING`, `POLYGON`, `MULTIPOINT`, `MULTILINESTRING`, `MULTIPOLYGON`, `GEOMETRYCOLLECTION` (+ `Z`/`M`/`ZM` variants) | Spatial | SpatiaLite blob via `GaiaGeoReader`/`GaiaGeoWriter` converter (`GeometryValueConverter<T>` : `TGeometry` ↔ `byte[]`) | Z/M ordinates are taken from the store type suffix |

## 7. Migrations

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| Table rebuild | automatic | Emulated ALTER TABLE | For AlterColumn, DropColumn (deferred), Add/Drop PK, Add/Drop unique, Add/Drop check, Add/Drop FK on an existing table, AlterTable, and comments on AddColumn: `CREATE TABLE "ef_temp_T" (…)`; `INSERT INTO "ef_temp_T" (…) SELECT … FROM "T"`; `PRAGMA foreign_keys = 0` (suppressTransaction); `DROP TABLE "T"`; `ALTER TABLE "ef_temp_T" RENAME TO "T"`; `PRAGMA foreign_keys = 1`; then indexes are re-created | Requires the target model. Skipped with `TableRebuildPendingWarning` when other ops (Sql, data ops) interleave |
| `IsSqlite` | `MigrationBuilder.IsSqlite()` | Provider check in migrations | | |
| Unsupported operations | throw `InvalidMigrationOperation` | | AlterColumn, Add/Drop PK, unique, check, FK and RenameIndex (when not rewritten), sequences (`SequencesNotSupported`) | RenameIndex is rewritten as DROP + CREATE when the model is available |
| Schemas | `EnsureSchema`/`DropSchema` | | No-op | |
| Inline PK / AUTOINCREMENT | CreateTable | | Single-column PK inlined: `"Id" INTEGER NOT NULL CONSTRAINT "PK_T" PRIMARY KEY AUTOINCREMENT` | Annotations `Sqlite:InlinePrimaryKey[Name]` |
| Computed columns | ColumnDefinition | | `"C" AS (sql) [STORED] [COLLATE x]` | No type in the generated column definition |
| Column comments | CreateTable | | Emitted as `-- comment` lines inside CREATE TABLE | Changing a comment triggers a rebuild |
| Spatial columns | AddColumn / CreateTable | | Removed from CREATE TABLE and emitted as `SELECT AddGeometryColumn('T','C', srid, 'POINT', -1, isNotNull)` | |
| `InitSpatialMetaData` | AlterDatabase annotation | Initialise spatial catalog | `SELECT InitSpatialMetaData()` when any spatial column exists | |
| Rename | | | `ALTER TABLE … RENAME TO …`, `ALTER TABLE … RENAME COLUMN … TO …` | |
| History table | `__EFMigrationsHistory` | | `CREATE TABLE IF NOT EXISTS`; existence check via `sqlite_master` | |
| Idempotent scripts | `GetBeginIfNotExistsScript` / `GetBeginIfExistsScript` / `GetEndIfScript` | | **Throw** `MigrationScriptGenerationNotSupported` | |
| Migration lock | `AcquireDatabaseLock[Async]` | Cross-process migration mutex | Table `__EFMigrationsLock("Id" INTEGER PK, "Timestamp" TEXT)`; `INSERT OR IGNORE … VALUES(1, ts)` in a retry loop with exponential backoff (cap about 1 minute); release with `DELETE … WHERE "Id" = 1` | `LockReleaseBehavior.Explicit`, so a crashed process leaves the lock row behind |

## 8. Database-level: creator, connection, execution

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `EnsureCreated`/`Create` | `SqliteDatabaseCreator.Create` | Create database | Opens the connection (creates the file) and runs `PRAGMA journal_mode = 'wal';` | WAL is enabled on create |
| `Exists` | | Database exists | `:memory:` or `Mode=Memory` gives true; otherwise opens a read-only, non-pooled connection, and `SQLITE_CANTOPEN` (14) gives false | |
| `HasTables` | | | `SELECT COUNT(*) FROM "sqlite_master" WHERE "type" = 'table' AND "rootpage" IS NOT NULL` | |
| `EnsureDeleted`/`Delete` | | Drop database | File: `SqliteConnection.ClearAllPools()` then `File.Delete(path)`. In-memory: clears the pool and reopens | |
| Connection init: UDFs | per connection | Provider-registered functions | `regexp(pattern, input)`, `ef_mod`, `ef_add`, `ef_divide`, `ef_compare`, `ef_multiply`, `ef_negate` (scalars); `ef_avg`, `ef_max`, `ef_min`, `ef_sum` (aggregates); collation `EF_DECIMAL` | Only on a `SqliteConnection`; other connection types log `UnexpectedConnectionTypeWarning` |
| Command timeout | | | `CommandTimeout` is copied to `SqliteConnection.DefaultTimeout` | |
| `SpatialiteLoader.Load(DbConnection)` / `TryLoad(DbConnection)` | public static | Load an extension | Locates `mod_spatialite` from the deps runtime graph, adjusts PATH, then `LoadExtension("mod_spatialite")` | Public API (`Microsoft.EntityFrameworkCore.Infrastructure`) |
| Transactions | SQL helper | | `BEGIN TRANSACTION;` | Identifiers are double-quoted; schema is always dropped from delimiting |
| Batching | `SqliteModificationCommandBatchFactory` | | `SingularModificationCommandBatch`: one command per DbCommand, no multi-statement batching | |
| Execution strategy | | | None provider-specific (default non-retrying) | |
| Read-only connection | `ISqliteRelationalConnection.CreateReadOnlyConnection()` | | `Mode=ReadOnly; Pooling=False` | Internal |
| Scaffolding | `SqliteDatabaseModelFactory` | Reverse engineering | Reads `sqlite_master` / pragma; infers CLR types from data (`InferringTypes`, `OutOfRangeWarning`, `FormatWarning`) | Design-time |

## 9. Diagnostics / event IDs (`SqliteEventId`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `SchemaConfiguredWarning` | Model.Validation | Schema ignored | | |
| `SequenceConfiguredWarning` | Model.Validation | Sequence ignored | | |
| `CompositeKeyWithValueGeneration` | Model.Validation | Composite PK cannot be AUTOINCREMENT | | |
| `ConflictingValueGenerationStrategiesWarning` | Model.Validation | Strategy conflicts with default value or computed SQL | | `ConflictingValueGenerationStrategiesEventData` (public) |
| `UnexpectedConnectionTypeWarning` | Infrastructure | Non-`SqliteConnection` passed, so UDFs and collation are not registered | | |
| `TableRebuildPendingWarning` | Migrations | Rebuild skipped because of interleaved operations | | |
| `ColumnFound`, `TableFound`, `IndexFound`, `ForeignKeyFound`, `PrimaryKeyFound`, `UniqueConstraintFound`, `SchemasNotSupportedWarning`, `MissingTableWarning`, `ForeignKeyReferencesMissingTableWarning`, `ForeignKeyPrincipalColumnMissingWarning`, `InferringTypes`, `OutOfRangeWarning`, `FormatWarning` | Scaffolding | Reverse-engineering logs | | |
| Error strings users hit | `SqliteStrings` | | `AggregateOperationNotSupported`, `ApplyNotSupported`, `DefaultNotSupported` (no `DEFAULT` keyword), `DuplicateColumnNameSridMismatch`, `ExecuteUpdateJsonPartialUpdateDoesNotSupportUlong`, `IncompatibleSqlReturningClauseMismatch`, `InvalidMigrationOperation`, `MigrationScriptGenerationNotSupported`, `OrderByNotSupported`, `QueryingIntoJsonCollectionsNotSupported`, `SequencesNotSupported`, `StoredProceduresNotSupported` | |

### Experimental / obsolete markers

- `[Obsolete]` `SqliteLegacyUpdateSqlGenerator` (internal): "Call UseSqlReturningClause(false) instead".
- `[Experimental(EFDiagnostics.PrecompiledQueryExperimental)]` on members of `SqliteQueryCompilationContext` and `SqliteQueryCompilationContextFactory` (precompiled/NativeAOT queries).
- `TrySerializeScalarToJson` override uses `EF9002` (an experimental base API).
- No public user-facing SQLite API is Experimental or Obsolete.
