# Microsoft.EntityFrameworkCore.SqlServer 10.0.11 — provider-specific surface

Source: `ef10011/src/EFCore.SqlServer` (read, not executed). Only things plain relational EF Core does **not** already provide are listed.
Conventions in this file:

- "+ generic" means a generic twin exists (`EntityTypeBuilder<TEntity>`, `PropertyBuilder<TProperty>`, `IndexBuilder<TEntity>`, `OwnedNavigation…<TOwner,TDependent>` …) with the same parameters, returning the generic builder.
- "+ convention" means matching `IConvention…Builder` methods exist: `X(…, bool fromDataAnnotation = false)` returning `IConvention…Builder?` plus `CanSetX(…)` returning `bool`. These are the plug-in/convention API, listed once per concept.
- Metadata read/write companions (`GetX/SetX/GetXConfigurationSource` on `IReadOnlyProperty`, `IMutableEntityType`, `IReadOnlyIndex`, `IReadOnlyKey`, `IReadOnlyModel`, `ITable`/`IColumn`…) exist for every annotation; they are summarised in one row at the end of section 2.
- No member in this provider carries `[Experimental]`. `[Obsolete]` members are flagged inline.
- Compatibility-level gating: `SqlServerCompatibilityLevel` default **150** (SQL Server 2019), `AzureSqlCompatibilityLevel` default **170**, Synapse default **30**. Translations below note when they depend on it.

---

## 1. Options builder

Entry points (on `DbContextOptionsBuilder` and `DbContextOptionsBuilder<TContext>`), each with 4 overloads: `()`, `(string? connectionString)`, `(DbConnection connection)`, `(DbConnection connection, bool contextOwnsConnection)`, all with an optional trailing `Action<TBuilder>?`.

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseSqlServer` | `DbContextOptionsBuilder.UseSqlServer([string? cs \| DbConnection conn[, bool contextOwnsConnection]], Action<SqlServerDbContextOptionsBuilder>?)` | select provider + engine flavour = on-prem SQL Server | Sets `EngineType = SqlServer`; registers SqlServer services; forces `ConflictingValueGenerationStrategiesWarning` → Throw | Throws if another engine type was already configured on the same options |
| `UseAzureSql` | same 4 overloads, `Action<AzureSqlDbContextOptionsBuilder>?` | engine flavour = Azure SQL DB | `EngineType = AzureSql`; **retrying execution strategy on by default**; compat default 170 (enables `json` type) | New in EF 9 |
| `UseAzureSynapse` | same 4 overloads, `Action<AzureSynapseDbContextOptionsBuilder>?` | engine flavour = Synapse (dedicated pool) | `EngineType = AzureSynapse`; retry on by default; compat default 30 | |
| `ConfigureSqlEngine` | `DbContextOptionsBuilder.ConfigureSqlEngine(Action<SqlEngineDbContextOptionsBuilder>?)` | engine-agnostic configuration (no engine chosen) | Adds the options extension without setting engine type; lets a library configure SQL family options and let the app pick the engine later | |
| `AddSqlServer<TContext>` / `AddAzureSql<TContext>` / `AddAzureSynapse<TContext>` | `IServiceCollection.AddSqlServer<TContext>(string? cs, Action<SqlServerDbContextOptionsBuilder>? = null, Action<DbContextOptionsBuilder>? = null)` | DI registration shortcut | `AddDbContext` + `UseX` | |
| `AddEntityFrameworkSqlServer` / `…AzureSql` / `…AzureSynapse` | `IServiceCollection.AddEntityFrameworkSqlServer()` | internal-service-provider registration | Registers provider services only | Advanced |
| `EnableRetryOnFailure` | `()`, `(int maxRetryCount)`, `(ICollection<int> errorNumbersToAdd)`, `(int maxRetryCount, TimeSpan maxRetryDelay, IEnumerable<int>? errorNumbersToAdd)` | transient-fault retry policy | Installs `SqlServerRetryingExecutionStrategy` (default 6 retries, 30 s max delay, +5 s min delay on throttling errors) | On all four builders |
| `EnableRetryOnFailureByDefault` | `SqlEngineDbContextOptionsBuilder.EnableRetryOnFailureByDefault()` | retry-by-default flag | Retry strategy applied if no explicit strategy is set | Only on `SqlEngineDbContextOptionsBuilder` |
| `UseCompatibilityLevel` | `(int compatibilityLevel)` | target server version / feature level | Gates translations: OPENJSON primitive collections (≥130), CONCAT_WS & non-constant JSON paths (≥140), GREATEST/LEAST, LTRIM/RTRIM(chars) (≥160), `json` type & `.modify()` (≥170) | **Upstream bug:** on `AzureSynapseDbContextOptionsBuilder` the value is ignored — `SqlServerOptionsExtension.WithAzureSynapseCompatibilityLevel` assigns the existing field (`clone._azureSynapseCompatibilityLevel = _azureSynapseCompatibilityLevel;`), not the parameter |
| `UseAzureSqlDefaults` | `SqlServerDbContextOptionsBuilder.UseAzureSqlDefaults(bool enable = true)` | legacy Azure flag | Sets legacy Azure mode | **[Obsolete]** "Use UseAzureSql instead of UseSqlServer with UseAzureSqlDefaults." |
| inherited from `RelationalDbContextOptionsBuilder` | `MaxBatchSize`, `MinBatchSize`, `CommandTimeout`, `MigrationsAssembly`, `MigrationsHistoryTable`, `UseQuerySplittingBehavior`, `UseRelationalNulls`, `ExecutionStrategy`, `TranslateParameterizedCollectionsTo…` | relational (not provider-specific) | SQL Server specifics: default MaxBatchSize **42**, hard max **1000** | listed for completeness only |

## 2. Model building API

### 2a. Property / column (`PropertyBuilder`, `ComplexTypePropertyBuilder`, `PrimitiveCollectionBuilder`, `ComplexTypePrimitiveCollectionBuilder`, `ColumnBuilder`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseIdentityColumn` | `PropertyBuilder.UseIdentityColumn(long seed = 1, int increment = 1)`; `(int seed, int increment = 1)` + generic | store-generated auto-increment key | `IDENTITY(seed, increment)` | Also on `ComplexTypePropertyBuilder` (+ generic) |
| `UseIdentityColumn` (table-specific) | `ColumnBuilder.UseIdentityColumn(long seed = 1, int increment = 1)` + generic | per-table identity override (TPC/table splitting) | `IDENTITY` on that table's column only | Used inside `ToTable(t => t.Property(...))` |
| `UseHiLo` | `PropertyBuilder.UseHiLo(string? name = null, string? schema = null)` + generic | sequence-backed client-side HiLo key generation | Creates `CREATE SEQUENCE … INCREMENT BY 10`; client reserves blocks via `SELECT NEXT VALUE FOR` | Default sequence `EntityFrameworkHiLoSequence`; also on `ComplexTypePropertyBuilder` |
| `UseSequence` | `PropertyBuilder.UseSequence(string? name = null, string? schema = null)` + generic | sequence-backed server default key | Column default `NEXT VALUE FOR [schema].[seq]` | Default name `{Table}Sequence`; also on `ComplexTypePropertyBuilder` |
| `HasHiLoSequence` / `CanSetHiLoSequence` | convention `(string? name, string? schema)` → `IConventionSequenceBuilder?` | (convention API for HiLo) | | |
| `HasSequence` / `CanSetSequence` | convention `(string? name, string? schema)` → `IConventionSequenceBuilder?` | (convention API for sequence default) | | |
| `HasIdentityColumnSeed` / `CanSetIdentityColumnSeed` | convention `(long? seed[, in StoreObjectIdentifier])` | identity seed | | per-table overload |
| `HasIdentityColumnIncrement` / `CanSetIdentityColumnIncrement` | convention `(int? increment[, in StoreObjectIdentifier])` | identity increment | | per-table overload |
| `HasValueGenerationStrategy` / `CanSetValueGenerationStrategy` | convention `(SqlServerValueGenerationStrategy? strategy[, in StoreObjectIdentifier])` | choose key-generation strategy | | |
| `IsSparse` | `PropertyBuilder.IsSparse(bool sparse = true)` + generic + convention | sparse (null-optimised) column storage | `… SPARSE` in column definition | Also on `PrimitiveCollectionBuilder`, `ComplexTypePropertyBuilder`, `ComplexTypePrimitiveCollectionBuilder` |
| `HasDefaultValue` (named) | `PropertyBuilder.HasDefaultValue(object? value, string defaultConstraintName)` + generic + convention | named default constraint (literal) | `CONSTRAINT [name] DEFAULT (value)` | Also on primitive-collection / complex builders. Pairs with relational `RelationalAnnotationNames.DefaultConstraintName` |
| `HasDefaultValueSql` (named) | `PropertyBuilder.HasDefaultValueSql(string? sql, string defaultConstraintName)` + generic + convention | named default constraint (SQL expr) | `CONSTRAINT [name] DEFAULT (sql)` | same builders as above |

