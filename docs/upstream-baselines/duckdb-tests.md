# DuckDB.EFCoreProvider (skuirrels, EF Core 10) — functional test suite as behaviour record

Source: `providers/duckdb-ef/test/DuckDB.EFCoreProvider.FunctionalTests` (349 .cs files), compiled against
`Microsoft.EntityFrameworkCore.Relational.Specification.Tests` 10.0.10 (spec source used for counting: EF 10.0.11 checkout).
Concept/API names follow `api-inventory/duckdb.md`. Method: static parsing of class declarations, test attributes,
`override` bodies and `Skip =` reasons; nothing was run.

**Counting caveats.** "Inherited" = distinct `[ConditionalFact]/[ConditionalTheory]/[Fact]/[Theory]` methods reachable through
the spec base chain (a theory counts once, not per data row); some chains that go through local abstract bases or multi-line
generic declarations were not resolved, so inherited counts are *lower bounds* (e.g. GraphUpdates shows 53 inherited but 127
skip overrides). Skip overrides declared on abstract bases (ManyToManyTracking, PropertyValues, StoreGenerated, OptimisticConcurrency)
apply to every concrete subclass.

## 1. Suite map

### Totals

| Measure | Value |
|---|---|
| Concrete spec-derived test classes | ~239 (+ ~40 abstract/local bases) |
| Inherited spec test methods (theory = 1) | **≈12,500** (lower bound; ≈14,000 if abstract bases double-count) |
| Spec tests skipped with `DuckDBSkipReasons.Tbd` ("TBD") | **≈3,800–4,160** (≈30–33 % of inherited; whole suites below) |
| Spec tests skipped with a specific reason | 52 (engine limits, DateTimeOffset offset, savepoints, FK-referenced rows, ...) |
| Spec overrides asserting a *different* outcome (throws / translation-failed) | ~49 |
| Spec overrides that keep the base test and add a DuckDB SQL baseline (`AssertSql`) | only ~100 (PrimitiveCollections 48, UpdateSqlGenerator 13, Where 11, Parquet 8, a few temporal/bulk) |
| Provider-only test classes | 70 classes, **640 tests** |
| Provider-only tests added inside spec-derived classes | 19 |
| **Inherited vs provider-only** | ≈12,500 inherited (≈8,400 effectively run) vs **659** provider-only |

**Assertion style.** Unlike the SQL Server / Npgsql / SQLite suites, the DuckDB port carries almost no SQL baselines: the
inherited spec tests run with EF's result comparison (`AssertQuery` against in-memory expected data) only. SQL baselines are
heavy only in `PrimitiveCollectionsQueryDuckDBTest` (native LIST functions), `DuckDBUpdateSqlGeneratorTest`,
`NorthwindWhereQueryDuckDBTest` (row values) and `ParquetTests`/`StructParquetTests` (`read_parquet`). `MigrationsSqlGeneratorTestBase`
overrides call `base` with no baseline (they only prove generation does not throw). Provider-only tests mostly assert results
and inspect SQL via `ToQueryString()`/command plans/captured commands (`Assert.Contains("read_parquet", …)`), i.e. *Both*.

### Spec suites that are effectively absent

| Status | Suites |
|---|---|
| Not derived at all | `UdfDbFunctionTestBase` (scalar/table UDF mapping, 106), `StoredProcedureUpdateTestBase` (sproc CUD mapping, 28), `FromSqlSprocQueryTestBase` (24), `SqlExecutorTestBase` (raw `ExecuteSql` matrix, 14). (`SpatialiteRequiredAttribute` exists but is unused.) |
| Derived but 100 % TBD-skipped (do not run) | `JsonQuery` (221), `JsonUpdate` (136), `PropertyValues` (167), `GraphUpdates` ×4 change-tracking strategies + `ProxyGraphUpdates` ×3 (127/85 each), `ManyToManyQuery` / `…NoTracking` / split / TPC / TPT variants (8 × ~120), `ManyToManyTracking` (66), `ManyToManyLoad*` (Proxy/NonLoading/Fields, ~120), `TPC/TPT/InheritanceRelationshipsQuery` (~57–59 each), `StoreGenerated` (58), `QueryFilterFuncletization` (28), `MigrationsInfrastructure` (25), `EntitySplittingQuery` (43), `TableSplitting` (24), `KeysWithConverters` (31), `TPTInheritanceQuery` (49), `ComplexJsonStructuralEquality`, `OwnedJsonCollection`, `TPH/TPT inheritance bulk updates`, `StoreValueGeneration`, `TimeOnlyTranslations` (14/17), `DateTimeOffsetTranslations` (20/24) |
| Derived, partially skipped | `ModelBuilding101` (44/124), generic model builder (OneToOne 62/141, OneToMany 52/85, ManyToOne 42/58), `OwnedQuery` (41/103), `AdHocJson` (39/46), `NorthwindSelect` (32/184), `JsonTypes` (31/244), `Migrations` (25 TBD + 12 reasoned + 36 throw-asserting of 127), `NorthwindMiscellaneous` (24/470), `AdHocMiscellaneous` (24/46), `SqlQuery` (22/61), `PrimitiveCollections` (23/158), `FromSql` (17/75), `NorthwindAggregate` (16/211), `NorthwindBulkUpdates` (14+6/94), `OptimisticConcurrency` (~27/31), `CompiledModel` (12/14), `TPC/TPT GearsOfWar` (44–49/593) |
| Run essentially unmodified (results only) | Northwind Where/Include/Join/GroupBy/Navigations/Functions/SetOperations/Keyless/AsNoTracking/ChangeTracking/Compiled/QueryFilters/QueryTagging/Sql, GearsOfWar (TPH), ComplexNavigations*, CompositeKeys*, NullSemantics, Operators, FunkyData, all `Translations/*` (Math, String, Enum, Guid, Bitwise, Arithmetic, Comparison, Logical, Misc), Associations (Navigations, ComplexTableSplitting, OwnedNavigations, OwnedTableSplitting, ComplexJson projections/collections), BuiltInDataTypes, CustomConverters, ConvertToProviderTypes, interception suites, Transaction, Seeding, Spatial, WithConstructors, NotificationEntities, MusicStore, ConferencePlanner, Ef6GroupBy, Warnings, NonSharedModelBulkUpdates, AdHoc{AdvancedMappings,ComplexType,Navigations,QueryFilters,QuerySplitting,ManyToMany}, Scaffolding DatabaseModelFactory |

### Per-class table (generated)

Columns: Skipped TBD / other = `[Conditional*(Skip = …)]` overrides; Divergent = overrides asserting a throw or translation failure;
SQL baselines = `AssertSql`/`AssertBaseline` calls in the file; Assertion style for provider-only classes from counts of
SQL-text inspections vs result assertions in the file.

