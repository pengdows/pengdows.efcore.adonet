# Pomelo.EntityFrameworkCore.MySql (main, 9.0.1 / EF Core 9): provider-specific API inventory

Source: `providers/pomelo/src/EFCore.MySql` (+ `EFCore.MySql.Json.Microsoft`, `EFCore.MySql.Json.Newtonsoft`, `EFCore.MySql.NTS`). Only surface **beyond plain relational EF Core** is listed.

MySQL/MariaDB divergence is driven by `ServerVersion` (`MySqlServerVersion` / `MariaDbServerVersion`) and its `Supports.*` flags (`Infrastructure/ServerVersionSupport.cs`). Marked **[M]** = MySQL-only / MySQL version gate, **[MDB]** = MariaDB-only / gate. Latest supported: MySQL 8.4.3, MariaDB 11.6.2.

Nothing in the provider carries `[Experimental]`. Obsolete members are marked **[Obsolete]**.

---

## 1. Options builder (`UseMySql(...)` and `MySqlDbContextOptionsBuilder`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseMySql` | `(this DbContextOptionsBuilder, ServerVersion, Action<MySqlDbContextOptionsBuilder>?)` (+ `string connectionString`, `DbConnection`, `DbDataSource` overloads; generic `<TContext>` variants) | Provider selection + mandatory server dialect/version | `ServerVersion` is **required** (throws if null); it drives every feature gate | 8 overloads total. No "auto" default, so the user must call `ServerVersion.AutoDetect` |
| `AddMySql<TContext>` | `(IServiceCollection, string connStr, ServerVersion, Action<MySqlDbContextOptionsBuilder>?, Action<DbContextOptionsBuilder>?)` | DI registration shortcut | Same as UseMySql | |
| `AddEntityFrameworkMySql` | `(IServiceCollection)` | Internal provider service registration | | |
| `EnableRetryOnFailure` | `()`, `(int maxRetryCount)`, `(int, TimeSpan maxRetryDelay, ICollection<int> errorNumbersToAdd)` | Transient-fault retry policy | `MySqlRetryingExecutionStrategy`; transient = `MySqlException.IsTransient` or `TimeoutException` (+ the extra error numbers) | Same shape as SqlServer/Npgsql |
| `ExecutionStrategy(...)`, `CommandTimeout`, `MaxBatchSize`, `MinBatchSize`, `MigrationsAssembly`, `MigrationsHistoryTable`, `UseQuerySplittingBehavior`, `UseRelationalNulls`, `TranslateParameterizedCollectionsToConstants`/`...ToParameters` | inherited from `RelationalDbContextOptionsBuilder` | (relational) | | Not Pomelo-specific. The MySQL pipeline additionally **inlines** parameters wherever MySQL can't accept them (`MySqlParameterInliningExpressionVisitor`), e.g. LIMIT in IN/ANY subqueries |
| `SchemaBehavior` | `(MySqlSchemaBehavior behavior, MySqlSchemaNameTranslator translator = null)` | Schema emulation policy | `Throw` (default): any schema throws. `Ignore`: drop it. `Translate`: delegate `(schema, object) => name` folds the schema into the object name | MySQL "schema" == database |
| `DefaultDataTypeMappings` | `(Func<MySqlDefaultDataTypeMappings, MySqlDefaultDataTypeMappings>)` | Default CLR to store type overrides | `WithClrBoolean(MySqlBooleanType{Default,TinyInt1,Bit1,None})`, `WithClrDateTime(MySqlDateTimeType{Default,DateTime,DateTime6,Timestamp6,Timestamp})`, `WithClrDateTimeOffset(same)`, `WithClrTimeSpan(MySqlTimeSpanType{Default,Time,Time6})`, `WithClrTimeOnly(int? precision 0-6)` | `MySqlBooleanType.None` is a legacy value (TODO remove) |
| `EnableIndexOptimizedBooleanColumns` | `(bool enable = true)` | Sargable boolean predicates | `e.Flag` becomes `Flag = TRUE`, `!e.Flag` becomes `Flag = FALSE` (instead of bare column / `NOT col`) | Default false |
| `LimitKeyedOrIndexedStringColumnLength` | `(bool enable = true)` | Auto-cap key/index string length | Unsized string key/index columns get `varchar(min(MaxKeyLength/(maxBytesPerChar*2),255))`. MaxKeyLength is 3072 (5.7.7+/MDB 10.2.2+), else 767 | Default **true**. Turn off when using `HasPrefixLength` |
| `EnableStringComparisonTranslations` | `(bool enable = true)` | Opt in to culture/case-insensitive string comparison | Enables `string.Equals/StartsWith/EndsWith/Contains/IndexOf(…, StringComparison)` translation (see sec. 5); otherwise these throw, and the error message points at this option | Default false (no index use) |
| `EnablePrimitiveCollectionsSupport` | `(bool enable = true)` | Opt in to primitive collections via JSON_TABLE | Without it, `TranslatePrimitiveCollection` throws "Primitive collections support has not been enabled" | Default false. Doc warns that MySQL can crash and MariaDB support is incomplete |
| `DisableBackslashEscaping` | `(bool setSqlModeOnOpen = true)` | Literal-escaping mode | Assumes `sql_mode=NO_BACKSLASH_ESCAPES` and escapes only `'`. Optionally sets the mode on open | Affects constants and inlined params |
| `SetSqlModeOnOpen` | `()` | Session initialisation | Runs `SET SESSION sql_mode = CONCAT(@@sql_mode, …)` after each open | |
| `DisableLineBreakToCharSubstition` | `()` | Literal formatting | Stops emitting `\r`/`\n` in literals as `CHAR(13)`/`CHAR(10)` concatenations | Typo "Substition" is the real name |
| `CharSet` | `(CharSet)` | Default charset | Throws | **[Obsolete(error)]**: use `HasCharSet` |
| `UseMicrosoftJson` (Json.Microsoft pkg) | `(MySqlCommonJsonChangeTrackingOptions = RootPropertyOnly)` / `(MySqlJsonChangeTrackingOptions)` | JSON column support (System.Text.Json) | Maps `JsonDocument`, `JsonElement`, string and POCO to `json` | Options enum: `RootPropertyOnly, FullHierarchyOptimizedFast, FullHierarchyOptimizedSemantically, FullHierarchySemantically` |
| `UseNewtonsoftJson` (Json.Newtonsoft pkg) | same shapes | JSON column support (Json.NET) | `JToken`/POCO/string to `json` | |
| `UseNetTopologySuite` (NTS pkg) | `()` | Spatial type support | Maps NTS `Geometry` family | |
| Connection-string driven (MySqlConnector) | `GuidFormat`, `OldGuids`, `TreatTinyAsBoolean` | Type-mapping inputs | Read by `MySqlConnectionSettings`; change the Guid and bool mappings | Effectively part of the option surface |