### 2b. Entity type / table (`EntityTypeBuilder`, `TableBuilder`, `OwnedNavigationTableBuilder`, `SplitTableBuilder`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `IsTemporal` | `TableBuilder.IsTemporal(bool temporal = true)` → `TemporalTableBuilder`; `TableBuilder.IsTemporal(Action<TemporalTableBuilder>)` → `TableBuilder`; + generic; + `OwnedNavigationTableBuilder` (+ generic) variants | system-versioned (temporal / history) table | `PERIOD FOR SYSTEM_TIME (PeriodStart, PeriodEnd)`, `WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = …))`; period columns `datetime2 GENERATED ALWAYS AS ROW START/END HIDDEN` | Period columns default to shadow properties `PeriodStart`/`PeriodEnd` |
| `TemporalTableBuilder.UseHistoryTable` | `(string name)`, `(string name, string? schema)` (+ generic, + owned) | history table name | `HISTORY_TABLE = [schema].[name]` | Default `{Table}History` |
| `TemporalTableBuilder.HasPeriodStart` / `HasPeriodEnd` | `(string propertyName)` → `TemporalPeriodPropertyBuilder` (+ owned) | map period columns to properties | | |
| `TemporalPeriodPropertyBuilder.HasColumnName` | `(string name)` (+ owned variant) | period column name | | |
| `TemporalPeriodPropertyBuilder.HasPrecision` | `(int precision)` (+ owned variant) | period column precision | `datetime2(p)` | |
| `IsTemporal` / `UseHistoryTableName` / `UseHistoryTableSchema` / `HasPeriodStart` / `HasPeriodEnd` (+ `CanSet…`) | convention, on `IConventionEntityTypeBuilder` | (convention API for temporal) | | |
| `IsMemoryOptimized` | `TableBuilder.IsMemoryOptimized(bool memoryOptimized = true)` + generic + `OwnedNavigationTableBuilder` (+ generic) | in-memory (Hekaton) table | `WITH (MEMORY_OPTIMIZED = ON)`; migrations add MEMORY_OPTIMIZED_DATA filegroup; PKs/indexes become NONCLUSTERED | |
| `IsMemoryOptimized` (entity level) | `EntityTypeBuilder.IsMemoryOptimized(bool = true)`, `OwnedNavigationBuilder.IsMemoryOptimized(bool = true)` + generics | same | | **[Obsolete]** "Configure this using ToTable(t => t.IsMemoryOptimized()) instead." Convention form `IsMemoryOptimized/CanSetIsMemoryOptimized` not obsolete |
| `UseSqlOutputClause` | `TableBuilder.UseSqlOutputClause(bool useSqlOutputClause = true)` + generic; `SplitTableBuilder` (+ generic); `OwnedNavigationTableBuilder` (+ generic) | how generated values are read back after DML (RETURNING vs fallback) | `true` (default): `OUTPUT INSERTED.*`; `false`: `OUTPUT INTO @temp` / `SELECT … WHERE @@ROWCOUNT` — needed for tables with **triggers** | Convention: `UseSqlOutputClause/CanUseSqlOutputClause` (+ `StoreObjectIdentifier` overload); mapping-fragment ext `IsSqlOutputClauseUsed`, `UseSqlOutputClause`, `GetUseSqlOutputClauseConfigurationSource` |