### (root)

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| AdoNetSubstitutionTests | provider-only | 0 | 0 | 0 | 0 | 0 | 12 | Both |
| AnalyticalTranslationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| ArrayRoundTripTests | provider-only | 0 | 0 | 0 | 0 | 0 | 6 | Both |
| BadDataJsonDeserializationDuckDBTest | BadDataJsonDeserializationTestBase | 5 | 5 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| BinaryJsonCompatibilityTests | provider-only | 0 | 0 | 0 | 0 | 0 | 2 | Results |
| BuiltInDataTypesDuckDBTest | BuiltInDataTypesTestBase | 28 | 0 | 5 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| BulkDeleteBatchingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 9 | Both |
| BulkInsertBatchingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 9 | Both |
| BulkInsertTests | provider-only | 0 | 0 | 0 | 0 | 0 | 5 | Results |
| BulkUpdateBatchingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 10 | Both |
| CaseInsensitiveStringSearchTests | provider-only | 0 | 0 | 0 | 0 | 0 | 6 | Both |
| CharacterConversionCompatibilityTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |
| CommandInterceptionDuckDBTest | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| CommandInterceptionDuckDBTestBase *(abstract)* | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| CommandInterceptionWithDiagnosticsDuckDBTest | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexTypesTrackingDuckDBTest | ComplexTypesTrackingRelationalTestBase | 128 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| CompositeKeyEndToEndDuckDBTest | CompositeKeyEndToEndTestBase | 3 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConcurrencyDetectorDisabledDuckDBTest | ConcurrencyDetectorDisabledRelationalTestBase | 9 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConcurrencyDetectorEnabledDuckDBTest | ConcurrencyDetectorEnabledRelationalTestBase | 9 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConferencePlannerDuckDBTest | ConferencePlannerTestBase | 22 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConnectionInitializationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 7 | Both |
| ConnectionInterceptionDuckDBTest | ConnectionInterceptionTestBase | 9 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConnectionInterceptionDuckDBTestBase *(abstract)* | ConnectionInterceptionTestBase | 9 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConnectionInterceptionWithDiagnosticsDuckDBTest | ConnectionInterceptionTestBase | 9 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ConvertToProviderTypesDuckDBTest | ConvertToProviderTypesTestBase | 30 | 2 | 3 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| CustomConvertersDuckDBTest | CustomConvertersTestBase | 57 | 4 | 3 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DataAnnotationDuckDBTest | DataAnnotationRelationalTestBase | 94 | 13 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DataBindingDuckDBTest | DataBindingTestBase | 38 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DatabaseExistsTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| DateTimeDateTranslationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| DecimalAndDateArithmeticTranslationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 6 | Both |
| DecimalPrecisionTests | provider-only | 0 | 0 | 0 | 0 | 0 | 5 | Results |
| DefaultMappingTest | FieldMappingTestBase | 101 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DesignTimeDuckDBTest | DesignTimeTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DualRolePrincipalUpdateTests | provider-only | 0 | 0 | 0 | 0 | 0 | 13 | Both |
| DuckDBApiConsistencyTest | ApiConsistencyTestBase | 19 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBCommandPlanTests | provider-only | 0 | 0 | 0 | 0 | 0 | 17 | Both |
| DuckDBComplianceTest | RelationalComplianceTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBDatabaseFacadeExtensionsTest | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |
| DuckDBServiceCollectionExtensionsTest | RelationalServiceCollectionExtensionsTestBase | 3 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBStructFieldConventionTest | provider-only | 0 | 0 | 0 | 0 | 0 | 14 | Both |
| DuckLakeAdditionalAttachmentTests | provider-only | 0 | 0 | 0 | 0 | 0 | 5 | SQL text + exceptions |
| DuckLakeCatalogMigrationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |
| DuckLakeCompatibilityTests | provider-only | 0 | 0 | 0 | 0 | 0 | 25 | Both |
| DuckLakeExternalIntegrationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Both |
| DuckLakeMaintenanceTests | provider-only | 0 | 0 | 0 | 0 | 0 | 6 | Results |
| DuckLakeTimeTravelTests | provider-only | 0 | 0 | 0 | 0 | 0 | 3 | Results + exceptions |
| DynamicQueryResultTests | provider-only | 0 | 0 | 0 | 0 | 0 | 9 | Both |
| EncryptedDatabaseTests | provider-only | 0 | 0 | 0 | 0 | 0 | 32 | Both |
| EnforceFieldForQueryTest | FieldMappingTestBase | 101 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| EnforceFieldTest | FieldMappingTestBase | 101 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| EnforcePropertyTest | FieldMappingTestBase | 101 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| EngineCapabilitiesTests | provider-only | 0 | 0 | 0 | 0 | 0 | 24 | Both |
| EntitySplittingDuckDBTest | EntitySplittingTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FieldMappingDuckDBTestBase *(abstract)* | FieldMappingTestBase | 101 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FieldsOnlyLoadDuckDBTest | FieldsOnlyLoadTestBase | 119 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FileSearchPathTests | provider-only | 0 | 0 | 0 | 0 | 0 | 5 | Results |
| FileSourceTests | provider-only | 0 | 0 | 0 | 0 | 0 | 9 | Both |
| FindDuckDBTest *(abstract)* | FindTestBase | 69 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FindDuckDBTestContext | FindTestBase | 69 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FindDuckDBTestNonGeneric | FindTestBase | 69 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FindDuckDBTestSet | FindTestBase | 69 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| JsonTypesDuckDBTest | JsonTypesRelationalTestBase | 244 | 31 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| KeysWithConvertersDuckDBTest | KeysWithConvertersTestBase | 47 | 31 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| LazyLoadProxyDuckDBTest | LazyLoadProxyRelationalTestBase | 75 | 25 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| LoadDuckDBTest | LoadTestBase | 101 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| LoggingDuckDBTest | LoggingRelationalTestBase | 13 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyFieldsLoadDuckDBTest | ManyToManyFieldsLoadTestBase | 22 | 23 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyLoadDuckDBTestBase *(abstract)* | ManyToManyLoadTestBase | 44 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyLoadProxyDuckDBTest | ManyToManyLoadTestBase | 44 | 48 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyTrackingDuckDBTest *(abstract)* | ManyToManyTrackingRelationalTestBase | 67 | 66 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MaterializationInterceptionDuckDBTest | MaterializationInterceptionTestBase | 7 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MemoryLimitTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| MigrationTableRebuildTests | provider-only | 0 | 0 | 0 | 0 | 0 | 5 | SQL text + exceptions |
| MigrationsLockTests | provider-only | 0 | 0 | 0 | 0 | 0 | 7 | Results + exceptions |
| ModelBuilding101DuckDBTest | ModelBuilding101RelationalTestBase | 124 | 44 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MonsterFixupChangedOnlyDuckDBTest *(abstract)* | MonsterFixupTestBase | 12 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MusicStoreDuckDBTest | MusicStoreTestBase | 16 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NativeArrayCompatibilityTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |
| NonLoadingNavigationsManyToManyLoadDuckDBTest | ManyToManyLoadTestBase | 44 | 48 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NotificationEntitiesDuckDBTest | NotificationEntitiesTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NullableRawSqlParameterTests | provider-only | 0 | 0 | 0 | 0 | 0 | 3 | SQL text |
| OptimisticConcurrencyDuckDBTest | OptimisticConcurrencyRelationalTestBase | 31 | 14 | 0 | 0 | 3 | 0 | SQL baselines (few) |
| OptimisticConcurrencyDuckDBTestBase *(abstract)* | OptimisticConcurrencyRelationalTestBase | 31 | 13 | 9 | 0 | 3 | 0 | SQL baselines (few) |
| OptimisticConcurrencyULongDuckDBTest | OptimisticConcurrencyRelationalTestBase | 31 | 1 | 0 | 0 | 3 | 0 | SQL baselines (few) |
| OverzealousInitializationDuckDBTest | OverzealousInitializationTestBase | 1 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedNavigationCompatibilityTests | provider-only | 0 | 0 | 0 | 0 | 0 | 3 | Results |
| ParquetExportTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| ParquetTests | provider-only | 0 | 0 | 0 | 0 | 8 | 6 | Both |
| ProductionWriteDuckDBTest | provider-only | 0 | 0 | 0 | 0 | 0 | 6 | Both + exceptions |
| PropertyValuesDuckDBTest *(abstract)* | PropertyValuesRelationalTestBase | 171 | 167 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ProviderOperationDiagnosticsTest | provider-only | 0 | 0 | 0 | 0 | 0 | 5 | Both |
| QuackProfileTests | provider-only | 0 | 0 | 0 | 0 | 0 | 15 | Both |
| QueryCompilationContextFactoryTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |
| QueryExpressionInterceptionDuckDBTest | QueryExpressionInterceptionTestBase | 4 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| QueryExpressionInterceptionDuckDBTestBase *(abstract)* | QueryExpressionInterceptionTestBase | 4 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| QueryExpressionInterceptionWithDiagnosticsDuckDBTest | QueryExpressionInterceptionTestBase | 4 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ReferencedPrincipalUpdateTests | provider-only | 0 | 0 | 0 | 0 | 0 | 8 | Both |
| SaveChangesInterceptionDuckDBTest | SaveChangesInterceptionTestBase | 6 | 5 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SaveChangesInterceptionDuckDBTestBase *(abstract)* | SaveChangesInterceptionTestBase | 6 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SaveChangesInterceptionWithDiagnosticsDuckDBTest | SaveChangesInterceptionTestBase | 6 | 5 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SeedingDuckDBTest | SeedingTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SerializationDuckDBTest | SerializationTestBase | 1 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SpatialDuckDBTest | SpatialTestBase | 5 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| StoreGeneratedDuckDBTest *(abstract)* | StoreGeneratedTestBase | 60 | 58 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| StoreGeneratedFixupDuckDBTest | StoreGeneratedFixupRelationalTestBase | 118 | 24 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| StoreTypeMappingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 2 | Results |
| StructDesignTimeWorkflowTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | SQL text |
| StructDuckLakeTests | provider-only | 0 | 0 | 0 | 0 | 0 | 2 | Results |
| StructModelValidationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 21 | Results + exceptions |
| StructParquetTests | provider-only | 0 | 0 | 0 | 0 | 0 | 22 | Both |
| StructRawSqlAndViewTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results + exceptions |
| StructRoundTripTests | provider-only | 0 | 0 | 0 | 0 | 0 | 40 | Both |
| StructSchemaPlannerTests | provider-only | 0 | 0 | 0 | 0 | 0 | 16 | Both |
| StructTieredStorageTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |
| TPTTableSplittingDuckDBTest | TPTTableSplittingTestBase | 26 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TableSplittingDuckDBTest | TableSplittingTestBase | 26 | 24 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ThreadsTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| TierControlTests | provider-only | 0 | 0 | 0 | 0 | 0 | 35 | Both |
| TieredStorageFailureTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Results |
| TieredStorageS3Tests | provider-only | 0 | 0 | 0 | 0 | 0 | 12 | Both |
| TieredStorageSafetyTests | provider-only | 0 | 0 | 0 | 0 | 0 | 28 | Both |
| TieredStorageSharedBindingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 9 | Results |
| TieredStorageTests | provider-only | 0 | 0 | 0 | 0 | 0 | 57 | Both |
| TieredViewQueryCompositionTests | provider-only | 0 | 0 | 0 | 0 | 0 | 10 | Both |
| TieredViewRegistrationTests | provider-only | 0 | 0 | 0 | 0 | 0 | 8 | Both |
| TransactionDuckDBTest | TransactionTestBase | 44 | 8 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TransactionInterceptionDuckDBTest | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TransactionInterceptionDuckDBTestBase *(abstract)* | TransactionInterceptionTestBase | 17 | 0 | 3 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TransactionInterceptionWithDiagnosticsDuckDBTest | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TwoDatabasesDuckDBTest | TwoDatabasesTestBase | 3 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| UpsertTests | provider-only | 0 | 0 | 0 | 0 | 0 | 26 | Both |
| ValueConvertersEndToEndDuckDBTest | ValueConvertersEndToEndTestBase | 25 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| WithConstructorsDuckDBTest | WithConstructorsTestBase | 41 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### BulkUpdates

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| NonSharedModelBulkUpdatesDuckDBTest | NonSharedModelBulkUpdatesRelationalTestBase | 17 | 10 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindBulkUpdatesDuckDBTest | NorthwindBulkUpdatesRelationalTestBase | 94 | 14 | 6 | 0 | 4 | 0 | SQL baselines (few) |
| TPCFiltersInheritanceBulkUpdatesDuckDBTest | TPCFiltersInheritanceBulkUpdatesTestBase | 18 | 0 | 2 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCInheritanceBulkUpdatesDuckDBTest | TPCInheritanceBulkUpdatesTestBase | 20 | 1 | 2 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPHFiltersInheritanceBulkUpdatesDuckDBTest | FiltersInheritanceBulkUpdatesRelationalTestBase | 18 | 17 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPHInheritanceBulkUpdatesDuckDBTest | TPHInheritanceBulkUpdatesTestBase | 20 | 19 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTFiltersInheritanceBulkUpdatesDuckDBTest | TPTFiltersInheritanceBulkUpdatesTestBase | 18 | 15 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTInheritanceBulkUpdatesDuckDBTest | TPTInheritanceBulkUpdatesTestBase | 20 | 15 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### GraphUpdates

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| ChangeTracking | ProxyGraphUpdatesTestBase | 69 | 85 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ChangeTrackingAndLazyLoading | ProxyGraphUpdatesTestBase | 69 | 85 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GraphUpdatesDuckDBChangedChangingNotificationsTest | GraphUpdatesTestBase | 53 | 127 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GraphUpdatesDuckDBChangedNotificationsTest | GraphUpdatesTestBase | 53 | 127 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GraphUpdatesDuckDBFullWithOriginalsNotificationsTest | GraphUpdatesTestBase | 53 | 127 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GraphUpdatesDuckDBSnapshotNotificationsTest | GraphUpdatesTestBase | 53 | 127 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GraphUpdatesDuckDBTestBase *(abstract)* | GraphUpdatesTestBase | 53 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| LazyLoading | ProxyGraphUpdatesTestBase | 69 | 85 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ProxyGraphUpdatesDuckDBTestBase *(abstract)* | ProxyGraphUpdatesTestBase | 69 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Migrations

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| DuckDBMigrationBuilderTest | provider-only | 0 | 0 | 0 | 0 | 0 | 2 | Results |
| DuckDBMigrationsSqlGeneratorTest | MigrationsSqlGeneratorTestBase | 49 | 2 | 0 | 2 | 3 | 3 | SQL baselines (few) |
| MigrationsDuckDBTest | MigrationsTestBase | 127 | 25 | 12 | 33 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MigrationsInfrastructureDuckDBTest | MigrationsInfrastructureTestBase | 25 | 25 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### ModelBuilding

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| DuckDBComplexCollection *(abstract)* | RelationalComplexCollectionTestBase | 46 | 0 | 0 | 1 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBComplexType *(abstract)* | RelationalComplexTypeTestBase | 96 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericComplexCollection | RelationalComplexCollectionTestBase | 46 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericComplexType | RelationalComplexTypeTestBase | 96 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericInheritance | RelationalInheritanceTestBase | 30 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericManyToMany | RelationalManyToManyTestBase | 36 | 9 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericManyToOne | RelationalManyToOneTestBase | 58 | 42 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericNonRelationship | RelationalNonRelationshipTestBase | 173 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericOneToMany | RelationalOneToManyTestBase | 85 | 52 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericOneToOne | RelationalOneToOneTestBase | 141 | 62 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBGenericOwnedTypes | RelationalOwnedTypesTestBase | 63 | 12 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBInheritance *(abstract)* | RelationalInheritanceTestBase | 30 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBManyToMany *(abstract)* | RelationalManyToManyTestBase | 36 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBManyToOne *(abstract)* | RelationalManyToOneTestBase | 58 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBNonRelationship *(abstract)* | RelationalNonRelationshipTestBase | 173 | 0 | 0 | 0 | 0 | 11 | results only (spec AssertQuery/assertions) |
| DuckDBOneToMany *(abstract)* | RelationalOneToManyTestBase | 85 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBOneToOne *(abstract)* | RelationalOneToOneTestBase | 141 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBOwnedTypes *(abstract)* | RelationalOwnedTypesTestBase | 63 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| AdHocAdvancedMappingsQueryDuckDBTest | AdHocAdvancedMappingsQueryRelationalTestBase | 19 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocComplexTypeQueryDuckDBTest | AdHocComplexTypeQueryRelationalTestBase | 14 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocJsonQueryDuckDBTest | AdHocJsonQueryRelationalTestBase | 46 | 39 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocManyToManyQueryDuckDBTest | AdHocManyToManyQueryRelationalTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocMiscellaneousQueryDuckDBTest *(abstract)* | AdHocMiscellaneousQueryRelationalTestBase | 46 | 24 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocNavigationsQueryDuckDBTest | AdHocNavigationsQueryRelationalTestBase | 21 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocPrecompiledQueryDuckDBTest | AdHocPrecompiledQueryRelationalTestBase | 7 | 2 | 0 | 0 | 0 | 1 | results only (spec AssertQuery/assertions) |
| AdHocQueryFiltersQueryDuckDBTest | AdHocQueryFiltersQueryRelationalTestBase | 21 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| AdHocQuerySplittingQueryDuckDBTest | AdHocQuerySplittingQueryTestBase | 10 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexNavigationsCollectionsSplitQueryDuckDBTest | spec-derived via local abstract base (chain not resolved) | 0 | 1 | 0 | 0 | 0 | 0 | Results |
| ComplexNavigationsQueryDuckDBTest | ComplexNavigationsQueryRelationalTestBase | 304 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexNavigationsSharedTypeQueryDuckDBTest | spec-derived via local abstract base (chain not resolved) | 0 | 17 | 0 | 0 | 0 | 0 | Results |
| ComplexTypeQueryDuckDBTest | ComplexTypeQueryRelationalTestBase | 74 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| CompositeKeysQueryDuckDBTest | CompositeKeysQueryRelationalTestBase | 7 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| CompositeKeysSplitQueryDuckDBTest | CompositeKeysSplitQueryRelationalTestBase | 7 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| Ef6GroupByDuckDBTest | Ef6GroupByTestBase | 55 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| EntitySplittingQueryDuckDBTest | EntitySplittingQueryTestBase | 58 | 43 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FromSqlQueryDuckDBTest | FromSqlQueryTestBase | 75 | 17 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FunkyDataQueryDuckDBTest | FunkyDataQueryTestBase | 19 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GearsOfWarFromSqlQueryDuckDBTest | GearsOfWarFromSqlQueryTestBase | 1 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GearsOfWarQueryDuckDBTest | GearsOfWarQueryRelationalTestBase | 593 | 10 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| IncludeOneToOneDuckDBTest | IncludeOneToOneTestBase | 12 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| IncompleteMappingInheritanceQueryDuckDBTest | TPHInheritanceQueryTestBase | 55 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| InheritanceQueryDuckDBTest | TPHInheritanceQueryTestBase | 55 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| InheritanceRelationshipsQueryDuckDBTest | InheritanceRelationshipsQueryRelationalTestBase | 70 | 57 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| JsonPathRewritingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 6 | Results |
| JsonQueryDuckDBTest | JsonQueryRelationalTestBase | 215 | 221 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyNoTrackingQueryDuckDBTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 120 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyNoTrackingSplitQueryDuckDBTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 120 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManyQueryDuckDBTest | ManyToManyQueryRelationalTestBase | 123 | 119 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ManyToManySplitQueryDuckDBTest | ManyToManyQueryRelationalTestBase | 123 | 119 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MappingQueryDuckDBTest | MappingQueryTestBase | 4 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NonSharedPrimitiveCollectionsQueryDuckDBTest | NonSharedPrimitiveCollectionsQueryRelationalTestBase | 38 | 9 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindAggregateOperatorsQueryDuckDBTest | NorthwindAggregateOperatorsQueryRelationalTestBase | 211 | 16 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindAsNoTrackingQueryDuckDBTest | NorthwindAsNoTrackingQueryTestBase | 11 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindAsTrackingQueryDuckDBTest | NorthwindAsTrackingQueryTestBase | 5 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindChangeTrackingQueryDuckDBTest | NorthwindChangeTrackingQueryTestBase | 17 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindCompiledQueryDuckDBTest | NorthwindCompiledQueryTestBase | 32 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindDbFunctionsQueryDuckDBTest | NorthwindDbFunctionsQueryRelationalTestBase | 15 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindEFPropertyIncludeQueryDuckDBTest | NorthwindEFPropertyIncludeQueryTestBase | 119 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindFunctionsQueryDuckDBTest | NorthwindFunctionsQueryRelationalTestBase | 10 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindGroupByQueryDuckDBTest | NorthwindGroupByQueryRelationalTestBase | 258 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindIncludeNoTrackingQueryDuckDBTest | NorthwindIncludeNoTrackingQueryTestBase | 118 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindIncludeQueryDuckDBTest | NorthwindIncludeQueryRelationalTestBase | 118 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindJoinQueryDuckDBTest | NorthwindJoinQueryRelationalTestBase | 68 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindKeylessEntitiesQueryDuckDBTest | NorthwindKeylessEntitiesQueryRelationalTestBase | 20 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindMiscellaneousQueryDuckDBTest | NorthwindMiscellaneousQueryRelationalTestBase | 470 | 24 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindNavigationsQueryDuckDBTest | NorthwindNavigationsQueryRelationalTestBase | 73 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindQueryFiltersQueryDuckDBTest | NorthwindQueryFiltersQueryTestBase | 17 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindQueryTaggingQueryDuckDBTest | NorthwindQueryTaggingQueryTestBase | 9 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindSelectQueryDuckDBTest | NorthwindSelectQueryRelationalTestBase | 184 | 32 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindSetOperationsQueryDuckDBTest | NorthwindSetOperationsQueryRelationalTestBase | 91 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindSplitIncludeNoTrackingQueryDuckDBTest | NorthwindSplitIncludeNoTrackingQueryTestBase | 118 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindSplitIncludeQueryDuckDBTest | NorthwindSplitIncludeQueryTestBase | 118 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindSqlQueryDuckDBTest | NorthwindSqlQueryTestBase | 4 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindStringIncludeQueryDuckDBTest | NorthwindStringIncludeQueryTestBase | 119 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NorthwindWhereQueryDuckDBTest | NorthwindWhereQueryRelationalTestBase | 204 | 0 | 0 | 6 | 11 | 0 | SQL baselines (heavy) |
| NullKeysDuckDBTest | NullKeysTestBase | 5 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NullSemanticsQueryDuckDBTest | NullSemanticsQueryTestBase | 168 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OperatorsQueryDuckDbTest | OperatorsQueryTestBase | 10 | 3 | 0 | 0 | 2 | 0 | SQL baselines (few) |
| OptionalDependentQueryDuckDBTest | OptionalDependentQueryTestBase | 10 | 10 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedEntityQueryDuckDBTest | OwnedEntityQueryRelationalTestBase | 23 | 18 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedQueryDuckDBTest | OwnedQueryRelationalTestBase | 103 | 41 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| PrecompiledQueryDuckDBTest | PrecompiledQueryRelationalTestBase | 104 | 1 | 0 | 0 | 0 | 2 | results only (spec AssertQuery/assertions) |
| PrecompiledSqlPregenerationQueryDuckDBTest | PrecompiledSqlPregenerationQueryRelationalTestBase | 14 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| PrimitiveCollectionsQueryDuckDBTest | PrimitiveCollectionsQueryRelationalTestBase | 158 | 23 | 0 | 0 | 0 | 2 | results only (spec AssertQuery/assertions) |
| QueryFilterFuncletizationDuckDBTest | QueryFilterFuncletizationTestBase | 28 | 28 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| QueryNoClientEvalDuckDBTest | QueryNoClientEvalTestBase | 14 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SharedTypeQueryDuckDBTest | SharedTypeQueryRelationalTestBase | 4 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SpatialQueryDuckDBTest | SpatialQueryRelationalTestBase | 84 | 9 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| SqlQueryDuckDBTest | SqlQueryTestBase | 61 | 22 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCFiltersInheritanceQueryDuckDBTest | TPCFiltersInheritanceQueryTestBase | 11 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCGearsOfWarQueryDuckDBTest | TPCGearsOfWarQueryRelationalTestBase | 593 | 44 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCInheritanceQueryDuckDBTest | TPCInheritanceQueryTestBase | 53 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCManyToManyNoTrackingQueryDuckDBTest | TPCManyToManyNoTrackingQueryRelationalTestBase | 124 | 120 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCManyToManyQueryDuckDBTest | TPCManyToManyQueryRelationalTestBase | 123 | 119 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPCRelationshipsQueryDuckDBTest | TPCRelationshipsQueryTestBase | 70 | 57 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPHFiltersInheritanceQueryDuckDBTest | FiltersInheritanceQueryTestBase | 11 | 11 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTFiltersInheritanceQueryDuckDBTest | TPTFiltersInheritanceQueryTestBase | 11 | 11 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTGearsOfWarQueryDuckDBTest | TPTGearsOfWarQueryRelationalTestBase | 593 | 49 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTInheritanceQueryDuckDBTest | TPTInheritanceQueryTestBase | 53 | 49 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTManyToManyNoTrackingQueryDuckDBTest | TPTManyToManyNoTrackingQueryRelationalTestBase | 124 | 120 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTManyToManyQueryDuckDBTest | TPTManyToManyQueryRelationalTestBase | 123 | 119 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TPTRelationshipsQueryDuckDBTest | TPTRelationshipsQueryTestBase | 70 | 59 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ToSqlQueryDuckDBTest | ToSqlQueryTestBase | 1 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| WarningsDuckDBTest | WarningsTestBase | 11 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Associations/ComplexJson

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| ComplexJsonBulkUpdateDuckDBTest | ComplexJsonBulkUpdateRelationalTestBase | 33 | 19 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexJsonCollectionDuckDBTest | ComplexJsonCollectionRelationalTestBase | 14 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexJsonMiscellaneousDuckDBTest | ComplexJsonMiscellaneousRelationalTestBase | 7 | 0 | 1 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexJsonPrimitiveCollectionDuckDBTest | ComplexJsonPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexJsonProjectionDuckDBTest | ComplexJsonProjectionRelationalTestBase | 28 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexJsonSetOperationsDuckDBTest | ComplexJsonSetOperationsRelationalTestBase | 5 | 5 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexJsonStructuralEqualityDuckDBTest | ComplexJsonStructuralEqualityRelationalTestBase | 16 | 16 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Associations/ComplexTableSplitting

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| ComplexTableSplittingBulkUpdateDuckDBTest | ComplexTableSplittingBulkUpdateRelationalTestBase | 33 | 1 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexTableSplittingMiscellaneousDuckDBTest | ComplexTableSplittingMiscellaneousRelationalTestBase | 7 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexTableSplittingPrimitiveCollectionDuckDBTest | ComplexTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComplexTableSplittingProjectionDuckDBTest | ComplexTableSplittingProjectionRelationalTestBase | 28 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Associations/Navigations

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| NavigationsCollectionDuckDBTest | NavigationsCollectionRelationalTestBase | 14 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NavigationsIncludeDuckDBTest | NavigationsIncludeRelationalTestBase | 9 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NavigationsMiscellaneousDuckDBTest | NavigationsMiscellaneousRelationalTestBase | 4 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NavigationsPrimitiveCollectionDuckDBTest | NavigationsPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NavigationsProjectionDuckDBTest | NavigationsProjectionRelationalTestBase | 24 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NavigationsSetOperationsDuckDBTest | NavigationsSetOperationsRelationalTestBase | 5 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NavigationsStructuralEqualityDuckDBTest | NavigationsStructuralEqualityRelationalTestBase | 15 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Associations/OwnedJson

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| OwnedJsonBulkUpdateDuckDBTest | OwnedJsonBulkUpdateRelationalTestBase | 3 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedJsonCollectionDuckDBTest | OwnedJsonCollectionRelationalTestBase | 14 | 14 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedJsonMiscellaneousDuckDBTest | OwnedJsonMiscellaneousRelationalTestBase | 4 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedJsonPrimitiveCollectionDuckDBTest | OwnedJsonPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedJsonProjectionDuckDBTest | OwnedJsonProjectionRelationalTestBase | 24 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedJsonStructuralEqualityDuckDBTest | OwnedJsonStructuralEqualityRelationalTestBase | 15 | 5 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Associations/OwnedNavigations

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| OwnedNavigationsCollectionDuckDBTest | OwnedNavigationsCollectionRelationalTestBase | 14 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedNavigationsMiscellaneousDuckDBTest | OwnedNavigationsMiscellaneousRelationalTestBase | 4 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedNavigationsPrimitiveCollectionDuckDBTest | OwnedNavigationsPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedNavigationsProjectionDuckDBTest | OwnedNavigationsProjectionRelationalTestBase | 24 | 10 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedNavigationsSetOperationsDuckDBTest | OwnedNavigationsSetOperationsRelationalTestBase | 5 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedNavigationsStructuralEqualityDuckDBTest | OwnedNavigationsStructuralEqualityRelationalTestBase | 15 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Associations/OwnedTableSplitting

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| OwnedTableSplittingMiscellaneousDuckDBTest | OwnedTableSplittingMiscellaneousRelationalTestBase | 4 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedTableSplittingPrimitiveCollectionDuckDBTest | OwnedTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedTableSplittingProjectionDuckDBTest | OwnedTableSplittingProjectionRelationalTestBase | 24 | 10 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| OwnedTableSplittingStructuralEqualityDuckDBTest | spec-derived via local abstract base (chain not resolved) | 0 | 3 | 0 | 0 | 0 | 0 | Results |