## 2. Model-building API

Delegation: `DelegationModes` [Flags] = `Default=0, ApplyToDatabases=1, ApplyToTables=2, ApplyToColumns=4, ApplyToAll=7`. It controls whether a charset or collation set at a level is also **written explicitly** onto child objects.

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `ModelBuilder.AutoIncrementColumns` | `()` | Default key generation = identity | Sets model `ValueGenerationStrategy = IdentityColumn` (already the convention default) | |
| `ModelBuilder.HasCharSet` | `(string charSet, DelegationModes? = null)` / `(string, bool explicitlyDelegateToChildren)` | Database default character set (encoding) | `CREATE/ALTER DATABASE … CHARACTER SET x`, propagated per delegation | `false` means `ApplyToDatabases` only |
| `ModelBuilder.UseCollation` | `(string collation, DelegationModes?)` / `(string, bool? explicitlyDelegateToChildren)` | Database default collation | `ALTER DATABASE COLLATE x` (+ tables/columns) | Overloads relational `UseCollation(string)` |
| `ModelBuilder.UseGuidCollation` | `(string collation)` | Collation for char-based Guid columns | Default `ascii_general_ci`. `""` means none | Only with Char36/Char32 Guid format |
| `EntityTypeBuilder[<T>].HasCharSet` | `(string, DelegationModes? = null)` / `(string, bool)` | Table character set | `CREATE TABLE … CHARACTER SET=x` | |
| `EntityTypeBuilder[<T>].UseCollation` | `(string, DelegationModes? = null)` / `(string, bool)` | Table collation | `… COLLATE=x` | |
| `EntityTypeBuilder[<T>].HasTableOption` | `(string name, string value)` | Raw table storage options | Appended to CREATE TABLE, e.g. `ENGINE=MyISAM`, `ROW_FORMAT=…`, `AUTO_INCREMENT=1000`. Diffed in ALTER TABLE | Stored as the `MySql:StoreOptions` annotation |
| `PropertyBuilder[<T>].UseMySqlIdentityColumn` | `()` | Identity / auto-increment column | `AUTO_INCREMENT` on integer/enum/decimal types. On `DateTime`/`DateTimeOffset` it means `DEFAULT CURRENT_TIMESTAMP[(n)]` | Source TODO says it will be renamed `UseAutoIncrementColumn` |
| `PropertyBuilder[<T>].UseMySqlComputedColumn` | `()` | Store-maintained timestamp (on add+update) | `DateTime`/`DateTimeOffset`: `DEFAULT CURRENT_TIMESTAMP(n) ON UPDATE CURRENT_TIMESTAMP(n)` | Not for general computed SQL; use relational `HasComputedColumnSql` |
| `PropertyBuilder[<T>].HasCharSet` | `(string charSet)` | Column character set | `col varchar(n) CHARACTER SET x` | |
| `ComplexTypePropertyBuilder[<T>].HasCharSet` | `(string charSet)` | Column charset for a complex-type property | as above | |
| `PropertyBuilder[<T>].HasCollation` | `(string)` | Column collation | | **[Obsolete]**: use relational `UseCollation` |
| `PropertyBuilder[<T>].HasSpatialReferenceSystem` | `(int? srid)` | Spatial column SRID constraint | `geometry /*!80003 SRID 4326 */` | **[M]** 8.0.3+ (`SpatialReferenceSystemRestrictedColumns`) |
| `IndexBuilder[<T>].IsFullText` | `(bool fullText = true, string parser = null)` | Full-text index | `CREATE FULLTEXT INDEX … [WITH PARSER ngram]` | Parser is **[M]** 5.7.3+ (`FullTextParser`) |
| `IndexBuilder[<T>].IsSpatial` | `(bool spatial = true)` | Spatial index | `CREATE SPATIAL INDEX` | |
| `IndexBuilder[<T>].HasPrefixLength` | `(params int[] prefixLengths)` | Index key prefix length per column | `INDEX (col(20), col2)`. 0 means the full column | |
| `KeyBuilder.HasPrefixLength` | `(params int[] prefixLengths)` | PK/AK key prefix length | `PRIMARY KEY (col(20))` | |
| `IndexBuilder[<T>].ForMySqlIsFullText` / `ForMySqlIsSpatial` | `(bool = true)` | | | **[Obsolete]** |
| `[MySqlCharSet("x", DelegationModes)]` | attribute on class/property | Charset via annotation | Same as `HasCharSet` | `DataAnnotations/` |
| `[MySqlCollation("x", DelegationModes)]` | attribute on class/property | Collation via annotation | Same as `UseCollation` | |
| `PropertyBuilder.UseJsonChangeTrackingOptions` (Json pkgs) | `(MySqlCommonJsonChangeTrackingOptions? / MySqlJsonChangeTrackingOptions?)` | Per-property JSON change-detection depth | Value comparer selection | |
| Convention builders: `HasValueGenerationStrategy`, `CanSetValueGenerationStrategy`, `HasCharSet`, `CanSetCharSet`, `CanSetCharSetDelegation`, `UseCollation`, `CanSetCollation(Delegation)`, `UseGuidCollation`, `CanSetGuidCollation`, `HasTableOption`, `CanSetTableOption` | on `IConvention*Builder` | Convention-level variants of the above | | |
| Metadata accessors (`MySqlModelExtensions`, `MySqlEntityTypeExtensions`, `MySqlPropertyExtensions`, `MySqlIndexExtensions`, `MySqlKeyExtensions`) | `Get/Set{CharSet, CharSetDelegation, Collation(Delegation), GuidCollation, ValueGenerationStrategy, TableOptions, SpatialReferenceSystem, IsFullText, FullTextParser, IsSpatial, PrefixLength}` | Read/write annotations | | `IsCompatibleAutoIncrementColumn(property)` helper is public |
| Annotations | `MySql:ValueGenerationStrategy, CharSet, CharSetDelegation, Collation, CollationDelegation, GuidCollation, StoreOptions, FullTextIndex, FullTextParser, SpatialIndex, IndexPrefixLength, SpatialReferenceSystemId` | Persisted model and migration annotations | | |