### 2c. Index (`IndexBuilder`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `IsClustered` | `IndexBuilder.IsClustered(bool clustered = true)` + generic + convention | clustered vs non-clustered index | `CREATE [UNIQUE] CLUSTERED|NONCLUSTERED INDEX` | |
| `IncludeProperties` | `IndexBuilder.IncludeProperties(params string[] propertyNames)` + generic; `IndexBuilder<TEntity>.IncludeProperties(Expression<Func<TEntity, object?>>)`; + convention `(IReadOnlyList<string>?)` | covering index (non-key included columns) | `… INCLUDE ([c1], [c2])` | PostgreSQL has the same concept |
| `IsCreatedOnline` | `IndexBuilder.IsCreatedOnline(bool createdOnline = true)` + generic + convention | online (non-blocking) index build | `WITH (ONLINE = ON)` | Enterprise/Azure only |
| `HasFillFactor` | `IndexBuilder.HasFillFactor(int fillFactor)` + generic + convention | page fill factor | `WITH (FILLFACTOR = n)` | 1..100 |
| `SortInTempDb` | `IndexBuilder.SortInTempDb(bool sortInTempDb = true)` + generic + convention | build-time sort location | `WITH (SORT_IN_TEMPDB = ON)` | |
| `UseDataCompression` | `IndexBuilder.UseDataCompression(DataCompressionType dataCompressionType)` + generic + convention | index compression | `WITH (DATA_COMPRESSION = NONE|ROW|PAGE)` | enum `DataCompressionType { None, Row, Page }` |

### 2d. Key (`KeyBuilder`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `IsClustered` | `KeyBuilder.IsClustered(bool clustered = true)` + generic + convention | clustered vs non-clustered PK/AK | `PRIMARY KEY CLUSTERED|NONCLUSTERED` | Default PK is clustered (except memory-optimized) |
| `HasFillFactor` | `KeyBuilder.HasFillFactor(int fillFactor)` + generic + convention | PK/AK fill factor | `WITH (FILLFACTOR = n)` | |

### 2e. Model (`ModelBuilder`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseIdentityColumns` | `ModelBuilder.UseIdentityColumns(long seed = 1, int increment = 1)`; `(int seed, int increment = 1)` | model-wide default: identity | Default strategy for integer keys | This is the default convention |
| `UseHiLo` | `ModelBuilder.UseHiLo(string? name = null, string? schema = null)` | model-wide default: HiLo | One shared sequence | |
| `UseKeySequences` | `ModelBuilder.UseKeySequences(string? nameSuffix = null, string? schema = null)` | model-wide default: one sequence per hierarchy as column default | `{Table}{nameSuffix}` sequences; `DEFAULT (NEXT VALUE FOR …)` | Suits TPC |
| `HasHiLoSequence` / `HasIdentityColumnSeed` / `HasIdentityColumnIncrement` / `HasValueGenerationStrategy` (+ `CanSet…`) | convention, `IConventionModelBuilder` | (convention API for model-wide key generation) | | |
| `HasDatabaseMaxSize` | `ModelBuilder.HasDatabaseMaxSize(string maxSize)` + convention | Azure SQL DB size | `CREATE DATABASE … (MAXSIZE = …)` / `ALTER DATABASE … MODIFY` | Azure only |
| `HasServiceTier` | `ModelBuilder.HasServiceTier(string serviceTier)` | Azure edition (literal) | `EDITION = 'x'` | Quotes and escapes the value |
| `HasServiceTierSql` | `ModelBuilder.HasServiceTierSql(string serviceTier)` + convention | Azure edition (raw SQL) | `EDITION = <sql>` | |
| `HasPerformanceLevel` | `ModelBuilder.HasPerformanceLevel(string performanceLevel)` | Azure service objective (literal) | `SERVICE_OBJECTIVE = 'x'` | |
| `HasPerformanceLevelSql` | `ModelBuilder.HasPerformanceLevelSql(string performanceLevel)` + convention | Azure service objective (raw SQL) | `SERVICE_OBJECTIVE = <sql>` | e.g. `ELASTIC_POOL(name = p)` |
| `UseNamedDefaultConstraints` | `ModelBuilder.UseNamedDefaultConstraints(bool value = true)` + convention | auto-name all default constraints | Generates a table+column-based constraint name (`GetDefaultDefaultConstraintName`) so constraints can be dropped deterministically | New in EF 10 |

### 2f. Metadata accessors (read side)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| Get/Set/…ConfigurationSource family | e.g. `IReadOnlyProperty.GetValueGenerationStrategy([in StoreObjectIdentifier])`, `GetIdentitySeed/Increment`, `GetHiLoSequenceName/Schema`, `FindHiLoSequence`, `GetSequenceName/Schema/NameSuffix`, `FindSequence`, `IsSparse`, `IsCompatibleWithValueGeneration`; `IReadOnlyEntityType.IsTemporal/IsMemoryOptimized/GetHistoryTableName/Schema/GetSchemaQualifiedHistoryTableName/GetPeriodStart/EndPropertyName/IsSqlOutputClauseUsed`; `IReadOnlyIndex.IsClustered/GetIncludeProperties/IsCreatedOnline/GetFillFactor/GetSortInTempDb/GetDataCompression`; `IReadOnlyKey.IsClustered/GetFillFactor`; `IReadOnlyModel.GetDatabaseMaxSize/GetServiceTierSql/GetPerformanceLevelSql/GetValueGenerationStrategy` | annotation read/write | Annotations prefixed `SqlServer:` (e.g. `SqlServer:ValueGenerationStrategy`, `SqlServer:Identity` = "seed, increment", `SqlServer:Include`, `SqlServer:IsTemporal`, `SqlServer:MemoryOptimized`…) | Plus `DatabaseFacade.IsSqlServer()` and `MigrationBuilder.IsSqlServer()` (`bool`, provider check) |