### Query/Translations

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| ByteArrayTranslationsDuckDBTest | ByteArrayTranslationsTestBase | 7 | 2 | 3 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| EnumTranslationsDuckDBTest | EnumTranslationsTestBase | 18 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GuidTranslationsDuckDBTest | GuidTranslationsTestBase | 4 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MathTranslationsDuckDBTest | MathTranslationsTestBase | 66 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MiscellaneousTranslationsDuckDBTest | MiscellaneousTranslationsRelationalTestBase | 18 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| StringTranslationsDuckDBTest | StringTranslationsRelationalTestBase | 104 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Translations/Operators

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| ArithmeticOperatorTranslationsDuckDBTest | ArithmeticOperatorTranslationsTestBase | 5 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| BitwiseOperatorTranslationsDuckDBTest | BitwiseOperatorTranslationsTestBase | 15 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ComparisonOperatorTranslationsDuckDBTest | ComparisonOperatorTranslationsTestBase | 6 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| LogicalOperatorTranslationsDuckDBTest | LogicalOperatorTranslationsTestBase | 6 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| MiscellaneousOperatorTranslationsDuckDBTest | MiscellaneousOperatorTranslationsTestBase | 2 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Query/Translations/Temporal

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| DateOnlyTranslationsDuckDBTest | DateOnlyTranslationsTestBase | 18 | 6 | 0 | 0 | 3 | 0 | SQL baselines (few) |
| DateTimeOffsetTranslationsDuckDBTest | DateTimeOffsetTranslationsTestBase | 24 | 20 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DateTimeTranslationsDuckDBTest | DateTimeTranslationsTestBase | 19 | 3 | 0 | 0 | 3 | 0 | SQL baselines (few) |
| TimeOnlyTranslationsDuckDBTest | TimeOnlyTranslationsTestBase | 17 | 14 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TimeSpanTranslationsDuckDBTest | TimeSpanTranslationsTestBase | 6 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Scaffolding

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| CompiledModelDuckDBTest | CompiledModelRelationalTestBase | 14 | 12 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBCatalogScaffoldingTests | provider-only | 0 | 0 | 0 | 0 | 0 | 4 | Both |
| DuckDBCodeGeneratorTest | provider-only | 0 | 0 | 0 | 0 | 0 | 2 | Results |

