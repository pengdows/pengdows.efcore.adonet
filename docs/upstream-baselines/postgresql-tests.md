# Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3: functional test suite as a behaviour record

Source: `test/EFCore.PG.FunctionalTests` at tag v10.0.3 (339 .cs files). This is a static read; nothing was built or run. Concept names follow `api-inventory/postgresql.md`.
Inherited test counts come from the EF Core 10.0.11 specification assemblies (`tools/specount/spec-10.0.11.tsv`), and each count is the union of [Fact]/[Theory] methods along the base chain. They count methods, not theory cases. Most query tests are `[ConditionalTheory, MemberData(IsAsyncData)]`, so each one runs twice.
"Provider-added" excludes `Check_all_tests_overridden`. That is a guard test in 80 classes that fails when a base test lacks an SQL-baseline override.

## 1. Suite map

### Totals

| Measure | Value |
|---|---|
| Concrete spec-derived test classes | 242. Interception suites run twice (plain and WithDiagnostics). ModelBuilding has 9 nested generic classes |
| Inherited spec test methods (sum over concrete classes) | **13,166** |
| Provider-added test methods inside spec-derived classes | **257** |
| Provider-only test classes | **44**. 26 derive from nothing in the spec; 18 use only the `QueryTestBase<TFixture>`/`SharedStoreFixtureBase` infrastructure with no inherited tests |
| Provider-only test methods | **881** |
| Grand total test methods | about **14,300**, or about 22,000 cases once async/sync theories are expanded |
| Inherited tests in classes with SQL-heavy assertions (AssertSql in at least 60% of overrides) | 5,798 tests in 103 classes |
| Inherited tests in classes that assert SQL partially | 551 tests in 16 classes |
| Inherited tests in classes that assert results only (no AssertSql in the PG file) | 6,817 tests in 123 classes |
| Provider-only tests that assert SQL | Nearly all query-translation suites. Arrays, JSON, FTS, ranges, network, cube, ltree and NodaTime have about one AssertSql per test |
| Provider-only tests that assert results or behaviour only | Infrastructure suites: ConnectionSpecification, DatabaseCreator, ExecutionStrategy, ValueGenerationScenarios, Sequence, Batching, Character, Navigation, Scaffolding (database-model asserts), Fuzzy/Trigrams (mostly), LegacyNodaTime mapping |