## 3. Value generation strategies and key generation

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `SqlServerValueGenerationStrategy.None` | enum | no provider strategy | | |
| `SqlServerValueGenerationStrategy.IdentityColumn` | enum | store auto-increment | `IDENTITY(1,1)`; read back via `OUTPUT INSERTED` | **Default** for `short/int/long` (and decimal(p,0) keys, with warning); `byte` identity warns |
| `SqlServerValueGenerationStrategy.SequenceHiLo` | enum | client-side HiLo from a sequence | `NEXT VALUE FOR` per block | `SqlServerSequenceHiLoValueGenerator` |
| `SqlServerValueGenerationStrategy.Sequence` | enum | sequence as column default | `DEFAULT (NEXT VALUE FOR [seq])` | |
| GUID keys | (no enum) | client-generated sequential GUID | `SequentialGuidValueGenerator` (SQL-Server-ordered byte layout) client side; not `NEWSEQUENTIALID()` | Temporary GUID when value is store-generated |
| `rowversion` | `.IsRowVersion()` on `byte[]`, `long`, `ulong` | optimistic-concurrency token auto-updated by store | `rowversion` column; `ulong`/`long` mapped with converter | |
| Conflict detection | `ConflictingValueGenerationStrategiesWarning` (throws by default) | | Strategy + default value / computed on same column | |

## 4. EF.Functions extensions (`SqlServerDbFunctionsExtensions`, `this DbFunctions _` first param)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `FreeText` | `bool FreeText(object propertyReference, string freeText)` | full-text natural-language match | `FREETEXT([col], @text)` | arg must be a column; needs full-text index |
| `FreeText` | `bool FreeText(object propertyReference, string freeText, [NotParameterized] int languageTerm)` | same with language | `FREETEXT([col], @text, LANGUAGE 1033)` | |
| `Contains` | `bool Contains(object propertyReference, string searchCondition)` | full-text boolean/proximity search | `CONTAINS([col], @cond)` | |
| `Contains` | `bool Contains(object propertyReference, string searchCondition, [NotParameterized] int languageTerm)` | same with language | `CONTAINS([col], @cond, LANGUAGE n)` | |
| `DateDiffYear` | `int DateDiffYear(T start, T end)`, `int?` for `T?`; `T ∈ {DateTime, DateTimeOffset, DateOnly}` | calendar-boundary difference | `DATEDIFF(year, s, e)` | counts boundaries crossed |
| `DateDiffMonth` | same overloads as Year | | `DATEDIFF(month, s, e)` | |
| `DateDiffWeek` | same overloads as Year | | `DATEDIFF(week, s, e)` | |
| `DateDiffDay` | same overloads as Year | | `DATEDIFF(day, s, e)` | |
| `DateDiffHour` | `T ∈ {DateTime, DateTimeOffset, DateOnly, TimeSpan, TimeOnly}` (+ nullable) | | `DATEDIFF(hour, s, e)` | |
| `DateDiffMinute` | same 5 types (+ nullable) | | `DATEDIFF(minute, s, e)` | |
| `DateDiffSecond` | same 5 types (+ nullable) | | `DATEDIFF(second, s, e)` | |
| `DateDiffMillisecond` | same 5 types (+ nullable) | | `DATEDIFF(millisecond, s, e)` | |
| `DateDiffMicrosecond` | same 5 types (+ nullable) | | `DATEDIFF(microsecond, s, e)` | int overflow risk |
| `DateDiffNanosecond` | same 5 types (+ nullable) | | `DATEDIFF(nanosecond, s, e)` | int overflow risk |
| `IsDate` | `bool IsDate(string expression)` | "is parseable as date" | `CAST(ISDATE(@e) AS bit)` | |
| `IsNumeric` | `bool IsNumeric(string expression)` | "is parseable as number" | `ISNUMERIC(@e) = 1` | |
| `DateFromParts` | `DateTime DateFromParts(int year, int month, int day)` | construct date | `DATEFROMPARTS(y, m, d)` → `date` | |
| `DateTimeFromParts` | `DateTime DateTimeFromParts(int year, int month, int day, int hour, int minute, int second, int millisecond)` | construct legacy datetime | `DATETIMEFROMPARTS(...)` → `datetime` | |
| `DateTime2FromParts` | `DateTime DateTime2FromParts(int year, int month, int day, int hour, int minute, int second, int fractions, int precision)` | construct timestamp | `DATETIME2FROMPARTS(...)` → `datetime2` | |
| `DateTimeOffsetFromParts` | `DateTimeOffset DateTimeOffsetFromParts(int year, int month, int day, int hour, int minute, int second, int fractions, int hourOffset, int minuteOffset, int precision)` | construct timestamptz | `DATETIMEOFFSETFROMPARTS(...)` | |
| `SmallDateTimeFromParts` | `DateTime SmallDateTimeFromParts(int year, int month, int day, int hour, int minute)` | construct smalldatetime | `SMALLDATETIMEFROMPARTS(...)` | |
| `TimeFromParts` | `TimeSpan TimeFromParts(int hour, int minute, int second, int fractions, int precision)` | construct time | `TIMEFROMPARTS(...)` → `time` | |
| `DataLength` | `int? DataLength(T? arg)`, `T ∈ {string, bool, double, decimal, DateTime, TimeSpan, DateTimeOffset, byte[], Guid}` | storage byte length | `DATALENGTH(x)` (cast from bigint for `(max)` types) | |
| `AtTimeZone` | `DateTimeOffset AtTimeZone(DateTime dateTime, string timeZone)` | convert to time zone | `x AT TIME ZONE 'zone'` (Windows zone names) | result precision follows operand |
| `AtTimeZone` | `DateTimeOffset AtTimeZone(DateTimeOffset dateTimeOffset, string timeZone)` | | `x AT TIME ZONE 'zone'` | |
| `PatIndex` | `long PatIndex(string pattern, string expression)` | wildcard-pattern position | `PATINDEX(@pattern, @expr)` | 1-based; LIKE wildcards |
| `StandardDeviationSample` | `double? StandardDeviationSample(IEnumerable<T> values)`, `T ∈ {byte, short, int, long, float, double, decimal}` | sample stddev aggregate | `STDEV(x)` | used inside `GroupBy` projection |
| `StandardDeviationPopulation` | same 7 overloads | population stddev | `STDEVP(x)` | |
| `VarianceSample` | same 7 overloads | sample variance | `VAR(x)` | |
| `VariancePopulation` | same 7 overloads | population variance | `VARP(x)` | |
| `VectorDistance<T>` | `double VectorDistance<T>([NotParameterized] string distanceMetric, SqlVector<T> vector1, SqlVector<T> vector2)` | vector similarity distance | `VECTOR_DISTANCE('cosine'|'euclidean'|'dot', v1, v2)` | at least one arg must be a vector column; SQL Server 2025 / Azure SQL |