### TestUtilities

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| DuckDBTestStoreForeignKeyTests | provider-only | 0 | 0 | 0 | 0 | 0 | 1 | Results |

### Types

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| BoolTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 1 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ByteArrayTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ByteTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DateOnlyTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 4 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DateTimeOffsetTypeTest | RelationalTypeTestBase | 7 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DateTimeTypeTest | RelationalTypeTestBase | 7 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DecimalTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DoubleTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| FloatTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| GuidTypeTest | RelationalTypeTestBase | 7 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| IntTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| LongTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| ShortTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| StringTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TimeOnlyTypeTest | RelationalTypeTestBase | 7 | 4 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| TimeSpanTypeTest | RelationalTypeTestBase | 7 | 3 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |

### Update

| Class | Spec base (or provider-only) | Inherited tests | Skipped TBD | Skipped other reason | Divergent overrides (throws / translation-failed) | SQL baselines in file | Provider-only tests | Assertion style |
|---|---|---|---|---|---|---|---|---|
| ComplexCollectionJsonUpdateDuckDBTest | ComplexCollectionJsonUpdateTestBase | 20 | 0 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| DuckDBUpdateSqlGeneratorTest | UpdateSqlGeneratorTestBase | 13 | 0 | 0 | 0 | 13 | 0 | SQL baselines (heavy) |
| JsonUpdateDuckDBTest | JsonUpdateTestBase | 136 | 136 | 0 | 2 | 0 | 0 | results only (spec AssertQuery/assertions) |
| NonSharedModelUpdatesDuckDBTest | NonSharedModelUpdatesTestBase | 2 | 2 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| StoreValueGenerationDuckDBTest | StoreValueGenerationTestBase | 19 | 15 | 0 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
| UpdatesDuckDBTest *(abstract)* | UpdatesRelationalTestBase | 29 | 24 | 3 | 0 | 0 | 0 | results only (spec AssertQuery/assertions) |
## 2. Provider-only behaviours