## 3. Value generation strategies and key generation

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `MySqlValueGenerationStrategy.None` | enum | No store generation | | |
| `MySqlValueGenerationStrategy.IdentityColumn` | enum | Identity/auto-increment (+ insert timestamp) | Integer/enum/decimal: `AUTO_INCREMENT`, read back with `SELECT LAST_INSERT_ID()` + `ROW_COUNT()`, or `RETURNING` on **[MDB]** 10.5+. DateTime/DTO: `DEFAULT CURRENT_TIMESTAMP(n)` | Model-wide default from the convention. Only one AUTO_INCREMENT column per table, and it must lead an index |
| `MySqlValueGenerationStrategy.ComputedColumn` | enum | Auto-updated timestamp | `ON UPDATE CURRENT_TIMESTAMP(n)` | TODO says it will be removed |
| Row version (`IsRowVersion()`/`[Timestamp]`) | `byte[]` or `DateTime` | Optimistic concurrency token | `byte[]` maps to `timestamp(6)` through `BytesToDateTimeConverter`, `DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)` | No native rowversion type |
| `MySqlSequentialGuidValueGenerator` | client-side | Sequential (time-ordered) GUID keys | Time-prefixed UUIDv4-variant; byte layout adapts to `GuidFormat` | Used for Guid keys when `ValueGeneratedOnAdd` and no default SQL. Otherwise `TemporaryGuidValueGenerator` |
| Sequences (`HasSequence`, relational) | | Sequence objects | `CREATE/ALTER/DROP/RENAME SEQUENCE` | **[MDB]** 10.3+ only (`Supports.Sequences`). MySQL has none, and there is no Hi-Lo or sequence key strategy |

## 4. `EF.Functions` extensions