## 5. LINQ translations beyond relational core

### 5a. string

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `string.Length` | member | char length | `CAST(LEN(x) AS int)` | **LEN ignores trailing spaces** |
| `string.IndexOf` | `(string)`, `(char)`, `(string, int)`, `(char, int)` | 0-based position | `CHARINDEX(p, x[, start+1]) - 1` (CASE for empty pattern) | cast from bigint for `(max)` |
| `string.Replace` | `(string, string)`, `(char, char)` | | `REPLACE(x, a, b)` | |
| `string.ToLower` / `ToUpper` | `()` | | `LOWER(x)` / `UPPER(x)` | |
| `string.Substring` | `(int)`, `(int, int)` | | `SUBSTRING(x, i+1, LEN(x))` / `SUBSTRING(x, i+1, n)` | |
| `string.IsNullOrEmpty` | static | | `x IS NULL OR x LIKE N''` | |
| `string.IsNullOrWhiteSpace` | static | | `x IS NULL OR x = N''` | relies on SQL Server trailing-space-insensitive `=` |
| `string.Trim` | `()`, `(char[] empty)` | | `LTRIM(RTRIM(x))` | Trim(chars) not translated |
| `string.TrimStart` / `TrimEnd` | `()`; `(char)` / `(char[])` when compat ≥160 or Synapse | | `LTRIM(x[, chars])` / `RTRIM(x[, chars])` | |
| `string.StartsWith` / `EndsWith` / `Contains` | `(string)`, `(char)` | prefix/suffix/substring | constant: `LIKE N'abc%'` (escaped, `ESCAPE N'\'`); parameter: `LIKE @p` with pattern built client-side; column: `LEFT(x, LEN(p)) = p` / `RIGHT(...)` / `CHARINDEX(p, x) > 0` | |
| `string.FirstOrDefault()` / `LastOrDefault()` | `Enumerable` over `string` | first/last char | `SUBSTRING(x, 1, 1)` / `SUBSTRING(x, LEN(x), 1)` | |
| `string.Join(sep, new[]{…})` (non-aggregate) | static | concat with separator | `CONCAT_WS(sep, COALESCE(a,''), …)` | compat ≥140 |
| `string.Join(sep, group.Select(…))` / `string.Concat(group.Select(…))` | aggregate | string aggregation | `COALESCE(STRING_AGG(COALESCE(x, N''), sep) [WITHIN GROUP (ORDER BY …)], N'')` | ordering honoured via WITHIN GROUP |
| `object.ToString()` | instance | to text | `COALESCE(CONVERT(varchar(n), x), '')`; bool → `CASE … 'True'/'False'` | per-type varchar sizes (int 11, Guid 36, …) |