"Asserted by": **SQL** = SQL text/baseline or captured command text; **Results** = data read back / .NET values / model metadata;
**Both**; **Exception** = asserts a throw (type and/or message). Counts are tests.

### 2.1 Sequence keys / value generation / RETURNING

| Behaviour (database-neutral) | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Opt-in auto-increment on an integer PK creates a sequence `<table>_<col>_seq` and a `DEFAULT nextval(...)` column (CREATE TABLE and ADD COLUMN) | sequence-backed identity | `UseAutoIncrement()`, `DuckDBValueGenerationStrategy.AutoIncrement` | 2 (MigrationsSqlGenerator) | SQL |
| Inline FOREIGN KEY emitted in CREATE TABLE; unsupported cascade action normalised to NO ACTION | FK in DDL | migrations SQL generator | 1 | SQL |
| Strategy only applies to single-column integer non-FK PKs without default/default-SQL/computed SQL; generic and non-generic builders | value-generation convention | `UseAutoIncrement`, `GetValueGenerationStrategy` | 11 (ModelBuilder) | Results (metadata) |
| Next sequence value statement is `SELECT nextval('"schema"."seq"')`, identifier escaped | sequence SQL | `GenerateNextSequenceValueOperation` | 2 (UpdateSqlGenerator) | SQL |
| Auto-increment on a non-integer property rejected at model validation; generated keys returned via `RETURNING` | validation, store-generated keys | `UseAutoIncrement` | 3 (ProductionWrite) | Both / Exception |
| Update/delete matching no rows raises `DbUpdateConcurrencyException`; `EXISTS` check never creates a missing DB file | concurrency, database creator | – | 3 (ProductionWrite) | Exception / Results |
| Capability service decides: sequences, RETURNING, batching, upsert strategy, migrations, generated/default columns, index/constraint filtering, rebuilds — native vs DuckLake profiles differ; batching without RETURNING rejects store-generated values | engine-capability matrix | internal capability service, `UseDuckLake` | 24 (EngineCapabilities) | Results / Exception |
| Updating a row that is referenced by FKs: scalar update succeeds; store-generated values re-read with a keyed SELECT instead of `UPDATE … RETURNING`; RETURNING used when capability allows; read-back failure rolls back; concurrency exceptions preserved; batching isolates read-back | FK-referenced principal update workaround | SaveChanges pipeline | 8 (ReferencedPrincipalUpdate) | Both |
| "Dual-role" row (referenced + own outbound FK): unchanged outbound FK writes omitted; sole unchanged FK write made conditional (`IS DISTINCT FROM`) with concurrency probe; genuine FK change with a child reported atomically as `DbUpdateException` naming table/column; custom comparers can't hide a change; DuckLake disables this planning | engine FK-update limitation workaround | SaveChanges pipeline | 13 (DualRolePrincipalUpdate) | Both / Exception |

