# Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3: provider-specific API inventory

Source: npgsql/efcore.pg, `src/EFCore.PG`, tag 10.0.3. Read-only static inventory; nothing was executed.
Known plugins that are not covered here: `Npgsql.EntityFrameworkCore.PostgreSQL.NodaTime` (`UseNodaTime()`: Instant, LocalDateTime, Period, Interval and others) and `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` (`UseNetTopologySuite()`: PostGIS geometry/geography). Both hook into the data source through `INpgsqlDataSourceConfigurationPlugin` and their own `ConfigureDataSource` calls.

Conventions: `_` = `this DbFunctions _` (accessed as `EF.Functions.X`). "Gen" = the overload pair `X(this IndexBuilder ...)` / `X<TEntity>(this IndexBuilder<TEntity> ...)` (and the same for the Property and EntityType builders). `IConvention*Builder` overloads (with `fromDataAnnotation`, plus `CanSet*`) exist for nearly every builder method and are omitted. Obsolete and internal members are flagged.

---

## 1. Options builder (`UseNpgsql(..., Action<NpgsqlDbContextOptionsBuilder>)`)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `UseNpgsql` | `(DbContextOptionsBuilder[, string? connectionString][, Action<NpgsqlDbContextOptionsBuilder>?])`; `(…, DbConnection, [bool contextOwnsConnection,] Action?)`; `(…, DbDataSource, Action?)`; generic `<TContext>` versions of each | Provider selection plus a connection source (connection string, connection, or data source) | When a connection string is given, an internal `NpgsqlDataSource` is built and cached per (connection string, options) by `NpgsqlDataSourceManager` | The parameterless overload expects the connection to be supplied later (`SetDbDataSource`/`SetConnectionString`) |
| `AddNpgsql<TContext>` | `(IServiceCollection, string? connStr, Action<NpgsqlDbContextOptionsBuilder>?, Action<DbContextOptionsBuilder>?)` | DI registration shortcut | Same as `AddDbContext` plus `UseNpgsql` | |
| `AddEntityFrameworkNpgsql` | `(IServiceCollection)` | Raw provider service registration | Registers the provider services | Rarely used directly |
| `ConfigureDataSource` | `(Action<NpgsqlDataSourceBuilder>)` | Driver-level connection factory configuration | Lets the caller configure the NpgsqlDataSourceBuilder (type mappers, plugins, auth, SSL, logging) used when the context builds its own data source | Replaces the obsolete callbacks below |
| `UseAdminDatabase` | `(string? dbName)` | Maintenance database for create/drop | Used by `EnsureCreated`/`EnsureDeleted`/`Exists`. Default `postgres`, with `template1` as fallback | |
| `SetPostgresVersion` | `(Version?)` / `(int major, int minor)` | Target server version (feature gate) | Enables or disables SQL features: identity needs ≥10, INCLUDE ≥11, gen_random_uuid ≥13, multirange ≥14, regexp_count ≥15, jsonb_set_lax partial update ≥16, uuidv7 ≥18 | The default is "latest". No automatic detection |
| `UseRedshift` | `(bool useRedshift = true)` | Dialect variant (Amazon Redshift) | Turns off unsupported constructs such as jsonb_set_lax | |
| `MapRange<TSubtype>` / `MapRange` | `(string rangeName, [Type subtypeClrType,] string? schemaName = null, string? subtypeName = null)` | User-defined range type mapping | Maps `NpgsqlRange<TSubtype>` to a user-created PG range type | Pair with the model-side `HasPostgresRange` |
| `MapEnum<T>` / `MapEnum` | `(string? enumName, string? schemaName, INpgsqlNameTranslator? nameTranslator)` (`[Type clrType]` for the non-generic form) | Native enum type mapping | Maps the CLR enum to a PG `CREATE TYPE … AS ENUM`, registers it with the data source, and calls `HasPostgresEnum` in the model through a convention | New in 9.0+: one call configures both ADO and EF. Labels are translated with the snake_case translator by default |
| `ReverseNullOrdering` | `(bool = true)` | Null ordering emulation (nulls sort low, as in SQL Server) | Appends `NULLS FIRST` to ASC and `NULLS LAST` to DESC on every ORDER BY | **internal** in 10.0.3 (testing only) |
| `ProvideClientCertificatesCallback` | `(ProvideClientCertificatesCallback?)` | TLS client certificate | Driver passthrough | **[Obsolete]**: use ConfigureDataSource |
| `RemoteCertificateValidationCallback` | `(RemoteCertificateValidationCallback?)` | TLS server validation | Driver passthrough | **[Obsolete]** |
| `ProvidePasswordCallback` | `(ProvidePasswordCallback?)` | Dynamic password | Driver passthrough | **[Obsolete]** |
| `EnableRetryOnFailure` | `()`, `(int maxRetryCount)`, `(ICollection<string>? errorCodesToAdd)`, `(int maxRetryCount, TimeSpan maxRetryDelay, ICollection<string>? errorCodesToAdd)` | Transient-fault retry | Installs `NpgsqlRetryingExecutionStrategy`. The defaults are 6 retries and a 30s maximum delay. Retries when `NpgsqlException.IsTransient`, on `TimeoutException`, or on the extra SQLSTATE codes | |
| (relational base) `CommandTimeout`, `MaxBatchSize`, `MinBatchSize`, `MigrationsAssembly`, `MigrationsHistoryTable`, `UseQuerySplittingBehavior`, `TranslateParameterizedCollectionsToConstants`… | inherited | n/a | Default MaxBatchSize 1000 | Not provider specific |
| `DatabaseFacade.IsNpgsql()` | `bool` | Provider detection | | |
| `DatabaseFacade.SetDbDataSource` | `(DbDataSource)` | Swap the connection source at runtime | | |
| AppContext `Npgsql.EnableLegacyTimestampBehavior` | switch | Legacy DateTime mapping | DateTime maps to `timestamp` instead of `timestamptz` | Global, read once |
| AppContext `Npgsql.DisableDateTimeInfinityConversions` | switch | ±infinity mapping | Turns off the DateTime.Min/MaxValue ↔ ±infinity mapping | |