### 5b. Date / time

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DateTime/DateTimeOffset.Year/Month/DayOfYear/Day/Hour/Minute/Second/Millisecond` | member | component extract | `DATEPART(part, x)` | DayOfWeek **not** translated |
| `…Microsecond` / `…Nanosecond` | member | | `DATEPART(microsecond, x) % 1000` / `DATEPART(nanosecond, x) % 1000` | |
| `DateTime.Date` / `DateTimeOffset.Date` | member | truncate to date | `CONVERT(date, x)` | |
| `.TimeOfDay` | member | | `CONVERT(time, x)` | |
| `DateTime.Now` / `UtcNow` / `Today` | static | current time | `GETDATE()` / `GETUTCDATE()` / `CONVERT(date, GETDATE())` | `datetime` precision |
| `DateTimeOffset.Now` / `UtcNow` | static | | `SYSDATETIMEOFFSET()` / `CAST(SYSUTCDATETIME() AS datetimeoffset)` | |
| `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds/AddMilliseconds` | `DateTime` and `DateTimeOffset` | date arithmetic | `DATEADD(part, CAST(n AS int), x)` | fractional doubles truncated; constants outside int range not translated |
| `DateTimeOffset.ToUnixTimeSeconds` / `ToUnixTimeMilliseconds` | instance | epoch | `DATEDIFF_BIG(second|millisecond, '1970-01-01T00:00:00+00:00', x)` | |
| DateTime `-`/`+` operators | binary | | **not translated** (client-eval error) | |
| `DateOnly.Year/Month/DayOfYear/Day` | member | | `DATEPART(part, x)` | |
| `DateOnly.DayNumber` | member | | `DATEDIFF(day, '0001-01-01', x)` | |
| `DateOnly.AddYears/AddMonths/AddDays` | instance | | `DATEADD(part, n, x)` | |
| `DateOnly.FromDateTime` | static | | `CAST(x AS date)` | |
| `DateOnly.ToDateTime(TimeOnly)` | instance | combine | `DATETIME2FROMPARTS(DATEPART(year,d), …, DATEPART(nanosecond,t)/100, 7)` | only for column/param/constant args |
| `TimeOnly.Hour/Minute/Second/Millisecond/Microsecond/Nanosecond` | member | | `DATEPART(...)` (`% 1000` for µs/ns) | |
| `TimeOnly.AddHours/AddMinutes` | instance | | `DATEADD(hour|minute, n, x)` | |
| `TimeOnly.IsBetween(a, b)` | instance | | `x >= a AND x < b` | simple operands only |
| `TimeOnly.FromDateTime` / `FromTimeSpan` | static | | `CAST(x AS time)` | |
| `TimeSpan.Hours/Minutes/Seconds/Milliseconds/Microseconds/Nanoseconds` | member | | `DATEPART(...)` (`% 1000` for µs/ns) | over `time` |

### 5c. Math / numeric / conversion / misc

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `Math.Abs` | decimal, double, float, int, long, sbyte, short | | `ABS(x)` | |
| `Math.Ceiling` / `Math.Floor` / `MathF.*` | decimal, double / float | | `CEILING(x)` / `FLOOR(x)` | |
| `Math.Pow` / `MathF.Pow` | | | `POWER(x, y)` | |
| `Math.Exp/Log10/Log(x)/Log(x, b)/Sqrt` (+ MathF) | | | `EXP` / `LOG10` / `LOG(x)` / `LOG(x, b)` / `SQRT` | |
| `Math.Acos/Asin/Atan/Atan2/Cos/Sin/Tan` (+ MathF) | | trig | `ACOS/ASIN/ATAN/ATN2/COS/SIN/TAN` | `ATN2` not `ATAN2` |
| `Math.Sign` | decimal, double, float, int, long, sbyte, short | | `SIGN(x)` | |
| `double/float.DegreesToRadians` / `RadiansToDegrees` | static | | `RADIANS(x)` / `DEGREES(x)` | |
| `Math.Truncate` / `MathF.Truncate` | decimal, double / float | | `ROUND(x, 0, 1)` | float result cast back |
| `Math.Round` / `MathF.Round` | `(x)`, `(x, int digits)` | | `ROUND(x, d)` | rounding mode differs from .NET banker's rounding |
| `Math.Max` / `Math.Min` | | | `GREATEST(a, b)` / `LEAST(a, b)` | compat ≥160 only (relational base otherwise) |
| `Convert.ToBoolean/ToByte/ToDecimal/ToDouble/ToInt16/ToInt32/ToInt64/ToString` | `(bool|byte|DateTime|decimal|double|float|int|long|short|string|object)` | type conversion | `CONVERT(bit|tinyint|decimal(18, 2)|float|smallint|int|bigint|nvarchar(max), x)` | |
| `Guid.NewGuid()` | static | random UUID | `NEWID()` | |
| `Queryable.LongCount` | aggregate | 64-bit count | `COUNT_BIG(*)` | |
| `byte[].Length` | member | | `DATALENGTH(x)` (cast for `varbinary(max)`) | |
| `byte[].Contains(byte)` | `Enumerable.Contains` | | `CHARINDEX(0x.., x) > 0` | |
| `byte[].First()` | `Enumerable.First` | | `CAST(SUBSTRING(x, 1, 1) AS tinyint)` | |
| `byte[][i]` / `ElementAt(i)` | index | | `CAST(SUBSTRING(x, i + 1, 1) AS tinyint)` | |
| bool in projection | | search-condition ↔ value | `CASE WHEN … THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END` | T-SQL has no boolean type |
| Aggregate over subquery | | | lifted to `OUTER APPLY` | SQL Server can't nest aggregates over subqueries |
| Paging | `Skip/Take` | | `OFFSET n ROWS FETCH NEXT m ROWS ONLY` (adds `ORDER BY (SELECT 1)`), `TOP(n)` | |
| Large `IN` lists | `Contains` over parameter collection | | Expanded/padded params up to **2098**, otherwise `OPENJSON` | 2100-parameter limit |

### 5d. Primitive collections / JSON

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| Primitive collection query (`list.Contains(x)`, `.Where/.Count/.Any` over `int[]`/`List<T>` column or parameter) | queryable | table-valued JSON array | `OPENJSON(@p) WITH ([value] int '$') AS [p]` or `OPENJSON(col)` with `[key]` for ordering | Requires compat ≥130, else `CompatibilityLevelTooLowForScalarCollections` |
| `ElementAt(i)` / `[i]` on JSON array | queryable | | `JSON_VALUE(col, '$[i]')` (non-constant index: `'$[' + CAST(@i AS nvarchar(max)) + ']'`, compat ≥140) | |
| JSON-mapped owned / complex types (`ToJson()`, `ComplexProperty().ToJson()`) | model | document column | scalar: `JSON_VALUE(col, '$.a.b')` wrapped in `CAST(... AS type)` (no cast for strings); sub-document: `JSON_QUERY(col, '$.a')`; collections: `OPENJSON(col, '$.a') WITH (... AS JSON)` | |
| `json` store type (SQL Server 2025 / Azure SQL) | type | native JSON | `JSON_VALUE(col, '$.a' RETURNING int)`; default mapping for JSON columns/primitive collections when compat ≥170 | `OPENJSON … WITH (x nvarchar(max) AS JSON)` where native json unsupported in WITH |
| `ExecuteUpdate` on JSON property | bulk update | partial JSON update | `json` type: `UPDATE … SET [col].modify('$.path', value)`; nvarchar: `JSON_MODIFY(col, '$.path', JSON_QUERY(value))` | multiple setters on one column fall back to nested `JSON_MODIFY` |
| scalar → JSON serialisation | | | `JSON_VALUE(JSON_OBJECT('v': x), '$.v')` (2022+) ; spatial via `.STAsText()` | `ExecuteUpdateCannotSetJsonPropertyOnOldSqlServer` below 2022 |

### 5e. Temporal query operators (`SqlServerDbSetExtensions`, on `DbSet<TEntity>`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `TemporalAsOf` | `IQueryable<TEntity> TemporalAsOf<TEntity>(this DbSet<TEntity>, DateTime utcPointInTime)` | point-in-time history query | `FROM [T] FOR SYSTEM_TIME AS OF '…'` | applies to navigations included in the query; results untracked |
| `TemporalFromTo` | `(DateTime utcFrom, DateTime utcTo)` | rows active in half-open range | `FOR SYSTEM_TIME FROM a TO b` | |
| `TemporalBetween` | `(DateTime utcFrom, DateTime utcTo)` | rows active in closed-end range | `FOR SYSTEM_TIME BETWEEN a AND b` | |
| `TemporalContainedIn` | `(DateTime utcFrom, DateTime utcTo)` | rows whose lifetime is inside range | `FOR SYSTEM_TIME CONTAINED IN (a, b)` | |
| `TemporalAll` | `()` | full history | `FOR SYSTEM_TIME ALL` | |
| Period columns | `EF.Property<DateTime>(e, "PeriodStart")` | access period | plain column ref | |

### 5f. Full-text, vector, spatial, hierarchyid

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `EF.Functions.FreeText` / `Contains` | see §4 | full-text predicate | `FREETEXT` / `CONTAINS` | no CONTAINSTABLE/FREETEXTTABLE (ranking) translation |
| `EF.Functions.VectorDistance` | see §4 | vector distance | `VECTOR_DISTANCE(metric, a, b)` | |
| `SqlVector<float>.Length` | member | vector dimensions | `VECTORPROPERTY(x, 'Dimensions')` | |
| Spatial (`NetTopologySuite` Geometry members) | — | | — | **Not in this package**: separate `Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite` plugin (`UseNetTopologySuite()`); core only has generic `SqlServerUdtTypeMapping` |
| `HierarchyId` | — | | — | **Not in this package**: separate `Microsoft.EntityFrameworkCore.SqlServer.HierarchyId` plugin (`UseHierarchyId()`) |

## 6. Store types / type mappings

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `int` / `long` / `short` / `byte` / `bool` | CLR default | | `int` / `bigint` / `smallint` / `tinyint` / `bit` | no unsigned types natively (`sbyte/ushort/uint/ulong` via relational converters) |
| `decimal` | CLR default | | `decimal(18,2)` | `DecimalTypeDefaultWarning` if unconfigured; also `money`, `smallmoney`, `numeric`, `dec` |
| `double` / `float` | CLR default | | `float` / `real` | `float(n)` aliases |
| `string` | CLR default | | `nvarchar(max)`; key/index: `nvarchar(450)`; ANSI `varchar(…)` (900 for keys); fixed `nchar/char`; legacy `ntext/text`; `xml` | max sizes 4000 (unicode) / 8000 (ANSI) |
| `byte[]` | CLR default | | `varbinary(max)`; key/index `varbinary(900)`; `binary(n)`; legacy `image` | |
| `Guid` | CLR default | | `uniqueidentifier` | client sequential GUIDs |
| `DateTime` | CLR default | | `datetime2`; also `datetime`, `smalldatetime`, `date` | |
| `DateTimeOffset` | CLR default | | `datetimeoffset` | |
| `DateOnly` / `TimeOnly` / `TimeSpan` | CLR default | | `date` / `time` / `time` | `time` can't exceed 24h |
| rowversion | `byte[]`/`long`/`ulong` + `IsRowVersion()` | auto-updating concurrency token | `rowversion` (alias `timestamp`) | |
| `sql_variant` | `object` + `HasColumnType("sql_variant")` | dynamically typed column | `SqlServerSqlVariantTypeMapping` | |
| `json` | `string` + `HasColumnType("json")`, or default for JSON-mapped types/primitive collections at compat ≥170 (AzureSql default) | native JSON type | `json` | otherwise `nvarchar(max)` |
| `vector` | `SqlVector<float>` (Microsoft.Data.SqlClient) + `HasColumnType("vector(n)")` | embedding vector | `vector(n)` | `SqlServerVectorTypeMapping(size)` |
| UDTs | `SqlServerUdtTypeMapping` | CLR user-defined types | `geometry`, `geography`, `hierarchyid` via plugins | |
| Primitive collections | `List<T>`, `T[]` | array-in-column | `nvarchar(max)` JSON (or `json`) | |
| Identifiers | | | `[name]` quoting; `GO` batch terminator in scripts | |

## 7. Migrations

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `SqlServerCreateDatabaseOperation` | `{ Name, FileName?, Collation? }` | create database | `CREATE DATABASE [n] [ON (NAME=…, FILENAME=…) LOG ON (…)] [COLLATE …]`; `ALTER DATABASE … SET READ_COMMITTED_SNAPSHOT ON` (non-Azure) | Azure: `(EDITION=…, SERVICE_OBJECTIVE=…, MAXSIZE=…)` |
| `SqlServerDropDatabaseOperation` | `{ Name }` | drop database | `ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE` | |
| Temporal table operations | annotations `SqlServer:IsTemporal`, `TemporalHistoryTableName/Schema`, `TemporalPeriodStart/EndColumnName` on Create/Alter/Drop table & column | system versioning | `PERIOD FOR SYSTEM_TIME`, `SET (SYSTEM_VERSIONING = ON/OFF)`, `ADD/DROP PERIOD`, history table maintenance | complex diffing; columns changed on both main and history tables |
| Memory-optimized | `SqlServer:MemoryOptimized` on table / `AlterDatabaseOperation` | in-memory table | adds `MEMORY_OPTIMIZED_DATA` filegroup + file if `IsXTPSupported` and not Azure; `MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT ON`; indexes inline `INDEX … NONCLUSTERED` | |
| Identity | `SqlServer:Identity` = "seed, increment" | | `IDENTITY(s, i)`; data seeding wraps `SET IDENTITY_INSERT … ON/OFF` | changing identity requires table rebuild (not supported in-place) |
| Sequences | `CreateSequenceOperation` / `AlterSequenceOperation` / `RestartSequenceOperation` / `RenameSequenceOperation` | | `CREATE/ALTER SEQUENCE … START WITH / INCREMENT BY / MINVALUE / MAXVALUE / CYCLE / RESTART`; rename via `sp_rename` | relational ops, SQL Server SQL |
| Index options | `SqlServer:Clustered`, `Include`, `Online`, `FillFactor`, `SortInTempDb`, `DataCompression`; filtered index | | `CREATE [UNIQUE] [NON]CLUSTERED INDEX … INCLUDE (…) WHERE … WITH (FILLFACTOR=…, ONLINE=ON, SORT_IN_TEMPDB=ON, DATA_COMPRESSION=…)` | unique indexes on nullable columns get automatic `WHERE [c] IS NOT NULL` filter |
| Sparse columns | `SqlServer:Sparse` | | `SPARSE` | |
| Computed columns | relational `HasComputedColumnSql(sql, stored)` | | `AS (expr) [PERSISTED]` | |
| Collation | relational `UseCollation` | | column `COLLATE x`; database `ALTER DATABASE … COLLATE` | |
| Default constraints | relational + named (`DefaultConstraintName`, `UseNamedDefaultConstraints`) | | drops discover unnamed `DF__` constraints dynamically via `sys.default_constraints` + `EXEC` | |
| Schema ops | `EnsureSchemaOperation`, rename table to new schema | | `IF SCHEMA_ID(N'x') IS NULL EXEC(N'CREATE SCHEMA [x]')`; `ALTER SCHEMA … TRANSFER` | |
| Comments | relational `HasComment` | | `sp_addextendedproperty 'MS_Description'` | |
| Rename | rename column/index/table | | `EXEC sp_rename` | |
| Azure options | `SqlServer:EditionOptions`, `DatabaseMaxSize`, `ServiceTierSql`, `PerformanceLevelSql` | | `ALTER DATABASE … MODIFY (EDITION=…, SERVICE_OBJECTIVE=…, MAXSIZE=…)` guarded by `SERVERPROPERTY('EngineEdition') = 5` | |
| Data compression on tables | `DataCompression` | | rebuild with `DATA_COMPRESSION`; decompress before memory-optimizing | |
| `IsSqlServer` | `MigrationBuilder.IsSqlServer()` | provider check in migration code | | |
| History table | `__EFMigrationsHistory` | | existence via `SELECT OBJECT_ID(N'[__EFMigrationsHistory]')`; `IF OBJECT_ID … IS NULL CREATE TABLE` | |
| Migration lock | `SqlServerHistoryRepository.AcquireDatabaseLock` | concurrent-migration guard | `sp_getapplock @Resource='__EFMigrationsLock', @LockOwner='Session', @LockMode='Exclusive'`; release `sp_releaseapplock` | `LockReleaseBehavior.Connection` (session-scoped) |
| Idempotent scripts | | | statements wrapped in `IF NOT EXISTS (…history…) BEGIN … END`, DDL inside `EXEC(N'…')` | |

## 8. Database-level

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `SqlServerDatabaseCreator.Create/CreateAsync` | via `Database.EnsureCreated()` | create DB | runs `SqlServerCreateDatabaseOperation` on a **master** connection, then `ClearPool` | `RetryDelay` 500 ms, `RetryTimeout` 1 min (public settable props) |
| `Delete/DeleteAsync` | `EnsureDeleted()` | drop DB | master connection, `SINGLE_USER WITH ROLLBACK IMMEDIATE`, `DROP DATABASE` | |
| `Exists/ExistsAsync` | | DB exists | opens connection + `SELECT 1`; errors 4060/1832/5120 = "does not exist"; retries on 233, -2, 18456, 203 | Azure DB creation can lag; retry loop |
| `HasTables/HasTablesAsync` | | any user tables | query on `sys.objects` | |
| `SqlServerRetryingExecutionStrategy` | ctors `(DbContext|ExecutionStrategyDependencies[, int maxRetryCount[, TimeSpan maxRetryDelay, IEnumerable<int>? errorNumbersToAdd]])`; `AdditionalErrorNumbers` | transient-fault retry | `SqlServerTransientExceptionDetector` (~100 Azure/SQL error numbers: 40613, 40197, 40501, 49918–49920, 4060, 1205 deadlock, 233, -2 timeout…) ; throttling adds 5 s | default for Azure SQL / Synapse |
| `SqlServerExecutionStrategy` (internal default) | | no-retry, but detects user transactions | | |
| Connection | `SqlServerConnection` | | `CreateMasterConnection()` (Initial Catalog=master); detects MARS (`IsMultipleActiveResultSetsEnabled`); supports ambient `System.Transactions` | savepoints disabled when MARS on |
| Engine type switch | `UseSqlServer` / `UseAzureSql` / `UseAzureSynapse` | engine flavour | toggles defaults (retry, compat, json, Azure DB options) | |
| Compatibility level | `UseCompatibilityLevel(int)` | server version targeting | see §1 | Synapse bug noted in §1 |
| `IsSqlServer` | `DatabaseFacade.IsSqlServer()` | provider check | | |
| Batching | `MaxBatchSize` | | default 42, max 1000; `SET NOCOUNT ON`; `MERGE … OUTPUT` for multi-row inserts with generated values; `OUTPUT INTO @t` when output clause disabled | |

## 9. Diagnostics / event IDs (`SqlServerEventId`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `DecimalTypeDefaultWarning` | validation | decimal without precision | silent truncation risk | configure `HasPrecision` |
| `DecimalTypeKeyWarning` | validation | decimal key with identity | | |
| `ByteIdentityColumnWarning` | validation | `byte` identity | overflows at 255 | |
| `ConflictingValueGenerationStrategiesWarning` | validation | strategy + default/computed conflict | | **set to Throw by `UseSqlServer`**; payload `ConflictingValueGenerationStrategiesEventData` |
| `SavepointsDisabledBecauseOfMARS` | transaction | savepoints off with MARS | failure in SaveChanges can't roll back to savepoint | |
| `JsonTypeExperimental` | — | | | no longer used (kept for ID stability) |
| Scaffolding IDs | `ColumnFound`, `DefaultSchemaFound`, `TypeAliasFound`, `MissingSchemaWarning`, `MissingTableWarning`, `ForeignKeyReferencesMissingPrincipalTableWarning`, `ForeignKeyReferencesUnknownPrincipalTableWarning`, `TableFound`, `SequenceFound`, `PrimaryKeyFound`, `UniqueConstraintFound`, `IndexFound`, `ForeignKeyFound`, `ForeignKeyPrincipalColumnMissingWarning`, `ReflexiveConstraintIgnored`, `DuplicateForeignKeyConstraintIgnored`, `ColumnWithoutTypeWarning`, `MissingViewDefinitionRightsWarning` | reverse-engineering | design-time only | |
| Notable exceptions (`SqlServerStrings`) | `CompatibilityLevelTooLowForScalarCollections`, `JsonValuePathExpressionsNotSupported`, `ExecuteUpdateCannotSetJsonPropertyOnOldSqlServer`, `InvalidColumnNameForFreeText`, `AlreadyConfiguredEngineType` | | | users hit these when targeting older compat levels |