`MySqlDbFunctionsExtensions` (core), `MySqlJsonDbFunctionsExtensions` (core), `MySqlSpatialDbFunctionsExtensions` (NTS pkg). Relational `EF.Functions.Like/Collate` are also available.

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DateDiffYear` | `(DateTime/DateTimeOffset/DateOnly [?] start, end) -> int[?]` | Date difference in years | `TIMESTAMPDIFF(YEAR, s, e)` | 6 overloads each (3 types x nullable) |
| `DateDiffQuarter` | same | Diff quarters | `TIMESTAMPDIFF(QUARTER, s, e)` | |
| `DateDiffMonth` | same | Diff months | `TIMESTAMPDIFF(MONTH, s, e)` | |
| `DateDiffWeek` | same | Diff weeks | `TIMESTAMPDIFF(WEEK, s, e)` | |
| `DateDiffDay` | same | Diff days | `TIMESTAMPDIFF(DAY, s, e)` | |
| `DateDiffHour` | same | Diff hours | `TIMESTAMPDIFF(HOUR, s, e)` | |
| `DateDiffMinute` | same | Diff minutes | `TIMESTAMPDIFF(MINUTE, s, e)` | |
| `DateDiffSecond` | same | Diff seconds | `TIMESTAMPDIFF(SECOND, s, e)` | |
| `DateDiffMillisecond` | same | Diff ms | `TIMESTAMPDIFF(MICROSECOND, s, e) DIV 1000` | |
| `DateDiffMicrosecond` | same | Diff µs | `TIMESTAMPDIFF(MICROSECOND, s, e)` | |
| `DateDiffTick` | same | Diff .NET ticks | `TIMESTAMPDIFF(MICROSECOND, s, e) * 10` | Pomelo-unique |
| `DateDiffNanosecond` | same | Diff ns | `TIMESTAMPDIFF(MICROSECOND, s, e) * 1000` | |
| `ConvertTimeZone` | `(DateTime/DateOnly[?], string from, string to) -> DateTime?/DateOnly?` | Time-zone conversion | `CONVERT_TZ(x, from, to)` | Needs tz tables loaded, else NULL |
| `ConvertTimeZone` | `(DateTime/DateOnly[?], string to)` | Convert from session TZ | `CONVERT_TZ(x, @@session.time_zone, to)` | |
| `ConvertTimeZone` | `(DateTimeOffset[?], string to)` | Convert from UTC | `CONVERT_TZ(x, '+00:00', to)` | 10 overloads total |
| `Like<T>` | `(T match, string pattern)` | LIKE on any scalar type | `match LIKE pattern` | T may be numeric, date, Guid, bool, byte[], etc. (implicit cast) |
| `Like<T>` | `(T match, string pattern, string escapeCharacter)` | LIKE with escape | `match LIKE pattern ESCAPE 'c'` | |
| `Match` | `(string property, string pattern, MySqlMatchSearchMode) -> double` | Full-text relevance score | `MATCH (col) AGAINST (pattern [IN BOOLEAN MODE / WITH QUERY EXPANSION])` | Needs a FULLTEXT index. Mode must be a constant |
| `Match` | `(string[] properties, …)` | Multi-column full-text score | `MATCH (c1, c2) AGAINST (…)` | Array must match the index columns |
| `IsMatch` | `(string, string pattern, MySqlMatchSearchMode) -> bool` | Full-text predicate | `MATCH(...) AGAINST(...) > 0`. A parameterised mode expands to an OR of all modes | |
| `IsMatch` | `(string[] …)` | Multi-column FT predicate | as above | |
| `MySqlMatchSearchMode` | enum `NaturalLanguage, NaturalLanguageWithQueryExpansion, Boolean` | FT query mode | | |
| `Hex<T>` | `(T value) -> string` | Hex-encode | `HEX(x)` | string, byte[], all numerics |
| `Unhex` | `(string) -> string` | Hex-decode | `UNHEX(x)` | |
| `Degrees` | `(double/float)` | Radians to degrees | `DEGREES(x)` | |
| `Radians` | `(double/float)` | Degrees to radians | `RADIANS(x)` | |
| `AsJson` | `(string) -> MySqlJsonString` | Treat a string as JSON | Applies the `json` type mapping (CAST to JSON on MySQL) | |
| `JsonType` | `(object json) -> string` | JSON value type | `JSON_TYPE(j)` | |
| `JsonQuote` | `(string)` | Quote as JSON string | `JSON_QUOTE(s)` | |
| `JsonUnquote` | `(object)` | Unquote JSON scalar | `JSON_UNQUOTE(j)` | |
| `JsonExtract<T>` | `(object json, params string[] paths)` | Extract by JSON path | `JSON_EXTRACT(j, p1[, p2…])` | |
| `JsonOverlaps` | `(object, object) -> bool` | Any common element | `JSON_OVERLAPS(a, b)` | **[M]** 8.0+, **[MDB]** 10.9+ |
| `JsonContains` | `(object json, object candidate[, string path])` | Containment | `JSON_CONTAINS(j, c[, path])` | |
| `JsonContainsPath` | `(object, string path)` | Path exists | `JSON_CONTAINS_PATH(j, 'one', p)` | |
| `JsonContainsPathAny` | `(object, params string[])` | Any path exists | `JSON_CONTAINS_PATH(j, 'one', p…)` | |
| `JsonContainsPathAll` | `(object, params string[])` | All paths exist | `JSON_CONTAINS_PATH(j, 'all', p…)` | |
| `JsonSearchAny` | `(object, string search[, string path[, string escapeChar]])` | Value search | `JSON_SEARCH(j, 'one', s[, esc, path]) IS NOT NULL` | |
| `SpatialDistancePlanar` (NTS) | `(Geometry, Geometry) -> double` | Cartesian distance | `ST_Distance(g1, g2)` (Cartesian even for SRID 4326) | |
| `SpatialDistanceSphere` (NTS) | `(Geometry, Geometry, SpatialDistanceAlgorithm{Native,Andoyer,Haversine})` | Geodesic distance | Native: `ST_Distance_Sphere`. Andoyer: `ST_Distance` on **[M]** 8.0+, emulated formula otherwise. Haversine: emulated SQL expression | |

## 5. LINQ translations beyond relational core

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `string.Length` | member | Character length | `CHAR_LENGTH(s)` | |
| `byte[].Length` | member | Byte length | `LENGTH(b)` | |
| `string.ToLower/ToUpper` | `()` | Case fold | `LOWER/UPPER` | |
| `string.Substring` | `(int)`, `(int,int)` | Substring | `SUBSTRING(s, i+1, len)`; 1-arg form uses `CHAR_LENGTH(s)` | |
| `string.Replace` | `(string,string)` | Replace | `REPLACE` | |
| `string.IndexOf` | `(string)`, `(string,int)` | Position | `LOCATE(sub, s[, start+1]) - 1` | |
| `string.Contains/StartsWith/EndsWith` | `(string)` | Pattern match | Constant: `LIKE '%x%'` with escaping. Parameter: escaped pattern parameter. Column: `LOCATE(...) > 0` / `LEFT(s, CHAR_LENGTH(p)) = p` / `RIGHT(...)` | |
| `string.Equals/StartsWith/EndsWith/Contains/IndexOf(…, StringComparison)` | incl. static `Equals(a,b,cmp)`, `IndexOf(s,int,cmp)` | Case/culture-aware comparison | Case-sensitive modes (Ordinal/CurrentCulture/InvariantCulture): `x COLLATE utf8mb4_bin`. IgnoreCase modes: `LCASE(x) … LCASE(y)` | Requires `EnableStringComparisonTranslations`. A parameterised comparison expands to a CASE over the modes |
| `string.Trim/TrimStart/TrimEnd` | `()`, `(char)`, `(char[])` | Trim | `TRIM([BOTH/LEADING/TRAILING] 'c' FROM s)` | |
| `string.PadLeft/PadRight` | `(int[, char])` | Pad | `LPAD/RPAD(s, n, c)` | |
| `string.Remove` | `(int)`, `(int,int)` | Remove range | `SUBSTRING`/`CONCAT` combos | |
| `string.IsNullOrWhiteSpace` | static | Blank check | `s IS NULL OR TRIM(s) = ''` | |
| `string.Concat` | all overloads | Concatenation | `CONCAT(a, b, …)` | |
| `string.Join` | `(string sep, …)` | Join values | `CONCAT_WS(sep, …)` | Not an aggregate (no GROUP_CONCAT) |
| `string.FirstOrDefault()/LastOrDefault()` | Enumerable over string | First/last char | `SUBSTRING(s,1,1)` / `SUBSTRING(s, CHAR_LENGTH(s), 1)` | |
| `s[i]` / `ElementAt` on string | | Char at | `SUBSTRING(s, i+1, 1)` | |
| `byte[].Contains(b)` | Enumerable | Byte search | `LOCATE(CHAR(b), x) > 0` | |
| `byte[].First()` / `b[i]` | Enumerable/index | Byte at | `ASCII(SUBSTRING(x, i+1, 1))` | |
| `Regex.IsMatch` | `(string input, string pattern)` | Regex match | `input REGEXP pattern` | No RegexOptions overload |
| `Guid.NewGuid()` | static | Server GUID | `UUID()` | |
| `x.ToString()` | on int types, decimal, double, float, char, bool, DateTime, DTO, TimeSpan, Guid, byte[] | To text | `CAST(x AS char)`. bool: `CASE WHEN x THEN 'True' ELSE 'False' END` | |
| `Convert.ToBoolean/ToByte/ToDecimal/ToDouble/ToInt16/ToInt32/ToInt64/ToString` | static | Type conversion | `CAST(x AS signed/unsigned/decimal/double/char …)` | double/float CAST **[M]** 8.0.17+ / **[MDB]** 10.4+. float is emitted as double |
| `Math.Abs/Ceiling/Floor/Round(x)/Round(x,n)/Truncate/Pow/Sqrt/Exp/Log/Log(x,b)/Log10/Sign/Sin/Cos/Tan/Asin/Acos/Atan/Atan2` + `MathF.*` | static | Math | `ABS, CEILING, FLOOR, ROUND, TRUNCATE(x,0), POWER, SQRT, EXP, LOG, LOG10, SIGN, SIN, COS, TAN, ASIN, ACOS, ATAN, ATAN2` | |
| `double/float.DegreesToRadians/RadiansToDegrees` | static | Angle conversion | `RADIANS/DEGREES` | |
| `Math.Max/Min` (and EF 9 Greatest/Least) | static | Greatest/least | `GREATEST(...)`, `LEAST(...)` | via `GenerateGreatest/GenerateLeast` |
| `DateTime/DTO/DateOnly/TimeOnly .Year/.Month/.Day/.Hour/.Minute/.Second` | member | Date part | `EXTRACT(year FROM x)` … | |
| `.Millisecond` | member | ms part | `EXTRACT(microsecond FROM x) DIV 1000` | |
| `.DayOfYear` / `.DayOfWeek` | member | Day ordinal | `DAYOFYEAR(x)` / `DAYOFWEEK(x) - 1` | |
| `.Date` | member | Truncate to date | `CONVERT(x, date)` | |
| `.TimeOfDay` | member | Time part | `CAST(x AS time)` | |
| `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow` | static member | Current time | `CURRENT_TIMESTAMP(6)` / `UTC_TIMESTAMP(6)` / `CURDATE()` / `UTC_DATE()` | Precision 6 only when `DateTime6` is supported |
| `DateOnly.DayNumber` | member | Day count | `TO_DAYS(x) - 366` | |
| `DateTimeOffset.DateTime/UtcDateTime` / `.LocalDateTime` | member | Offset handling | CAST to datetime / `CONVERT_TZ(x, '+00:00', @@session.time_zone)` | DTO is stored as UTC (no offset type) |
| `TimeSpan.Hours/Minutes/Seconds/Milliseconds` | member | Time part | `EXTRACT(hour FROM t)` … | |
| `DateTime/DTO/DateOnly .AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds/AddMilliseconds`, `TimeOnly.AddHours/AddMinutes` | method | Date arithmetic | `DATE_ADD(x, INTERVAL n unit)` (ms becomes `INTERVAL n*1000 microsecond`). TimeOnly is wrapped in `TIME(...)` | |
| `TimeOnly.Add(TimeSpan)` | method | Time arithmetic | `ADDTIME(t, ts)` | |
| `TimeOnly.IsBetween` | method | Time range | `t >= a AND t < b` | |
| `TimeOnly.FromDateTime/FromTimeSpan`, `DateOnly.FromDateTime`, `DateOnly.ToDateTime(TimeOnly)` | static | Date/time construction | `TIME(x)`, `DATE(x)`, `ADDTIME(CAST(d AS datetime), t)` | |
| `DateTimeOffset.ToUnixTimeSeconds/Milliseconds` | method | Unix epoch | `TIMESTAMPDIFF(second/microsecond, <UnixEpoch constant>, x)` (ms: `DIV 1000`) | |
| JSON POCO / DOM member access (Json pkgs) | `e.JsonCol.Prop`, `JsonElement.GetProperty("a")`, `.GetString()/GetInt32()/…`, `GetArrayLength()`, `JsonDocument.RootElement`, list `.Count`, `[i]` | JSON navigation | `JSON_UNQUOTE(JSON_EXTRACT(col, '$.a[0]'))`. Non-constant paths use `CONCAT('$.', …)`. `JSON_LENGTH(...)` for counts | `TryGet*` throws |
| Owned/complex `ToJson()` scalar access (EF core JSON) | | Mapped JSON columns | `JSON_VALUE(col, '$.p' RETURNING type)` when supported (**[M]** 8.0.21+, **[MDB]** always), else `JSON_UNQUOTE(JSON_EXTRACT(...))` | |
| Primitive collections (`int[]`/`List<T>` columns or params with `Contains/Any/Count/…`) | queryable | Collection to table | `JSON_TABLE(json, '$[*]' COLUMNS (value T PATH '$'))` | Opt-in only. **[M]** 8.0.4+. **[MDB]** 10.6+ gives wrong results with outer refs (no LATERAL). Parameter-as-source is inlined to avoid a MySQL crash |
| Inline collections (`new[]{a,b}.Contains(x)`) | queryable | VALUES table | `VALUES ROW(...)` (**[M]** 8.0.19+) / `VALUES (...)` (**[MDB]** 10.3.3+), else `IN (...)` | |
| `Skip` without `Take` | queryable | Paging | `LIMIT 18446744073709551610 OFFSET n` | MySQL needs LIMIT for OFFSET |
| CROSS/OUTER APPLY (SelectMany with correlated subquery) | queryable | Lateral join | `JOIN LATERAL` / `LEFT JOIN LATERAL` | **[M]** 8.0.14+. **[MDB]** unsupported (translation fails) |
| `ExecuteUpdate/ExecuteDelete` | queryable | Bulk DML | MySQL multi-table `UPDATE t JOIN … SET` / `DELETE t FROM t JOIN …` | MySQL forbids referencing the target table in a subquery, so some shapes are rejected |
| Except/Intersect | queryable | Set ops | `EXCEPT` / `INTERSECT` | **[M]** 8.0.31+, **[MDB]** 10.3+ |
| Window functions / CTEs | (internal) | | | **[M]** 8.0+, **[MDB]** 10.2+ |
| Integer `/` and `%` | binary | Integer division | `DIV` for integral types | |
| Spatial (NTS) `Geometry.*` members/methods | `Area, AsBinary/ToBinary, AsText/ToText, Boundary, Buffer, Centroid, Contains, ConvexHull, Crosses, Difference, Dimension, Disjoint, Distance, Envelope, EqualsTopologically, GeometryType, OgcGeometryType, GetGeometryN, NumGeometries, InteriorPoint, PointOnSurface, Intersection, Intersects, IsEmpty, IsSimple, IsValid, IsWithinDistance, Length, NumPoints, Overlaps, Relate, SRID, SymmetricDifference, Touches, Union, Within`; `Point.X/Y`; `LineString.Count/EndPoint/StartPoint/GetPointN/IsClosed/IsRing`; `MultiLineString.IsClosed`; `Polygon.ExteriorRing/GetInteriorRingN/NumInteriorRings`; `GeometryCollection.Count` | Spatial predicates/measures | `ST_*` equivalents (`ST_Area`, `ST_AsBinary`, `ST_Buffer`, `ST_Contains`, `ST_Distance`, `ST_Equals`, `ST_GeometryType`, `ST_X/ST_Y`, `ST_SRID`, …) | Several are only partly supported on **[MDB]** (`SpatialSupportFunctionAdditions`, `SpatialSetSridFunction` are false) |

## 6. Store types / type mappings

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `bool` | | Boolean | `tinyint(1)` (default) or `bit(1)` (`MySqlBooleanType.Bit1`) | No native boolean type |
| `sbyte/byte/short/ushort/int/uint/long/ulong` | | Signed and unsigned integers | `tinyint`, `tinyint unsigned`, `smallint[ unsigned]`, `int[ unsigned]`, `bigint[ unsigned]` | Native unsigned types. `mediumint` is read-only. `bit(n)` maps to ulong |
| `decimal/float/double` | | Numeric | `decimal(65,30)` default, `float`, `double` | `DecimalTypeDefaultWarning` |
| `string` | | Text | `longtext` (unbounded), `varchar(n)`, `char(n)` (fixed). Key/index columns are capped (see `LimitKeyedOrIndexedStringColumnLength`). Lengths over 65535/bytesPerChar become text. Also `tinytext/text/mediumtext`, `nchar/nvarchar`, `enum(...)` | Charset/collation are per column |
| `byte[]` | | Binary | `longblob`, `varbinary(n)`, `binary(n)`; keys get `varbinary(MaxKeyLength)` | |
| `byte[]` row version | | Concurrency token | `timestamp(6)` + converter | |
| `Guid` | | UUID | Set by connection-string `GuidFormat`: `Char36` (default) `char(36)`, `Char32` `char(32)`, `Binary16`/`TimeSwapBinary16`/`LittleEndianBinary16` (`OldGuids=true`) `binary(16)` | Char Guids get `ascii_general_ci` collation. No native uuid type |
| `DateTime` | | Timestamp w/o TZ | `datetime(6)` default (on 5.6.4+/MDB 10.1.2+), alternatives `datetime`, `timestamp[(6)]` | |
| `DateTimeOffset` | | Timestamp with offset | `datetime(6)`/`timestamp(6)`, **offset not stored** (normalised to UTC) | |
| `DateOnly` / `TimeOnly` / `TimeSpan` | | Date / time | `date` / `time(n)` / `time(6)` | `year` maps to short |
| `MySqlJsonString`, `string` with `HasColumnType("json")`, `JsonDocument`, `JsonElement`, `JToken`, POCOs (Json pkgs) | | JSON | `json` | **[MDB]** `json` is an alias of `longtext` + `CHECK (JSON_VALID(...))`, so the provider uses `JsonDataTypeEmulation` / identifies JSON columns by check constraints |
| NTS `Geometry`, `Point`, `LineString`, `Polygon`, `Multi*`, `GeometryCollection` | | Spatial | `geometry`, `point`, `linestring`, `polygon`, `multipoint`, `multilinestring`, `multipolygon`, `geometrycollection` | SRID column attribute **[M]** 8.0.3+ |
| `enum` / `set` store types | | Enumerated string | `enum(...)` parsed as string mapping (scaffolding). No `set` mapping | |
| `CHARACTER SET x` in `HasColumnType` | | Legacy charset spec | Parsed out of the store type into the charset annotation | |

## 7. Migrations

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `MySqlCreateDatabaseOperation` / `MySqlDropDatabaseOperation` | operation | Create/drop database | `CREATE DATABASE … [CHARACTER SET][COLLATE]`, `DROP DATABASE` | `EnsureSchema`/`DropSchema` are no-ops or map through SchemaBehavior |
| `AlterDatabaseOperation` (+ charset/collation annotations) | | DB defaults | `ALTER DATABASE CHARACTER SET x` / `ALTER DATABASE COLLATE y` | |
| Create/AlterTable with charset, collation, `StoreOptions` | | Table options | `CREATE TABLE … CHARACTER SET=x COLLATE=y ENGINE=…`. Charset changes use a dynamic `@__pomelo_TableCharset` script | |
| Column charset/collation/SRID | | Column options | `CHARACTER SET x COLLATE y`, `/*!80003 SRID n */` | |
| AUTO_INCREMENT columns | | Identity | `… AUTO_INCREMENT` (no DEFAULT allowed). Adds a UNIQUE index if the column isn't first in a composite PK | |
| Timestamp columns | | Store-generated time | `DEFAULT CURRENT_TIMESTAMP(n) [ON UPDATE CURRENT_TIMESTAMP(n)]` | Needs `DateTimeCurrentTimestamp` (**[M]** 5.6.5+) |
| `CreateIndexOperation` + FullText/Spatial/PrefixLength/Parser | | Index variants | `CREATE [UNIQUE/FULLTEXT/SPATIAL] INDEX ix ON t (c(10) [DESC]) [WITH PARSER p]` | Descending indexes **[M]** 8.0.1+ |
| `RenameIndex` / `RenameColumn` | | Rename | `ALTER TABLE … RENAME INDEX a TO b` (5.7+/MDB 10.5.2+) else drop+create. `RENAME COLUMN` (8.0+/MDB 10.5.2+) else `CHANGE old new <full def>` | |
| `AlterColumn` | | Change column | `ALTER TABLE … MODIFY/CHANGE …` (full redefinition) | |
| `DropPrimaryKey(name, table, schema, bool recreateForeignKeys)` | `MigrationBuilder` ext | Drop PK safely | Temporary `POMELO_BEFORE_DROP_PRIMARY_KEY` proc removes AUTO_INCREMENT first. Optionally drops and recreates dependent FKs | |
| `DropUniqueConstraint(name, table, schema, bool recreateForeignKeys)` | `MigrationBuilder` ext | Drop AK safely | Recreates dependent FKs | |
| `AddPrimaryKey` | | Add PK | `POMELO_AFTER_ADD_PRIMARY_KEY` proc restores AUTO_INCREMENT | |
| `MigrationBuilder.IsMySql()` | `bool` | Provider check | | |
| Sequences ops | | | `CREATE/ALTER/DROP/RENAME SEQUENCE` | **[MDB]** only |
| History repository | | Migration history + lock | `__EFMigrationsHistory` with `CREATE TABLE IF NOT EXISTS`. Lock is `SELECT GET_LOCK('<db>…', 259200)` / `RELEASE_LOCK`. Idempotent scripts use temp stored procedures (`DROP PROCEDURE IF EXISTS …; CREATE PROCEDURE…`) | MySQL DDL is non-transactional (implicit commit) |
| `DefaultValueNotSupportedWarning` | | | Emitted when a default expression is unsupported (expression defaults need **[M]** 8.0.13+, or **[MDB]** 10.2.7+ in alternative form) | |

## 8. Database-level

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `ServerVersion.AutoDetect` / `AutoDetectAsync` | `(string connStr)`, `(MySqlConnection)`, `(MySqlDataSource)` | Server dialect/version detection | Opens a connection and parses `SELECT VERSION()` (detects "MariaDB") | |
| `ServerVersion.Parse/TryParse/Create` | `(string[, ServerType?])`, `(Version, ServerType)`, `(int,int,int,ServerType)` | Static dialect selection | | `ServerType {MySql, MariaDb}` |
| `MySqlServerVersion` / `MariaDbServerVersion` | ctor `(Version)` / `(string)`, `.LatestSupportedServerVersion` | Dialect objects | | |
| `ServerVersion.Supports.*` | ~55 bool flags | Feature matrix | See `ServerVersionSupport.cs` (Json, JsonTable, Returning, Sequences, WindowFunctions, CrossApply, Values, DescendingIndexes, …) | Public, so it can be read for capability checks |
| `ServerVersion.MaxKeyLength` | `int` | Index key byte limit | 3072 or 767 | |
| `MySqlDatabaseCreator` | | Create/exists/has-tables | `CREATE DATABASE`, `DROP DATABASE`, `information_schema.tables` count | MySQL returns Int64, MariaDB Int32 |
| `MySqlRetryingExecutionStrategy` | `(ctx[, maxRetry[, maxDelay, errorNumbersToAdd]])` | Retry | Default max retry count and delay. `ShouldRetryOn` checks `MySqlException.IsTransient` | |
| `DatabaseFacade.IsMySql()` | `bool` | Provider check | | |
| `DatabaseFacade.SetDbDataSource(DbDataSource)` | | Swap data source at runtime | | |
| Schema behaviour | see `SchemaBehavior` | Schemas are databases | Throw / Ignore / Translate | |
| `INSERT` key/computed read-back | | | `LAST_INSERT_ID()` + `ROW_COUNT()` select. **[MDB]** 10.5+: `RETURNING` | |
| Session `sql_mode` management | `SetSqlModeOnOpen` | | `SET SESSION sql_mode` | |

## 9. Diagnostics / event IDs

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `MySqlEventId.DecimalTypeDefaultWarning` | validation | Decimal with no precision | Warns that `decimal(65,30)` is used | |
| `MySqlEventId.ByteIdentityColumnWarning` | validation | byte identity | Small identity range | |
| `MySqlEventId.DefaultValueNotSupportedWarning` | migrations | Unsupported default expression | Default dropped | |
| `MySqlEventId.*Found/*Warning` (ColumnFound, TableFound, IndexFound, ForeignKeyFound, MissingTableWarning, ReflexiveConstraintIgnored, …) | scaffolding | Reverse-engineering logs | | ~30 IDs |
| Errors: `IdentityBadType`, `ComputedBadType`, schema-not-supported, "Primitive collections support has not been enabled", StringComparison-needs-option | exceptions | Model/query validation | | |