### 2.2 SaveChanges batching / bulk / appender / upsert

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Opt-in multi-row INSERT batching persists all rows, correlates store-generated keys, splits by write shape and by a cell limit (logged), off by default | insert batching | `EnableBulkInsertBatching()` | 9 | Both |
| Opt-in UPDATE batching (`UPDATE … FROM (VALUES …)`): per-row distinct values, only targeted rows, composite keys, mixed insert+update, incompatible column shapes split; off by default | update batching | `EnableBulkUpdateBatching()` | 10 | Both |
| Opt-in DELETE batching: only targeted rows, composite keys, orphan cleanup, concurrency checks preserved; planner rejects empty runs; off by default | delete batching | `EnableBulkDeleteBatching()` | 9 | Both |
| Appender bulk insert (sync/async) writes all rows incl. nulls, value converters, field-only converter fallback, configured field access; bypasses tracking | appender bulk load | `BulkInsert`, `BulkInsertAsync` | 5 (+ DuckLake/Quack variants) | Results |
| Bulk insert / upsert of entities with STRUCT complex properties throws | limitation | `BulkInsert`, `Upsert` | 2 | Exception |
| Upsert: insert new + update existing; distinct values per row; duplicate conflict keys within input → last occurrence wins (key-only upsert keeps first / does nothing on conflict); composite keys; conflict on alternate key or composite unique index; client-generated OnAdd keys kept; non-unique conflict target rejected | upsert (ON CONFLICT) | `Upsert`, `UpsertAsync` | 14 | Both / Exception |
| Upsert batching: width-aware staged-cell budget, explicit limits, default batch request, capacity from known input size; unknown input modes rejected; "distinct input" contract skips staging de-dup | upsert batching | `DuckDBUpsertBatching`, input-mode option | 6 | Results / Exception |
| Logical-key MERGE when target has no unique constraint: inserts/updates; fails *before mutation* if a key matches several rows (incl. composites); SQL NULL never matches; generated columns omitted; physically unique target skips cardinality check | MERGE-based upsert | `UpsertAsync` (LogicalKeyMerge strategy) | 6 | Both / Exception |

### 2.3 Lists / arrays (native LIST)

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| `List<T>`/arrays round-trip incl. empty, large, nullable elements, converted elements; read-only lists bind without copy | native array columns | type mapping `T[]` | 7 | Results |
| Primitive collection operators translate to native list functions: `array_length`, `array_contains`, slicing `[2:]`/`[:2]`, `list_distinct`, `[i]` 1-based indexing, `unnest` with `CROSS JOIN LATERAL`, `VALUES` for parameter collections | primitive collections | query translation | 48 baselines (spec overrides) | SQL (+ spec results) |
| `Append`/`Prepend` on a column collection translate | list concat | query translation | 2 | Results |
| Faceted/fixed-size array store types report element mapping | store-type mapping | `GetDuckDBStoreTypeMapping` | 4 | Results |

### 2.4 STRUCT mapping (complex properties → one STRUCT column)

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Complex property opt-in maps to a single physical STRUCT column; insert/read back mixed, JSON, converted leaves; nested structs; multiple struct columns; quoted field names; explicit field naming | struct columns | `UseStructMapping()`, `HasStructField`, `HasStructFieldName` | ~25 (StructRoundTrip) | Both |
| Sub-field projection, filter, order-by, sub-query, join, set operation, compiled query, split/single include | struct field access | query translation | ~10 | Both |
| Null root vs present-all-null root distinguished on insert, read, bulk update; partial leaf update preserves siblings | struct null semantics | SaveChanges | 6 | Results |
| Convention infers lower-case field names only when marked; explicit names win; unique EF column names for same leaf name; deep subqueries | struct field convention | `DuckDBStructFieldConvention` | 14 | Results (metadata/expression) |
| Model validation rejects struct concurrency tokens, defaults, physical column collisions (case-insensitive, prefix, overrides), views with struct, tiered roots with struct, generated column in DuckLake struct; allows optional root | validation | `UseStructMapping` | 21 + 4 | Exception |
| Logical FK through a struct field (one-to-one/one-to-many, requiredness inferred from leaf nullability, overridable); no physical FK DDL; rebuild omits it | struct foreign keys | `HasStructForeignKey` | ~12 | Results |
| Migrations/snapshots/compiled models render the struct fluent API; struct schema planner orders nested required fields, rejects defaults/collisions | design-time support | struct schema planner | 16 + 1 | Both |
| Struct reads from Parquet: sub-field projection pushes only the selected field, filter/order, joins via struct FKs, whole-struct projection with converters, sparse/nullable/nested structs | struct + Parquet | `FromParquet` + struct | 22 | Both |
| Raw SQL with struct column materialises sub-fields | raw SQL | `FromSqlRaw` | 4 | Results |

### 2.5 JSON

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| JSON collection paths: `json_each` rewrite keeps preceding path segments, parameterised nested index executes, nested count executes, configured JSON property names escaped | JSON path translation | query translation | 6 | Both |
| Unsupported owned-JSON partial update leaves document intact; binary values in complex JSON keep null/empty/updated | JSON update | SaveChanges | 2 | Results |
| ulong enum collection in JSON stores 18446744073709551615 (UBIGINT) | JSON numeric range | type mapping | 1 (spec override) | Results |

### 2.6 Parquet / CSV / JSON files, export

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Entity mapped to a Parquet file is queried through `read_parquet` for simple, where, join, include, relationship joins; path from context configuration | file-backed entities | `[FromParquet]`/`FromParquet` | 6 | Both |
| CSV and JSON-file entities via `read_csv`/`read_json`; path rendered as literal; custom function safely delimited; shared-table file-source conflicts rejected | file sources | `FromCsv`, `FromJsonFile` | 9 (+1 precompiled) | Both / Exception |
| Export a translated LINQ query to Parquet with parameters and typed partition columns; rejects queries from another context and struct partition keys | COPY TO Parquet | `ExportToParquetAsync` | 4 | Results / Exception |

### 2.7 Tiered storage (hot table + cold Parquet archive)

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Archive whole aggregate (root + children) older than a cutoff to partitioned Parquet; tiered union view equals full history; idempotent reruns; crash between copy and delete self-heals; late hot rows stay visible | hot/cold archive | `ToTieredStore`, `PartitionBy`, `WithTieredView`, `WithReadModel`, `ArchiveTierAsync` | ~57 (TieredStorage) | Both |
| View/copy SQL generation: watermark filter, Hive partition casts, partition order/transform/alias, child semijoins, recursive glob, manifest probes, credential-free paths | tier SQL | internal SQL builders | 35 (TierControl) | SQL |
| Safety: corrected/reopened archived keys rejected, approved corrections publish new generation, tombstones cascade, restore moves graph back, match-key uniqueness required, redaction of URI credentials | tier integrity | `ReconcileArchiveTierAsync`, `RestoreArchiveTierAsync`, `PreflightTieredStorageAsync` | 28 | Both / Exception |
| Retention trim / recovery / bootstrap / contract rewrite plans and publishes immutable generations; rejects drifted plans | retention | `PlanArchiveRetentionAsync`, `PublishArchiveRetentionAsync`, `PlanArchiveRecoveryAsync`, `BootstrapArchiveTierAsync` | (in above) | Both |
| Shared child bindings are root-scoped and order-independent; ambiguous children fail before writes | multi-root aggregates | `ToTieredStore` | 9 | Results / Exception |
| LINQ over tiered views prunes Parquet files (EXPLAIN ANALYZE), ordering/keyset/grouping/joins stay server-side | tiered query | `ToTieredView` | 10 + 8 | Both |
| Failure injection at each stage recovers; S3/MinIO/GCS/Azure/AWS round-trips | resilience, object storage | `IDuckDBTierFailureInjector` (test), `LoadExtension("httpfs")` | 4 + 12 | Results |