## 2. Model building API

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| **ModelBuilder** | | | | |
| `UseHiLo` | `(ModelBuilder, string? name = null, string? schema = null)` | Model default key generation: client-side blocks from a sequence | `CREATE SEQUENCE EntityFrameworkHiLoSequence INCREMENT BY 10` plus `nextval()` per block | Sets strategy `SequenceHiLo` |
| `UseSerialColumns` | `(ModelBuilder)` | Model default: legacy auto-increment | `serial`/`bigserial`/`smallserial` column types | Strategy `SerialColumn` |
| `UseIdentityAlwaysColumns` | `(ModelBuilder)` | Model default: identity that the user cannot override | `GENERATED ALWAYS AS IDENTITY` | Needs PG ≥10. Inserting explicit values emits `OVERRIDING SYSTEM VALUE` |
| `UseIdentityByDefaultColumns` | `(ModelBuilder)` | Model default: overridable identity | `GENERATED BY DEFAULT AS IDENTITY` | **This is the provider default** |
| `UseIdentityColumns` | `(ModelBuilder)` | Alias | Same as ByDefault | |
| `UseKeySequences` | `(ModelBuilder, string? nameSuffix = null, string? schema = null)` | Model default: one server sequence per key | `CREATE SEQUENCE "<Table><suffix>"` and `DEFAULT nextval('…')` | Strategy `Sequence`; the default suffix is `Sequence` |
| `HasPostgresExtension` | `(ModelBuilder, string name)` / `(ModelBuilder, string? schema, string name, string? version = null)` | Server feature/extension requirement | `CREATE EXTENSION IF NOT EXISTS name [SCHEMA s] [VERSION v]` | Added automatically by convention for hstore, citext, ltree and cube columns |
| `HasPostgresEnum` | `(ModelBuilder, string? schema, string name, string[] labels)` / `(ModelBuilder, string name, string[] labels)` / `<TEnum>(ModelBuilder, string? schema = null, string? name = null, INpgsqlNameTranslator? nameTranslator = null)` | Named enum type definition | `CREATE TYPE s.name AS ENUM ('a','b')`. Migrations can `ALTER TYPE … ADD VALUE … BEFORE/AFTER` | Called automatically by `MapEnum` |
| `HasPostgresRange` | `(ModelBuilder, string? schema, string name, string subtype, string? canonicalFunction, string? subtypeOpClass, string? collation, string? subtypeDiff)` / `(ModelBuilder, string name, string subtype)` | User-defined range type | `CREATE TYPE name AS RANGE (SUBTYPE=…, …)` | |
| `HasCollation` | `(ModelBuilder, string name, string locale, string? provider = null, bool? deterministic = null)`; `(…, string? schema, string name, string locale, …)`; `(…, string? schema, string name, string lcCollate, string lcCtype, string? provider, bool? deterministic)` | Named collation definition (including ICU and non-deterministic, i.e. case-insensitive) | `CREATE COLLATION s.name (LC_COLLATE=…, LC_CTYPE=… / LOCALE=…, PROVIDER=icu, DETERMINISTIC=false)` | Use with the relational `UseCollation()`. Non-deterministic collations break LIKE |
| `UseDatabaseTemplate` | `(ModelBuilder, string templateDatabaseName)` | Database creation template | `CREATE DATABASE … TEMPLATE t` | Used by EnsureCreated and migrations |
| `UseTablespace` | `(ModelBuilder, string tablespace)` | Database storage location | `CREATE DATABASE … TABLESPACE t` | |
| (relational) `UseCollation(string)` on ModelBuilder | inherited | Default database collation | `CREATE DATABASE … LC_COLLATE` | Not PG-specific, but PG also honours column and index collations |
| **EntityTypeBuilder** | | | | |
| `IsUnlogged` | Gen `(bool unlogged = true)` | Non-durable (no WAL) table | `CREATE UNLOGGED TABLE`, `ALTER TABLE SET LOGGED/UNLOGGED` | |
| `HasStorageParameter` | Gen `(string parameterName, object? parameterValue)` | Table storage options | `CREATE TABLE … WITH (fillfactor=70, autovacuum_enabled=false)`, `ALTER TABLE SET/RESET (…)` | |
| `HasGeneratedTsVectorColumn<TEntity>` | `(Expression<Func<TEntity,NpgsqlTsVector>> tsVectorProperty, string config, Expression<Func<TEntity,object>> includeExpression)` | Stored, computed full-text document column | `tsv tsvector GENERATED ALWAYS AS (to_tsvector('english', coalesce("A",'') || ' ' || coalesce("B",''))) STORED` | Needs PG ≥12 |
| `UseCockroachDbInterleaveInParent` | Gen `(Type parentTableType, List<string> interleavePrefix)` | CockroachDB interleaved table | `INTERLEAVE IN PARENT parent (cols)` | CockroachDB-only and effectively legacy |
| (convention) uint concurrency token | `Property<uint>("xmin").IsRowVersion()` or `[Timestamp] uint Version` | Row-version optimistic concurrency token | A `uint` that is OnAddOrUpdate, a concurrency token, and mapped to `xid` is renamed to the system column `xmin`. The column is never created by migrations | No `UseXminAsConcurrencyToken()` exists in 10.x; it was removed in 7.0 |
| **PropertyBuilder** | | | | |
| `UseHiLo` | Gen `(string? name = null, string? schema = null)` | Per-key HiLo | as above | |
| `UseSequence` | Gen `(string? name = null, string? schema = null)` | Per-key sequence default | `DEFAULT nextval('seq')`. Creates the sequence | Strategy `Sequence` |
| `UseSerialColumn` | Gen `()` | Legacy auto-increment | `serial` | |
| `UseIdentityAlwaysColumn` | Gen `()` | Non-overridable identity | `GENERATED ALWAYS AS IDENTITY` | |
| `UseIdentityByDefaultColumn` | Gen `()` | Overridable identity | `GENERATED BY DEFAULT AS IDENTITY` | |
| `UseIdentityColumn` | Gen `()` | Alias | ByDefault | |
| `HasIdentityOptions` | Gen `(long? startValue, long? incrementBy, long? minValue, long? maxValue, bool? cyclic, long? numbersToCache)` | Identity sequence options (seed and increment) | `GENERATED … AS IDENTITY (START WITH s INCREMENT BY i MINVALUE … MAXVALUE … CYCLE CACHE n)` and `ALTER … SET …`/`RESTART` | Comparable to SQL Server's IDENTITY(seed, inc) |
| `HasPostgresArrayConversion<TElementProperty,TElementProvider>` | `(PropertyBuilder<TElement[]> \| PropertyBuilder<List<TElement>>, Expression to, Expression from)` | Element-wise value conversion for array columns | e.g. `enum[]` → `text[]` | The `(ValueConverter elementValueConverter)` overloads **throw NotSupportedException** |
| `IsGeneratedTsVectorColumn` | `(PropertyBuilder[<NpgsqlTsVector>], string config, params string[] includedPropertyNames)` | Stored, computed full-text column | as above | |
| `UseCompressionMethod` | Gen `(string? compressionMethod)` | Column TOAST compression (pglz/lz4) | `col text COMPRESSION lz4`, `ALTER COLUMN SET COMPRESSION` | PG ≥14 |
| (relational) `HasColumnType("jsonb"/"citext"/"int[]"…)`, `UseCollation`, `HasComputedColumnSql(stored:true)` | inherited | n/a | Virtual computed columns: only STORED is supported before PG18 | |
| **IndexBuilder** | | | | |
| `HasMethod` | Gen `(string? method)` | Index access method | `CREATE INDEX … USING gin\|gist\|brin\|hash\|spgist\|btree` | |
| `HasOperators` | Gen `(params string[]? operators)` | Per-column operator class | `USING gin (col gin_trgm_ops)` | |
| `IsTsVectorExpressionIndex` | Gen `(string config)` | Full-text expression index | `CREATE INDEX … USING GIN (to_tsvector('english', coalesce(a,'') \|\| ' ' \|\| coalesce(b,'')))` | Must be used with `HasMethod("GIN")` |
| `UseCollation` | Gen `(params string[]? values)` | Per-column index collation | `(col COLLATE "x")` | |
| `HasNullSortOrder` | Gen `(params NullSortOrder[]? values)` | Per-column NULLS FIRST/LAST in the index | `(col DESC NULLS LAST)` | enum `NullSortOrder { Unspecified, NullsFirst, NullsLast }`. Use the relational `IsDescending` for sort order. The PG `SortOrder` enum is legacy |
| `IncludeProperties` | Gen `(params string[] propertyNames)` / `<TEntity>(Expression<Func<TEntity,object?>>)` | Covering (non-key) index columns | `CREATE INDEX … (a) INCLUDE (b, c)` | PG ≥11 |
| `IsCreatedConcurrently` | Gen `(bool createdConcurrently = true)` | Online, non-locking index build | `CREATE INDEX CONCURRENTLY` | The migration step cannot run in a transaction |
| `AreNullsDistinct` | Gen `(bool nullsDistinct = true)` | Unique-index null semantics | `CREATE UNIQUE INDEX … NULLS NOT DISTINCT` | PG ≥15 |
| `HasStorageParameter` | Gen `(string parameterName, object? parameterValue)` | Index storage options | `WITH (fillfactor=…, fastupdate=…)` | |
| (relational) `HasFilter`, `IsDescending`, `IsUnique` | inherited | n/a | Partial index via `WHERE` | |
| **KeyBuilder / TableBuilder** | none | n/a | No PG-specific key or table builder extensions (no clustered/fillfactor on keys) | |
| **MigrationBuilder** | | | | |
| `EnsurePostgresExtension` | `(MigrationBuilder, string name, string? schema = null, string? version = null)` | Ensure a server extension exists | `CREATE EXTENSION IF NOT EXISTS` | |
| `IsNpgsql` | `(MigrationBuilder) → bool` | Provider detection in migrations | | |
| **Metadata accessors** (`NpgsqlModelExtensions`, `NpgsqlEntityTypeExtensions`, `NpgsqlPropertyExtensions`, `NpgsqlIndexExtensions`) | `Get/Set*` for every item above (`GetValueGenerationStrategy`, `GetHiLoSequenceName`, `GetSequenceName`, `GetIdentityStartValue`…, `GetPostgresExtensions`, `GetPostgresEnums`, `GetPostgresRanges`, `GetCollations`, `GetDatabaseTemplate`, `GetTablespace`, `GetIsUnlogged`, `GetStorageParameters`, `GetTsVectorConfig`, `GetTsVectorProperties`, `GetCompressionMethod`, `GetMethod`, `GetOperators`, `GetNullSortOrder`, `GetIncludeProperties`, `IsCreatedConcurrently`, `GetAreNullsDistinct`, `GetTsVectorConfig`) | Read and write model annotations | Annotations use the prefix `Npgsql:` | Several annotation names are `[Obsolete]` (legacy comment, collation, serial and bulk config) |