**Pattern.** SQL baselines are asserted heavily in these suites: Northwind GroupBy, Json, ComplexType, PrimitiveCollections, Precompiled, BulkUpdates, Associations/*, Translations/* (spec), Types/*, Migrations, and SqlQuery/Udf. The rest of the spec suites have no AssertSql in the PG file and compare results with LINQ-to-objects only. Those suites include ComplexNavigations*, GearsOfWar/TPC/TPT (only a few overrides), ManyToMany*, Include*, Inheritance*, Northwind Select/Where/Misc/Aggregate (mostly), Owned*, FieldsOnlyLoad and the change-tracking and update suites. (Base classes that call AssertSql with no baseline override would fail `Check_all_tests_overridden`, so a PG file with zero AssertSql means the suite checks results only.)

### Per-folder tables

"Overridden" counts methods that override a base test, whether to supply a baseline or to diverge.

### (root)

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| BadDataJsonDeserializationNpgsqlTest | BadDataJsonDeserializationTestBase | 5 | 0 | 0 | 0 | results only |
| BatchingTest | provider-only | 0 | 0 | 4 | 0 | results only |
| BuiltInDataTypesNpgsqlTest | BuiltInDataTypesTestBase | 28 | 7 | 9 | 4 | SQL partial |
| CommandInterceptionNpgsqlTest | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | results only |
| CommandInterceptionWithDiagnosticsNpgsqlTest | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | results only |
| ComplexTypesTrackingNpgsqlTest | ComplexTypesTrackingRelationalTestBase | 128 | 1 | 0 | 0 | results only |
| CompositeKeyEndToEndNpgsqlTest | CompositeKeyEndToEndTestBase | 3 | 0 | 0 | 0 | results only |
| ComputedColumnTest | provider-only | 0 | 0 | 3 | 0 | results only |
| ConcurrencyDetectorDisabledNpgsqlTest | ConcurrencyDetectorDisabledRelationalTestBase | 9 | 1 | 0 | 0 | results only |
| ConcurrencyDetectorEnabledNpgsqlTest | ConcurrencyDetectorEnabledRelationalTestBase | 9 | 1 | 0 | 0 | results only |
| ConferencePlannerNpgsqlTest | ConferencePlannerTestBase | 22 | 1 | 0 | 0 | results only |
| ConnectionInterceptionNpgsqlTest | ConnectionInterceptionTestBase | 9 | 4 | 0 | 0 | results only |
| ConnectionInterceptionWithDiagnosticsNpgsqlTest | ConnectionInterceptionTestBase | 9 | 0 | 0 | 0 | results only |
| ConnectionSpecificationTest | provider-only | 0 | 0 | 13 | 0 | results only |
| ConvertToProviderTypesNpgsqlTest | ConvertToProviderTypesTestBase | 30 | 3 | 0 | 0 | results only |
| CustomConvertersNpgsqlTest | CustomConvertersTestBase | 57 | 6 | 0 | 0 | results only |
| DataAnnotationNpgsqlTest | DataAnnotationRelationalTestBase | 97 | 3 | 0 | 0 | results only |
| DataBindingNpgsqlTest | DataBindingTestBase | 38 | 0 | 0 | 0 | results only |
| DefaultValuesTest | provider-only | 0 | 0 | 1 | 0 | results only |
| DesignTimeNpgsqlTest | DesignTimeTestBase | 2 | 0 | 0 | 0 | results only |
| EntitySplittingNpgsqlTest | EntitySplittingTestBase | 2 | 0 | 0 | 0 | results only |
| ExecutionStrategyTest | provider-only | 0 | 0 | 9 | 0 | results only |
| ExistingConnectionTest | provider-only | 0 | 0 | 2 | 0 | results only |
| FieldMappingNpgsqlTest | FieldMappingTestBase | 101 | 0 | 0 | 0 | results only |
| FindNpgsqlTestContext | FindTestBase | 69 | 0 | 0 | 0 | results only |
| FindNpgsqlTestNonGeneric | FindTestBase | 69 | 0 | 0 | 0 | results only |
| FindNpgsqlTestSet | FindTestBase | 69 | 0 | 0 | 0 | results only |
| JsonTypesNpgsqlTest | JsonTypesRelationalTestBase | 244 | 47 | 15 | 0 | results only |
| KeysWithConvertersNpgsqlTest | KeysWithConvertersTestBase | 47 | 0 | 0 | 0 | results only |
| LazyLoadProxyNpgsqlTest | LazyLoadProxyRelationalTestBase | 75 | 2 | 0 | 0 | results only |
| LoadNpgsqlTest | LoadTestBase | 203 | 0 | 0 | 0 | results only |
| LoggingNpgsqlTest | LoggingRelationalTestBase | 13 | 0 | 7 | 0 | results only |
| ManyToManyFieldsLoadNpgsqlTest | ManyToManyFieldsLoadTestBase | 23 | 0 | 0 | 0 | results only |
| ManyToManyLoadNpgsqlTest | ManyToManyLoadTestBase | 48 | 0 | 0 | 0 | results only |
| ManyToManyTrackingNpgsqlTest | ManyToManyTrackingRelationalTestBase | 67 | 0 | 0 | 0 | results only |
| MaterializationInterceptionNpgsqlTest | MaterializationInterceptionTestBase | 7 | 0 | 0 | 0 | results only |
| ModelBuilding101NpgsqlTest | ModelBuilding101RelationalTestBase | 124 | 0 | 0 | 0 | results only |
| MonsterFixupChangedChangingNpgsqlTest | MonsterFixupTestBase | 12 | 0 | 0 | 0 | results only |
| MusicStoreNpgsqlTest | MusicStoreTestBase | 16 | 0 | 0 | 0 | results only |
| NotificationEntitiesNpgsqlTest | NotificationEntitiesTestBase | 2 | 0 | 0 | 0 | results only |
| NpgsqlApiConsistencyTest | ApiConsistencyTestBase | 19 | 0 | 0 | 0 | results only |
| NpgsqlComplianceTest | RelationalComplianceTestBase | 2 | 0 | 0 | 0 | results only |
| NpgsqlDatabaseCreatorExistsTest | provider-only | 0 | 0 | 16 | 0 | results only |
| NpgsqlServiceCollectionExtensionsTest | RelationalServiceCollectionExtensionsTestBase | 3 | 0 | 0 | 0 | results only |
| NpgsqlValueGenerationScenariosTest | provider-only | 0 | 0 | 13 | 0 | results only |
| OptimisticConcurrencyBytesNpgsqlTest | OptimisticConcurrencyRelationalTestBase | 34 | 1 | 3 | 2 | SQL partial |
| OptimisticConcurrencyNpgsqlTest | OptimisticConcurrencyRelationalTestBase | 34 | 0 | 0 | 0 | results only |
| OverzealousInitializationNpgsqlTest | OverzealousInitializationTestBase | 1 | 0 | 0 | 0 | results only |
| PropertyValuesNpgsqlTest | PropertyValuesRelationalTestBase | 171 | 0 | 0 | 0 | results only |
| QueryExpressionInterceptionNpgsqlTest | QueryExpressionInterceptionTestBase | 4 | 0 | 0 | 0 | results only |
| QueryExpressionInterceptionWithDiagnosticsNpgsqlTest | QueryExpressionInterceptionTestBase | 4 | 0 | 0 | 0 | results only |
| SaveChangesInterceptionNpgsqlTest | SaveChangesInterceptionTestBase | 6 | 0 | 0 | 0 | results only |
| SaveChangesInterceptionWithDiagnosticsNpgsqlTest | SaveChangesInterceptionTestBase | 6 | 0 | 0 | 0 | results only |
| SeedingNpgsqlTest | SeedingTestBase | 2 | 0 | 0 | 0 | results only |
| SequenceEndToEndTest | provider-only | 0 | 0 | 7 | 0 | results only |
| SerializationNpgsqlTest | SerializationTestBase | 1 | 0 | 0 | 0 | results only |
| SpatialNpgsqlTest | SpatialTestBase | 5 | 1 | 0 | 0 | results only |
| StoreGeneratedFixupNpgsqlTest | StoreGeneratedFixupRelationalTestBase | 118 | 0 | 1 | 0 | results only |
| StoreGeneratedNpgsqlTest | StoreGeneratedTestBase | 60 | 0 | 0 | 0 | results only |
| SystemColumnTest | provider-only | 0 | 0 | 1 | 0 | results only |
| TPTTableSplittingNpgsqlTest | TPTTableSplittingTestBase | 26 | 1 | 0 | 0 | results only |
| TableSplittingNpgsqlTest | TableSplittingTestBase | 26 | 1 | 0 | 1 | SQL heavy |
| TransactionInterceptionNpgsqlTest | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | results only |
| TransactionInterceptionWithDiagnosticsNpgsqlTest | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | results only |
| TransactionNpgsqlTest | TransactionTestBase | 44 | 4 | 0 | 0 | results only |
| TwoDatabasesNpgsqlTest | TwoDatabasesTestBase | 3 | 0 | 0 | 0 | results only |
| UpdatesNpgsqlTest | UpdatesRelationalTestBase | 29 | 1 | 0 | 0 | results only |
| ValueConvertersEndToEndNpgsqlTest | ValueConvertersEndToEndTestBase | 25 | 1 | 2 | 0 | results only |
| WithConstructorsNpgsqlTest | WithConstructorsTestBase | 41 | 0 | 0 | 0 | results only |

### BulkUpdates

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| NonSharedModelBulkUpdatesNpgsqlTest | NonSharedModelBulkUpdatesRelationalTestBase | 17 | 17 | 1 | 18 | SQL heavy |
| NorthwindBulkUpdatesNpgsqlTest | NorthwindBulkUpdatesRelationalTestBase | 94 | 91 | 0 | 37 | SQL partial |
| TPCFiltersInheritanceBulkUpdatesNpgsqlTest | TPCFiltersInheritanceBulkUpdatesTestBase | 18 | 18 | 0 | 10 | SQL partial |
| TPCInheritanceBulkUpdatesNpgsqlTest | TPCInheritanceBulkUpdatesTestBase | 20 | 20 | 0 | 10 | SQL partial |
| TPHFiltersInheritanceBulkUpdatesNpgsqlTest | FiltersInheritanceBulkUpdatesRelationalTestBase | 18 | 18 | 0 | 10 | SQL partial |
| TPHInheritanceBulkUpdatesNpgsqlTest | TPHInheritanceBulkUpdatesTestBase | 20 | 20 | 0 | 10 | SQL partial |
| TPTFiltersInheritanceBulkUpdatesSqlServerTest | TPTFiltersInheritanceBulkUpdatesTestBase | 18 | 18 | 0 | 10 | SQL partial |
| TPTInheritanceBulkUpdatesNpgsqlTest | TPTInheritanceBulkUpdatesTestBase | 20 | 20 | 0 | 10 | SQL partial |

### Migrations

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| MigrationsInfrastructureNpgsqlTest | MigrationsInfrastructureTestBase | 25 | 13 | 1 | 0 | results only |
| MigrationsNpgsqlTest | MigrationsTestBase | 127 | 102 | 77 | 171 | SQL heavy |
| NpgsqlMigrationsSqlGeneratorTest | MigrationsSqlGeneratorTestBase | 49 | 34 | 8 | 38 | SQL heavy |

### ModelBuilding

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| NpgsqlGenericComplexCollection | RelationalComplexCollectionTestBase | 46 | 0 | 0 | 0 | results only |
| NpgsqlGenericComplexType | RelationalComplexTypeTestBase | 96 | 0 | 0 | 0 | results only |
| NpgsqlGenericInheritance | RelationalInheritanceTestBase | 31 | 0 | 0 | 0 | results only |
| NpgsqlGenericManyToMany | RelationalManyToManyTestBase | 36 | 0 | 0 | 0 | results only |
| NpgsqlGenericManyToOne | RelationalManyToOneTestBase | 59 | 0 | 0 | 0 | results only |
| NpgsqlGenericNonRelationship | RelationalNonRelationshipTestBase | 173 | 0 | 0 | 0 | results only |
| NpgsqlGenericOneToMany | RelationalOneToManyTestBase | 90 | 0 | 0 | 0 | results only |
| NpgsqlGenericOneToOne | RelationalOneToOneTestBase | 142 | 0 | 0 | 0 | results only |
| NpgsqlGenericOwnedTypes | RelationalOwnedTypesTestBase | 63 | 0 | 0 | 0 | results only |

### Query

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| AdHocAdvancedMappingsQueryNpgsqlTest | AdHocAdvancedMappingsQueryRelationalTestBase | 19 | 6 | 0 | 0 | results only |
| AdHocComplexTypeQueryNpgsqlTest | AdHocComplexTypeQueryRelationalTestBase | 14 | 2 | 0 | 2 | SQL heavy |
| AdHocJsonQueryNpgsqlTest | AdHocJsonQueryRelationalTestBase | 46 | 22 | 2 | 3 | SQL partial |
| AdHocManyToManyQueryNpgsqlTest | AdHocManyToManyQueryRelationalTestBase | 2 | 0 | 0 | 0 | results only |
| AdHocMiscellaneousQueryNpgsqlTest | AdHocMiscellaneousQueryRelationalTestBase | 46 | 4 | 0 | 0 | results only |
| AdHocNavigationsQueryNpgsqlTest | AdHocNavigationsQueryRelationalTestBase | 21 | 1 | 0 | 0 | results only |
| AdHocPrecompiledQueryNpgsqlTest | AdHocPrecompiledQueryRelationalTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| AdHocQueryFiltersQueryNpgsqlTest | AdHocQueryFiltersQueryRelationalTestBase | 21 | 0 | 0 | 0 | results only |
| AdHocQuerySplittingQueryNpgsqlTest | AdHocQuerySplittingQueryTestBase | 10 | 0 | 0 | 0 | results only |
| ArrayArrayQueryTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 56 | 58 | SQL heavy |
| ArrayListQueryTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 50 | 57 | SQL heavy |
| CharacterQueryNpgsqlTest | provider-only | 0 | 0 | 4 | 0 | results only |
| CompatibilityQueryNpgsqlTest | provider-only | 0 | 0 | 1 | 2 | SQL heavy |
| ComplexNavigationsCollectionsQueryNpgsqlTest | ComplexNavigationsCollectionsQueryRelationalTestBase | 156 | 0 | 0 | 0 | results only |
| ComplexNavigationsCollectionsSharedTypeQueryNpgsqlTest | ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase | 156 | 0 | 0 | 0 | results only |
| ComplexNavigationsCollectionsSplitQueryNpgsqlTest | ComplexNavigationsCollectionsSplitQueryRelationalTestBase | 156 | 0 | 0 | 0 | results only |
| ComplexNavigationsCollectionsSplitSharedTypeQueryNpgsqlTest | ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase | 156 | 0 | 0 | 0 | results only |
| ComplexNavigationsQueryNpgsqlTest | ComplexNavigationsQueryRelationalTestBase | 308 | 2 | 0 | 0 | results only |
| ComplexNavigationsSharedTypeQueryNpgsqlTest | ComplexNavigationsSharedTypeQueryRelationalTestBase | 308 | 5 | 0 | 0 | results only |
| ComplexTypeQueryNpgsqlTest | ComplexTypeQueryRelationalTestBase | 74 | 74 | 0 | 75 | SQL heavy |
| CompositeKeysQueryNpgsqlTest | CompositeKeysQueryRelationalTestBase | 7 | 0 | 0 | 0 | results only |
| CompositeKeysSplitQueryNpgsqlTest | CompositeKeysSplitQueryRelationalTestBase | 7 | 0 | 0 | 0 | results only |
| Ef6GroupByNpgsqlTest | Ef6GroupByTestBase | 55 | 3 | 0 | 4 | SQL heavy |
| EntitySplittingQueryNpgsqlTest | EntitySplittingQueryTestBase | 58 | 57 | 0 | 57 | SQL heavy |
| FieldsOnlyLoadNpgsqlTest | FieldsOnlyLoadTestBase | 120 | 0 | 0 | 0 | results only |
| FiltersInheritanceQueryNpgsqlTest | FiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | results only |
| FromSqlQueryNpgsqlTest | FromSqlQueryTestBase | 75 | 7 | 0 | 0 | results only |
| FullTextSearchDbFunctionsNpgsqlTest | provider-only | 0 | 0 | 60 | 59 | SQL heavy |
| FunkyDataQueryNpgsqlTest | FunkyDataQueryTestBase | 19 | 1 | 2 | 1 | SQL partial |
| FuzzyStringMatchQueryNpgsqlTest | provider-only | 0 | 0 | 9 | 1 | SQL partial |
| GearsOfWarFromSqlQueryNpgsqlTest | GearsOfWarFromSqlQueryTestBase | 1 | 1 | 0 | 0 | results only |
| GearsOfWarQueryNpgsqlTest | GearsOfWarQueryRelationalTestBase | 596 | 5 | 8 | 12 | SQL heavy |
| IncludeOneToOneNpgsqlTest | IncludeOneToOneTestBase | 12 | 0 | 0 | 0 | results only |
| InheritanceRelationshipsQueryNpgsqlTest | InheritanceRelationshipsQueryTestBase | 48 | 0 | 0 | 0 | results only |
| JsonDomQueryTest | provider-only | 0 | 0 | 29 | 29 | SQL heavy |
| JsonPocoQueryTest | provider-only | 0 | 0 | 36 | 36 | SQL heavy |
| JsonQueryNpgsqlTest | JsonQueryRelationalTestBase | 226 | 219 | 0 | 226 | SQL heavy |
| JsonStringQueryTest | provider-only | 0 | 0 | 11 | 11 | SQL heavy |
| LegacyNpgsqlNodaTimeTypeMappingTest | provider-only | 0 | 0 | 6 | 0 | results only |
| LegacyTimestampQueryTest | provider-only | 0 | 0 | 3 | 3 | SQL heavy |
| ManyToManyNoTrackingQueryNpgsqlTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 0 | 0 | results only |
| ManyToManyQueryNpgsqlTest | ManyToManyQueryRelationalTestBase | 123 | 0 | 0 | 0 | results only |
| MappingQueryNpgsqlTest | MappingQueryTestBase | 4 | 0 | 0 | 0 | results only |
| NavigationTest | provider-only | 0 | 0 | 2 | 0 | results only |
| NonSharedPrimitiveCollectionsQueryNpgsqlTest | NonSharedPrimitiveCollectionsQueryRelationalTestBase | 38 | 6 | 4 | 3 | SQL partial |
| NorthwindAggregateOperatorsQueryNpgsqlTest | NorthwindAggregateOperatorsQueryRelationalTestBase | 211 | 9 | 0 | 8 | SQL heavy |
| NorthwindAsNoTrackingQueryNpgsqlTest | NorthwindAsNoTrackingQueryTestBase | 11 | 0 | 0 | 0 | results only |
| NorthwindAsTrackingQueryNpgsqlTest | NorthwindAsTrackingQueryTestBase | 5 | 0 | 0 | 0 | results only |
| NorthwindChangeTrackingQueryNpgsqlTest | NorthwindChangeTrackingQueryTestBase | 17 | 0 | 0 | 0 | results only |
| NorthwindCompiledQueryNpgsqlTest | NorthwindCompiledQueryTestBase | 32 | 0 | 0 | 0 | results only |
| NorthwindDbFunctionsQueryNpgsqlTest | NorthwindDbFunctionsQueryRelationalTestBase | 15 | 5 | 11 | 17 | SQL heavy |
| NorthwindEFPropertyIncludeQueryNpgsqlTest | NorthwindEFPropertyIncludeQueryTestBase | 119 | 1 | 0 | 2 | SQL heavy |
| NorthwindFunctionsQueryNpgsqlTest | NorthwindFunctionsQueryRelationalTestBase | 10 | 0 | 55 | 42 | SQL heavy |
| NorthwindGroupByQueryNpgsqlTest | NorthwindGroupByQueryRelationalTestBase | 258 | 258 | 1 | 260 | SQL heavy |
| NorthwindIncludeNoTrackingQueryNpgsqlTest | NorthwindIncludeNoTrackingQueryTestBase | 118 | 1 | 0 | 0 | results only |
| NorthwindIncludeQueryNpgsqlTest | NorthwindIncludeQueryRelationalTestBase | 118 | 0 | 0 | 0 | results only |
| NorthwindJoinQueryNpgsqlTest | NorthwindJoinQueryRelationalTestBase | 68 | 1 | 0 | 0 | results only |
| NorthwindKeylessEntitiesQueryNpgsqlTest | NorthwindKeylessEntitiesQueryRelationalTestBase | 20 | 1 | 0 | 2 | SQL heavy |
| NorthwindMiscellaneousQueryNpgsqlTest | NorthwindMiscellaneousQueryRelationalTestBase | 471 | 10 | 13 | 23 | SQL heavy |
| NorthwindNavigationsQueryNpgsqlTest | NorthwindNavigationsQueryRelationalTestBase | 73 | 0 | 0 | 0 | results only |
| NorthwindQueryFiltersQueryNpgsqlTest | NorthwindQueryFiltersQueryTestBase | 17 | 0 | 0 | 0 | results only |
| NorthwindQueryTaggingQueryNpgsqlTest | NorthwindQueryTaggingQueryTestBase | 9 | 0 | 0 | 0 | results only |
| NorthwindSelectQueryNpgsqlTest | NorthwindSelectQueryRelationalTestBase | 186 | 3 | 0 | 4 | SQL heavy |
| NorthwindSetOperationsQueryNpgsqlTest | NorthwindSetOperationsQueryRelationalTestBase | 91 | 1 | 5 | 7 | SQL heavy |
| NorthwindSplitIncludeNoTrackingQueryNpgsqlTest | NorthwindSplitIncludeNoTrackingQueryTestBase | 118 | 0 | 0 | 0 | results only |
| NorthwindSplitIncludeQueryNpgsqlTest | NorthwindSplitIncludeQueryTestBase | 118 | 0 | 0 | 0 | results only |
| NorthwindSqlQueryNpgsqlTest | NorthwindSqlQueryTestBase | 4 | 4 | 0 | 5 | SQL heavy |
| NorthwindStringIncludeQueryNpgsqlTest | NorthwindStringIncludeQueryTestBase | 119 | 1 | 0 | 0 | results only |
| NorthwindWhereQueryNpgsqlTest | NorthwindWhereQueryRelationalTestBase | 204 | 9 | 13 | 22 | SQL heavy |
| NullKeysNpgsqlTest | NullKeysTestBase | 5 | 0 | 0 | 0 | results only |
| NullSemanticsQueryNpgsqlTest | NullSemanticsQueryTestBase | 168 | 0 | 3 | 4 | SQL heavy |
| OperatorsQueryNpgsqlTest | OperatorsQueryTestBase | 10 | 10 | 1 | 12 | SQL heavy |
| OptionalDependentQueryNpgsqlTest | OptionalDependentQueryTestBase | 10 | 10 | 0 | 11 | SQL heavy |
| OwnedEntityQueryNpgsqlTest | OwnedEntityQueryRelationalTestBase | 23 | 0 | 0 | 0 | results only |
| OwnedQueryNpgsqlTest | OwnedQueryRelationalTestBase | 104 | 0 | 0 | 0 | results only |
| PrecompiledQueryNpgsqlTest | PrecompiledQueryRelationalTestBase | 120 | 120 | 0 | 121 | SQL heavy |
| PrecompiledSqlPregenerationQueryNpgsqlTest | PrecompiledSqlPregenerationQueryRelationalTestBase | 14 | 14 | 0 | 14 | SQL heavy |
| PrimitiveCollectionsQueryNpgsqlTest | PrimitiveCollectionsQueryRelationalTestBase | 156 | 155 | 6 | 161 | SQL heavy |
| QueryBugsTest | provider-only | 0 | 0 | 1 | 1 | SQL heavy |
| QueryFilterFuncletizationNpgsqlTest | QueryFilterFuncletizationTestBase | 28 | 1 | 0 | 0 | results only |
| QueryNoClientEvalNpgsqlTest | QueryNoClientEvalTestBase | 14 | 0 | 0 | 0 | results only |
| SharedTypeQueryNpgsqlTest | SharedTypeQueryRelationalTestBase | 4 | 1 | 0 | 0 | results only |
| SpatialQueryNpgsqlGeographyTest | SpatialQueryRelationalTestBase | 84 | 66 | 3 | 18 | SQL partial |
| SpatialQueryNpgsqlGeometryTest | SpatialQueryRelationalTestBase | 84 | 59 | 2 | 61 | SQL heavy |
| SqlExecutorNpgsqlTest | SqlExecutorTestBase | 14 | 0 | 0 | 0 | results only |
| SqlQueryNpgsqlTest | SqlQueryTestBase | 61 | 44 | 0 | 40 | SQL heavy |
| TPCFiltersInheritanceQueryNpgsqlTest | TPCFiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | results only |
| TPCGearsOfWarQueryNpgsqlTest | TPCGearsOfWarQueryRelationalTestBase | 596 | 4 | 0 | 4 | SQL heavy |
| TPCInheritanceQueryNpgsqlTest | TPCInheritanceQueryTestBase | 53 | 0 | 0 | 0 | results only |
| TPCManyToManyNoTrackingQueryNpgsqlTest | TPCManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 0 | 0 | results only |
| TPCManyToManyQueryNpgsqlTest | TPCManyToManyQueryRelationalTestBase | 123 | 0 | 0 | 0 | results only |
| TPCRelationshipsQueryNpgsqlTest | TPCRelationshipsQueryTestBase | 70 | 0 | 0 | 0 | results only |
| TPHInheritanceQueryNpgsqlTest | TPHInheritanceQueryTestBase | 55 | 0 | 0 | 0 | results only |
| TPTFiltersInheritanceQuerySqlServerTest | TPTFiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | results only |
| TPTGearsOfWarQueryNpgsqlTest | TPTGearsOfWarQueryRelationalTestBase | 596 | 4 | 0 | 4 | SQL heavy |
| TPTInheritanceQueryNpgsqlTest | TPTInheritanceQueryTestBase | 53 | 0 | 0 | 0 | results only |
| TPTManyToManyNoTrackingQueryNpgsqlTest | TPTManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 0 | 0 | results only |
| TPTManyToManyQueryNpgsqlTest | TPTManyToManyQueryRelationalTestBase | 123 | 0 | 0 | 0 | results only |
| TPTRelationshipsQueryNpgsqlTest | TPTRelationshipsQueryTestBase | 70 | 0 | 0 | 0 | results only |
| ToSqlQuerySqlServerTest | ToSqlQueryTestBase | 1 | 1 | 0 | 1 | SQL heavy |
| UdfDbFunctionNpgsqlTests | UdfDbFunctionTestBase | 106 | 40 | 0 | 39 | SQL heavy |
| WarningsNpgsqlTest | WarningsTestBase | 11 | 0 | 0 | 0 | results only |

### Query/Associations/ComplexJson

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| ComplexJsonBulkUpdateNpgsqlTest | ComplexJsonBulkUpdateRelationalTestBase | 33 | 33 | 0 | 3 | SQL partial |
| ComplexJsonCollectionNpgsqlTest | ComplexJsonCollectionRelationalTestBase | 14 | 14 | 0 | 14 | SQL heavy |
| ComplexJsonMiscellaneousNpgsqlTest | ComplexJsonMiscellaneousRelationalTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| ComplexJsonPrimitiveCollectionNpgsqlTest | ComplexJsonPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 6 | SQL heavy |
| ComplexJsonProjectionNpgsqlTest | ComplexJsonProjectionRelationalTestBase | 28 | 27 | 0 | 27 | SQL heavy |
| ComplexJsonSetOperationsNpgsqlTest | ComplexJsonSetOperationsRelationalTestBase | 5 | 5 | 0 | 5 | SQL heavy |
| ComplexJsonStructuralEqualityNpgsqlTest | ComplexJsonStructuralEqualityRelationalTestBase | 16 | 16 | 0 | 16 | SQL heavy |

### Query/Associations/ComplexTableSplitting

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| ComplexTableSplittingBulkUpdateNpgsqlTest | ComplexTableSplittingBulkUpdateRelationalTestBase | 33 | 33 | 0 | 3 | SQL partial |
| ComplexTableSplittingMiscellaneousNpgsqlTest | ComplexTableSplittingMiscellaneousRelationalTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| ComplexTableSplittingPrimitiveCollectionNpgsqlTest | ComplexTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 6 | SQL heavy |
| ComplexTableSplittingProjectionNpgsqlTest | ComplexTableSplittingProjectionRelationalTestBase | 28 | 27 | 0 | 27 | SQL heavy |
| ComplexTableSplittingStructuralEqualityNpgsqlTest | ComplexTableSplittingStructuralEqualityRelationalTestBase | 16 | 16 | 0 | 16 | SQL heavy |

### Query/Associations/Navigations

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| NavigationsCollectionNpgsqlTest | NavigationsCollectionRelationalTestBase | 14 | 14 | 0 | 14 | SQL heavy |
| NavigationsIncludeNpgsqlTest | NavigationsIncludeRelationalTestBase | 9 | 9 | 0 | 9 | SQL heavy |
| NavigationsMiscellaneousNpgsqlTest | NavigationsMiscellaneousRelationalTestBase | 4 | 4 | 0 | 4 | SQL heavy |
| NavigationsPrimitiveCollectionNpgsqlTest | NavigationsPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 6 | SQL heavy |
| NavigationsProjectionNpgsqlTest | NavigationsProjectionRelationalTestBase | 24 | 23 | 0 | 23 | SQL heavy |
| NavigationsSetOperationsNpgsqlTest | NavigationsSetOperationsRelationalTestBase | 5 | 5 | 0 | 5 | SQL heavy |
| NavigationsStructuralEqualityNpgsqlTest | NavigationsStructuralEqualityRelationalTestBase | 15 | 15 | 0 | 15 | SQL heavy |

### Query/Associations/OwnedJson

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| OwnedJsonBulkUpdateNpgsqlTest | OwnedJsonBulkUpdateRelationalTestBase | 3 | 0 | 0 | 0 | results only |
| OwnedJsonCollectionNpgsqlTest | OwnedJsonCollectionRelationalTestBase | 14 | 14 | 1 | 15 | SQL heavy |
| OwnedJsonMiscellaneousNpgsqlTest | OwnedJsonMiscellaneousRelationalTestBase | 4 | 4 | 0 | 4 | SQL heavy |
| OwnedJsonPrimitiveCollectionNpgsqlTest | OwnedJsonPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 6 | SQL heavy |
| OwnedJsonProjectionNpgsqlTest | OwnedJsonProjectionRelationalTestBase | 24 | 23 | 0 | 23 | SQL heavy |
| OwnedJsonStructuralEqualityNpgsqlTest | OwnedJsonStructuralEqualityRelationalTestBase | 15 | 15 | 0 | 15 | SQL heavy |

### Query/Associations/OwnedNavigations

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| OwnedNavigationsCollectionNpgsqlTest | OwnedNavigationsCollectionRelationalTestBase | 14 | 14 | 0 | 14 | SQL heavy |
| OwnedNavigationsMiscellaneousNpgsqlTest | OwnedNavigationsMiscellaneousRelationalTestBase | 4 | 4 | 0 | 4 | SQL heavy |
| OwnedNavigationsPrimitiveCollectionNpgsqlTest | OwnedNavigationsPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 6 | SQL heavy |
| OwnedNavigationsProjectionNpgsqlTest | OwnedNavigationsProjectionRelationalTestBase | 24 | 23 | 0 | 23 | SQL heavy |
| OwnedNavigationsSetOperationsNpgsqlTest | OwnedNavigationsSetOperationsRelationalTestBase | 5 | 5 | 0 | 4 | SQL heavy |
| OwnedNavigationsStructuralEqualityNpgsqlTest | OwnedNavigationsStructuralEqualityRelationalTestBase | 15 | 15 | 0 | 15 | SQL heavy |

### Query/Associations/OwnedTableSplitting

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| OwnedTableSplittingMiscellaneousNpgsqlTest | OwnedTableSplittingMiscellaneousRelationalTestBase | 4 | 4 | 0 | 4 | SQL heavy |
| OwnedTableSplittingPrimitiveCollectionNpgsqlTest | OwnedTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 6 | 0 | 6 | SQL heavy |
| OwnedTableSplittingProjectionNpgsqlTest | OwnedTableSplittingProjectionRelationalTestBase | 24 | 23 | 0 | 23 | SQL heavy |
| OwnedTableSplittingStructuralEqualityNpgsqlTest | OwnedTableSplittingStructuralEqualityRelationalTestBase | 15 | 15 | 0 | 15 | SQL heavy |

### Query/Translations

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| BigIntegerTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 7 | 8 | SQL heavy |
| ByteArrayTranslationsNpgsqlTest | ByteArrayTranslationsTestBase | 7 | 7 | 1 | 9 | SQL heavy |
| CitextTranslationsTest | provider-only | 0 | 0 | 15 | 16 | SQL heavy |
| CubeTranslationsTest | provider-only | 0 | 0 | 61 | 56 | SQL heavy |
| EnumTranslationsNpgsqlTest | EnumTranslationsTestBase | 18 | 18 | 0 | 19 | SQL heavy |
| EnumTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 11 | 11 | SQL heavy |
| GuidTranslationsNpgsqlTest | GuidTranslationsTestBase | 4 | 4 | 1 | 8 | SQL heavy |
| LTreeTranslationsTest | provider-only | 0 | 0 | 29 | 29 | SQL heavy |
| MathTranslationsNpgsqlTest | MathTranslationsTestBase | 66 | 66 | 0 | 58 | SQL heavy |
| MiscellaneousTranslationsNpgsqlTest | MiscellaneousTranslationsRelationalTestBase | 18 | 18 | 0 | 12 | SQL heavy |
| MultirangeTranslationsTest | provider-only | 0 | 0 | 31 | 32 | SQL heavy |
| NetworkTranslationsNpgsqlTest | provider-only | 0 | 0 | 117 | 116 | SQL heavy |
| RangeTranslationsTest | provider-only | 0 | 0 | 31 | 32 | SQL heavy |
| StringTranslationsNpgsqlTest | StringTranslationsRelationalTestBase | 104 | 104 | 0 | 94 | SQL heavy |
| TimestampTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 50 | 33 | SQL heavy |
| TrigramsTranslationsTest | provider-only | 0 | 0 | 16 | 3 | SQL partial |

### Query/Translations/NodaTime

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| DateIntervalTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 10 | 11 | SQL heavy |
| DurationTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 11 | 12 | SQL heavy |
| InstantTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 9 | 9 | SQL heavy |
| IntervalTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 8 | 9 | SQL heavy |
| LocalDateTimeTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 14 | 15 | SQL heavy |
| LocalDateTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 12 | 13 | SQL heavy |
| LocalTimeTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 4 | 5 | SQL heavy |
| PeriodTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 25 | 19 | SQL heavy |
| ZonedDateTimeTranslationsTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 13 | 14 | SQL heavy |

### Query/Translations/Operators

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| ArithmeticOperatorTranslationsNpgsqlTest | ArithmeticOperatorTranslationsTestBase | 5 | 5 | 0 | 6 | SQL heavy |
| BitwiseOperatorTranslationsNpgsqlTest | BitwiseOperatorTranslationsTestBase | 15 | 15 | 0 | 14 | SQL heavy |
| ComparisonOperatorTranslationsNpgsqlTest | ComparisonOperatorTranslationsTestBase | 6 | 6 | 0 | 7 | SQL heavy |
| LogicalOperatorTranslationsNpgsqlTest | LogicalOperatorTranslationsTestBase | 6 | 6 | 0 | 7 | SQL heavy |
| MiscellaneousOperatorTranslationsNpgsqlTest | MiscellaneousOperatorTranslationsTestBase | 2 | 2 | 0 | 3 | SQL heavy |

### Query/Translations/Temporal

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| DateOnlyTranslationsNpgsqlTest | DateOnlyTranslationsTestBase | 18 | 18 | 0 | 19 | SQL heavy |
| DateTimeOffsetTranslationsNpgsqlTest | DateTimeOffsetTranslationsTestBase | 24 | 24 | 0 | 18 | SQL heavy |
| DateTimeTranslationsNpgsqlTest | DateTimeTranslationsTestBase | 19 | 19 | 0 | 16 | SQL heavy |
| DateTimeTranslationsWithoutTimeZoneTest | DateTimeTranslationsTestBase | 19 | 19 | 0 | 19 | SQL heavy |
| TimeOnlyTranslationsNpgsqlTest | TimeOnlyTranslationsTestBase | 17 | 17 | 0 | 15 | SQL heavy |
| TimeSpanTranslationsNpgsqlTest | TimeSpanTranslationsTestBase | 6 | 6 | 0 | 5 | SQL heavy |

### Scaffolding

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| NpgsqlDatabaseModelFactoryTest | provider-only | 0 | 0 | 69 | 0 | results only |

### Types/Miscellaneous

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| ByteArrayTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlBoolTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlGuidTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| StringTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |

### Types/Networking

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| NpgsqlInetTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlMacaddrTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |

### Types/Numeric

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| NpgsqlDecimalTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlDoubleTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlFloatTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlIntTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlLongTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlShortTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |

### Types/Temporal

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| DateTimeOffsetTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| DateTimeUnspecifiedTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| DateTimeUtcTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlBoolTypeTest | provider-only (on QueryTestBase/SharedStoreFixture) | 0 | 0 | 1 | 7 | SQL heavy |
| NpgsqlTimeOnlyTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |
| NpgsqlTimeSpanTypeTest | RelationalTypeTestBase | 7 | 7 | 0 | 7 | SQL heavy |

### Update

| Class | Spec base (or provider-only) | Inherited tests | Overridden | Provider-added | AssertSql calls | Assertion style |
|---|---|---|---|---|---|---|
| ComplexCollectionJsonUpdateNpgsqlTest | ComplexCollectionJsonUpdateTestBase | 20 | 18 | 0 | 18 | SQL heavy |
| JsonUpdateNpgsqlTest | JsonUpdateTestBase | 136 | 121 | 0 | 90 | SQL heavy |
| NonSharedModelUpdatesNpgsqlTest | NonSharedModelUpdatesTestBase | 2 | 0 | 0 | 0 | results only |
| StoreValueGenerationNpgsqlTest | StoreValueGenerationTestBase | 19 | 18 | 0 | 18 | SQL heavy |
| StoredProcedureUpdateNpgsqlTest | StoredProcedureUpdateTestBase | 28 | 28 | 1 | 17 | SQL partial |

TOTALS: spec-derived concrete classes=242, inherited test methods=13166, provider-added in spec classes=257; provider-only classes=44, provider-only test methods=881

## 2. Provider-only behaviours (by concept)

Notation: PO means a provider-only class and SA means a provider-added test in a spec-derived class. The "SQL" column says whether the tests carry a baseline. "SQL" is the shape the baseline asserts.

### 2.1 Arrays and primitive collections (native array column type)

| Behaviour asserted (database-neutral) | Tests (group) | Concept | Provider API / SQL |
|---|---|---|---|
| A collection-typed column (array, List, IList) round-trips as a native array, with no JSON | ArrayArrayQueryTest / ArrayListQueryTest `Roundtrip` (PO, x2 suites: T[] and List<T>) | Native array column | `integer[]`, `text[]` mapping |
| Element access by index (constant, parameter, nullable) is 1-based on the server. An out-of-range index compares to null as NULL. On a non-nullable element type a null comparison folds to FALSE | `Index_with_constant/parameter`, `Nullable_index_*`, `*_array_index_compare_to_null` | Array indexing | `col[@x + 1]`, `WHERE FALSE` |
| Indexing a byte[] reads a byte | `Index_bytea_with_constant` | Binary indexing | `get_byte(col, 0)` |
| Whole-array equality against a parameter | `SequenceEqual_with_parameter`, `SequenceEqual_over_nullable_with_parameter` | Array equality | `col = @arr` |
| Membership: column array contains a literal, parameter or column. Uses `Any(x => x == v)`, `Contains` and value-converted elements | `Array_column_Any_*`, `Array_column_Contains_*`, `IList_column_contains_constant`, `Array_column_Contains_value_converted_*`, `Array_column_Contains_in_scalar_subquery` | Element membership | `v = ANY(col)` |
| Searching for NULL inside an array is null-aware. A null *parameter* does not match (this is documented behaviour) | `Array_column_Contains_null_constant`, `Array_column_Contains_null_parameter_does_not_work` | Null semantics in arrays | `array_position(col, NULL) IS NOT NULL` |
| A parameter list Contains a column, with full three-valued null compensation and negated forms | `Array_param_Contains_*`, `Array_param_with_null_Contains_*(_negated)`, `Byte_array_parameter_contains_column`, `Array_param_Contains_value_converted_column_*`; also SA Northwind Misc `Array_Contains_parameter(_with_null)` | Parameterized IN-list as an array | `col = ANY(@p) OR (col IS NULL AND array_position(@p,NULL) IS NOT NULL)` |
| A constant list Contains a column is inlined | `Array_constant_Contains_column`, SA `Array_Contains_constant` | IN-list | `IN ('a','b')` |
| A value-converted array column is contained in a parameter | `Array_param_Contains_value_converted_array_column` | Containment | `col <@ @p` |
| Length, Count and Any() with no predicate | `Array_Length`, `Nullable_array_Length`, `Array_Length_on_EF_Property`, `Any_no_predicate` | Array length | `cardinality(col)` |
| Pattern match of a scalar against any or all of a list of patterns (LIKE/ILIKE), including negation | `Any_like`, `Any_ilike`, `Any_like_anonymous`, `All_like`, `All_ilike`; SA Northwind Misc `Array_Any_Like`, `Array_All_Like(_negated)`, `Array_Any_ILike(_negated)`, `Array_All_ILike` | Quantified LIKE | `x LIKE ANY(@arr)`, `ILIKE ALL(...)` |
| Overlap and containment between two arrays | `Any_Contains_on_constant_array`, `Any_Contains_between_column_and_List/array/other_type`, `All_Contains` | Array overlap/containment | `&&`, `<@` |
| Append, IndexOf (with start) and String.Join over an array column; Join rejects a non-array-mapped parameter | `Append`, `Array_IndexOf1/2`, `String_Join_with_array_of_*`, `String_Join_disallow_non_array_type_mapped_parameter` | Array functions | `array_append`, `COALESCE(array_position(...)-1,-1)`, `array_to_string(col, ', ', '')` |
| Removing an element from a primitive collection; HashSet or dictionary-values parameters with value converters; a parameter collection of ranges; Intersect/Concat of a parameter with a column | SA PrimitiveCollections `Array_remove`, `Parameter_collection_HashSet_with_value_converter_Contains`, `Parameter_collection_Dictionary_Values_with_value_converter_Contains`, `Parameter_collection_of_ranges_Contains` (PG14), `Column_collection_Intersect_Parameter_collection_Any`, `Parameter_collection_Concat_Column_collection_Concat_parameter` | Primitive collections | `array_remove`, `ARRAY(... INTERSECT ...)`, `||` |
| Contains over an array column picks containment (`@>`) when a GIN index exists and `= ANY` otherwise, including through a view | SA NonSharedPrimitiveCollections `Column_collection_Contains_with_GIN_index_uses_containment`, `View_column_…`, `…_btree_index_does_not_use_containment` | Index-aware translation | `col @> ARRAY[4]` vs `4 = ANY(col)` |
| An array of UTC DateTime round-trips | SA `Array_of_DateTime_utc` | timestamptz[] | |
| An array value-converted column round-trips; a converter maps to appropriately nullable columns | SA ValueConvertersEndToEnd `Can_insert_and_read_back_with_value_converted_array`, `Properties_with_conversions_map_to_appropriately_null_columns` | Converters plus arrays | |
| Under the Redshift dialect, array Contains expands into separate parameters | PO CompatibilityQuery `Array_contains_is_not_parameterized_with_array_on_redshift` | Dialect switch | `UseRedshift()`: `IN (@n1, @n2)` |
| An update can use a primitive collection in the value selector | SA NonSharedModelBulkUpdates `Update_with_primitive_collection_in_value_selector` | ExecuteUpdate | |
| String split into an array, then indexed | SA NorthwindDbFunctions `StringToArray(_with_null_string/_with_index)` | Array-returning function | `string_to_array(s, ' ' [, null])[1]` |
| Array aggregation in GroupBy | SA NorthwindFunctions `GroupBy_ArrayAgg` | Aggregate to array | `array_agg(x ORDER BY …)` |

### 2.2 Ranges and multiranges

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| Range operators: contains a value or range, contained-by, equality (operator and method), overlaps, strictly left/right, does-not-extend-left/right, adjacent | RangeTranslationsTest `Contains_value`, `Contains_range`, `ContainedBy`, `Equals_*`, `Overlaps_range`, `IsStrictly*`, `DoesNotExtend*`, `IsAdjacentTo` (PO) | Range type `NpgsqlRange<T>` | `@>`, `<@`, `&&`, `<<`, `>>`, `&<`, `&>`, `-|-` |
| Set algebra: union, intersect, except, merge; aggregates `range_agg` and `range_intersect_agg` | `Union`, `Intersect`, `Except`, `Merge`, `Union_aggregate`, `Intersect_aggregate` (PG14) | Range algebra | `+`, `*`, `-`, `range_merge`, `range_agg` |
| Bound accessors: lower/upper, empty, inclusive, infinite | `LowerBound`, `UpperBound`, `IsEmpty`, `*BoundIsInclusive`, `*BoundInfinite` | Range bounds | `lower()`, `upper_inc()`, `lower_inf()` … |
| Subtype coverage: int, long, decimal, date (DateOnly and DateTime) | `IntRange`, `LongRange`, `DecimalRange`, `Daterange_*` | Built-in range types | int4range/int8range/numrange/daterange |
| User-defined range types (including schema-qualified) | `User_defined`, `User_defined_and_schema_qualified` | `MapRange`/`HasPostgresRange` | `MapRange("doublerange", typeof(double))` |
| Multirange: the same operator set plus range-vs-multirange variants, union/intersect/except, intersect aggregate, emptiness, merge, int/long/decimal/date multiranges; Contains over an array of ranges | MultirangeTranslationsTest, 31 tests (class gated PG14) | Multirange (`NpgsqlRange<T>[]`) | `int4multirange`, `range_intersect_agg` |
| A range parameter contains a timestamp-without-time-zone column | Timestamp `Range_parameter_contains_timestamp_with_no_time_zone_column` | Range plus timestamp kinds | |
| NodaTime DateInterval and Interval as ranges (Start/End/Length/Contains/Intersection/Union, plus range_agg) | NodaTime DateInterval (10), Interval (8) | NodaTime plugin ranges | daterange/tstzrange |

### 2.3 JSON (jsonb/json columns: POCO, DOM, string; plus owned/complex JSON)

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| Three mapping styles round-trip: strongly-typed POCO, `JsonDocument`/`JsonElement` DOM, raw string with `jsonb` type | JsonPoco/JsonDom/JsonString `Roundtrip`, `Literal*`, `Parameter*` (PO) | Weakly-typed JSON column | `HasColumnType("jsonb"/"json")` |
| Member and path access with typed extraction (string, int, Guid, bool, DateTime, DateTimeOffset, nullable), nested paths, array index (constant or parameter), root value | `Output_*`, `Text_output*`, `Integer_output`, `Guid_output`, `Bool_output`, `Nested(_twice)`, `Array_of_objects`, `Array_nested`, `Array_parameter_index`, `Array_toplevel`, `Where_root_value`, `Where_nullable_guid`, `Nullable`, `Compare_to_null` | JSON path extraction | `->>`, `#>>`, `CAST(… AS integer)`, `#>> ARRAY[…,@i]` |
| Array length / Count / Any on JSON arrays, for both json and jsonb | `GetArrayLength(_json)`, `Array_Length(_json)`, `List_Count(_json)`, `Array_Any_toplevel` | JSON array length | `jsonb_array_length`, `json_array_length` |
| LIKE on an extracted text value | `Like` (x2) | | |
| Containment and key existence: contains, contained, key exists, any or all keys exist, typeof | `JsonContains_*`, `JsonContained_*`, `JsonExists`, `JsonExistAny`, `JsonExistAll`, `JsonTypeof(_json)` | JSON operators (`EF.Functions.JsonContains` …) | `@>`, `<@`, `?`, `?|`, `?&`, `jsonb_typeof` |
| JSON aggregation from GroupBy into arrays or objects (or Dictionary) | SA NorthwindFunctions `GroupBy_JsonAgg`, `GroupBy_JsonbAgg`, `GroupBy_JsonObjectAgg(_as_Dictionary)`, `GroupBy_JsonbObjectAgg(_as_Dictionary)` | JSON aggregates | `json_agg`, `jsonb_object_agg` |
| Predicates on JSON-stored bytea (base64) and interval values | SA AdHocJson `Json_predicate_on_bytea`, `Json_predicate_on_interval` | JSON value decoding | `decode(x ->> 'B','base64')`, `CAST(… AS interval)` |
| JSON value read/write for provider types: timetz, PG enum, arrays, BigInteger, BitArray, LogSequenceNumber, point without SRID; a multidimensional array is rejected; the PG-specific temporal JSON format (DateOnly/DateTime/DateTimeOffset collections) | SA JsonTypes 15 `*_npgsql` / provider tests | JSON value serialization | |
| GroupBy with a predicate over a JSON collection, projecting group elements | SA OwnedJsonCollection `GroupBy_with_json_collection_predicate_and_projecting_group_elements` | Owned JSON | |
| Inherited JSON suites (JsonQuery 219 overrides, Associations/ComplexJson + OwnedJson, JsonUpdate, ComplexCollectionJsonUpdate, AdHocJson): baselines are jsonb path operators, `jsonb_set`/`jsonb_set_lax` partial updates, and `jsonb_array_elements`/`jsonb_to_recordset` for collections | spec-derived (see section 3 for divergences) | Structured JSON mapping | `ToJson()` / complex JSON |

### 2.4 Full-text search, fuzzy matching, trigrams, unaccent

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| Building documents and queries: parse-to-cast, array to tsvector, to_tsvector/plainto/phraseto/to/websearch_to_tsquery with no config, a constant config or a parameter config | FullTextSearchDbFunctions (60, PO) `*_converted_to_cast`, `ArrayToTsVector_*`, `ToTsVector*`, `PlainToTsQuery*`, `PhraseToTsQuery*`, `ToTsQuery*`, `WebSearchToTsQuery*` (PG11) | Full-text search | `EF.Functions.ToTsVector` …; `NpgsqlTsVector.Parse` gives `'..'::tsvector` |
| Query algebra: and, or, negate, contains, contained-in, node count, query tree, rewrite (including the subquery form), phrase (with distance) | `TsQuery_*`, `GetNodeCount`, `GetQueryTree`, `Rewrite*`, `ToPhrase*` | tsquery operators | `&&`, `||`, `!!`, `@>`, `<@`, `numnode`, `ts_rewrite`, `tsquery_phrase` |
| Matching against a string or query; concatenation, set weight (enum/char, with lexemes), delete lexemes, filter, length, strip | `Matches_with_*`, `TsVector_Concat`, `SetWeight_*`, `Delete_*`, `Filter`, `GetLength`, `ToStripped` | tsvector ops | `@@`, `setweight`, `ts_delete`, `ts_filter`, `strip` |
| Ranking (rank, cover density, with weights and normalization), headlines with options and config | `Rank*`, `RankCoverDensity*`, `GetResultHeadline*` | Ranking | `ts_rank`, `ts_rank_cd`, `ts_headline` |
| Operator precedence of @@ with boolean operators; basic and complex query shapes | `Match_and_boolean_operator_precedence`, `Basic_where`, `Complex_query` | | |
| Accent removal with an optional dictionary (constant or parameter) | `Unaccent*` | unaccent extension | `unaccent([@d::regdictionary,] x)` |
| Fuzzy string matching: soundex, difference, levenshtein (with costs, less-equal), metaphone, double metaphone | FuzzyStringMatchQuery (9, PO; results only) | fuzzystrmatch extension | `EF.Functions.FuzzyStringMatch*` |
| Trigram similarity, word, strict word, operators and distances (including inverted), plus operator precedence with concatenation and unknown binary operators | TrigramsTranslationsTest (16, PO; mostly results, 3 SQL) | pg_trgm extension | `EF.Functions.Trigrams*`; `%`, `<%`, `<<->` … |
| A generated tsvector column (migrations: over text, jsonb or mixed; changing the config); GIN index on tsvector | SA Migrations `Add_column_generated_tsvector_*`, `Alter_column_generated_tsvector_change_config`, `Create_index_tsvector(_using_gin)` | `HasGeneratedTsVectorColumn` | `GENERATED ALWAYS AS (to_tsvector(...)) STORED` |

### 2.5 Enums

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| A CLR enum mapped to a native database enum type round-trips; comparison with a constant is emitted as a typed literal (schema-qualified, custom label translation) | EnumTranslationsTest (11, PO) `Roundtrip`, `Where_with_constant(_schema_qualified)` | Native enum (`MapEnum`) | `'sad'::test.mapped_enum` |
| Parameters of mapped and unmapped enums; downcasts to the underlying integer do not change the SQL | `Where_with_parameter`, `Where_with_*_downcasts_*` | | |
| Enum ToString on a native enum becomes a text cast | `Enum_ToString` | | `col::text LIKE …` |
| Array-of-enum Contains for byte enums, unmapped enums and upper-case named types | `Where_*_enum_array_contains_enum` | | `= ANY(@values)` |
| Enum in JSON; enum mapping via the data source | SA JsonTypes `Can_read_write_pg_enum_JSON_values`; BuiltInDataTypes fixture `MapEnum<Mood>` | | |
| Migrations: create, schema-qualified create, drop, add label at end or middle; reordering does nothing; drop or rename of a label throws NotSupported; an existing enum is not altered | SA Migrations `Create_enum*`, `Drop_enum`, `Alter_enum_*`, `Do_not_alter_existing_enum_when_creating_new_one` | `HasPostgresEnum` | `CREATE TYPE … AS ENUM`, `ALTER TYPE … ADD VALUE … BEFORE` |
| Scaffolding reads enums | Scaffolding `Enums` | | |
| An insert with an enum mapped in a fresh database (bug regression) | QueryBugTest `Bug920` | | |
| Computed column with a nullable enum | ComputedColumnTest | | |

### 2.6 Network types, cube, ltree, citext, BigInteger, character(n)

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| IP address / network / cidr / MAC (6- and 8-byte) values: parse; compare (<, <=, >, >=); contained-by(-or-equal), contains(-or-equal), contains-or-contained-by; bitwise not/and/or; add or subtract an int; subtract an address; abbreviate, broadcast, family, host, hostmask, masklen, netmask, network, set_masklen, text, same family, merge; truncate and set7bit on MACs. Value-type parameters are duplicated (a documented quirk) | NetworkTranslations (117, PO; macaddr8 gated PG10) | Network types (`IPAddress`, `NpgsqlInet`, `NpgsqlCidr`, `PhysicalAddress`) | `<<`, `<<=`, `>>`, `>>=`, `&&`, `~`, `&`, `|`, `+`, `-`, `abbrev`, `broadcast`, `inet_merge`, `trunc`, `macaddr8_set7bit` |
| Network column types round-trip and compare (inet, macaddr) | Types/Networking NpgsqlInetTypeTest, NpgsqlMacaddrTypeTest (spec RelationalTypeTestBase) | | |
| Multi-dimensional cube: contains/contained-by (cube or point), overlaps, three distance metrics in ORDER BY (KNN), nth coordinate (including KNN form), lower-left/upper-right coordinate (constant, parameter or column index), equality, dimensions, is-point, union, intersect, enlarge, subset (constant, parameter, column or mixed index arrays, converted to 1-based), constructors from coordinates or arrays, null-propagating constructors, edge cases (1-D, negative, zero-volume, large) | CubeTranslations (61, PO; class gated PG14) | cube extension (`NpgsqlCube`) | `@>`, `<@`, `&&`, `<->`, `<#>`, `<=>`, `->`, `~>`, `cube_ll_coord`, `cube_subset(c, (SELECT array_agg(u.x + 1)…))` |
| Using LowerLeft/UpperRight as a collection (Count, FirstOrDefault, Any, projection) throws a helpful InvalidOperationException | Cube `*_throws_helpful_error` (6) | Translation guard | |
| Hierarchical label paths: compare with a string, cast to string, ancestor/descendant, match lquery (literal, column, concatenation, any-of), ltxtquery, concat, array any-contains/contained/matches, first-matching element, subtree, subpath, nlevel, index, longest common ancestor | LTreeTranslations (29, PO; class gated PG13) | ltree extension (`LTree`, `LQuery`, `LTxtQuery`) | `@>`, `<@`, `~`, `?`, `@`, `?@>`, `subltree`, `subpath`, `nlevel`, `index`, `lca` |
| Case-insensitive text column: StartsWith/EndsWith/Contains/IndexOf/Replace with literal, parameter pattern or parameter instance; the instance form casts back to citext | CitextTranslations (15, PO) | citext extension | `LIKE`, `left(@p, length(c))::citext = c`, `strpos` |
| Arbitrary-precision integer: Abs, Pow, Max/Min, IsZero/IsOne/IsEven | BigIntegerTranslations (7, PO) | `BigInteger` as numeric | `abs`, `power`, `GREATEST/LEAST`, `% 2` |
| Fixed-length character keys: Find (database and local), change tracking with padded keys | CharacterQuery (4, PO; results only) | `character(n)` padding semantics | `HasColumnType("character(8)")` |
| Money compare (constant or parameter); type-mapper-driven literals (TIME literal for TimeSpan); Sum over converted bigint/smallint keeps types; the full mapped-type matrix (query and insert, with nulls) | SA BuiltInDataTypes `Money_compare_*`, `Sql_translation_uses_type_mapper_*`, `Sum_Conversions`, `Can_query_using_any_mapped_data_type*`, `Can_insert_and_read_back_all_mapped_data_types*` | Provider type matrix | `TIME '00:01:02'`, `COALESCE(sum(x),0)::bigint` |
| GUID v7 generation server-side (PG18 `uuidv7()`, else client) | SA GuidTranslations `CreateVersion7` (runtime version branch 13/18) | UUID functions | `uuidv7()` |
| byte[] Any() becomes a length check | SA ByteArrayTranslations `Any` | | `length(b) <> 0` |

### 2.7 Row values

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| Tuple comparison >, <, >=, <= with constants, parameters, ValueTuple constructors and differing types | SA NorthwindWhere `Row_value_GreaterThan*`, `Row_value_LessThan`, `*OrEqual`, `Row_value_with_ValueTuple_constructor` | Row value comparison | `EF.Functions.GreaterThan(ValueTuple, ValueTuple)` gives `(a,b) > (x,y)` |
| Tuples of mismatched arity throw ArgumentException with the provider message | `Row_value_parameter_count_mismatch` | | `NpgsqlStrings.RowValueComparisonRequiresTuplesOfSameLength` |
| Tuple Equals / != (including nullable members, with null expansion) | `Row_value_equals`, `Row_value_not_equals(_with_nullable)`; SA NullSemantics `Compare_row_values_equal_without_expansion`, `_with_expansion`, `Compare_row_values_not_equal` | Row equality with null semantics | `(a,b) = (c,d) AND (n1 = n2 OR both NULL)` |
| Projecting a tuple materializes a row | `Row_value_project` | | `SELECT (o."CustomerID", o."OrderDate")` |

### 2.8 Case-insensitive matching, LIKE, regex, collation, string functions

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| ILIKE (with escape, negated); LIKE with a backslash literal (empty ESCAPE) | SA NorthwindDbFunctions `String_ILike_*`, `String_Like_Literal_With_Backslash` | Case-insensitive LIKE | `EF.Functions.ILike`; `LIKE '\' ESCAPE ''` |
| Case-insensitive comparison by applying a non-deterministic collation (PG12, not on Windows) | Collate_case_insensitive (override; gated) | Collation | `COLLATE "some-ci"` |
| Regex IsMatch / Replace / Count with constant or parameter patterns and options (None, IgnoreCase, Multiline, Singleline, IgnorePatternWhitespace); unsupported options throw; Count needs PG15 | SA NorthwindFunctions `Regex_*` (33) | Regex translation | `~`, `~*`, `(?n)`, `(?p)`, `(?x)` flags, `regexp_replace(... 'g')`, `regexp_count` |
| Substring without length (with IndexOf, constant or closure); PadLeft/PadRight (string or char, constant or parameter); reverse; StartsWith with escape characters in an argument | SA `Substring_without_length_*`, `PadLeft_*`, `PadRight_*`, `String_reverse`, FunkyData `String_starts_with_on_argument_with_escape_*` | String functions | `substring(s, n)`, `lpad`/`rpad`, `reverse`, `left(...) = ...` |
| Statistics aggregates: stddev and variance (sample/population), plus correlation, covariance and regression family | SA `StandardDeviation`, `Variance`, `Other_statistics_functions` | Statistical aggregates | `stddev_samp`, `var_pop`, `corr`, `regr_*` |
| NullIf recognised from the conditional pattern (left or right sided, equality or inequality) | SA `NullIf_with_*` (4) | Conditional to NULLIF | `NULLIF(a, b)` |
| Parse to date or timestamp with a format | SA `ToDate`, `ToTimestamp` | | `to_date`, `to_timestamp` |
| Distance operator between timestamps used in ORDER BY | SA `Distance_with_timestamp` | KNN distance | `<->` (btree_gist) |

### 2.9 Timestamps, time zones and temporal arithmetic (BCL types)

| Behaviour | Tests | Concept | API / SQL |
|---|---|---|---|
| DateTime maps to timestamptz by default (scalar, array and range) | TimestampTranslations `DateTime_*_maps_to_timestamptz_by_default` (PO, 50) | timestamptz default | |
| Kind enforcement: a UTC DateTime cannot be written to timestamp; Unspecified or Local cannot be written to timestamptz; comparisons between mismatched kinds and columns throw; ToUniversalTime/ToLocalTime bridge the two | `Cannot_insert_*`, `Cannot_compare_*`, `Compare_*`, `Compare_timestamptz_column_to_timestamp_column_with_To*Time` | Kind/type safety | throws InvalidOperationException/ArgumentException |
| Now/UtcNow/Date on both kinds; constructors (local or UTC); SpecifyKind conversions (a parameter kind throws); time zone conversion (ConvertTimeBySystemTimeZoneId, ConvertTimeToUtc) allowed only on the right column kind | `Where_datetime_now/utcnow`, `Where_datetime_date_on_*` (PG12 for timestamptz), `Where_datetime_ctor*`, `DateTime_SpecifyKind_*`, `Where_ConvertTime*` | AT TIME ZONE | `now()`, `date_trunc`, `AT TIME ZONE 'UTC'`, `make_timestamp(tz)` |
| DateTimeOffset to DateTime / UtcDateTime / LocalDateTime / Date | `DateTimeOffset_*` | | |
| DateOnly/TimeOnly from DateTime (both kinds), to DateTime, DayNumber (and subtraction), FromDayNumber, TimeOnly to TimeSpan | `DateOnly_*`, `TimeOnly_*` | | `::date`, `- DATE '0001-01-01'` |
| Legacy mode: DateTime maps to timestamp; now/utcnow behave as before | LegacyTimestampQueryTest (3, PO) | `Npgsql.EnableLegacyTimestampBehavior` switch | |
| Date arithmetic: add years from a parameter, subtract TimeSpan, DateTime minus DateTime yields days, AT TIME ZONE plus addition | SA Misc `Select_expression_date_add_year_param`, `DateTime_subtract_TimeSpan`, `DateTimeFunction_subtract_DateTime`; Operators `AtTimeZone_and_addition` | interval arithmetic | `+ CAST(@y::text || ' years' AS interval)`, `date_part('day', …)` |
| TimeSpan totals (days, hours, minutes, seconds, ms); Sum and Average over TimeSpan; DateTime subtraction in a predicate | SA GearsOfWar `Where_TimeSpan_Total*`, `GroupBy_Property_Select_Sum/Average_over_TimeSpan`, `Where_datetime_subtraction` | interval aggregates | `date_part('epoch', d) / 86400.0`, `sum(interval)`, `avg(interval)` |
| Types/Temporal: DateOnly, DateTimeOffset, DateTime UTC and Unspecified, TimeOnly, TimeSpan column round-trips (spec RelationalTypeTestBase) | Types/Temporal | | |

### 2.10 NodaTime plugin (UseNodaTime)

| Behaviour | Tests | Concept |
|---|---|---|
| Instant: subtract Duration, InUtc, InZone (constant or parameter zone; fails without a LocalDateTime), ToDateTimeUtc, current instant, distance | InstantTranslations (9) | timestamptz, `AT TIME ZONE` |
| LocalDateTime / LocalDate / LocalTime: subtraction, components (Year…Second, DayOfYear, DayOfWeek), Date/Time, InZoneLeniently().ToInstant (constant or column zone), distance; LocalDate add Period, At, AtMidnight | LocalDateTime (14), LocalDate (12), LocalTime (4) | timestamp/date/time |
| ZonedDateTime: subtraction, components, Date, DayOfWeek, LocalDateTime, ToInstant, distance | ZonedDateTime (13) | |
| Duration: Total* and components; Sum and Average | Duration (11) | interval |
| Period: components and From* factories; Weeks/Milliseconds/Nanoseconds/Ticks and FromMilliseconds/Nanoseconds/Ticks are *not translated* (AssertTranslationFailed); Sum and Average | Period (25) | interval (`make_interval`) |
| DateInterval / Interval: covered in 2.2 | DateInterval (10), Interval (8) | ranges |
| Legacy-mode type mapping: timestamp and timestamptz both map to Instant; LocalDateTime does not map to timestamptz; literal generation including infinity and ranges | LegacyNpgsqlNodaTimeTypeMappingTest (6) | legacy switch |

### 2.11 Spatial (PostGIS via UseNetTopologySuite; gated RequiresPostgis)

| Behaviour | Tests | Concept |
|---|---|---|
| Geography: distance using the spheroid, KNN distance ordering, IsWithinDistance using the spheroid | SA SpatialQueryNpgsqlGeography `Distance_with_spheroid`, `DistanceKnn`, `IsWithinDistance_with_spheroid` | `EF.Functions.Distance(…, useSpheroid)`, `<->`, `ST_DWithin` |
| Geometry: Force2D; Any over a multi-linestring | SA Geometry `Force2D`, `MultiString_Any` | `ST_Force2D` |
| JSON points and geometries with M/Z and SRID | SA JsonTypes `Can_read_write_point*` | GeoJSON reader/writer |
| A database holding only PostGIS system tables counts as "no tables"; scaffolding ignores PostGIS system tables | DatabaseCreator `Returns_false_when_database_exists_and_has_only_postgis_tables`; Scaffolding `System_tables_are_ignored` | HasTables semantics |

### 2.12 Key generation: identity, serial, sequences, HiLo, xmin

| Behaviour | Tests | Concept | API |
|---|---|---|---|
| Insert with a sequence key, HiLo, a default taken from a sequence (key or non-key), identity columns behind uint/string value converters, explicit keys over generated ones, non-key defaults (read-only), a serial non-key column, client and server GUID keys | NpgsqlValueGenerationScenarios (13, PO; results only) | Value generation strategies | `UseHiLo`, `HasDefaultValueSql("nextval(...)")`, `uuid_generate_v4()` |
| A sequence works end to end: sync and async, multiple databases, concurrent contexts, explicit values, nullable keys (sequence and identity) | SequenceEndToEnd (7, PO) | HiLo/identity | `UseHiLo`, `UseIdentityColumn` |
| Temporary key values are replaced on save | SA StoreGeneratedFixup `Temp_values_are_replaced_on_save` | | |
| Server defaults (`now()` and others) populate on insert | DefaultValuesTest `Can_use_Npgsql_default_values` | `HasDefaultValueSql` | |
| Stored computed columns (including nulls and nullable enum) | ComputedColumnTest (3, PG12) | `HasComputedColumnSql(stored: true)` | |
| The system column xmin as a row version changes on every update; optimistic concurrency with a uint (xid) token and a bytes token: changing only the token is a no-op, the token updates for every entity sharing a row, and the original token is used when an owned instance is replaced | SystemColumnTest `Xmin`; OptimisticConcurrency (3 added, 2 classes) | xmin concurrency token | `[Timestamp] uint`, `IsRowVersion()` |
| Migrations: identity (by default or always, with options, add/alter/restart/change type), serial/bigserial (add, alter, in a schema, to identity), a system or oid column, insert-data restarts identity, smallint sequence | SA Migrations ~25 tests | Identity/serial DDL | `GENERATED BY DEFAULT AS IDENTITY (START WITH …)`, `ALTER … RESTART` |
| Scaffolding reads serial, identity (with options) and sequence types and facets | Scaffolding `SequenceSerial`, `SequenceNonSerial`, `Identity*`, `Sequence_types`, `Create_sequences_with_facets`, `Sequence_min_max_start_values_are_null_if_default` | Reverse engineering | |

### 2.13 DDL / migrations / database creation (provider additions)

| Behaviour | Tests | Concept |
|---|---|---|
| Table options: storage parameters (create and alter), unlogged/logged, a system column (ignored), oid | SA Migrations `Create_table_with_storage_parameter/unlogged/system_column/oid_column`, `Alter_table_*` | `HasStorageParameter`, `IsUnlogged` |
| Columns: upper-case store type, huge varchar, compression method (add, set, reset), alter type preserves collation, computed column collation | `Add_column_with_*`, `Alter_column_set_compression_method*`, `Alter_column_change_type_preserves_collation`, `Alter_column_computed_set_collation` | `UseCompressionMethod` |
| Indexes: INCLUDE (with filter, unique), CONCURRENTLY, method (gin/gist…), operator classes, collation, NULLS FIRST/LAST, NULLS NOT DISTINCT (PG15), storage parameters, a legacy descending annotation, tsvector GIN | `Create_index_*` (14) | `IncludeProperties`, `IsCreatedConcurrently`, `HasMethod`, `HasOperators`, `HasNullSortOrder`, `AreNullsDistinct` |
| Extensions (with schema), collations (deterministic and non-deterministic PG12, drop; alter throws), the public schema is not created | `Ensure_postgres_extension*`, `Create_collation*`, `Drop_collation`, `Alter_collation_throws`, `Create_schema_public_is_ignored` | `HasPostgresExtension`, `HasCollation` |
| CREATE DATABASE with a collation, template or tablespace; idempotent schema and serial-to-identity scripts; CockroachDB interleave; serial on the old annotation throws | SA MigrationsSqlGenerator (8) | `UseDatabaseTemplate`, `UseTablespace`, `UseCockroachDbInterleaveInParent` |
| An empty migration creates the database | SA MigrationsInfrastructure `Empty_Migration_Creates_Database` | |
| Database creator: exists, has-tables, create, create-tables-only, delete; throws PostgresException when the database is missing or already exists; generated create script | NpgsqlDatabaseCreatorTest (16, PO) | `IRelationalDatabaseCreator` |
| Scaffolding: tables, views, materialized views, columns (domains, precision, max length, datetime precision, defaults, computed values, nullability), keys, unique constraints, indexes (method, operators, collation, descending, null sort, covering, nulls distinct, filter), foreign keys (actions), comments, column collation (the database default collation is not scaffolded), dropped columns, extensions, enums, filtering by schema or table (quoted names), warnings for missing objects, regression test Bug453, column default type names | NpgsqlDatabaseModelFactoryTest (69, PO; asserts the database model, not SQL) | `IDatabaseModelFactory` |

### 2.14 Connection, execution strategy, batching, logging, misc infrastructure

| Behaviour | Tests | Concept |
|---|---|---|
| A connection string or connection is supplied in OnConfiguring (with or without the default service provider); missing configuration throws; options can be injected; an admin connection can be built from a data source, connection string or connection | ConnectionSpecificationTest (13) | `UseNpgsql` overloads, `UseAdminDatabase` |
| An existing connection that is open or closed is used without being closed by EF | ExistingConnectionTest (2) | |
| Retry strategy: commit failure handled (sync, async, multiple SaveChanges, with verification); retries for SaveChanges, query, FromSqlRaw, OpenConnection and BeginTransaction; verification shares the retry limit (RetryLimitExceededException) | ExecutionStrategyTest (9) | `EnableRetryOnFailure`, `NpgsqlRetryingExecutionStrategy` |
| Batching: inserts batched, mixed inserts and updates batched, mixed database types, MinBatchSize threshold with log messages and statement count | BatchingTest (4) | `MinBatchSize`/`MaxBatchSize` |
| Context-initialization log lines show provider options: admin db, PostgresVersion, certificate/password callbacks, ReverseNullOrdering, user range definitions | SA LoggingNpgsqlTest (7) | Option introspection |
| Navigation fixup does not duplicate entries (principal or dependent) | NavigationTest (2) | |
| A stored procedure with a rows-affected parameter plus another output parameter | SA StoredProcedureUpdate `Rows_affected_parameter_with_another_output_parameter` | PG procedures with OUT/INOUT |
| A lateral join over a table is rewritten as a subquery; set operations with NULLs in the leftmost branch get an explicit type (`NULL::int`); GroupBy+OrderBy+Average puts ORDER BY inside the aggregate | SA Misc `Lateral_join_with_table_is_rewritten_with_subquery`; SetOps `Leftmost_nulls_*` (5); GroupBy `GroupBy_OrderBy_Average` | Typed NULLs in UNION; ordered-set aggregates |

## 3. Divergences from the shared spec

Counts are override methods. "ATF" means the test expects `AssertTranslationFailed` where the base expects a result. "Throws" means it expects an exception or an assertion failure where the base passes. "Skip" means unconditionally skipped.

### 3.1 Translation gaps (the base translates; PG expects translation failure)

| Area | Overrides | Reason given in the source |
|---|---|---|
| Math | `Round_with_digits_double/float/decimal`, `Log_float`, `Log_with_newBase(_float)`, `Log2`, `Acosh`, `Asinh`, `Atanh`, `Cosh`, `Sinh`, `Tanh` (13) | round(v, s) and log(b, x) exist only over numeric; hyperbolic functions are not translated |
| String | `IndexOf_with_*starting_position*` (5), `IndexOf_with_one_parameter_arg_char` | strpos has no start position |
| String (char overloads and literal forms) | `Replace(_Char)`, `StartsWith_Literal(_Char)`, `StartsWith_Parameter_Char`, `EndsWith_Literal(_Char)`, `EndsWith_Parameter(_Char)`, `Contains_Literal(_Char)`; `IndexOf`, `IndexOf_Char` throw | "TODO #3547": these char overloads are not translated. The base expectations were written for case-insensitive SQL Server |
| DateTime / DateTimeOffset / TimeOnly / TimeSpan components | `Second`, `Millisecond` on DateTime (both with and without time zone); DTO `Second`, `Millisecond`, `Microsecond`, `Nanosecond`, `ToUnixTimeMilliseconds`, `ToUnixTimeSecond`; TimeOnly `Second`, `Millisecond`, `Microsecond`, `Nanosecond`; TimeSpan `Microseconds`, `Nanoseconds`; Northwind `Where_nanosecond_and_microsecond_component` | Fractional-second extraction is not implemented ("too annoying", #3406); PG precision is microseconds |
| Bitwise | `Left_shift`, `Right_shift` | Not translated |
| Misc | `Convert_ToString`, `Convert_ToInt64` (DateTime), plus `Convert_ToBoolean/Byte/Decimal/Double/Int16` expecting failures | Convert on some source types is unsupported |
| Anonymous/tuple constant comparison | Northwind Where `Where_compare_constructed_*`, `Where_compare_tuple_create_constructed_*` (6) | Issue #14672. Note that PG *does* support row values through the explicit `EF.Functions` API (2.7) |
| Client-eval patterns | `GroupJoin_client_method_in_OrderBy` (2 suites), `Client_code_unknown_method`, `Member_binding_after_ctor_arguments_fails_with_client_eval`, `Contains_with_local_anonymous_type_array_closure`, `Select_DTO_constructor_distinct_…_after_client_eval`, `Value_conversion_on_enum_collection_contains` | Same as the relational defaults or provider-specific limits |

### 3.2 Expected exceptions (the base succeeds or expects a different exception)

| Area | Overrides | Reason |
|---|---|---|
| timestamptz offset rule | AdHocAdvancedMappings `Query_generates_correct_datetime2/datetimeoffset_parameter_definition`, `Projecting_*converter*`, `Projecting_one_of_two_similar_complex_types…` (6); GearsOfWar/TPC/TPT `DateTimeOffsetNow_minus_timespan`, `DateTimeOffset_Date_returns_datetime`; DTO `Now`, `UtcNow`, `Date` | Only a DateTimeOffset with offset 0 can be written to timestamptz; there is no non-UTC DateTimeOffset now() |
| DateTime kind rule | DateTimeTranslations `UtcNow`, `Today`, `subtract_and_TotalDays`, `Parse_with_constant/parameter`; AdHocJson `OnModelCreating` | Local or Unspecified DateTime cannot be compared with timestamptz |
| Nested collections | JsonTypes: 24 nested array/list tests expect EqualException; JsonUpdate `Edit_single_property_collection_of_collection_*` (11, NotEqualException), `Edit_single_property_relational_collection_of_*` (18, EqualException, properties ignored in the model); ModelBuilding `Mapping_throws_for_non_ignored_three_dimensional_array`; NonSharedPrimitive `Multidimensional_array_is_not_supported` | Arrays of arrays are not supported. Lists outside JSON map to arrays only for supported element types |
| NUL character in text | JsonUpdate `Edit_single_property_collection_of_char`, `Add_and_update_nested_optional_primitive_collection` | PG text cannot contain `\0` (untranslatable_character) |
| Computed columns in migrations | Migrations `Create_table_with_computed_column`, `Add_column_with_computedSql`, `Add_column_computed_with_collation`, `Alter_column_make_computed`, `Alter_column_change_computed(_type)`, `Alter_column_make_non_computed`, `Alter_computed_column_add_comment`, `Drop_column_computed_and_non_computed_with_dependency` (9) | Only *stored* generated columns exist (virtual is PG18), and altering a computed column needs drop/add. Overrides assert NotSupported or the stored DDL |
| Migrations type conversion | `Convert_string_column_to_a_json_column_containing_*` (3) | `column cannot be cast automatically to type jsonb` |
| Stored procedures | `Insert_with_result_column`, `Insert_with_two_result_columns`, `Insert_with_output_parameter_and_result_column`, `Update_with_output_parameter_and_rows_affected_result_column(_concurrency_failure)`, `Rows_affected_result_column(_and_concurrency_failure)`, `Rows_affected_return_value(_and_concurrency_failure)`, `Tph`, `Tpt` (11); ModelBuilding `Can_use_sproc_mapping_with_owned_reference` | PG procedures cannot return result sets or return values; only OUT parameters (PG14+) |
| Bad JSON handling | AdHocJson `Bad_json_properties_*` (6), `Project_*missing*/null_required*` (12), `Try_project_*` (2) | Different exception types and messages from jsonb (duplicate keys collapse, so some base cases become NotSupported) |
| FromSql/SqlQuery column-case mismatch | `FromSqlRaw_queryable_simple_different_cased_columns_and_not_enough_columns_throws`, SqlQuery `…columns_out_of_order_and_extra_columns`, `…different_cased_columns…` | Unquoted identifiers fold to lower case, so the failure happens at a different stage |
| Query-filter list | QueryFilterFuncletization `DbContext_list_is_parameterized` | PG sends the list as an array parameter, so a null list fails differently from the base NRE |
| Misc relational | Northwind Misc `Client_code_using_instance_*`, `Max_on_empty_sequence_throws`, `Entity_equality_through_subquery_composite_key`; Join `Join_local_collection_int_closure_is_cached_correctly`; Aggregate `Contains_with_local_tuple_array_closure`, `Contains_with_local_enumerable_inline`; Include `Include_collection_with_last_no_orderby` (x3 suites); KeylessEntities `KeylessEntity_with_nav_defining_query`; Select `Correlated_collection_after_distinct_…`; SetOps `Client_eval_Union_FirstOrDefault`; ComplexNavigations `Join_with_result_selector_returning_queryable_throws_validation_error` (x2); AdHocNavigations `Reference_include_on_derived_type_with_sibling_works`; PrimitiveCollections `Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer`; JsonQuery `Json_collection_index_in_projection_using_untranslatable_client_method(2)`; OwnedNavigations SetOps `Over_associate_collection_projected`; ComplexTableSplitting BulkUpdate `Update_inside_primitive_collection` (#3622) | Mostly mirrors relational behaviour with PG-specific exception types or messages |
| Transactions | `SaveChanges_can_be_used_with_no_savepoint` | After an error PG aborts the transaction (25P02), so continuing without a savepoint throws |
| Migrations infrastructure | `Can_apply_two_migrations_in_transaction(_async)` | #3407: the database drop needs PostgresVersion, which `CreateEmptyContext` lacks |
| Spatial | `Mutation_of_tracked_values_does_not_mutate_values_in_store` | NTS mutation semantics |

### 3.3 Different results or SQL shape (passes, but with provider-specific expectations)

| Divergence | Where | Effect on a shared API |
|---|---|---|
| **Null ordering**: PG sorts NULLs last on ASC. The test store sets `ReverseNullOrdering()` globally, so every ORDER BY in every baseline carries `NULLS FIRST` (ASC) / `NULLS LAST` (DESC) | `NpgsqlTestStore.AddProviderOptions`, TransactionNpgsqlTest | Spec result comparisons assume SQL Server/.NET null ordering. A neutral core needs a null-ordering switch |
| **Count type**: `count(*)::int`, `COALESCE(sum(x), 0)::int`; `avg(x::double precision)` | all SQL baselines | PG count returns bigint |
| Typed NULL in the leftmost UNION branch (`NULL::int`) | SetOps `Leftmost_nulls_*` | |
| FromSql with DateTime parameters must send `timestamp without time zone` | FromSql `*_composed_with_parameters_and_closure_parameters*` (4) | DateTime defaults to timestamptz |
| JSON temporal/TimeSpan value tests replaced with `*_npgsql` variants; the base theory tests are no-ops (`Task.CompletedTask`) | JsonTypes ~20 overrides | PG uses a different JSON text format for DateTime/DTO/TimeSpan/DateOnly |
| Spatial geography suite rewritten: 52 overrides do not call the base (geography supports a subset of functions; results use the spheroid) | SpatialQueryNpgsqlGeographyTest | |
| Bulk updates: ExecuteUpdate/Delete with joins become `UPDATE … FROM`/`DELETE … USING`; 5 Northwind overrides customised | BulkUpdates/* (SQL heavy) | |
| Parameterized collections are sent as a single array parameter (`= ANY(@p)`), not an expanded IN list; tests set `UseParameterizedCollectionMode` explicitly | PrimitiveCollections, AdHocMiscellaneous, NonSharedPrimitive | |
| Stored computed columns are required by the TableSplitting suite (gated PG12) | TableSplittingNpgsqlTest | |
| StoreValueGeneration baselines branch on PG ≥ 12 | Update/StoreValueGeneration | |

### 3.4 Unconditional skips

| Suite | Tests | Reason |
|---|---|---|
| BuiltInDataTypes (6), ConvertToProviderTypes (3), CustomConverters (4), ValueConvertersEndToEnd (1) | `Can_insert_and_read_back_*backed_data_types`, `Can_query_using_any_data_type*_shadow`, `Can_insert_and_read_back_with_conversions` | DateTimeOffset with a non-zero offset (efcore#26068) |
| EntitySplittingQuery (20) | `*_owning_a_split_*` | Issue29075 (also skipped upstream) |
| ConnectionInterception (4) | `Intercept_connection_creation_*`, `…override_creation`, `…after_creation` | #2368: the data-source path does not go through connection creation interception |
| MigrationsInfrastructure (5) | `Can_apply_all_migrations(_async)`, `Can_apply_range_of_migrations`, `Can_revert_*` | efcore#33056 |
| Migrations (2) | `Add_required_primitive_collection_with_custom_converter_to_existing_table` (+typo variant) | #33038 |
| SqlQuery (4), FromSql (2) | `SqlQueryRaw_annotations_do_not_affect_successive_calls`, `SqlQuery_parameterization_issue_12213`, `Bad_data_error_handling_invalid_cast(_projection)` | efcore PR 30384 / issues 6563, 20364 |
| UdfDbFunctions (3) | `Scalar_Function_Where_Not_Correlated_*`, `Scalar_Function_with_nullable_value_return_type_throws` | efcore#25980, PR 30388 |
| ComplexNavigationsSharedType (3) | `Subquery_with_Distinct_Skip_FirstOrDefault_without_OrderBy`, `GroupBy_aggregate_where_required_relationship(_2)` | efcore#26353, #26104 |
| NorthwindGroupBy (1), AdHocMiscellaneous (1), LazyLoadProxy (1) | `Select_nested_collection_with_groupby`, `StoreType_for_UDF_used`, `Lazy_load_one_to_one_reference_with_recursive_property` | upstream PRs; "possibly requires MARS" |
| JsonPoco (1), FullText (1), PrimitiveCollections (2) | individual skips inside provider suites | |
| CompiledModelNpgsqlTest | the whole file is commented out | Compiled-model baselines are not maintained |

## 4. Conditions and flags

| Gate | Mechanism | What it gates |
|---|---|---|
| `[MinimumPostgresVersion(10,0)]` | `ITestCondition` against `TestEnvironment.PostgresVersion` (queried live from the server) | macaddr8 network tests (9); scaffolding identity (3) |
| `(11,0)` | | websearch_to_tsquery (3), strict word similarity in trigrams (5), scaffolding covering index / sequence types / sequence defaults (3) |
| `(12,0)` | | **class** ComputedColumnTest and TableSplittingNpgsqlTest (stored generated columns); non-deterministic collation (Collate_case_insensitive, Create_collation_non_deterministic); `Where_datetime_date_on_timestamptz`; scaffolding computed columns (2) |
| `(13,0)` | | **class** LTreeTranslations; `Can_generate_no_migration_script` (DROP DATABASE … WITH FORCE) |
| `(14,0)` | | **class** Multirange, Cube (binary cube I/O), StoredProcedureUpdate (OUT parameters in procedures); range_agg/range_intersect_agg (Range and NodaTime, 6); parameter collection of ranges |
| `(15,0)` | | regexp_count (9); NULLS NOT DISTINCT index (migrations and scaffolding) |
| Runtime version branches | `TestEnvironment.PostgresVersion` checks inside tests | Guid v7 (uuidv7 ≥18, gen_random_uuid ≥13); Migrations (≥11 INCLUDE, ≥12 stored, <14 compression, ≥18 virtual-generated); Northwind store adds a non-deterministic collation for ≥12; StoreValueGeneration ≥12 |
| `SetPostgresVersion(TestEnvironment.PostgresVersion)` | Fixtures: Northwind, BasicTypes, Spatial, DatabaseCreator, DefaultValues, Migrations | Makes the generated SQL match the server version (the provider default is "latest") |
| `[RequiresPostgis]` | `pg_available_extensions` contains postgis, or env `NPGSQL_TEST_POSTGIS=1/true` | Classes SpatialNpgsqlTest, SpatialQueryGeography and SpatialQueryGeometry (about 170 tests); DatabaseCreator postgis-only tables; Scaffolding System_tables_are_ignored |
| `[PlatformSkipCondition(Windows)]` | | Collate_case_insensitive (ICU collation) |
| `ReverseNullOrdering()` | **Every** test-store context (NpgsqlTestStore) | Makes all spec result comparisons match SQL Server/.NET null ordering. Internal API in 10.0.3 |
| `UseQuerySplittingBehavior(SingleQuery)`, `CommandTimeout(600)` | `ApplyConfiguration()` on all contexts | |
| `UseRedshift()` | CompatibilityQuery | Array parameters expand into scalar parameters |
| `UseParameterizedCollectionMode(mode)` | AdHocMiscellaneous, NonSharedPrimitiveCollections | Theory over the constant / parameter / multiple-parameters modes |
| `MapEnum`, `MapRange`, `UseNodaTime`, `UseNetTopologySuite` | Fixture `AddOptions` | Must be set on the EF options so EF builds the NpgsqlDataSource itself; the comments explain this |
| AppContext `Npgsql.EnableLegacyTimestampBehavior` | LegacyTimestampQueryTest, LegacyNpgsqlNodaTimeTypeMappingTest | Swaps the DateTime/Instant mapping in a separate fixture |
| Extensions required on the server | citext, ltree, cube, hstore, pg_trgm, fuzzystrmatch, unaccent, btree_gist, postgis, uuid-ossp | Created through `HasPostgresExtension` in fixtures; tests fail (not skip) when the extension is unavailable |

## 5. Test infrastructure

| Component | Role | PostgreSQL-specific? |
|---|---|---|
| `TestEnvironment` | Reads `config.json`, `config.test.json` and environment variables (`Test:Npgsql:DefaultConnection`, default `Server=localhost;Username=npgsql_tests;Password=npgsql_tests;Port=5432`); live server version; PostGIS probe; forces en-US culture | Version and PostGIS probes are PG-specific. The config pattern is generic |
| `NpgsqlTestStore` (RelationalTestStore) | Creates the database through the admin `postgres` database (`CREATE DATABASE`); polls until it exists, clearing pools; drops with `pg_terminate_backend` + `DROP DATABASE`; script execution (Northwind.sql); `additionalSql` hook; `AddProviderOptions`, which **always applies ReverseNullOrdering** | Admin db, termination of backends, pool clearing and null ordering are PG-specific |
| `NpgsqlNorthwindTestStoreFactory` and `Northwind.sql` | A PG dialect Northwind script plus additional SQL (a non-deterministic ICU collation on PG ≥12) | Script dialect |
| `NpgsqlDatabaseCleaner` (RelationalDatabaseCleaner) | Drops tables, sequences and types (enums, ranges, extensions) through `information_schema`/`pg_catalog` | PG catalogs; also has to remove enum, range and extension objects |
| `NpgsqlTestHelpers`, `NpgsqlTestStoreFactory`, `NpgsqlPrecompiledQueryTestHelpers` | Provider wiring for the spec harness (UseNpgsql, service registration, precompiled-query references) | Thin |
| `NpgsqlDbContextOptionsBuilderExtensions.ApplyConfiguration` | SingleQuery splitting and a 600 s timeout | Generic |
| `NpgsqlDatabaseFacadeExtensions.EnsureClean` | Clean through the execution strategy | Generic |
| `TestNpgsqlRetryingExecutionStrategy` | Retries on test-server transient errors; used by `ExecuteWithStrategyInTransaction` | SQLSTATE list |
| `TestNpgsqlConnection`, `TestRelationalCommandBuilderFactory`, `TestRelationalTransaction` | Fault injection for ExecutionStrategyTest (fail on open, execute, commit with chosen error codes) | Error codes are PG-specific |
| `MinimumPostgresVersionAttribute`, `RequiresPostgisAttribute` | xunit `ITestCondition` gates | Yes |
| Fixtures: `NpgsqlFixture`, `F1NpgsqlFixture` (xmin uint and bytea row versions), `SpatialNpgsqlFixture`, `NodaTimeQueryNpgsqlFixture`, `ArrayQueryFixture`, `BasicTypesQueryNpgsqlFixture`, plus one `*NpgsqlFixture` per spec fixture (Northwind, GearsOfWar, TPC/TPT, ManyToMany, Inheritance, BulkUpdates, Associations) | Model customisation: column types (jsonb, timestamptz vs timestamp), `HasPostgresExtension`, enum and range mapping, store-type overrides for DateTimeOffset/DateTime kinds | Model tweaks are PG-specific: kinds, xmin, extensions |
| `TestModels/Array`, `TestModels/NodaTime`, `NorthwindNpgsqlContext` | Provider-only test models and seed data (`ISetSource` expected data for AssertQuery) | Array and NodaTime models |
| SQL capture | `TestSqlLoggerFactory` + `AssertSql` (spec infrastructure); `Check_all_tests_overridden` in 80 classes enforces baseline coverage | Generic |

### Porting notes for a homogenized API (derived from the above)
- Null ordering must be a provider switch that the shared spec harness turns on for PostgreSQL, and also for SQLite, MySQL and DuckDB, whose defaults differ from SQL Server. Otherwise the 6,800 results-only tests diverge.
- DateTime kind and DateTimeOffset offset rules are enforced at write and compare time. A neutral core must either expose a "temporal strictness" capability or normalise values; at least 30 spec overrides depend on it.
- Arrays as a first-class column type, with `= ANY(@array)` parameter semantics, are the basis for primitive collections, IN-list parameterisation, LIKE ANY and the overlap and containment operators.