### 2.8 DuckLake (attached lakehouse catalog) and time travel

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| EF create/query/insert/update/delete and affected-row concurrency against a DuckLake catalog; multi-command SaveChanges is one transaction; duplicate logical-key matches roll back | lakehouse backend | `UseDuckLake` | ~12 | Both |
| Connection initialisation: attach, `USE`, re-initialise already-open connections, named secrets before attach, read-only reopen, data-path override, automatic catalog migration (v0.3 file embedded) | backend profile | `UseDuckLake`, `UseNamedSecret`, `AutomaticMigration`, `DataPath` | ~8 + 1 | Both |
| Unsupported features fail fast: migrations, `EnsureDeleted`, auto-increment, value-generated-on-update, SaveChanges batching, mixing with native/encrypted | capability guard | `UseDuckLake` | ~5 | Exception |
| Appender bulk insert and MERGE upsert (key-only keeps first duplicate, alternate keys, converters, explicit transactions) | bulk on DuckLake | `BulkInsert`, `UpsertAsync` | ~6 | Results |
| Additional catalogs usable from catalog-qualified dynamic SQL; quoting; alias conflicts rejected | multi-catalog | `AlsoAttach`, `AlsoAttachNamedSecret` | 5 | Both / Exception |
| Snapshots, expiry, cleanup (dry-run default), commit message on explicit transaction, read-only rejects mutation | lake maintenance | `GetSnapshotsAsync`, `ExpireSnapshotsAsync`, `SetCommitMessageAsync`, … | 6 | Results / Exception |
| LINQ over a historical snapshot / timestamp; historical attach is read-only | time travel | `AsOfSnapshot`, `AsOfTimestamp` | 3 | Results / Exception |
| PostgreSQL metadata + MinIO data round trip (external) | remote catalog | `UseDuckLake` + secret | 1 (gated) | Results |

### 2.9 Remote server (Quack) and ADO.NET substitution

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Quack profile: EF commands, transactions, bulk, upsert round-trip over remote protocol (gated); secret never logged; validation before opening; deletion refused | remote DuckDB | `UseQuack` | 15 (1 gated) | Both / Exception |
| Client-side parameter inlining preserves quoted/dollar-quoted/escape strings and casts, invariant temporal literals, rejects output params/non-text commands | literal parameter expansion | Quack command | 6 | SQL / Exception |
| A registered `DbProviderFactory` (fake ADO.NET connection) can replace the native connection end to end: parameter names normalised, array params, temporal reader values; appender/archive operations refuse substituted connections | provider-factory substitution | connection factory | 12 | Both / Exception |

### 2.10 Connection / engine settings / encryption

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Post-open initializer runs (secrets etc.), extensions installed/loaded or caller-managed, settings composed into one command, failed init closes connection, checkpoint threshold applied | connection initialisation | `ConfigureConnection`, `LoadExtension`, `CheckpointThreshold` | 7 | Both / Exception |
| `memory_limit`, `threads`, `file_search_path` applied on open (incl. read-only connections); validation | engine settings | `MemoryLimit`, `Threads`, `FileSearchPath` | 13 | Results / Exception |
| Encrypted database file: no plaintext in file, wrong/empty key fails, key resolved per attach and never stored or logged (redaction), alias rules, temp-file encryption default, migrations history inside encrypted catalog, not combinable with DuckLake | encryption at rest | `UseEncryptedDuckDB`, `UseEncryptedDatabase` | 32 | Both / Exception |
| `Database.Exists` false for absent file, true for present / unopenable / in-memory | database creator | – | 4 | Results |
| Migrations lock acquire/release/timeout with actionable message; timeout option validation | migration locking | `MigrationLockTimeout` | 7 | Results / Exception |
| `IsDuckDB()` on facade and migration builder; scaffolding `UseDuckDB` code generation | provider identity | `IsDuckDB` | 5 | Results |

### 2.11 Query extras, command plans, dynamic results

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| `SplitPart` (1-based), sample standard deviation, `ArgMax`/`ArgMin` over tuples incl. filtered groups | analytic functions | `EF.Functions.SplitPart/StandardDeviationSample/ArgMax/ArgMin` | 4 | Results |
| Opt-in case-insensitive `Contains/StartsWith/EndsWith` (ILIKE with escaped wildcards), default stays case-sensitive; part of options; DuckLake too | case-insensitive search | `UseCaseInsensitiveStringSearches()` | 6 | Both |
| `DateTime.Date` projection/group-by/filter; decimal division precision; fractional `AddDays` on DateTime/DateTimeOffset; DateOnly add methods; numeric casts with char mappings | temporal & numeric translation correctness | query translation | 15 | Results (+SQL) |
| Decimal default/custom/high precision round-trips; store type reflects precision | decimal mapping | type mapping | 5 | Results |
| Capture the exact SQL + parameters of a query or terminal aggregate (Count/Any/Sum/Average…) without opening the connection; replay as dynamic command; rejects split queries/other context | command plans | `GetDuckDBCommandPlan`, `SqlQueryDynamicCommandAsync` | 17 | Both / Exception |
| Stream unknown-shape results with runtime column metadata; named parameters; blobs row-owned; single enumeration | dynamic result sets | `SqlQueryDynamicRawAsync`, `SqlQueryDynamicAsync` | 9 | Results / Exception |
| `ExecuteSqlInterpolated` binds nulls in all positions; explicit parameter names normalised | raw SQL params | – | 3 | Results |
| FromSql with arrays/nulls re-binds in precompiled queries; precompiled quoting preserves file-source metadata | precompiled queries | – | 3 | Results |
| Owned references from separately mapped views require explicit loading; optional owned in TPH table | owned loading | `DuckDBOwnedValueConvention` | 3 | Results |
| Structured diagnostic events for raw/tiered operations and extension load failures (LogTo + DiagnosticSource) | diagnostics | `DuckDBEventId` | 5 | Results |
| Catalog-scoped reverse engineering, DuckLake keyless scaffolding, factory-based scaffolding | scaffolding | `DuckDBDatabaseModelFactory` | 4 + 2 | Results / Exception |

### 2.12 Migrations table rebuilds

| Behaviour | Concept | Provider API | Tests | Asserted by |
|---|---|---|---|---|
| Constraint ALTER fails fast with a message naming the opt-in; when enabled, FK change rebuilds the table (copy/swap) and the FK is enforced afterwards; mixed column-shape changes rejected; final indexes emitted once | table rebuild | `EnableMigrationTableRebuilds()` | 5 (+1 SqlGenerator override) | Both / Exception |

## 3. Divergences from the shared spec

### 3.1 Reasoned skips and different outcomes

| Area | Spec tests | DuckDB expectation | Reason / capability gap | Asserted by |
|---|---|---|---|---|
| Migrations: add check / unique constraint, alter/drop check, drop PK / PK column, rename table (incl. with PK / JSON column) | 12 | throws `DuckDBException` "Not implemented Error: No support for that ALTER TABLE option yet!" | engine ALTER TABLE surface | Exception/message |
| Migrations: add column with default / default SQL / required / check / JSON column / required primitive collection (8 variants), make non-computed | 15 | throws "Parser Error: Adding columns with constraints not yet supported" | engine | Exception/message |
| Migrations: add/alter computed column, make computed | 4 | throws "Adding generated columns after table creation is not supported yet" | engine (also no STORED generated columns) | Exception/message |
| Migrations: move table / sequence to another schema; rename sequence | 3 | throws "T_AlterObjectSchemaStmt" / "Schema element not supported yet!" | engine | Exception/message |
| Migrations: add/drop FK | 3 | skipped (`NotSupportedByDuckDB`); SQL generator throws `NotSupportedException` pointing at `EnableMigrationTableRebuilds` | engine; provider opt-in rebuild | Exception |
| Migrations: descending index, mixed sort, alter sort order | 3 | skipped (`IndexDirectionNotRetained`) | DESC discarded | – |
| Migrations: filtered (partial) indexes | 2 | skipped | engine | – |
| Migrations: rename index | 1 | skipped | engine | – |
| Migrations: owned↔JSON conversion, computed column in CREATE TABLE | 3 | skipped (`NotYetImplemented`) | provider roadmap | – |
| Migrations: add primary key | 3 | PK name read back as `<table>_<col>_pkey` (`People_somefield_pkey`), not the EF name | engine does not keep constraint names | Results |
| MigrationsSqlGenerator overrides (AddColumn facets, Rename, InsertData/UpdateData/DeleteData, DefaultValue line breaks, sequence restart) | ~30 | run base with **no SQL baseline** | baselines not written | – |
| Bulk ExecuteUpdate/ExecuteDelete touching FK-referenced rows (`Update_Where_*set_null`, `multiple_set`, join set, RightJoin; TPC `Delete_where_using_hierarchy*`); `Save_replaced_principal`, `SaveChanges_processes_all_tracked_entities` | 13 | skipped (`ReferencedRowsCannotBeUpdated`) | engine: referenced row cannot be updated/deleted while referenced | – |
| Savepoints (create/rollback/release interception) | 3 | skipped | no savepoints | – |
| DateTimeOffset round-trip with non-zero offset (BuiltInDataTypes, CustomConverters, ConvertToProviderTypes) | 11 | skipped | TIMESTAMPTZ normalises offset | – |
| `byte[].Contains` (column/constant/parameter) | 3 | skipped | DuckDB blob search (duckdb discussion 7071) | – |
| Optimistic-concurrency conflict resolution (store/client/new values, Reload, accept-changes, update-then-delete, independent associations, two-issue graphs) | 9 | skipped "Optimistic Offline Lock #2195" | provider issue | – |
| ComplexJson `FromSql_on_root` (EF11 only) | 1 | skipped | EF upstream #34627 | – |
| JSON update: set nullable DateTime/DateOnly inside JSON to null | 2 | throws `InvalidOperationException` ("partial … update") | JSON partial update not supported for those | Exception |
| `ExecuteUpdate` setting JSON property to a non-JSON column (type tests ×5) | 5 | throws `ExecuteUpdateCannotSetJsonPropertyToNonJsonColumn` | relational limit surfaced | Exception/message |
| `Where_compare_constructed_*`, `Where_compare_tuple_create_constructed_*` | 6 | translation failed | anonymous types / `Tuple.Create` not compared | Exception + SQL (empty) |
| `Where_compare_tuple_constructed_*` | 3 | translates to row value `(a, b) = ('x', 'y')`; `<>` expanded to OR chain | row values | SQL |
| `Like_literal` | 1 | expected rows = case-*sensitive* `Contains("M")` | LIKE is case-sensitive | Results |
| FromSql with `DbParameter` | 2 | placeholders `$name` | DuckDB parameter syntax | Results |
| `Identifiers_are_generated_correctly` | 1 | long names **not truncated** (no identifier limit) | engine | Results |
| `DateTime.Parse` tests | 2 | forced en-US culture | test harness | Results |
| `Join_local_bytes_closure_is_cached_correctly`, `Average_over_nested_subquery` | 2 | replaced bodies (VALUES parameterisation; Average via helper) | precision/shape | Results |
| `Equality_in_query` (bool) | 1 | calls parameter variant on EF11 | EF version | Results |
| OptimisticConcurrency `Property_entry_original_value_is_set` | 1 | UPDATE … `RETURNING 1` baseline | RETURNING used for row count | SQL |
| Transaction fixture reseed | – | delete+seed rather than truncate/reseed identity | no identity reseed | – |
| Spatial fixture | – | collapses geometry subtypes (POINTZ etc.) to single GEOMETRY | single geometry type | – |
| Northwind fixture | – | `OrderDetail` mapped to table `OrderDetails` | quoting/space in name | – |