## 3. Value generation strategies and key generation

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `NpgsqlValueGenerationStrategy.None` | enum | No store generation | | |
| `.SequenceHiLo` | enum | Client-side block allocation from a sequence | `SELECT nextval('EntityFrameworkHiLoSequence')`, then the client hands out N ids | Ids are known before SaveChanges |
| `.SerialColumn` | enum | Legacy auto-increment | `serial` | |
| `.IdentityAlwaysColumn` | enum | Strict identity | `GENERATED ALWAYS AS IDENTITY` | Explicit values use `OVERRIDING SYSTEM VALUE` |
| `.IdentityByDefaultColumn` | enum | Identity (default) | `GENERATED BY DEFAULT AS IDENTITY` | Provider default for short/int/long keys |
| `.Sequence` | enum | Server-side sequence default | `DEFAULT nextval('seq')` | |
| `IsIdentity()` | ext on the enum | helper | | |
| Guid keys | `NpgsqlSequentialGuidValueGenerator` | Client-side time-ordered UUID | `Guid.CreateVersion7()` (UUIDv7) | Used when there is no default SQL |
| String keys | `NpgsqlSequentialStringValueGenerator` | Client-side UUIDv7 as a string | | |
| Store-generated read-back | n/a | Return generated values | `INSERT … RETURNING col` (also for UPDATE) | Requires no triggers-related config, unlike SQL Server |
| `Guid.NewGuid()` in queries | | Server UUID | `gen_random_uuid()` (≥13), otherwise `uuid_generate_v4()` | `Guid.CreateVersion7()` becomes `uuidv7()` on PG ≥18 |

## 4. EF.Functions / DbFunctions extensions

### 4a. `NpgsqlDbFunctionsExtensions` (general)
| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `ILike` | `(_, string matchExpression, string pattern)` / `(…, string escapeCharacter)` | Case-insensitive LIKE | `a ILIKE b [ESCAPE 'c']` | Also translates inside `array.Any(p => EF.Functions.ILike(x, p))` as `x ILIKE ANY (array)` |
| `StringToArray` | `(_, string value, string delimiter)` / `(…, string nullString)` | Split a string into an array | `string_to_array(v, d[, n])` | |
| `Reverse` | `(_, string value)` | String reverse | `reverse(v)` | |
| `GreaterThan` / `LessThan` / `GreaterThanOrEqual` / `LessThanOrEqual` | `(_, ITuple a, ITuple b)` | Row-value (tuple) comparison, used for keyset pagination | `(a1, a2) > (b1, b2)` | Arguments are `ValueTuple.Create(...)` or `(x, y)` |
| `Distance` | `(_, DateOnly a, DateOnly b) → int`; `(_, DateTime a, DateTime b) → TimeSpan` | Absolute distance (KNN) | `a <-> b` | Needs the `btree_gist` extension |
| `ToDate` | `(_, string value, string format) → DateOnly` | Parse a date with a format | `to_date(v, fmt)` | |
| `ToTimestamp` | `(_, string value, string format) → DateTime` | Parse a timestamp with a format | `to_timestamp(v, fmt)` | |
| (EF core) `EF.Functions.Like`, `Random`, `Collate` | inherited | | `LIKE` (EF escape is normalised because PG's default escape is `\`), `random()`, `COLLATE` | Listed for completeness |

### 4b. `NpgsqlAggregateDbFunctionsExtensions` (used inside GroupBy as `EF.Functions.X(g.Select(...))`)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `ArrayAgg<T>` | `(_, IEnumerable<T>) → T[]` | Aggregate into an array | `array_agg(x [ORDER BY …])` | Supports OrderBy and Distinct inside the aggregate |
| `JsonAgg<T>` / `JsonbAgg<T>` | `(_, IEnumerable<T>) → T[]` | Aggregate into a JSON array | `json_agg(x)` / `jsonb_agg(x)` | |
| `JsonObjectAgg<T1,T2>` / `JsonObjectAgg<T1,T2,TReturn>` | `(_, IEnumerable<(T1,T2)>)` | Aggregate key/value pairs into a JSON object | `json_object_agg(k, v)` | |
| `JsonbObjectAgg<T1,T2>` / `<T1,T2,TReturn>` | same | same | `jsonb_object_agg(k, v)` | |
| `Sum` | `(_, IEnumerable<TimeSpan>) → TimeSpan?` | Interval sum | `sum(interval)` | |
| `Average` | `(_, IEnumerable<TimeSpan>) → TimeSpan?` | Interval average | `avg(interval)` | |
| `StandardDeviationSample` | `(_, IEnumerable<byte\|short\|int\|long\|float\|double\|decimal>) → double?` | Sample standard deviation | `stddev_samp(x)` | 7 overloads |
| `StandardDeviationPopulation` | same, 7 overloads | Population standard deviation | `stddev_pop(x)` | |
| `VarianceSample` | same, 7 overloads | Sample variance | `var_samp(x)` | |
| `VariancePopulation` | same, 7 overloads | Population variance | `var_pop(x)` | |
| `Correlation` | `(_, IEnumerable<(double,double)>) → double?` | Correlation | `corr(y, x)` | |
| `CovariancePopulation` / `CovarianceSample` | same | Covariance | `covar_pop` / `covar_samp` | |
| `RegrAverageX` / `RegrAverageY` / `RegrCount` (`long?`) / `RegrIntercept` / `RegrR2` / `RegrSlope` / `RegrSXX` / `RegrSXY` | same | Linear regression aggregates | `regr_avgx`, `regr_avgy`, `regr_count`, `regr_intercept`, `regr_r2`, `regr_slope`, `regr_sxx`, `regr_sxy` | |
| (core LINQ) `string.Join(sep, g.Select(...))`, `string.Concat(g.Select(...))` | | String aggregation | `string_agg(x, sep [ORDER BY])`, `COALESCE(…, '')` | |

### 4c. Full-text search: `NpgsqlFullTextSearchDbFunctionsExtensions` (EF.Functions)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `ToTsVector` | `(_, string document)` / `(_, [NotParameterized] string config, string document)` | Build a full-text document | `to_tsvector([config::regconfig,] doc)` | |
| `ArrayToTsVector` | `(_, string[] lexemes)` | Build a document from lexemes | `array_to_tsvector(a)` | |
| `PlainToTsQuery` | `(_, [config,] string query)` | Plain-text query | `plainto_tsquery(...)` | |
| `PhraseToTsQuery` | `(_, [config,] string query)` | Phrase query | `phraseto_tsquery(...)` | |
| `ToTsQuery` | `(_, [config,] string query)` | Boolean query syntax | `to_tsquery(...)` | |
| `WebSearchToTsQuery` | `(_, [config,] string query)` | Web-search syntax query | `websearch_to_tsquery(...)` | |
| `Unaccent` | `(_, string text)` / `(_, string regDictionary, string text)` | Accent folding | `unaccent([dict::regdictionary,] t)` | `unaccent` extension |
| `NpgsqlTsVector.Parse(string)` / `NpgsqlTsQuery.Parse(string)` | static | Literal cast | `CAST(x AS tsvector/tsquery)` | Never evaluated on the client |

### 4d. Full-text search: `NpgsqlFullTextSearchLinqExtensions` (instance-style)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `Matches` | `(this NpgsqlTsVector, string query)` / `(this NpgsqlTsVector, NpgsqlTsQuery)` | Full-text match | `v @@ plainto_tsquery(q)` / `v @@ q` | Core full-text predicate |
| `Rank` | `(this NpgsqlTsVector, NpgsqlTsQuery)`, `(…, NpgsqlTsRankingNormalization)`, `(this NpgsqlTsVector, float[] weights, NpgsqlTsQuery)`, `(…, weights, query, normalization)` | Relevance score | `ts_rank([weights,] v, q[, norm])` | |
| `RankCoverDensity` | same 4 overloads | Cover-density relevance | `ts_rank_cd(...)` | |
| `And` / `Or` / `ToNegative` | `(this NpgsqlTsQuery, NpgsqlTsQuery)` / `(this NpgsqlTsQuery)` | Combine queries | `q1 && q2`, `q1 \|\| q2`, `!!q` | |
| `Contains` / `IsContainedIn` | `(this NpgsqlTsQuery, NpgsqlTsQuery)` | Query containment | `@>` / `<@` | |
| `GetNodeCount` | `(this NpgsqlTsQuery) → int` | Query size | `numnode(q)` | |
| `GetQueryTree` | `(this NpgsqlTsQuery) → string` | Indexable part of a query | `querytree(q)` | |
| `GetResultHeadline` | `(this NpgsqlTsQuery, string document)`, `(…, document, options)`, `(…, config, document, options)` | Highlighted snippet | `ts_headline([config,] doc, q[, opts])` | |
| `Rewrite` | `(this NpgsqlTsQuery, NpgsqlTsQuery target, NpgsqlTsQuery substitute)` / `(this NpgsqlTsQuery, string select)` | Query rewriting | `ts_rewrite(...)` | |
| `ToPhrase` | `(this NpgsqlTsQuery, NpgsqlTsQuery)` / `(…, int distance)` | Phrase (followed-by) | `tsquery_phrase(q1, q2[, d])` | |
| `Concat` | `(this NpgsqlTsVector, NpgsqlTsVector)` | Merge documents | `v1 \|\| v2` | |
| `SetWeight` | `(this NpgsqlTsVector, NpgsqlTsVector.Lexeme.Weight)`, `(…, Weight, string[] lexemes)`, `(…, char)`, `(…, char, string[])` | Weight lexemes | `setweight(v, 'A'[, lexemes])` | |
| `Delete` | `(this NpgsqlTsVector, string lexeme)` / `(…, string[] lexemes)` | Remove lexemes | `ts_delete(v, …)` | |
| `Filter` | `(this NpgsqlTsVector, char[] weights)` | Filter by weight | `ts_filter(v, weights::"char"[])` | |
| `GetLength` | `(this NpgsqlTsVector) → int` | Lexeme count | `length(v)` | |
| `ToStripped` | `(this NpgsqlTsVector)` | Strip positions | `strip(v)` | |

### 4e. `NpgsqlTrigramsDbFunctionsExtensions` (pg_trgm)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `TrigramsShow` | `(_, string) → string[]` | Show trigrams | `show_trgm(t)` | |
| `TrigramsSimilarity` | `(_, string, string) → double` | Similarity score | `similarity(a, b)` | |
| `TrigramsWordSimilarity` | `(_, string, string) → double` | Word similarity | `word_similarity(a, b)` | |
| `TrigramsStrictWordSimilarity` | `(_, string, string) → double` | Strict word similarity | `strict_word_similarity(a, b)` | |
| `TrigramsAreSimilar` | `(_, string, string) → bool` | Fuzzy match predicate | `a % b` | |
| `TrigramsAreWordSimilar` / `TrigramsAreNotWordSimilar` | `→ bool` | Word fuzzy match | `a <% b` / `a %> b` | |
| `TrigramsAreStrictWordSimilar` / `TrigramsAreNotStrictWordSimilar` | `→ bool` | Strict word fuzzy match | `a <<% b` / `a %>> b` | |
| `TrigramsSimilarityDistance` | `→ double` | Distance (KNN ordering) | `a <-> b` | |
| `TrigramsWordSimilarityDistance` / `…Inverted` | `→ double` | Word distance | `a <<-> b` / `a <->> b` | |
| `TrigramsStrictWordSimilarityDistance` / `…Inverted` | `→ double` | Strict word distance | `a <<<-> b` / `a <->>> b` | |

### 4f. `NpgsqlFuzzyStringMatchDbFunctionsExtensions` (fuzzystrmatch)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `FuzzyStringMatchSoundex` | `(_, string) → string` | Phonetic code | `soundex(t)` | SQL Server and MySQL have SOUNDEX too |
| `FuzzyStringMatchDifference` | `(_, string, string) → int` | Soundex difference | `difference(a, b)` | Same as SQL Server DIFFERENCE |
| `FuzzyStringMatchLevenshtein` | `(_, s, t)` / `(_, s, t, int ins, int del, int sub)` | Edit distance | `levenshtein(...)` | |
| `FuzzyStringMatchLevenshteinLessEqual` | `(_, s, t, int max)` / `(_, s, t, ins, del, sub, max)` | Bounded edit distance | `levenshtein_less_equal(...)` | |
| `FuzzyStringMatchMetaphone` | `(_, string, int maxLen)` | Phonetic code | `metaphone(t, n)` | |
| `FuzzyStringMatchDoubleMetaphone` / `…Alt` | `(_, string)` | Phonetic code | `dmetaphone(t)` / `dmetaphone_alt(t)` | |

### 4g. `NpgsqlJsonDbFunctionsExtensions`
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `JsonContains` | `(_, object json, object contained)` | JSON containment | `json @> contained` | jsonb only. Accepts a column, a string or a POCO |
| `JsonContained` | `(_, object contained, object json)` | Reverse containment | `contained <@ json` | |
| `JsonExists` | `(_, object json, string key)` | Top-level key exists | `json ? key` | |
| `JsonExistAny` | `(_, object json, params string[] keys)` | Any of the keys exists | `json ?\| keys` | |
| `JsonExistAll` | `(_, object json, params string[] keys)` | All of the keys exist | `json ?& keys` | |
| `JsonTypeof` | `(_, object json) → string` | JSON value kind | `jsonb_typeof(j)` / `json_typeof(j)` | |

### 4h. `NpgsqlRangeDbFunctionsExtensions` (`NpgsqlRange<T>` instance-style)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `Contains<T>` | `(this NpgsqlRange<T>, T value)` / `(this NpgsqlRange<T>, NpgsqlRange<T>)` | Interval contains | `r @> v` | |
| `ContainedBy<T>` | `(this NpgsqlRange<T> a, NpgsqlRange<T> b)` | Contained by | `a <@ b` | |
| `Overlaps<T>` | `(a, b)` | Interval overlap | `a && b` | Key temporal predicate |
| `IsStrictlyLeftOf<T>` / `IsStrictlyRightOf<T>` | `(a, b)` | Strict ordering | `a << b` / `a >> b` | |
| `DoesNotExtendLeftOf<T>` / `DoesNotExtendRightOf<T>` | `(a, b)` | Bound comparisons | `a &> b` / `a &< b` | |
| `IsAdjacentTo<T>` | `(a, b)` | Adjacency | `a -\|- b` | |
| `Union<T>` / `Intersect<T>` / `Except<T>` | `(a, b) → NpgsqlRange<T>` | Range algebra | `a + b`, `a * b`, `a - b` | |
| `Merge<T>` | `(a, b)` | Smallest covering range | `range_merge(a, b)` | |
| `RangeAgg<T>` | `(this IEnumerable<NpgsqlRange<T>>) → NpgsqlRange<T>[]` | Aggregate union into a multirange | `range_agg(r)` | PG ≥14 |
| `RangeIntersectAgg<T>` | `(this IEnumerable<NpgsqlRange<T>>)` / `(this IEnumerable<NpgsqlRange<T>[]>)` | Aggregate intersection | `range_intersect_agg(r)` | PG ≥14 |

### 4i. `NpgsqlMultirangeDbFunctionsExtensions` (multirange = `NpgsqlRange<T>[]` or `List<NpgsqlRange<T>>`, PG ≥14)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `Contains<T>` | (mr, T) / (mr, mr) / (mr, range), each for array and List (6) | Contains | `@>` | |
| `ContainedBy<T>` | (mr, mr) ×2 / (range, mr) ×2 | Contained by | `<@` | |
| `Overlaps<T>` | (mr, mr) ×2 / (mr, range) ×2 | Overlap | `&&` | |
| `IsStrictlyLeftOf<T>` / `IsStrictlyRightOf<T>` | (mr, mr) ×2 / (mr, range) ×2 each | Strict ordering | `<<` / `>>` | |
| `DoesNotExtendLeftOf<T>` / `DoesNotExtendRightOf<T>` | same | Bound comparisons | `&>` / `&<` | |
| `IsAdjacentTo<T>` | same | Adjacency | `-\|-` | |
| `Union<T>` / `Intersect<T>` / `Except<T>` | (mr, mr) for array and List | Multirange algebra | `+` / `*` / `-` | |
| `Merge<T>` | `(this NpgsqlRange<T>[] \| List<…>) → NpgsqlRange<T>` | Covering range | `range_merge(mr)` | |

### 4j. `NpgsqlNetworkDbFunctionsExtensions` (inet/cidr/macaddr)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `LessThan` / `LessThanOrEqual` / `GreaterThanOrEqual` / `GreaterThan` | `(_, NpgsqlInet, NpgsqlInet)`, `(_, PhysicalAddress, PhysicalAddress)` | Address ordering | `<`, `<=`, `>=`, `>` | |
| `ContainedBy` / `ContainedByOrEqual` | `(_, NpgsqlInet, NpgsqlInet)` | Subnet containment | `<<` / `<<=` | |
| `Contains` / `ContainsOrEqual` | `(_, NpgsqlInet, NpgsqlInet)` | Subnet containment | `>>` / `>>=` | |
| `ContainsOrContainedBy` | `(_, NpgsqlInet, NpgsqlInet)` | Either contains | `&&` | |
| `BitwiseNot` / `BitwiseAnd` / `BitwiseOr` | inet or macaddr | Bitwise ops | `~`, `&`, `\|` | |
| `Add` | `(_, NpgsqlInet, int)` | Address arithmetic | `inet + n` | |
| `Subtract` | `(_, NpgsqlInet, long)` / `(_, NpgsqlInet, NpgsqlInet) → int` | Address arithmetic | `inet - n` / `a - b` | |
| `Abbreviate` | `(_, NpgsqlInet)` / `(_, IPNetwork)` / `(_, NpgsqlCidr)` | Text form | `abbrev(x)` | |
| `Broadcast` / `HostMask` / `Netmask` | `(_, NpgsqlInet)` | Address parts | `broadcast`, `hostmask`, `netmask` | |
| `Family` | `(_, NpgsqlInet) → int` | IP version | `family(x)` | |
| `Host` / `Text` | `(_, NpgsqlInet) → string` | Text form | `host(x)` / `text(x)` | |
| `MaskLength` | `(_, NpgsqlInet) → int` | Prefix length | `masklen(x)` | |
| `Network` | `(_, NpgsqlInet) → IPNetwork` | Network part | `network(x)` | |
| `SetMaskLength` | `(_, NpgsqlInet, int)` / `(_, IPNetwork, int)` / `(_, NpgsqlCidr, int)` | Set prefix | `set_masklen(x, n)` | |
| `SameFamily` | `(_, NpgsqlInet, NpgsqlInet) → bool` | Same IP version | `inet_same_family(a, b)` | |
| `Merge` | `(_, NpgsqlInet, NpgsqlInet) → IPNetwork` | Smallest common network | `inet_merge(a, b)` | |
| `Truncate` | `(_, PhysicalAddress)` | MAC OUI | `trunc(mac)` | |
| `Set7BitMac8` | `(_, PhysicalAddress)` | macaddr8 bit | `macaddr8_set7bit(m)` | |
| All overloads taking `(IPAddress Address, int Subnet)` tuples or `IPAddress` (about 30: LessThan…Merge) | | Legacy cidr representation | | **[Obsolete(error: true)]**: use NpgsqlCidr/IPNetwork. `NpgsqlCidr` is itself obsolete in Npgsql in favour of `IPNetwork` |
| `IPAddress.Parse` / `PhysicalAddress.Parse` | static | Literal | `CAST(x AS inet/macaddr)` | |

### 4k. `NpgsqlCubeDbFunctionsExtensions` (cube extension; new in 10.0)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `Overlaps` / `Contains` / `ContainedBy` | `(this NpgsqlCube, NpgsqlCube) → bool` | N-dimensional box predicates | `&&`, `@>`, `<@` | |
| `Distance` / `DistanceTaxicab` / `DistanceChebyshev` | `(this NpgsqlCube, NpgsqlCube) → double` | Vector distances (Euclidean, L1, L∞) | `<->`, `<#>`, `<=>` | KNN-indexable, comparable to vector search |
| `NthCoordinate` / `NthCoordinateKnn` | `(this NpgsqlCube, int index) → double` | Coordinate extraction | `c -> n`, `c ~> n` | |
| `Union` / `Intersect` | `(this NpgsqlCube, NpgsqlCube)` | Box algebra | `cube_union`, `cube_inter` | |
| `Enlarge` | `(this NpgsqlCube, double radius, int dimensions)` | Grow box | `cube_enlarge(c, r, n)` | |
| `NpgsqlCube.Dimensions` / `IsPoint` / `LowerLeft[i]` / `UpperRight[i]` / `ToSubset(int[])` / `new NpgsqlCube(...)` | members | Cube accessors | `cube_dim`, `cube_is_point`, `cube_ll_coord(c, i+1)`, `cube_ur_coord`, `cube_subset(c, ARRAY[i+1…])`, `cube(...)` | Indexes are converted from zero-based to one-based |

### 4l. `LTree` (provider type `Microsoft.EntityFrameworkCore.LTree`; ltree extension)
| API | Shape | Concept | SQL | Notes |
|---|---|---|---|---|
| `IsAncestorOf` / `IsDescendantOf` | `(LTree other) → bool` | Hierarchy path predicates | `a @> b` / `a <@ b` | Comparable to SQL Server hierarchyid |
| `MatchesLQuery` / `MatchesLTxtQuery` | `(string) → bool` | Path pattern match | `t ~ q::lquery` / `t @ q::ltxtquery` | |
| `Subtree` / `Subpath` (2 overloads) / `Index` (2) / `NLevel` / `LongestCommonAncestor(params LTree[])` | members | Path ops | `subltree`, `subpath`, `index`, `nlevel`, `lca` | |
| Array forms (`ltrees.Any(t => t.IsAncestorOf(x))`, `FirstOrDefault(...)`) | LINQ | Path set ops | `?@>`, `?<@`, `?~`, `?` | |

## 5. LINQ translations beyond the relational core

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| **Strings** | | | | |
| `string.Contains(string)` | instance | Substring test | constant or parameter: `LIKE '%x%'` with `\` escaping; column: `strpos(a, b) > 0` | |
| `string.StartsWith` / `EndsWith` | instance | Prefix/suffix | `LIKE 'x%'`; column pattern: `left(a, length(b)) = b` / `right(...)` | |
| `string.IndexOf(string\|char)` | instance | Position | `strpos(a, b) - 1` | |
| `string.Replace(string,string)` | instance | Replace | `replace(a, b, c)` | |
| `ToLower` / `ToUpper` | instance | Case | `lower` / `upper` | |
| `Substring(i[, n])` | instance | Substring | `substring(a, i + 1[, n])` | |
| `string.IsNullOrWhiteSpace` | static | Blank test | `a IS NULL OR btrim(a, E' \t\n\r') = ''` | |
| `Trim/TrimStart/TrimEnd` (none, char, char[]) | instance | Trim | `btrim` / `ltrim` / `rtrim(a[, chars])` | |
| `PadLeft/PadRight(n[, c])` | instance | Pad | `lpad` / `rpad` | |
| `string.FirstOrDefault()` / `LastOrDefault()` | LINQ on string | First/last char | `substr(a, 1, 1)` / `substr(a, length(a), 1)` | |
| `string.Join(sep, a, b, …)` / `string.Join(sep, array)` | static | Concat with separator | `concat_ws(sep, …)` / `array_to_string(arr, sep, '')` | |
| `string.Length` | member | Length | `length(a)` | |
| `x.ToString()` | any | Text cast | `x::text` | bool, enum and others have special handling |
| string `+` | binary | Concatenation | `a \|\| b` | Also used for tsvector and arrays |
| `Regex.IsMatch(s, pattern[, options])` | static | Regex match | `s ~ p`, `s ~* p` (IgnoreCase), with an inline `(?n)`/`(?p)`/`x` flags prefix | RightToLeft and ECMAScript options are not supported |
| `Regex.Replace(s, pattern, repl[, options])` | static | Regex replace | `regexp_replace(s, p, r, 'g[flags]')` | |
| `Regex.Count(s, pattern[, options])` | static | Regex count | `regexp_count(s, p[, 1, flags])` | PG ≥15 |
| `EF.Functions.Like` over arrays: `patterns.Any(p => EF.Functions.Like(x, p))` / `.All(...)` | LINQ | Match any/all patterns | `x LIKE ANY (arr)` / `x LIKE ALL (arr)` (also ILIKE) | |
| **Math / numeric** | | | | |
| `Math.Abs/Ceiling/Floor/Pow/Exp/Log/Log10/Sqrt/Acos/Asin/Atan/Atan2/Cos/Sin/Tan/Sign/Round/Truncate`, `MathF.*`, `double/float.DegreesToRadians/RadiansToDegrees` | static | Math | `abs`, `ceiling`, `floor`, `power`, `exp`, `ln`, `log`, `sqrt`, …, `radians`, `degrees`, `round(x[, n])`, `trunc` | float results are cast back to `real` |
| `Math.Max/Min` (and `BigInteger.Max/Min`) | static | Greatest/least | `GREATEST(a, b)` / `LEAST(a, b)` | NULL-ignoring semantics differ from .NET |
| `double/float.IsNaN/IsPositiveInfinity/IsNegativeInfinity` | static | Special floats | `x = 'NaN'`, `'Infinity'`, `'-Infinity'` | |
| `BigInteger` (`numeric`): `Abs`, `Pow`, `IsZero`, `IsOne`, `IsEven` | | Arbitrary-precision integer | `abs`, `power`, `x = 0`, `x = 1`, `x % 2 = 0` | |
| `Convert.ToBoolean/ToByte/ToDecimal/ToDouble/ToInt16/ToInt32/ToInt64/ToString` | static | Casts | `x::bool`, `::smallint`, `::numeric`, `::double precision`, `::int`, `::bigint`, `::text` | |
| `^` (XOR) | binary | XOR | ints: `a # b`; bools: `a <> b` | PG's `^` is exponentiation |
| `Queryable.Sum/Count/LongCount/Average/Max/Min` | aggregate | Typed aggregates | `count(*)::int`; `sum(int)::int`; `avg(int::double precision)` | Casts back to the CLR type |
| **Date/time** (timestamptz semantics: DateTime with Kind=Utc maps to `timestamp with time zone`; Unspecified/Local values must be `timestamp`, and Npgsql throws on a mismatch) | | | | |
| `DateTime.UtcNow` / `DateTime.Now` / `DateTime.Today` / `DateTimeOffset.UtcNow`/`Now` | static | Current time | `now()` / `now()::timestamp` / `date_trunc('day', now()::timestamp)` | |
| `DateTime.Date` | member | Truncate to day | `date_trunc('day', x, 'UTC')` for timestamptz; `date_trunc('day', x)` otherwise | |
| `DateTime.Year/Month/Day/DayOfYear/Hour/Minute/Second/DayOfWeek` | member | Date parts | `date_part('year', x)::int` (doy, dow…); timestamptz is first converted with `AT TIME ZONE 'UTC'` | `Millisecond` is not translated |
| `DateTime.TimeOfDay` | member | Time part | `x::time` | |
| `DateTimeOffset.DateTime/UtcDateTime/LocalDateTime/Date` | member | Offset conversions | `x AT TIME ZONE 'UTC'`, no-op, `x::timestamp`, `date_trunc('day', x AT TIME ZONE 'UTC')` | Only UTC offsets are supported |
| `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds` (DateTime, DateTimeOffset); `TimeOnly.AddHours/AddMinutes/Add` | instance | Date arithmetic | `x + INTERVAL '1 days'` or `x + CAST($p \|\| ' days' AS interval)` | `AddMilliseconds` is not translated |
| `DateTime - DateTime`, `DateTime ± TimeSpan` | binary | Interval arithmetic | native `-` and `+`, returning interval | |
| `ToUniversalTime()` / `ToLocalTime()` / `DateTime.SpecifyKind(x, kind)` | instance/static | Time zone conversion | `x::timestamptz` / `x::timestamp` / `x AT TIME ZONE 'UTC'` | SpecifyKind requires a constant kind |
| `TimeZoneInfo.ConvertTimeBySystemTimeZoneId(x, tz)` / `ConvertTimeToUtc(x)` | static | Time zone conversion | `x AT TIME ZONE tz` / `x::timestamptz` | |
| `new DateTime(y,m,d[,h,mi,s[,kind]])` / `new DateOnly(y,m,d)` | ctor | Construction | `make_date`, `make_timestamp`, `make_timestamptz` | |
| `DateOnly.FromDateTime` / `ToDateTime` / `DayNumber` / `FromDayNumber` / `AddDays` / `AddMonths` / `AddYears` / `Year`… | | DateOnly | `x::date`, `x + time`, `x - DATE '0001-01-01'`, `DATE '0001-01-01' + n`, `x + n`, `CAST(x + interval AS date)` | |
| `TimeOnly.FromDateTime/FromTimeSpan/ToTimeSpan/IsBetween`, `Hour`… | | TimeOnly | `x::time`, `x::interval`, `x >= a AND x < b` | |
| `TimeSpan.Days/Hours/Minutes/Seconds/Milliseconds` | member | Interval parts | `floor(date_part('day', x))::int` | |
| `TimeSpan.TotalDays/TotalHours/TotalMinutes/TotalSeconds/TotalMilliseconds` | member | Interval totals | `date_part('epoch', x) / 86400` and similar | |
| `EF.Functions.Sum/Average(TimeSpan)` | aggregate | see 4b | | |
| **Guid** | | | | |
| `Guid.NewGuid()` / `Guid.CreateVersion7()` | static | Server UUID | `gen_random_uuid()` / `uuidv7()` (PG ≥18) | |
| **Arrays / List (primitive collections; `T[]`/`List<T>` → `elem[]`)** | | | | |
| `arr[i]` / `list[i]` / `ElementAt(i)` | index | Element access | `arr[i + 1]` | `byte[]` mapped to bytea uses `get_byte(b, i)` |
| `arr.Length` / `list.Count` / `Count()` | member | Length | `cardinality(arr)` | |
| `arr.Contains(x)` / `list.Contains(x)` | LINQ | Membership | `x = ANY (arr)`, with null compensation via `array_position(arr, x) IS NOT NULL`; constant or parameter vs column: `arr @> ARRAY[x]` (GIN-indexable) | Parameterised collections are sent as a single array parameter: `= ANY(@p)` |
| `arr.Any()` | LINQ | Non-empty | `cardinality(arr) > 0` | |
| `a.Any(x => b.Contains(x))` / `a.All(x => b.Contains(x))` | LINQ | Overlap/containment | `a && b` / `b @> a` | |
| `Array.IndexOf(arr, x[, start])` / `list.IndexOf(x)` | static | Position | `COALESCE(array_position(arr, x) - 1, -1)` | |
| `arr.SequenceEqual(other)` | LINQ | Array equality | `a = b` | |
| `arr.Concat(other)` / `Append(x)` | LINQ | Concatenate | `a \|\| b`, `array_cat`, `array_append` | |
| `arr.Skip(n)` / `Take(n)` | LINQ | Slice | `arr[n+1:]` / `arr[:n]` | |
| `arr.Where(x => x != c)` | LINQ | Remove | `array_remove(arr, c)` | |
| Other LINQ over arrays (Select, OrderBy, Sum…) | LINQ | Table-valued expansion | `FROM unnest(arr) WITH ORDINALITY AS a(value, ordinality)` | Natural ordering is preserved via ordinality |
| `EF.Functions.ArrayAgg` | | see 4b | | |
| **JSON** | | | | |
| EF complex types / owned `.ToJson()` (default `jsonb`) | model | Structured JSON mapping | property access: `col ->> 'Prop'`, `col #>> '{a,b}'`; nested `->`; collections `jsonb_array_length`, `jsonb_to_recordset(col) AS (…)`, `jsonb_array_elements_text`; ExecuteUpdate partial update `jsonb_set(col, '{path}', value)` (`jsonb_set_lax` on PG ≥16) | Primitive collections stored as JSON are also supported |
| Legacy POCO mapping (`[Column(TypeName="jsonb")] MyPoco`) | model | JSON POCO | `col ->> 'Name'`, `(col ->> 'Age')::int`, `jsonb_array_length(col -> 'Arr')`, `col #>> '{a,0}'` | Older approach; ToJson is preferred |
| `JsonDocument` / `JsonElement` columns: `RootElement`, `GetProperty("x")`, `[i]`, `GetArrayLength()`, `GetString/GetInt32/GetBoolean/GetDateTime/GetDecimal/GetDouble/GetGuid/GetSingle…` | DOM | JSON DOM traversal | `col -> 'x' ->> 'y'`, `CAST(… AS int)`, `jsonb_array_length` | |
| string column with `HasColumnType("jsonb")` | model | Raw JSON text | EF.Functions.Json* only | |
| **Ranges** | | | | |
| `NpgsqlRange<T>.LowerBound/UpperBound/IsEmpty/LowerBoundIsInclusive/UpperBoundIsInclusive/LowerBoundInfinite/UpperBoundInfinite` | member | Range accessors | `lower`, `upper`, `isempty`, `lower_inc`, `upper_inc`, `lower_inf`, `upper_inf` | |
| multirange `.Any()` | LINQ | Non-empty | `NOT isempty(mr)` | |
| **Row values / tuples** | | | | |
| `ValueTuple.Create(a, b)` / `(a, b)` / `new ValueTuple<…>` | ctor | Row value | `(a, b)` | Used with EF.Functions.GreaterThan and similar, and in tuple equality |
| **Enums** | | | | |
| CLR enum mapped by `MapEnum` | model | Native enum | Comparisons on the PG enum type (`x = 'happy'::mood`), ordering follows declaration order. Unmapped enums are stored as int | ToString → `x::text` |
| **Network / bytea / bit / misc** | | | | |
| `IPAddress`/`NpgsqlInet`/`IPNetwork` comparisons, casts | | inet/cidr | Implicit inet↔cidr cast stripping | |
| `byte[]` (bytea): `Length`, `Contains(b)`, `IndexOf`, `First()`, indexing | | Binary ops | `length(b)`, `position(set_byte('\x00',0,v) IN b) > 0`, `get_byte(b, i)` | |
| `BitArray` (varbit) | | Bit strings | `bit(n)`/`varbit` comparisons | |
| `Dictionary<string,string>` (hstore) | | Key/value map | Mapping only. 10.0.3 has **no** hstore query translator, so ContainsKey and the indexer are not translated | |
| **Query shape / SQL generation** | | | | |
| `Skip/Take` | | Paging | `LIMIT n OFFSET m` | |
| CROSS/OUTER APPLY | | Lateral join | `JOIN LATERAL (…) ON TRUE` / `LEFT JOIN LATERAL` | |
| `ExecuteDelete` with joins | | Multi-table delete | `DELETE FROM t USING t2 WHERE …` | |
| `ExecuteUpdate` with joins | | Multi-table update | `UPDATE t SET … FROM t2 WHERE …` | |
| Set operations | | UNION/INTERSECT/EXCEPT | typing injected for mismatched column types | |
| Inline collections `new[] {a, b}` | | Array literal | `ARRAY[a, b]::int[]` | |
| ORDER BY (ReverseNullOrdering off) | | Null ordering | PG native: NULLs sort as larger than any value (last in ASC); internal ReverseNullOrdering adds NULLS FIRST/LAST | Cross-DB difference: SQL Server, MySQL and SQLite sort NULLs first in ASC |
| `a == b ? null : a` | | NULLIF | `NULLIF(a, b)` | |
| Identifier quoting | | Quoting | Quoted only when the identifier needs it (mixed case, keyword, special characters) | |

## 6. Store types / type mappings (CLR → default store type)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| bool / byte / short / int / long | | Integers | `boolean`, `smallint` (byte), `smallint`, `integer`, `bigint` | No unsigned integer types. sbyte is unmapped |
| float / double / decimal / BigInteger | | Numerics | `real`, `double precision`, `numeric`, `numeric` | `money` is available by store type |
| string / char | | Text | `text` / `character(1)`; also `character varying(n)`, `citext` (auto-extension) | |
| byte[] | | Binary | `bytea` | |
| Guid | | UUID | `uuid` | |
| DateTime | | Timestamp | `timestamp with time zone` (Kind=Utc required); `timestamp without time zone` via column type or the legacy switch | |
| DateTimeOffset | | Timestamp with offset | `timestamp with time zone` (offset 0 only) | |
| DateOnly / TimeOnly / TimeSpan | | Date/time | `date`, `time without time zone`, `interval` | `time with time zone` maps to DateTimeOffset |
| uint / ulong | | Transaction ids | `xid` / `xid8` (also `oid`, `cid`, `regtype`, `lo`) | uint concurrency token maps to `xmin` |
| T[] / List<T> | | Native arrays | `elem[]` (for example `integer[]`, `text[]`) | Arbitrary element mappings, including enums and ranges |
| NpgsqlRange<int/long/decimal/DateTime/DateTimeOffset/DateOnly> | | Ranges | `int4range`, `int8range`, `numrange`, `tstzrange` (`tsrange` legacy), `tstzrange`, `daterange` | User ranges via MapRange |
| NpgsqlRange<T>[] / List<NpgsqlRange<T>> | | Multiranges | `int4multirange`, … `datemultirange` | PG ≥14, otherwise an array of ranges |
| JsonDocument / JsonElement / string(+type) / POCO / complex `.ToJson()` | | JSON | `jsonb` by default; `json`, `jsonpath` | |
| CLR enum | | Enum | PG enum (via MapEnum) or `integer` | |
| IPAddress / NpgsqlInet / IPNetwork / NpgsqlCidr / PhysicalAddress | | Network | `inet`, `inet`, `cidr`, `cidr` (obsolete), `macaddr` (`macaddr8` by store type) | |
| BitArray / bool(bit(1)) | | Bit strings | `bit varying`; `bit(n)` by store type | |
| Dictionary<string,string> / ImmutableDictionary | | Key-value | `hstore` (auto-extension) | |
| NpgsqlTsVector / NpgsqlTsQuery / NpgsqlTsRankingNormalization | | Full-text | `tsvector`, `tsquery`, `integer`; also `regconfig`, `regdictionary` | |
| NpgsqlPoint/Box/Line/LSeg/Path/Polygon/Circle | | Geometric | `point`, `box`, `line`, `lseg`, `path`, `polygon`, `circle` | PostGIS through the NTS plugin |
| NpgsqlCube | | Cube | `cube` (auto-extension) | |
| LTree / string(ltree) | | Hierarchy | `ltree`, `lquery`, `ltxtquery` (auto-extension) | |
| NpgsqlTid / NpgsqlLogSequenceNumber | | System | `tid`, `pg_lsn` | |
| `xml` | | XML | `xml` (string) | |
| Store-type aliases | | | `int`, `int4`, `int2`, `int8`, `float4`, `float8`, `bool`, `varchar`, `varbit`, `decimal` are accepted (case-insensitive) | |

## 7. Migrations (beyond relational)

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| Extension annotations on `AlterDatabaseOperation` | annotation `Npgsql:PostgresExtension:` | Ensure extension | `CREATE EXTENSION IF NOT EXISTS x SCHEMA s VERSION v` | Extensions are never dropped |
| Enum annotations | `Npgsql:Enum:` | Enum DDL | `CREATE TYPE … AS ENUM`, `ALTER TYPE … ADD VALUE 'x' BEFORE/AFTER`, `DROP TYPE` | Labels cannot be removed or renamed. `Migrate()` calls `ReloadTypes()` afterwards |
| Range annotations | `Npgsql:Range:` | Range DDL | `CREATE TYPE … AS RANGE (…)`, `DROP TYPE` | |
| Collation annotations | `Npgsql:CollationDefinition:` | Collation DDL | `CREATE COLLATION`, `DROP COLLATION` | |
| Identity | `Npgsql:ValueGenerationStrategy`, `Npgsql:IdentitySequenceOptions` | Identity DDL | `GENERATED … AS IDENTITY (…)`, `ALTER COLUMN … ADD/DROP IDENTITY`, `SET GENERATED ALWAYS/BY DEFAULT`, `SET INCREMENT BY`, `RESTART` | Serial→identity conversion is supported |
| Sequences (relational `CreateSequence`, `AlterSequence`, `RestartSequence`) | | Sequences | `CREATE SEQUENCE … AS type START WITH INCREMENT BY MINVALUE MAXVALUE CYCLE CACHE`, `OWNED BY` for serial | |
| Storage parameters | `Npgsql:StorageParameter:` | Table/index options | `WITH (…)`, `ALTER TABLE SET (…)`, `RESET (…)` | |
| Unlogged | `Npgsql:UnloggedTable` | Durability | `CREATE UNLOGGED TABLE`, `ALTER TABLE SET LOGGED/UNLOGGED` | |
| Index method/ops/collation/nulls/include/concurrently/nulls-distinct/tsvector | `Npgsql:IndexMethod`, `IndexOperators`, `IndexCollation`, `IndexNullSortOrder`, `IndexInclude`, `CreatedConcurrently`, `NullsDistinct`, `TsVectorConfig` | Index DDL | `CREATE [UNIQUE] INDEX [CONCURRENTLY] … USING m (c ops COLLATE x DESC NULLS LAST) INCLUDE (…) NULLS NOT DISTINCT WITH (…) WHERE …` | |
| Column compression | `Npgsql:Compression:` | Column storage | `COMPRESSION lz4`, `ALTER COLUMN SET COMPRESSION` | |
| Generated tsvector / computed | `Npgsql:TsVectorConfig`, `TsVectorProperties` | Computed column | `GENERATED ALWAYS AS (…) STORED` | |
| Alter column | | Type change | `ALTER COLUMN … TYPE t [USING]`, `SET/DROP NOT NULL`, `SET/DROP DEFAULT`; indexes on the column are recreated when needed | |
| Comments | relational HasComment | | `COMMENT ON TABLE/COLUMN` | |
| Rename/schema | | | `ALTER TABLE … RENAME TO`, `SET SCHEMA`, `ALTER SEQUENCE … RENAME`, `CREATE SCHEMA IF NOT EXISTS` (via `DO $EF$ … END $EF$` blocks) | |
| `NpgsqlCreateDatabaseOperation` | `{ Name, Template, Tablespace, Collation }` | Create database | `CREATE DATABASE n [TEMPLATE t] [TABLESPACE ts] [LC_COLLATE …]` | |
| `NpgsqlDropDatabaseOperation` | `{ Name }` | Drop database | `DROP DATABASE n` (the connection pool is cleared first) | |
| CockroachDB interleave | `CockroachDb:InterleaveInParent` | | `INTERLEAVE IN PARENT` | Legacy |
| History repository | `NpgsqlHistoryRepository` | Migration history | `__EFMigrationsHistory`; idempotent scripts use `DO $EF$ BEGIN IF NOT EXISTS(...) THEN … END IF; END $EF$;`; `CREATE TABLE IF NOT EXISTS` | |
| Migration lock | `AcquireDatabaseLock` | Concurrency control | `LOCK TABLE "__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE` | `LockReleaseBehavior.Transaction` |
| `NpgsqlMigrator` | | Post-migration hook | Calls `NpgsqlConnection.ReloadTypes()` when a migration created enums, ranges or extensions | |
| Non-transactional ops | | | `CREATE INDEX CONCURRENTLY`, `CREATE DATABASE`, and enum `ADD VALUE` (PG <12) are suppressed from the transaction | |

## 8. Database-level

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `NpgsqlDatabaseCreator` | `EnsureCreated/EnsureDeleted/Exists/HasTables/CreateTables` | Database lifecycle | Connects to the admin DB (`UseAdminDatabase`); `CREATE DATABASE … TEMPLATE/TABLESPACE`; `DROP DATABASE`; clears the Npgsql pool; `HasTables` queries `pg_class`/`information_schema` excluding extension-owned tables | Exists: tries to open, treats `3D000` as false |
| `NpgsqlRetryingExecutionStrategy` | ctor `(ExecutionStrategyDependencies\|DbContext, int maxRetryCount = 6, TimeSpan maxRetryDelay = 30s, ICollection<string>? errorCodesToAdd)` | Retry policy | Transient = `NpgsqlException.IsTransient` (driver classifies SQLSTATE classes 08, 53, 57P0x, 40001 serialization…) or a Timeout, plus custom SQLSTATEs | |
| `NpgsqlExecutionStrategy` | default | No-retry strategy | Detects transient errors and suggests EnableRetryOnFailure | |
| Data source management | `NpgsqlDataSourceManager` | Connection factory | One `NpgsqlDataSource` per (connection string + enum/range/plugin config), so type mappings are registered on the data source | Using an external DbConnection skips EF-registered enum mapping |
| `INpgsqlDataSourceConfigurationPlugin` | `void Configure(NpgsqlDataSourceBuilder)` | Plugin hook | Used by NodaTime and NTS | |
| Batching | `NpgsqlModificationCommandBatch` | Command batching | Uses `NpgsqlBatch` (protocol-level batching, not `;` concatenation); `INSERT/UPDATE … RETURNING`; `OVERRIDING SYSTEM VALUE` for explicit identity always | |
| Stored procedures | `AppendStoredProcedureCall` | Sproc CUD mapping | `CALL proc(…)`; result columns and return values are **not supported** (the model validator throws) | |
| Sequence read | | | `SELECT nextval('seq')` | |
| Version switches | `SetPostgresVersion` | Feature gating | see §1 | |
| `UseRedshift` | | Dialect variant | | |
| Admin DB | | | `postgres`, with `template1` as fallback | |

## 9. Diagnostics / event IDs

| API (exact name) | Shape | Concept | SQL / behaviour | Notes |
|---|---|---|---|---|
| `NpgsqlEfEventId.ColumnFound`, `TableFound`, `SequenceFound`, `PrimaryKeyFound`, `UniqueConstraintFound`, `IndexFound`, `ForeignKeyFound`, `CollationFound` | EventId (scaffolding, debug) | Reverse-engineering trace | | Scaffolding only |
| `NpgsqlEfEventId.MissingSchemaWarning`, `MissingTableWarning`, `ForeignKeyReferencesMissingPrincipalTableWarning`, `ForeignKeyPrincipalColumnMissingWarning` | EventId (warning) | Scaffolding problems | | |
| `NpgsqlEfEventId.EnumColumnSkippedWarning`, `ExpressionIndexSkippedWarning`, `UnsupportedColumnIndexSkippedWarning`, `UnsupportedColumnConstraintSkippedWarning` | EventId (warning) | Unsupported objects skipped by scaffolding | | |
| Model validation errors (exceptions, not events) | `NpgsqlModelValidator` | Config errors | Identity on PG <10; identity/sequence on non-integer types (`IdentityBadType`, `SequenceBadType`); INCLUDE property missing, duplicated or already in the index; stored-procedure result columns or return values; table-sharing compression mismatch | |
| Runtime: DateTime Kind mismatch | Npgsql `ArgumentException` | timestamptz safety | Writing a non-UTC DateTime to timestamptz, or a UTC one to timestamp, throws | Most common migration pain from other providers |
| (relational) `RelationalEventId.*`, `CoreEventId.*` | inherited | | MigrationsLock via `AcquiringMigrationLock` | |