### 3.2 "TBD" skips (≈4,000; unclassified backlog)

| Cluster | Approx skipped | Likely capability behind it |
|---|---|---|
| JSON query & update suites (`JsonQuery`, `JsonUpdate`, `AdHocJson`, OwnedJson/ComplexJson structural equality & collection & bulk update, `JsonTypes` 31) | ~520 | owned/complex JSON mapping only partially implemented |
| Change tracking graph suites (`GraphUpdates`×4, `ProxyGraphUpdates`×3, `PropertyValues`, `ManyToManyTracking`, `StoreGenerated`, `StoreGeneratedFixup`, `KeysWithConverters`, `TableSplitting`) | ~1,250 | FK-referenced row update limitation + cyclic FK graphs (spec stores built without FKs) |
| Many-to-many query/load suites (8 query variants + 3 load) | ~1,100 | skip navigations / join-entity queries (TBD) |
| Inheritance relationship suites (TPC/TPT relationships, InheritanceRelationships, TPT inheritance, TPC/TPT GearsOfWar, TPH/TPT bulk updates) | ~420 | TPT/TPC joins & updates |
| Model building (`ModelBuilding101`, generic OneToOne/OneToMany/ManyToOne/Owned/ManyToMany) | ~220 | model builder expectations on relational naming |
| Northwind query partial skips (Select, Misc, Aggregate, BulkUpdates, FromSql, SqlQuery) | ~150 | correlated collections after Distinct/Take, correlated LIMIT/OFFSET, string aggregates |
| Temporal translations (DateTimeOffset 20, TimeOnly 14, DateOnly few) | ~40 | offset/time arithmetic functions |
| Migrations (25), MigrationsInfrastructure (25), CompiledModel (12) | ~60 | JSON-column migrations, sequence alters, multiline comments, SqlOperation |
| QueryFilterFuncletization (28), EntitySplittingQuery (43), OwnedQuery (41), OwnedEntityQuery (18) | ~130 | various |

## 4. Conditions and flags

| Gate | Mechanism | What it gates |
|---|---|---|
| `DuckDBSkipReasons.*` constants | `Skip =` on overrides | all spec skips (Tbd, NotSupportedByDuckDB, IndexDirectionNotRetained, PartialIndexesNotSupported, RenameIndexNotSupported, ReferencedRowsCannotBeUpdated, NotYetImplemented, Investigating) |
| `DUCKLAKE_EXTERNAL_TESTS=1` (after `scripts/test-ducklake-external.sh`, compose file with PostgreSQL + MinIO) | `[DuckLakeExternalFact]` | DuckLake with PostgreSQL metadata and MinIO data (1 test). Other DuckLake tests use local file metadata and run always (need `ducklake` extension install) |
| `DUCKDB_S3_TEST_ENDPOINT` + `DUCKDB_S3_TEST_BUCKET` (+`_KEY/_SECRET/_SSL/_PREFIX/_REGION`) | `[MinIoFact]` | tiered storage on S3-compatible object storage (8 tests) |
| `DUCKDB_AWS_S3_TEST_BUCKET` (+ key/secret/session token/region/prefix) | `[RealAwsFact]` | real AWS S3 archive matrix (1) |
| `DUCKDB_GCS_TEST_BUCKET/_KEY/_SECRET` | `[RealGcsFact]` | GCS archive matrix (1) |
| `DUCKDB_AZURE_TEST_CONNECTION_STRING` + `_CONTAINER` | `[RealAzureFact]` | Azure archive matrix (1) |
| `DUCKDB_QUACK_EXTENSION_PATH` (+ `DUCKDB_QUACK_TEST_PORT`, default 19494) | `[QuackIntegrationFact]` | remote Quack round trip (1); other 14 Quack tests run offline |
| `#if NET11_0_OR_GREATER` / `!NET11_0_OR_GREATER` (73 blocks) | compile-time | EF11 vs EF10 spec differences (e.g. `Equality_in_query_with_parameter`, strings-contains-null skip, ComplexJson FromSql skip) |
| `OperatingSystem.IsWindows()` | runtime branch | encrypted DB symlink test |
| `xunit.runner.json` `parallelizeTestCollections=false` | runner config | shared spec stores collide under DuckDB MVCC ("Conflict on update!") |

## 5. Test infrastructure

| Piece | Role | DuckDB-specific? |
|---|---|---|
| `DuckDBTestStore` (`RelationalTestStore`) | file DB `<name>.db`, `"` delimiters, `ExecuteNonQuery/Scalar` with `DuckDBParameter("p0"…)`, optional `INSTALL/LOAD spatial` on open | yes (file per store, extension loading) |
| `DuckDBTestStoreFactory` / `DuckDBNorthwindTestStoreFactory` / `SpatialDuckDBTestStoreFactory` | store creation; Northwind uses a shared store named `northwind` seeded by EF fixture (no SQL script) | partly |
| `DuckDBTestMigrationsSqlGenerator` | spec stores created **without physical FKs** (spec models have cyclic FK graphs DuckDB can't create); production generator untouched (`DuckDBTestStoreForeignKeyTests` guards this) | yes — key porting caveat |
| `DuckDBDatabaseCleaner` | drops tables in FK-dependency order instead of dropping FKs first (engine can't drop FKs) | yes |
| `DuckDBTestHelpers` | in-memory `:memory:` connection, `DuckDBLoggingDefinitions` | standard |
| `DuckDBTestBase` | per-test temp file DB `ddbtest_<guid>.db`, `FileOptions<TContext>(configure)` helper, deletes file | generic pattern |
| `CapturingFakeDbConnection` / `CapturingProviderFactory` | **pengdows.crud.fakeDb** `fakeDbConnection` (EmulatedProduct=Sqlite) capturing commands/parameters for substitution tests | no (reusable) |
| `TieredStorageTestHelpers.Explain` | `EXPLAIN ANALYZE` to assert file pruning | yes |
| Embedded `TestData/DuckLake/v03.db.gz.b64` | legacy DuckLake catalog for automatic-migration test | yes |
| Compose files `ducklake-external.compose.yml`, `tiered-storage-s3.compose.yml` (+ request proxy) | PostgreSQL + MinIO for gated tests | yes |
| Spec fixtures overridden | Northwind (`OrderDetails` table), Spatial (single GEOMETRY), BasicTypes, JSON, ComplexJson/OwnedJson fixtures, GearsOfWar TPC/TPT, F1 (`ulong?` row version variant) | thin overrides |
| `ModelCacheKeyFactory` replacements, `IDuckDBTierFailureInjector` | per-test models; fault injection for archive stages | yes |
