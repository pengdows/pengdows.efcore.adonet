# Microsoft.EntityFrameworkCore.Sqlite 10.0.11: the test suite as a behaviour record

Sources read (read-only, nothing executed): `test/EFCore.Sqlite.FunctionalTests` (372 .cs files, 360 with a class), `test/EFCore.Sqlite.Tests` (20 files, exported from tag v10.0.11), and for base-class resolution `EFCore.Specification.Tests`, `EFCore.Relational.Specification.Tests`, `EFCore.Relational.Tests` and `EFCore.Tests` (exported read-only for counting only). Concept names follow `api-inventory/sqlite.md`.

Counting method: a "test" is a method carrying `[ConditionalFact]`/`[ConditionalTheory]`/`[Fact]`/`[Theory]`. Theory rows are **not** expanded, so the real xUnit case count is several times higher. "Inherited" = test methods reachable through the spec base-class chain; "Overridden" = those the SQLite class overrides (nearly always to add an `AssertSql` baseline, sometimes to change the expectation); "Provider-only" = test methods declared only in the SQLite class.

"Asserts" in section 1, and "Asserted by" in sections 2 and 3, use these values:
- **SQL**: an `AssertSql` baseline, or a generated-SQL string compared exactly.
- **Results**: query or saved data compared with expected data, usually LINQ-to-Objects over the seeded data (`AssertQuery`, `AssertCount`), or a value read back.
- **Both**: the base `AssertQuery`/`Test` compares results, and the SQLite override adds an `AssertSql` baseline. This is the default shape of every Query/Translations/BulkUpdates/Migrations override.
- **Exception/message**: an exception type is expected, and usually its exact message too (`SqliteStrings.*`, `CoreStrings.*`, `RelationalStrings.*`).
- **Model**: metadata, annotations or a validation warning or error.

## Totals

| Project | Classes with tests | Inherited test methods | of which overridden | Provider-only test methods |
|---|---:|---:|---:|---:|
| FunctionalTests (script count) | ~270 | 13,842 | 2,382 | 239 |
| + corrections: `Types/*` (16 classes per file pair, only first counted: about +91), `ModelBuilding` nested classes (9 nested suites, about +736 inherited, +10 provider-only) | | ~14,670 | ~2,390 | ~249 |
| Unit tests (EFCore.Sqlite.Tests) | 20 | 452 | 15 | 80 |
| **Total** | | **~15,100 inherited** | **~2,400** | **~330 provider-only** |

Caveats: abstract SQLite bases (GraphUpdatesSqliteTestBase, ManyToManyLoadSqliteTestBase, interception bases) are counted once as a base and once per concrete subclass. Nested concrete classes (for example `CommandInterceptionSqliteTest.InterceptionSqliteTest` and `...WithDiagnostics`) run their base twice and are counted once. Of the provider-only methods, 30 are `Check_all_tests_overridden` guards, which are infrastructure and not behaviour.

**SQL-heavy suites.** Query/Translations/*, GearsOfWar (596 baselines), PrimitiveCollections, Spatial, ComplexType, Json, BulkUpdates/*, Update/JsonUpdate and StoreValueGeneration, Migrations and MigrationsSqlGenerator, and NullSemantics. Each of these has a `Check_all_tests_overridden` guard, so every base test there carries an SQLite SQL baseline. **Results-only suites.** Load, Find, PropertyValues, GraphUpdates, ManyToMany*, ComplexTypesTracking, FieldsOnlyLoad, StoreGeneratedFixup, Transaction, ConcurrencyDetector, Interception, DataBinding, KeysWithConverters, WithConstructors, Northwind Include/AsNoTracking/Compiled/Navigations, Inheritance*, ComplexNavigations (non-collections), Owned*, and the TPC/TPT relationships suites.

---

## 1. Suite map

Columns: inherited/overridden/provider-only test-method counts, the number of `AssertSql(` calls in the file, and the dominant assertion style. Rows with 0/0/0 and a spec base are usually shells (abstract generic hosts, nested-class files): see the corrections above.

### EFCore.Sqlite.FunctionalTests


#### (root)

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| AutoincrementTest | provider-only | 0 | 0 | 1 | 0 | Results only |
| BadDataJsonDeserializationSqliteTest | BadDataJsonDeserializationTestBase | 5 | 0 | 0 | 0 | Results only |
| BuiltInDataTypesSqliteTest | BuiltInDataTypesTestBase | 28 | 1 | 60 | 16 | SQL baselines (+results) |
| CommandConfigurationTest | provider-only | 0 | 0 | 1 | 0 | Results only |
| CommandInterceptionSqliteTestBase | CommandInterceptionTestBase | 27 | 0 | 0 | 3 | Mostly results; few SQL baselines |
| ComplexTypesTrackingSqliteTest | ComplexTypesTrackingRelationalTestBase | 128 | 0 | 0 | 0 | Results only |
| CompositeKeyEndToEndSqliteTest | CompositeKeyEndToEndTestBase | 3 | 1 | 0 | 0 | Results only |
| ConcurrencyDetectorDisabledSqliteTest | ConcurrencyDetectorDisabledRelationalTestBase | 9 | 0 | 0 | 0 | Results only |
| ConcurrencyDetectorEnabledSqliteTest | ConcurrencyDetectorEnabledRelationalTestBase | 9 | 0 | 0 | 0 | Results only |
| ConferencePlannerSqliteTest | ConferencePlannerTestBase | 22 | 0 | 0 | 0 | Results only |
| ConnectionInterceptionSqliteTestBase | ConnectionInterceptionTestBase | 9 | 0 | 0 | 0 | Results only |
| ConvertToProviderTypesSqliteTest | ConvertToProviderTypesTestBase | 30 | 0 | 0 | 0 | Results only |
| CustomConvertersSqliteTest | CustomConvertersTestBase | 57 | 10 | 0 | 9 | Mostly results; few SQL baselines |
| DataAnnotationSqliteTest | DataAnnotationRelationalTestBase | 83 | 5 | 0 | 3 | Mostly results; few SQL baselines |
| DataBindingSqliteTest | DataBindingTestBase | 38 | 0 | 0 | 0 | Results only |
| DefaultValuesTest | SharedStoreFixtureBase | 0 | 0 | 1 | 0 | Results only |
| DesignTimeSqliteTest | DesignTimeTestBase | 2 | 0 | 0 | 0 | Results only |
| EntitySplittingSqliteTest | EntitySplittingTestBase | 2 | 0 | 0 | 0 | Results only |
| F1ULongSqliteFixture | F1RelationalFixture | 0 | 0 | 0 | 0 | Results only |
| FieldsOnlyLoadSqliteTest | FieldsOnlyLoadTestBase | 120 | 0 | 0 | 0 | Results only |
| FindSqliteTest | FindTestBase | 69 | 0 | 0 | 0 | Results only |
| JsonTypesSqliteTest | JsonTypesRelationalTestBase | 244 | 32 | 0 | 0 | Results only |
| KeysWithConvertersSqliteTest | KeysWithConvertersTestBase | 47 | 0 | 0 | 0 | Results only |
| LazyLoadProxySqliteTest | LazyLoadProxyRelationalTestBase | 75 | 0 | 5 | 0 | Results only |
| LoadSqliteTest | LoadTestBase | 203 | 0 | 0 | 0 | Results only |
| LoggingSqliteTest | LoggingRelationalTestBase | 13 | 0 | 1 | 0 | Results only |
| ManyToManyFieldsLoadSqliteTest | ManyToManyFieldsLoadTestBase | 23 | 0 | 0 | 0 | Results only |
| ManyToManyLoadProxySqliteTest | ManyToManyLoadTestBase | 48 | 0 | 0 | 0 | Results only |
| ManyToManyLoadSqliteTest | ManyToManyLoadTestBase | 48 | 0 | 0 | 0 | Results only |
| ManyToManyLoadSqliteTestBase | ManyToManyLoadTestBase | 48 | 0 | 0 | 0 | Results only |
| ManyToManyTrackingSqliteTest | ManyToManyTrackingRelationalTestBase | 67 | 0 | 0 | 0 | Results only |
| MaterializationInterceptionSqliteTest | MaterializationInterceptionTestBase | 7 | 1 | 0 | 0 | Results only |
| ModelBuilding101SqliteTest | ModelBuilding101RelationalTestBase | 124 | 0 | 0 | 0 | Results only |
| MonsterFixupChangedOnlySqliteTest | MonsterFixupTestBase | 12 | 0 | 0 | 0 | Results only |
| MusicStoreSqliteTest | MusicStoreTestBase | 16 | 0 | 0 | 0 | Results only |
| NonLoadingNavigationsManyToManyLoadSqliteTest | ManyToManyLoadTestBase | 48 | 0 | 0 | 0 | Results only |
| NotificationEntitiesSqliteTest | NotificationEntitiesTestBase | 2 | 0 | 0 | 0 | Results only |
| OptimisticConcurrencyULongSqliteTest | OptimisticConcurrencyRelationalTestBase | 31 | 0 | 0 | 2 | Mostly results; few SQL baselines |
| OverzealousInitializationSqliteTest | OverzealousInitializationTestBase | 1 | 0 | 0 | 0 | Results only |
| PropertyValuesSqliteTest | PropertyValuesRelationalTestBase | 171 | 0 | 0 | 0 | Results only |
| QueryExpressionInterceptionSqliteTestBase | QueryExpressionInterceptionTestBase | 4 | 0 | 0 | 0 | Results only |
| SaveChangesInterceptionSqliteTestBase | SaveChangesInterceptionTestBase | 6 | 0 | 0 | 0 | Results only |
| SeedingSqliteTest | SeedingTestBase | 2 | 0 | 0 | 0 | Results only |
| SerializationSqliteTest | SerializationTestBase | 1 | 0 | 0 | 0 | Results only |
| SpatialSqliteFixture | SpatialFixtureBase | 0 | 0 | 0 | 0 | Results only |
| SpatialSqliteTest | SpatialTestBase | 5 | 0 | 0 | 0 | Results only |
| SqliteApiConsistencyTest | ApiConsistencyTestBase | 19 | 0 | 0 | 0 | Results only |
| SqliteComplianceTest | RelationalComplianceTestBase | 2 | 0 | 0 | 0 | Results only |
| SqliteDatabaseCreatorTest | provider-only | 0 | 0 | 8 | 0 | Results only |
| SqliteServiceCollectionExtensionsTest | RelationalServiceCollectionExtensionsTestBase | 3 | 0 | 0 | 0 | Results only |
| SqliteValueGenerationScenariosTest | provider-only | 0 | 0 | 13 | 0 | Results only |
| StoreGeneratedFixupSqliteTest | StoreGeneratedFixupRelationalTestBase | 118 | 0 | 1 | 0 | Results only |
| StoreGeneratedSqliteTest | StoreGeneratedTestBase | 60 | 1 | 1 | 0 | Results only |
| TPTTableSplittingSqliteTest | TPTTableSplittingTestBase | 26 | 1 | 0 | 0 | Results only |
| TableSplittingSqliteTest | TableSplittingTestBase | 26 | 1 | 0 | 1 | Mostly results; few SQL baselines |
| TransactionInterceptionSqliteTestBase | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | Results only |
| TransactionSqliteTest | TransactionTestBase | 44 | 0 | 0 | 0 | Results only |
| TwoDatabasesSqliteTest | TwoDatabasesTestBase | 3 | 0 | 0 | 0 | Results only |
| ValueConvertersEndToEndSqliteTest | ValueConvertersEndToEndTestBase | 25 | 0 | 1 | 0 | Results only |
| WithConstructorsSqliteTest | WithConstructorsTestBase | 41 | 0 | 0 | 0 | Results only |

#### BulkUpdates

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| NonSharedModelBulkUpdatesSqliteTest | NonSharedModelBulkUpdatesRelationalTestBase | 17 | 17 | 1 | 19 | SQL baselines (+results) |
| NorthwindBulkUpdatesSqliteFixture | NorthwindBulkUpdatesRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| NorthwindBulkUpdatesSqliteTest | NorthwindBulkUpdatesRelationalTestBase | 94 | 94 | 1 | 90 | SQL baselines heavy (+results) |
| TPCFiltersInheritanceBulkUpdatesSqliteFixture | TPCInheritanceBulkUpdatesFixture | 0 | 0 | 0 | 0 | Results only |
| TPCFiltersInheritanceBulkUpdatesSqliteTest | TPCFiltersInheritanceBulkUpdatesTestBase | 18 | 18 | 1 | 20 | SQL baselines (+results) |
| TPCInheritanceBulkUpdatesSqliteFixture | TPCInheritanceBulkUpdatesFixture | 0 | 0 | 0 | 0 | Results only |
| TPCInheritanceBulkUpdatesSqliteTest | TPCInheritanceBulkUpdatesTestBase | 20 | 20 | 1 | 22 | SQL baselines (+results) |
| TPHFiltersInheritanceBulkUpdatesSqliteFixture | TPHInheritanceBulkUpdatesFixture | 0 | 0 | 0 | 0 | Results only |
| TPHFiltersInheritanceBulkUpdatesSqliteTest | FiltersInheritanceBulkUpdatesRelationalTestBase | 18 | 18 | 1 | 20 | SQL baselines (+results) |
| TPHInheritanceBulkUpdatesSqliteFixture | TPHInheritanceBulkUpdatesFixture | 0 | 0 | 0 | 0 | Results only |
| TPHInheritanceBulkUpdatesSqliteTest | TPHInheritanceBulkUpdatesTestBase | 20 | 20 | 1 | 22 | SQL baselines (+results) |
| TPTFiltersInheritanceBulkUpdatesSqliteFixture | TPTInheritanceBulkUpdatesFixture | 0 | 0 | 0 | 0 | Results only |
| TPTFiltersInheritanceBulkUpdatesSqliteTest | TPTFiltersInheritanceBulkUpdatesTestBase | 18 | 18 | 1 | 18 | SQL baselines (+results) |
| TPTInheritanceBulkUpdatesSqliteFixture | TPTInheritanceBulkUpdatesFixture | 0 | 0 | 0 | 0 | Results only |
| TPTInheritanceBulkUpdatesSqliteTest | TPTInheritanceBulkUpdatesTestBase | 20 | 20 | 1 | 20 | SQL baselines (+results) |

#### GraphUpdates

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| GraphUpdatesSqliteChangedChangingNotificationsTest | GraphUpdatesTestBase | 127 | 0 | 0 | 0 | Results only |
| GraphUpdatesSqliteChangedNotificationsTest | GraphUpdatesTestBase | 127 | 0 | 0 | 0 | Results only |
| GraphUpdatesSqliteFullWithOriginalsNotificationsTest | GraphUpdatesTestBase | 127 | 0 | 0 | 0 | Results only |
| GraphUpdatesSqliteSnapshotNotificationsTest | GraphUpdatesTestBase | 127 | 0 | 0 | 0 | Results only |
| GraphUpdatesSqliteTestBase | GraphUpdatesTestBase | 127 | 6 | 0 | 0 | Results only |

#### Migrations

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| MigrationsInfrastructureSqliteTest | MigrationsInfrastructureTestBase | 25 | 13 | 0 | 0 | Results only |
| MigrationsSqliteTest | MigrationsTestBase | 127 | 93 | 6 | 88 | SQL baselines heavy (+results) |
| SqliteMigrationsSqlGeneratorTest | MigrationsSqlGeneratorTestBase | 49 | 34 | 27 | 44 | SQL baselines heavy (+results) |

#### ModelBuilding

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteModelBuilderGenericTest | RelationalModelBuilderTest | 546 | 0 | 0 | 0 | Results only |
| SqliteModelBuilderTestBase | RelationalModelBuilderTest | 536 | 1 | 10 | 0 | Results only |

#### Query

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| AdHocAdvancedMappingsQuerySqliteTest | AdHocAdvancedMappingsQueryRelationalTestBase | 19 | 0 | 0 | 0 | Results only |
| AdHocComplexTypeQuerySqliteTest | AdHocComplexTypeQueryRelationalTestBase | 14 | 0 | 0 | 0 | Results only |
| AdHocJsonQuerySqliteTest | AdHocJsonQueryRelationalTestBase | 46 | 1 | 0 | 0 | Results only |
| AdHocManyToManyQuerySqliteTest | AdHocManyToManyQueryRelationalTestBase | 2 | 0 | 0 | 0 | Results only |
| AdHocMiscellaneousQuerySqliteTest | AdHocMiscellaneousQueryRelationalTestBase | 46 | 2 | 0 | 3 | Mostly results; few SQL baselines |
| AdHocNavigationsQuerySqliteTest | AdHocNavigationsQueryRelationalTestBase | 21 | 3 | 0 | 3 | Mostly results; few SQL baselines |
| AdHocPrecompiledQuerySqliteTest | AdHocPrecompiledQueryRelationalTestBase | 7 | 0 | 0 | 0 | Results only |
| AdHocQueryFiltersQuerySqliteTest | AdHocQueryFiltersQueryRelationalTestBase | 21 | 0 | 0 | 0 | Results only |
| AdHocQuerySplittingQuerySqliteTest | AdHocQuerySplittingQueryTestBase | 10 | 0 | 0 | 0 | Results only |
| BadDataSqliteTest | provider-only | 0 | 0 | 8 | 0 | Results only |
| ComplexNavigationsCollectionsQuerySqliteTest | ComplexNavigationsCollectionsQueryRelationalTestBase | 155 | 23 | 0 | 0 | Results only |
| ComplexNavigationsCollectionsSharedTypeQuerySqliteTest | ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase | 155 | 20 | 0 | 0 | Results only |
| ComplexNavigationsCollectionsSplitQuerySqliteTest | ComplexNavigationsCollectionsSplitQueryRelationalTestBase | 155 | 19 | 0 | 0 | Results only |
| ComplexNavigationsCollectionsSplitSharedTypeQuerySqliteTest | ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase | 155 | 16 | 0 | 0 | Results only |
| ComplexNavigationsQuerySqliteFixture | ComplexNavigationsQueryRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| ComplexNavigationsQuerySqliteTest | ComplexNavigationsQueryRelationalTestBase | 304 | 8 | 0 | 0 | Results only |
| ComplexNavigationsSharedTypeQuerySqliteFixture | ComplexNavigationsSharedTypeQueryRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| ComplexNavigationsSharedTypeQuerySqliteTest | ComplexNavigationsSharedTypeQueryRelationalTestBase | 304 | 8 | 0 | 0 | Results only |
| ComplexTypeQuerySqliteTest | ComplexTypeQueryRelationalTestBase | 74 | 74 | 1 | 75 | SQL baselines heavy (+results) |
| CompositeKeysQuerySqliteFixture | CompositeKeysQueryRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| CompositeKeysQuerySqliteTest | CompositeKeysQueryRelationalTestBase | 7 | 0 | 0 | 0 | Results only |
| CompositeKeysSplitQuerySqliteTest | CompositeKeysSplitQueryRelationalTestBase | 7 | 0 | 0 | 0 | Results only |
| Ef6GroupBySqliteTest | Ef6GroupByTestBase | 55 | 6 | 0 | 4 | Mostly results; few SQL baselines |
| EntitySplittingQuerySqliteTest | EntitySplittingQueryTestBase | 58 | 20 | 0 | 20 | SQL baselines (+results) |
| FromSqlQuerySqliteTest | FromSqlQueryTestBase | 74 | 6 | 0 | 3 | Mostly results; few SQL baselines |
| FunkyDataQuerySqliteTest | FunkyDataQueryTestBase | 19 | 1 | 0 | 2 | Mostly results; few SQL baselines |
| GearsOfWarFromSqlQuerySqliteTest | GearsOfWarFromSqlQueryTestBase | 1 | 0 | 0 | 0 | Results only |
| GearsOfWarQuerySqliteFixture | GearsOfWarQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| GearsOfWarQuerySqliteTest | GearsOfWarQueryRelationalTestBase | 593 | 591 | 1 | 596 | SQL baselines heavy (+results) |
| IncludeOneToOneSqliteTest | IncludeOneToOneTestBase | 12 | 0 | 0 | 0 | Results only |
| IncompleteMappingInheritanceQuerySqliteFixture | TPHInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| IncompleteMappingInheritanceQuerySqliteTest | TPHInheritanceQueryTestBase | 55 | 1 | 0 | 0 | Results only |
| InheritanceQuerySqliteTest | TPHInheritanceQueryTestBase | 55 | 0 | 0 | 0 | Results only |
| InheritanceRelationshipsQuerySqliteTest | InheritanceRelationshipsQueryRelationalTestBase | 70 | 0 | 0 | 0 | Results only |
| JsonQuerySqliteFixture | JsonQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| JsonQuerySqliteTest | JsonQueryRelationalTestBase | 215 | 39 | 1 | 16 | SQL baselines (+results) |
| ManyToManyNoTrackingQuerySqliteTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 2 | 0 | 0 | Results only |
| ManyToManyNoTrackingSplitQuerySqliteTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 0 | 0 | Results only |
| ManyToManyQuerySqliteFixture | ManyToManyQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| ManyToManyQuerySqliteTest | ManyToManyQueryRelationalTestBase | 123 | 2 | 0 | 0 | Results only |
| ManyToManySplitQuerySqliteFixture | ManyToManyQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| ManyToManySplitQuerySqliteTest | ManyToManyQueryRelationalTestBase | 123 | 0 | 0 | 0 | Results only |
| MappingQuerySqliteTest | MappingQueryTestBase | 4 | 4 | 0 | 0 | Results only |
| NonSharedPrimitiveCollectionsQuerySqliteTest | NonSharedPrimitiveCollectionsQueryRelationalTestBase | 38 | 28 | 0 | 32 | SQL baselines (+results) |
| NorthwindAggregateOperatorsQuerySqliteTest | NorthwindAggregateOperatorsQueryRelationalTestBase | 211 | 17 | 0 | 15 | SQL baselines (+results) |
| NorthwindAsNoTrackingQuerySqliteTest | NorthwindAsNoTrackingQueryTestBase | 11 | 0 | 0 | 0 | Results only |
| NorthwindAsTrackingQuerySqliteTest | NorthwindAsTrackingQueryTestBase | 5 | 0 | 0 | 0 | Results only |
| NorthwindChangeTrackingQuerySqliteTest | NorthwindChangeTrackingQueryTestBase | 17 | 0 | 0 | 0 | Results only |
| NorthwindCompiledQuerySqliteTest | NorthwindCompiledQueryTestBase | 32 | 0 | 0 | 0 | Results only |
| NorthwindDbFunctionsQuerySqliteTest | NorthwindDbFunctionsQueryRelationalTestBase | 15 | 1 | 2 | 4 | Mostly results; few SQL baselines |
| NorthwindEFPropertyIncludeQuerySqliteTest | NorthwindEFPropertyIncludeQueryTestBase | 119 | 5 | 0 | 0 | Results only |
| NorthwindFunctionsQuerySqliteTest | NorthwindFunctionsQueryRelationalTestBase | 10 | 8 | 0 | 5 | Mostly results; few SQL baselines |
| NorthwindGroupByQuerySqliteTest | NorthwindGroupByQueryRelationalTestBase | 258 | 14 | 0 | 2 | Mostly results; few SQL baselines |
| NorthwindIncludeNoTrackingQuerySqliteTest | NorthwindIncludeNoTrackingQueryTestBase | 118 | 5 | 0 | 0 | Results only |
| NorthwindIncludeQuerySqliteTest | NorthwindIncludeQueryRelationalTestBase | 118 | 4 | 0 | 0 | Results only |
| NorthwindJoinQuerySqliteTest | NorthwindJoinQueryRelationalTestBase | 68 | 7 | 0 | 0 | Results only |
| NorthwindKeylessEntitiesQuerySqliteTest | NorthwindKeylessEntitiesQueryRelationalTestBase | 20 | 1 | 0 | 0 | Results only |
| NorthwindMiscellaneousQuerySqliteTest | NorthwindMiscellaneousQueryRelationalTestBase | 470 | 36 | 3 | 24 | SQL baselines (+results) |
| NorthwindNavigationsQuerySqliteTest | NorthwindNavigationsQueryRelationalTestBase | 73 | 0 | 0 | 0 | Results only |
| NorthwindQueryFiltersQuerySqliteTest | NorthwindQueryFiltersQueryTestBase | 17 | 1 | 0 | 2 | Mostly results; few SQL baselines |
| NorthwindQuerySqliteFixture | NorthwindQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| NorthwindQueryTaggingQuerySqliteTest | NorthwindQueryTaggingQueryTestBase | 9 | 0 | 0 | 0 | Results only |
| NorthwindSelectQuerySqliteTest | NorthwindSelectQueryRelationalTestBase | 184 | 38 | 1 | 16 | SQL baselines (+results) |
| NorthwindSetOperationsQuerySqliteTest | NorthwindSetOperationsQueryRelationalTestBase | 91 | 1 | 0 | 0 | Results only |
| NorthwindSplitIncludeNoTrackingQuerySqliteTest | NorthwindSplitIncludeNoTrackingQueryTestBase | 118 | 4 | 0 | 0 | Results only |
| NorthwindSplitIncludeQuerySqliteTest | NorthwindSplitIncludeQueryTestBase | 118 | 4 | 0 | 0 | Results only |
| NorthwindSqlQuerySqliteTest | NorthwindSqlQueryTestBase | 4 | 0 | 0 | 1 | Mostly results; few SQL baselines |
| NorthwindStringIncludeQuerySqliteTest | NorthwindStringIncludeQueryTestBase | 119 | 4 | 0 | 0 | Results only |
| NorthwindWhereQuerySqliteTest | NorthwindWhereQueryRelationalTestBase | 203 | 11 | 0 | 13 | SQL baselines (+results) |
| NullKeysSqliteTest | NullKeysTestBase | 5 | 0 | 0 | 0 | Results only |
| NullSemanticsQuerySqliteFixture | NullSemanticsQueryFixtureBase | 0 | 0 | 0 | 0 | Results only |
| NullSemanticsQuerySqliteTest | NullSemanticsQueryTestBase | 168 | 26 | 0 | 27 | SQL baselines (+results) |
| OperatorsProceduralSqliteTest | OperatorsProceduralQueryTestBase | 0 | 0 | 0 | 0 | Results only |
| OperatorsQuerySqliteTest | OperatorsQueryTestBase | 10 | 10 | 0 | 11 | SQL baselines (+results) |
| OptionalDependentQuerySqliteFixture | OptionalDependentQueryFixtureBase | 0 | 0 | 0 | 0 | Results only |
| OptionalDependentQuerySqliteTest | OptionalDependentQueryTestBase | 10 | 10 | 0 | 11 | SQL baselines (+results) |
| OwnedEntityQuerySqliteTest | OwnedEntityQueryRelationalTestBase | 23 | 0 | 0 | 0 | Results only |
| OwnedQuerySqliteTest | OwnedQueryRelationalTestBase | 103 | 0 | 0 | 0 | Results only |
| PrecompiledQuerySqliteTest | PrecompiledQueryRelationalTestBase | 104 | 0 | 2 | 0 | Results only |
| PrecompiledSqlPregenerationQuerySqlServerTest | PrecompiledSqlPregenerationQueryRelationalTestBase | 14 | 0 | 0 | 0 | Results only |
| PrimitiveCollectionsQuerySqliteTest | PrimitiveCollectionsQueryRelationalTestBase | 158 | 158 | 2 | 133 | SQL baselines heavy (+results) |
| QueryFilterFuncletizationSqliteTest | QueryFilterFuncletizationTestBase | 28 | 1 | 0 | 2 | Mostly results; few SQL baselines |
| QueryNoClientEvalSqliteFixture | NorthwindQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| QueryNoClientEvalSqliteTest | QueryNoClientEvalTestBase | 14 | 0 | 0 | 0 | Results only |
| SharedTypeQuerySqliteTest | SharedTypeQueryRelationalTestBase | 4 | 0 | 0 | 0 | Results only |
| SpatialQuerySqliteFixture | SpatialQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| SpatialQuerySqliteTest | SpatialQueryRelationalTestBase | 84 | 83 | 0 | 83 | SQL baselines heavy (+results) |
| SqlQuerySqliteTest | SqlQueryTestBase | 60 | 8 | 0 | 5 | Mostly results; few SQL baselines |
| TPCFiltersInheritanceQuerySqliteFixture | TPCInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| TPCFiltersInheritanceQuerySqliteTest | TPCFiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | Results only |
| TPCGearsOfWarQuerySqliteFixture | TPCGearsOfWarQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| TPCGearsOfWarQuerySqliteTest | TPCGearsOfWarQueryRelationalTestBase | 593 | 23 | 0 | 3 | Mostly results; few SQL baselines |
| TPCInheritanceQuerySqliteFixture | TPCInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| TPCInheritanceQuerySqliteTest | TPCInheritanceQueryTestBase | 53 | 0 | 0 | 0 | Results only |
| TPCManyToManyNoTrackingQuerySqliteTest | TPCManyToManyNoTrackingQueryRelationalTestBase | 124 | 2 | 0 | 0 | Results only |
| TPCManyToManyQuerySqliteFixture | TPCManyToManyQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| TPCManyToManyQuerySqliteTest | TPCManyToManyQueryRelationalTestBase | 123 | 2 | 0 | 0 | Results only |
| TPCRelationshipsQuerySqliteTest | TPCRelationshipsQueryTestBase | 70 | 0 | 0 | 0 | Results only |
| TPHFiltersInheritanceQuerySqliteFixture | TPHInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| TPHFiltersInheritanceQuerySqliteTest | FiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | Results only |
| TPHInheritanceQuerySqliteFixture | TPHInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| TPTFiltersInheritanceQuerySqliteFixture | TPTInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| TPTFiltersInheritanceQuerySqliteTest | TPTFiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | Results only |
| TPTGearsOfWarQuerySqliteFixture | TPTGearsOfWarQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| TPTGearsOfWarQuerySqliteTest | TPTGearsOfWarQueryRelationalTestBase | 593 | 23 | 0 | 3 | Mostly results; few SQL baselines |
| TPTInheritanceQuerySqliteFixture | TPTInheritanceQueryFixture | 0 | 0 | 0 | 0 | Results only |
| TPTInheritanceQuerySqliteTest | TPTInheritanceQueryTestBase | 53 | 0 | 0 | 0 | Results only |
| TPTManyToManyNoTrackingQuerySqliteTest | TPTManyToManyNoTrackingQueryRelationalTestBase | 124 | 2 | 0 | 0 | Results only |
| TPTManyToManyQuerySqliteFixture | TPTManyToManyQueryRelationalFixture | 0 | 0 | 0 | 0 | Results only |
| TPTManyToManyQuerySqliteTest | TPTManyToManyQueryRelationalTestBase | 123 | 2 | 0 | 0 | Results only |
| TPTRelationshipsQuerySqliteTest | TPTRelationshipsQueryTestBase | 70 | 0 | 0 | 0 | Results only |
| ToSqlQuerySqliteTest | ToSqlQueryTestBase | 1 | 1 | 1 | 2 | Mostly results; few SQL baselines |
| WarningsSqliteTest | WarningsTestBase | 11 | 0 | 0 | 0 | Results only |

#### Query/Associations/ComplexJson

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| ComplexJsonBulkUpdateSqliteTest | ComplexJsonBulkUpdateRelationalTestBase | 33 | 0 | 0 | 0 | Results only |
| ComplexJsonCollectionSqliteTest | ComplexJsonCollectionRelationalTestBase | 14 | 0 | 0 | 0 | Results only |
| ComplexJsonMiscellaneousSqliteTest | ComplexJsonMiscellaneousRelationalTestBase | 7 | 0 | 0 | 0 | Results only |
| ComplexJsonPrimitiveCollectionSqliteTest | ComplexJsonPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | Results only |
| ComplexJsonProjectionSqliteTest | ComplexJsonProjectionRelationalTestBase | 28 | 6 | 0 | 0 | Results only |
| ComplexJsonSetOperationsSqliteTest | ComplexJsonSetOperationsRelationalTestBase | 5 | 0 | 0 | 0 | Results only |
| ComplexJsonSqliteFixture | ComplexJsonRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| ComplexJsonStructuralEqualitySqliteTest | ComplexJsonStructuralEqualityRelationalTestBase | 16 | 0 | 0 | 0 | Results only |

#### Query/Associations/ComplexTableSplitting

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| ComplexTableSplittingBulkUpdateSqliteTest | ComplexTableSplittingBulkUpdateRelationalTestBase | 33 | 0 | 0 | 0 | Results only |
| ComplexTableSplittingMiscellaneousSqliteTest | ComplexTableSplittingMiscellaneousRelationalTestBase | 7 | 0 | 0 | 0 | Results only |
| ComplexTableSplittingPrimitiveCollectionSqliteTest | ComplexTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | Results only |
| ComplexTableSplittingProjectionSqliteTest | ComplexTableSplittingProjectionRelationalTestBase | 28 | 3 | 0 | 0 | Results only |
| ComplexTableSplittingSqliteFixture | ComplexTableSplittingRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| ComplexTableSplittingStructuralEqualitySqliteTest | ComplexTableSplittingStructuralEqualityRelationalTestBase | 16 | 0 | 0 | 0 | Results only |

#### Query/Associations/Navigations

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| NavigationsCollectionSqliteTest | NavigationsCollectionRelationalTestBase | 14 | 1 | 0 | 0 | Results only |
| NavigationsIncludeSqliteTest | NavigationsIncludeRelationalTestBase | 9 | 0 | 0 | 0 | Results only |
| NavigationsMiscellaneousSqliteTest | NavigationsMiscellaneousRelationalTestBase | 4 | 0 | 0 | 0 | Results only |
| NavigationsPrimitiveCollectionSqliteTest | NavigationsPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | Results only |
| NavigationsProjectionSqliteTest | NavigationsProjectionRelationalTestBase | 24 | 3 | 0 | 0 | Results only |
| NavigationsSetOperationsSqliteTest | NavigationsSetOperationsRelationalTestBase | 5 | 0 | 0 | 0 | Results only |
| NavigationsSqliteFixture | NavigationsRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| NavigationsStructuralEqualitySqliteTest | NavigationsStructuralEqualityRelationalTestBase | 15 | 0 | 0 | 0 | Results only |

#### Query/Associations/OwnedJson

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| OwnedJsonBulkUpdateSqliteTest | OwnedJsonBulkUpdateRelationalTestBase | 3 | 0 | 0 | 0 | Results only |
| OwnedJsonCollectionSqliteTest | OwnedJsonCollectionRelationalTestBase | 14 | 1 | 0 | 0 | Results only |
| OwnedJsonMiscellaneousSqliteTest | OwnedJsonMiscellaneousRelationalTestBase | 4 | 0 | 0 | 0 | Results only |
| OwnedJsonPrimitiveCollectionSqliteTest | OwnedJsonPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | Results only |
| OwnedJsonProjectionSqliteTest | OwnedJsonProjectionRelationalTestBase | 24 | 6 | 0 | 0 | Results only |
| OwnedJsonSqliteFixture | OwnedJsonRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| OwnedJsonStructuralEqualitySqliteTest | OwnedJsonStructuralEqualityRelationalTestBase | 15 | 15 | 1 | 15 | SQL baselines (+results) |

#### Query/Associations/OwnedNavigations

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| OwnedNavigationsCollectionSqliteTest | OwnedNavigationsCollectionRelationalTestBase | 14 | 1 | 0 | 0 | Results only |
| OwnedNavigationsMiscellaneousSqliteTest | OwnedNavigationsMiscellaneousRelationalTestBase | 4 | 0 | 0 | 0 | Results only |
| OwnedNavigationsPrimitiveCollectionSqliteTest | OwnedNavigationsPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | Results only |
| OwnedNavigationsSetOperationsSqliteTest | OwnedNavigationsSetOperationsRelationalTestBase | 5 | 1 | 0 | 0 | Results only |
| OwnedNavigationsStructuralEqualitySqliteTest | OwnedNavigationsStructuralEqualityRelationalTestBase | 15 | 15 | 1 | 15 | SQL baselines (+results) |
| OwnedNavigationsSqliteFixture | OwnedNavigationsRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |

#### Query/Associations/OwnedTableSplitting

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| OwnedTableSplittingMiscellaneousSqliteTest | OwnedTableSplittingMiscellaneousRelationalTestBase | 4 | 0 | 0 | 0 | Results only |
| OwnedTableSplittingPrimitiveCollectionSqliteTest | OwnedTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 0 | 0 | 0 | Results only |
| OwnedTableSplittingSqliteFixture | OwnedTableSplittingRelationalFixtureBase | 0 | 0 | 0 | 0 | Results only |
| OwnedTableSplittingStructuralEqualitySqliteTest | OwnedTableSplittingStructuralEqualityRelationalTestBase | 15 | 15 | 1 | 15 | SQL baselines (+results) |

#### Query/Translations

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| BasicTypesQuerySqliteFixture | BasicTypesQueryFixtureBase | 0 | 0 | 0 | 0 | Results only |
| ByteArrayTranslationsSqliteTest | ByteArrayTranslationsTestBase | 7 | 7 | 1 | 6 | Mostly results; few SQL baselines |
| EnumTranslationsSqliteTest | EnumTranslationsTestBase | 18 | 18 | 1 | 19 | SQL baselines (+results) |
| GuidTranslationsSqliteTest | GuidTranslationsTestBase | 4 | 4 | 1 | 4 | Mostly results; few SQL baselines |
| MathTranslationsSqliteTest | MathTranslationsTestBase | 66 | 66 | 1 | 64 | SQL baselines heavy (+results) |
| MiscellaneousTranslationsSqliteTest | MiscellaneousTranslationsRelationalTestBase | 18 | 18 | 1 | 11 | SQL baselines (+results) |
| StringTranslationsSqliteTest | StringTranslationsRelationalTestBase | 104 | 104 | 2 | 105 | SQL baselines heavy (+results) |

#### Query/Translations/Operators

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| ArithmeticOperatorTranslationsSqliteTest | ArithmeticOperatorTranslationsTestBase | 5 | 5 | 1 | 6 | Mostly results; few SQL baselines |
| BitwiseOperatorTranslationsSqliteTest | BitwiseOperatorTranslationsTestBase | 15 | 15 | 1 | 12 | SQL baselines (+results) |
| ComparisonOperatorTranslationsSqliteTest | ComparisonOperatorTranslationsTestBase | 6 | 6 | 1 | 7 | Mostly results; few SQL baselines |
| LogicalOperatorTranslationsSqlServerTest | LogicalOperatorTranslationsTestBase | 6 | 6 | 1 | 7 | Mostly results; few SQL baselines |
| MiscellaneousOperatorTranslationsSqlServerTest | MiscellaneousOperatorTranslationsTestBase | 2 | 2 | 1 | 3 | Mostly results; few SQL baselines |

#### Query/Translations/Temporal

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| DateOnlyTranslationsSqliteTest | DateOnlyTranslationsTestBase | 18 | 18 | 3 | 21 | SQL baselines (+results) |
| DateTimeOffsetTranslationsSqliteTest | DateTimeOffsetTranslationsTestBase | 24 | 24 | 1 | 23 | SQL baselines (+results) |
| DateTimeTranslationsSqliteTest | DateTimeTranslationsTestBase | 19 | 19 | 1 | 19 | SQL baselines (+results) |
| TimeOnlyTranslationsSqliteTest | TimeOnlyTranslationsTestBase | 17 | 17 | 1 | 18 | SQL baselines (+results) |
| TimeSpanTranslationsSqliteTest | TimeSpanTranslationsTestBase | 6 | 6 | 1 | 7 | Mostly results; few SQL baselines |

#### Scaffolding

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| CompiledModelSqliteTest | CompiledModelRelationalTestBase | 14 | 3 | 0 | 0 | Results only |
| SqliteDatabaseModelFactoryTest | provider-only | 0 | 0 | 51 | 0 | Results only |

#### Types

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| BoolTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| ByteTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| DateTimeTypeTest | RelationalTypeTestBase | 7 | 1 | 0 | 0 | Results only |

#### Update

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| ComplexCollectionJsonUpdateSqliteTest | ComplexCollectionJsonUpdateTestBase | 20 | 19 | 0 | 19 | SQL baselines (+results) |
| JsonUpdateSqliteFixture | JsonUpdateFixtureBase | 0 | 0 | 0 | 0 | Results only |
| JsonUpdateSqliteTest | JsonUpdateTestBase | 136 | 104 | 0 | 99 | SQL baselines heavy (+results) |
| NonSharedModelUpdatesSqliteTest | NonSharedModelUpdatesTestBase | 2 | 2 | 0 | 3 | Mostly results; few SQL baselines |
| SqliteUpdateSqlGeneratorTest | UpdateSqlGeneratorTestBase | 13 | 2 | 0 | 0 | Results only |
| StoreValueGenerationWithoutReturningSqliteTest | StoreValueGenerationTestBase | 19 | 18 | 0 | 18 | SQL baselines (+results) |
| StoreValueGenerationSqliteFixture | StoreValueGenerationFixtureBase | 0 | 0 | 0 | 0 | Results only |
| StoreValueGenerationSqliteTest | StoreValueGenerationTestBase | 19 | 18 | 0 | 18 | SQL baselines (+results) |
| UpdatesSqliteTest | UpdatesRelationalTestBase | 29 | 2 | 0 | 0 | Results only |

**Subtotal (EFCore.Sqlite.FunctionalTests)**: inherited test methods 13842 (of which overridden 2382), provider-only test methods 239.

### EFCore.Sqlite.Tests (unit)


#### (root)

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| DbSetAsTableNameSqliteTest | DbSetAsTableNameTest | 14 | 0 | 0 | - | Unit (model/SQL strings/exceptions) |
| SqliteNTSApiConsistencyTest | ApiConsistencyTestBase | 19 | 0 | 0 | - | Unit (model/SQL strings/exceptions) |

#### Design

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteDesignTimeProviderServicesTest | DesignTimeProviderServicesTest | 1 | 0 | 0 | - | Unit (model/SQL strings/exceptions) |

#### Diagnostics

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteEventIdTest | EventIdTestBase | 0 | 0 | 1 | - | Unit (model/SQL strings/exceptions) |

#### Extensions

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteDatabaseFacadeExtensionsTest | provider-only | 0 | 0 | 2 | - | Unit (model/SQL strings/exceptions) |
| SqliteDbContextOptionsBuilderExtensionsTest | provider-only | 0 | 0 | 12 | - | Unit (model/SQL strings/exceptions) |
| SqliteMetadataExtensionsTest | provider-only | 0 | 0 | 11 | - | Unit (model/SQL strings/exceptions) |

#### Infrastructure

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteModelValidatorTest | RelationalModelValidatorTest | 380 | 7 | 9 | - | Unit (model/SQL strings/exceptions) |

#### Metadata/Builders

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteBuilderExtensionsTest | provider-only | 0 | 0 | 7 | - | Unit (model/SQL strings/exceptions) |
| SqliteMetadataBuilderExtensionsTest | provider-only | 0 | 0 | 1 | - | Unit (model/SQL strings/exceptions) |

#### Metadata/Conventions

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteConventionSetBuilderTests | ConventionSetBuilderTests | 2 | 0 | 0 | - | Unit (model/SQL strings/exceptions) |

#### Migrations

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteHistoryRepositoryTest | provider-only | 0 | 0 | 7 | - | Unit (model/SQL strings/exceptions) |
| SqliteMigrationAnnotationProviderTest | provider-only | 0 | 0 | 7 | - | Unit (model/SQL strings/exceptions) |
| SqliteMigrationBuilderTest | provider-only | 0 | 0 | 2 | - | Unit (model/SQL strings/exceptions) |
| SqliteModelDifferTest | MigrationsModelDifferTestBase | 0 | 0 | 6 | - | Unit (model/SQL strings/exceptions) |

#### Scaffolding

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteCodeGeneratorTest | provider-only | 0 | 0 | 3 | - | Unit (model/SQL strings/exceptions) |

#### Storage

| Class | Spec base | Inherited | Overridden | Provider-only | AssertSql calls | Asserts |
|---|---|---:|---:|---:|---:|---|
| SqliteRelationalConnectionTest | provider-only | 0 | 0 | 2 | - | Unit (model/SQL strings/exceptions) |
| SqliteStringTypeMappingTest | provider-only | 0 | 0 | 1 | - | Unit (model/SQL strings/exceptions) |
| SqliteTypeMappingSourceTest | RelationalTypeMappingSourceTestBase | 0 | 0 | 7 | - | Unit (model/SQL strings/exceptions) |
| SqliteTypeMappingTest | RelationalTypeMappingTest | 36 | 8 | 2 | - | Unit (model/SQL strings/exceptions) |

**Subtotal (EFCore.Sqlite.Tests (unit))**: inherited test methods 452 (of which overridden 15), provider-only test methods 80.


---

## 2. Provider-only behaviours

These are the behaviours the SQLite suite asserts that no shared spec test asserts. The tables are grouped by concept, and near-identical tests are merged into one row. F = FunctionalTests, U = unit tests.

### 2.1 Autoincrement / rowid key generation

| Tests | Behaviour (database-neutral) | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `AutoincrementTest.Autoincrement_prevents_reusing_rowid` | After you delete the row with the highest generated key, the next insert does not reuse that key: keys increase monotonically and are never recycled | Autoincrement strategy | `UseAutoincrement` (by convention on an integer PK); DDL `INTEGER PRIMARY KEY AUTOINCREMENT` | Results |
| F `SqliteValueGenerationScenariosTest.Insert_with_Identity_column`, `Insert_uint_to_Identity_column_using_value_converter`, `Insert_int_enum_to_Identity_column`, `Insert_ushort_enum_to_Identity_column`, `Insert_string_to_Identity_column_using_value_converter` | A store-generated integer key yields 1, 2… on insert, including when the CLR key is uint, an int- or ushort-backed enum, or a string converted to an integer | Autoincrement and value converters | `ValueGeneratedOnAdd`, `HasConversion<int>` | Results |
| F `…Insert_with_explicit_non_default_keys`, `Insert_with_explicit_with_default_keys`, `Insert_with_implicit_default_keys`, `Insert_with_explicit_default_keys` | With `ValueGeneratedNever`, explicit keys (66, 67, or 0 on a nullable key) are stored verbatim | Key generation off | `ValueGeneratedNever` | Results |
| F `…Insert_with_client_generated_GUID_key` | A GUID key generated on the client is stable across SaveChanges and round-trips | Client GUID generation | (core) | Results |
| F `…Insert_with_non_key_default_value`, `…_readonly`, `Insert_explicit_value_throws_when_readonly_before_save` | A non-key column with `HasDefaultValueSql("CURRENT_TIMESTAMP")` or a constant default fills when the value is unset, and an explicit value wins. A read-only-before-save property with an explicit value throws `PropertyReadOnlyBeforeSave` | Store defaults | `HasDefaultValueSql`, `HasDefaultValue`, `SetBeforeSaveBehavior` | Results; Exception/message |
| F `StoreGeneratedSqliteTest.Identity_key_works_when_not_aliasing_rowid` | A `byte[]` key with `HasDefaultValueSql("randomblob(16)")` (not a rowid alias) is read back after insert (16 bytes) | Generated non-integer key via RETURNING | `HasDefaultValueSql` and the RETURNING clause | Results |
| F `StoreGeneratedFixupSqliteTest.Temp_values_can_be_made_permanent` | If you mark a temporary key value as non-temporary before save, the value is inserted as-is | Temporary key values | (core plus the SQLite generator) | Results |
| F `ModelBuilding/SqliteModelBuilderTestBase` (10): `UseAutoincrement_sets_value_generation_strategy` (generic and non-generic), `Default_value_generation_strategy_for_integer_primary_key`, `No_autoincrement_for_{composite_primary_key, non_integer_primary_key, non_primary_key}`, `No_autoincrement_when_{default_value_set, default_value_sql_set, computed_column_sql_set, property_is_foreign_key}` | The value-generation strategy is Autoincrement only for a single-column integer PK that is not an FK and has no default or computed SQL. Otherwise it is None | Autoincrement convention | `UseAutoincrement`, `GetValueGenerationStrategy`, `SqliteValueGenerationStrategy` | Model |
| U `SqliteMetadataExtensionsTest` (9 of 11): same matrix through the metadata API, plus `Can_set_value_generation_strategy_on_mutable_property` | The same rules on IMutableProperty and IConventionProperty | Autoincrement metadata | `SetValueGenerationStrategy`/`GetValueGenerationStrategy` | Model |
| U `SqliteMigrationAnnotationProviderTest` (7) | The migration annotation `Sqlite:Autoincrement` is emitted only for an OnAdd integer PK. It is not emitted for Never, OnUpdate, OnAddOrUpdate, non-key, non-integer or default properties | Autoincrement in migrations | `SqliteAnnotationNames.Autoincrement` | Model |
| U `SqliteModelDifferTest` (6): `Add_property_with_autoincrement_strategy`, `Alter_property_{add,remove}_autoincrement_strategy`, `Autoincrement_with_value_converter_generates_consistent_migrations`, `No_repeated_alter_column_for_autoincrement_with_converter`, `Noop_when_changing_to_autoincrement_property_with_converter` | Strategy changes appear as AlterColumn. A converter on the key does not cause spurious or repeated diffs | Model differ | `MigrationsModelDiffer` and the annotation | Model (operations list) |
| F `Migrations/MigrationsSqliteTest`: `Create_table_with_autoincrement_and_value_converter`, `…_by_convention`, `Create_table_with_composite_primary_key_ignores_autoincrement`, `Alter_column_remove_autoincrement`, `Replace_string_primary_key_with_autoincrement_identity`, `ValueGeneratedOnAdd_on_properties` | The DDL emits `INTEGER … PRIMARY KEY AUTOINCREMENT` for a single integer key (also behind a converter), never for a composite key. Removing autoincrement or swapping a string PK for an autoincrement PK uses a table rebuild | Autoincrement DDL | `UseAutoincrement` | Both (model check plus SQL) |
| F `SqliteMigrationsSqlGeneratorTest.CreateTableOperation_old_autoincrement_annotation` | The legacy annotation `"Autoincrement"=true` still produces `AUTOINCREMENT` | Back-compat | annotation | SQL |
| U `SqliteModelValidatorTest.Detects_conflicting_autoincrement_and_default_value_sql`, `…_computed_column`; override `Store_generated_in_composite_key` | Warns when autoincrement is combined with default SQL or computed SQL, and when a composite key has a generated part | Validation | `SqliteEventId` warnings | Model (warning text) |

### 2.2 Table-rebuild migrations and migration limits

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `SqliteMigrationsSqlGeneratorTest.Rebuild_preserves_column_order`, `DropColumn_defers_subsequent_RenameColumn`, `DropColumn_defers_subsequent_AddColumn_required`, `Deferred_RenameColumn_defers_subsequent_{AddColumn, CreateIndex_unique}`, `Deferred_AddColumn_defers_subsequent_CreateIndex`, `RenameTable_preserves_pending_rebuilds` | An operation the engine cannot do in place triggers a rebuild: `CREATE TABLE "ef_temp_X"`, `INSERT … SELECT`, `PRAGMA foreign_keys = 0`, `DROP TABLE`, `ALTER TABLE … RENAME TO`, `PRAGMA foreign_keys = 1`, then recreate the indexes. Later operations on the same table fold into the rebuild. Column order is preserved, and a rename keeps pending rebuilds | Table rebuild | `SqliteMigrationsSqlGenerator` (needs the target model) | SQL |
| F `MigrationsSqliteTest` overrides (≈45, see the `ef_temp_` list): Add/Drop PK, FK, unique, check constraint; Drop_column; Alter_column make required/computed/collation/comment; Alter_table comments; Rename_table_with_json_column; Convert_json↔owned | The base spec expects in-place ALTERs; SQLite expects the rebuild sequence. The final database model is still checked by the base | Table rebuild | same | Both |
| F `…It_lifts_foreign_key_additions` | An AddForeignKey that follows CreateTable on the same table is inlined into the CREATE TABLE | FK inlining | generator | SQL |
| F `…DropColumn_in_table_which_has_another_spatial_column`, `AddColumnOperation_with_spatial_type` | A spatial column is added with `SELECT AddGeometryColumn('T','C',srid,'GEOMETRYZM',-1,0)`. A rebuild handles the other geometry columns | Spatial DDL | `HasSrid`, `Sqlite:Srid` | SQL |
| F `…{AddCheckConstraint, AddPrimaryKey, AddUniqueConstraint, DropColumn, DropForeignKey, DropPrimaryKey, DropUniqueConstraint, RenameIndexOperations}_throws_when_no_model`, `AlterColumnOperation_computed` | Without a target model (a script built by hand) these operations throw `NotSupportedException(InvalidMigrationOperation(op))` | Rebuild needs a model | generator | Exception/message |
| F `…AddColumnOperation_with_comment_mostly_works_when_no_model`, `AlterTable_mostly_works_when_no_model` | Comments are emitted as `-- comment` lines inside CREATE TABLE. With no model, AlterTable(comment) emits nothing | Comments as SQL comments | generator | SQL |
| F `…CreateSchemaOperation_is_ignored`, `DropSchemaOperation_is_ignored` | Schema operations produce no SQL | No schemas | generator | SQL (empty) |
| F `…RestartSequenceOperation_not_supported` | Sequence operations throw `SequencesNotSupported` | No sequences | generator | Exception/message |
| F `…DefaultValue_formats_literal_correctly` | A DateTime default is rendered `'2015-04-12 17:05:00'` (TEXT literal) | Text-stored dates | type mapping literal | SQL |
| F `…CreateTableOperation_with_annotations` | Provider annotations on CreateTable are honoured | DDL | generator | SQL |
| U `SqliteHistoryRepositoryTest`: `GetCreateScript`, `GetCreateIfNotExistsScript`, `GetDeleteScript`, `GetInsertScript` | The history table DDL and DML use `CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory"` | History table | `SqliteHistoryRepository` | SQL |
| U `…GetBeginIfNotExistsScript`, `GetBeginIfExistsScript`, `GetEndIfScript` | Idempotent scripts are not supported: they throw `MigrationScriptGenerationNotSupported` | No procedural SQL blocks | same | Exception/message |
| U `SqliteMigrationBuilderTest.IsSqlite_*`, F/U `SqliteDatabaseFacadeExtensionsTest.IsSqlite_*` | A provider-identity check on MigrationBuilder and DatabaseFacade | Provider detection | `IsSqlite()` | Model (bool) |

### 2.3 Text-stored dates, decimals and other "converted" types

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `BuiltInDataTypesSqliteTest.Can_insert_and_read_back_all_mapped_*` (28 variants: plain, nullable, sized, null, scale, precision+scale, ×with_Identity ×in_batch) | Every CLR type maps to an SQLite store type and round-trips exactly, including explicit store-type names such as `nvarchar(max)`, `decimal(18,2)`, `datetime2`, which SQLite accepts by affinity | Type affinity mapping | `SqliteTypeMappingSource` | Results |
| F `…Can_get_column_types_from_built_model` | Each property's column type equals the mapping source's store type | Mapping consistency | `IRelationalTypeMappingSource` | Model |
| F `…Can_insert_and_query_decimal` | A decimal stored as TEXT is found by equality (`== 3m`) | Decimal as TEXT | decimal mapping | Results |
| F `…Projecting_arithmetic_operations_on_decimals` | Decimal + − × ÷ % and negate run server-side through UDFs `ef_add`, `ef_negate`, `ef_multiply`, `ef_divide`, `ef_mod` | Decimal UDFs | connection-registered functions | Both |
| F `…Can_query_{Sum, Average, add, subtract, multiply, divide, modulo, negation, less_than(_or_equal), greater_than(_or_equal), OrderBy, ThenBy}_of_converted_types` | For decimal, DateTimeOffset, TimeSpan and ulong, comparisons and aggregates work where a provider workaround exists (decimal via `ef_sum`/`ef_avg`/`ef_compare`, the EF_DECIMAL collation). OrderBy of decimal works | Converted-type operations | UDFs, collation | Both |
| F `…Cant_query_{Min, Max}_of_converted_types` | Min/Max over DateTimeOffset, TimeSpan and ulong throws `NotSupportedException(AggregateOperationNotSupported(op,type))` | Aggregate gap | — | Exception/message |
| F `…Cant_query_{OrderBy, ThenBy}_of_converted_types` | Ordering by DateTimeOffset, TimeSpan or ulong throws `OrderByNotSupported(type)` | Ordering gap | — | Exception/message |
| F `…Character_to_integer_conversion`, `Integer_to_character_conversion` | A char↔integer cast translates to `unicode(x)` / `char(x)` | Char as TEXT | translators | Both |
| F `…Can_query_using_char_ToLower`/`ToUpper` | `char.ToLower/ToUpper` translate to `lower`/`upper` | String functions | translators | Both |
| F `ValueConvertersEndToEndSqliteTest.Properties_with_conversions_map_to_appropriately_null_columns` (theory) | A converted property gets the expected store type (TEXT/INTEGER/…) and nullability | Converter mapping | — | Model |
| F `DefaultValuesTest.Can_use_SQLite_default_values` | A DateTime constant default is applied when the value is unset and read back | Defaults on TEXT dates | `HasDefaultValue` | Results |
| F `Query/NorthwindSelectQuerySqliteTest.Select_datetime_year_component_composed`; `NorthwindMiscellaneousQuerySqliteTest.Select_datetime_{millisecond, TimeOfDay}_component_composed` | `AddYears(1).Year/.Millisecond/.TimeOfDay` compose into one `strftime('%Y'/'%f'/…, col, '+1 years')` | Date arithmetic via modifiers | DateTime translators | Both |
| F `Translations/Temporal/DateOnlyTranslationsSqliteTest.Where_AddYears_AddMonths`, `Where_AddYears_Year` | Chained DateOnly additions fold into a single `date(col, 'N years', 'N months')` | Same | DateOnly translators | Both |
| U `SqliteTypeMappingSourceTest` (7: CLR→store, store→CLR, both, defaults for values/nulls, plugin override, unknown type throws) | INTEGER for all integral types and enums, TEXT for string/Guid/DateTime/DateTimeOffset/TimeSpan/decimal, REAL for float/double, BLOB for byte[]. Unknown store names resolve by affinity rules | Type affinity | `SqliteTypeMappingSource` | Model |
| U `SqliteTypeMappingTest.It_maps_strings_to_not_null_types`, `SQLite_type_mapping_works_even_when_using_non_SQLite_store_type`; `SqliteStringTypeMappingTest.GenerateProviderValueSqlLiteral_works`; 8 literal-format overrides | Literal formats: dates as `'yyyy-MM-dd HH:mm:ss.FFFFFFF'`, Guid as uppercase TEXT, decimals as `'1.0'` text, and so on | Literal generation | type mappings | SQL (literal strings) |

### 2.4 JSON / primitive collections

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `PrimitiveCollectionsQuerySqliteTest.Empty_string_used_for_primitive_collection_throws` | An empty string in a JSON primitive-collection column fails materialization with `EmptyJsonString` | JSON collection storage | `json_each` and materializer | Exception/message |
| F `JsonQuerySqliteTest.FromSqlInterpolated_on_entity_with_json_with_predicate` | Raw SQL with a provider parameter object (`SqliteParameter`) composes over an entity with JSON columns | FromSql and JSON | FromSql | Both |
| (overrides, see 3.x) | All JSON querying is `json_each`/`->>`-based. The base results hold, and the SQL is asserted | — | — | Both |

### 2.5 Pattern matching: GLOB and REGEXP

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `NorthwindDbFunctionsQuerySqliteTest.Glob`, `Glob_negated` | `EF.Functions.Glob(col,"*M*")` → `col GLOB '*M*'` (case-sensitive, Unix wildcards), and its negation | Glob | `SqliteDbFunctionsExtensions.Glob` | Both |
| F `StringTranslationsSqliteTest.Regex_IsMatch_negated` (plus the overridden `Regex_IsMatch*`) | `Regex.IsMatch` → `col REGEXP 'pat'`, negated → `NOT REGEXP`. This relies on the provider registering a `regexp` UDF on the connection | Regex | connection function `regexp` | Both |
| F `PrecompiledQuerySqliteTest.Glob`, `Regexp` | The same two translations work under NativeAOT precompiled queries | Precompilation | — | Results (compiles and runs) |

### 2.6 Byte and string functions (EF.Functions)

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `BuiltInDataTypesSqliteTest.Can_query_using_hex_function` | `EF.Functions.Hex(bytes)` → `hex()` and equals .NET uppercase hex | Hex | `Hex` | Both |
| F `…Can_query_using_unhex_function`, `…_with_ignore_chars`, `Can_filter_using_unhex_function` | `Unhex(s[,ignore])` → `unhex()`. Invalid hex yields NULL, so it can be used as a filter | Unhex | `Unhex` | Both |
| F `…Can_query_using_substr_function`, `…_with_length` | `EF.Functions.Substr(bytes, start[, len])` → `substr()` on BLOB | Substr on bytes | `Substr` | Both |

### 2.7 Database lifecycle and connection

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `SqliteDatabaseCreatorTest.Exists_returns_{false,true}_*` (×async ×CanConnect) | Exists and CanConnect are false for a missing file and true after creation. They do not create the file | File database existence | `SqliteDatabaseCreator` | Results (bool) |
| F `…Exists_returns_true_when_memory` (`:memory:`, `Mode=Memory;Cache=Shared`) | In-memory databases always "exist" | In-memory | — | Results |
| F `…HasTables_returns_{false,true}_*` | HasTables reflects whether any user table exists | Creator | — | Results |
| F `…Create_sets_journal_mode_to_wal` | EnsureCreated switches the file database to `journal_mode=wal` | WAL | creator | Results (PRAGMA value) |
| F `…Delete_works_even_when_different_connection_exists_to_same_file` | EnsureDeleted removes the file even while another connection is open (pool clear) | File delete | creator | Results |
| F `…Delete_works_for_in_memory_database` | EnsureDeleted on an open in-memory connection empties it, and EnsureCreated recreates it | In-memory reset | creator | Results |
| F `CommandConfigurationTest.Constructed_select_query_CommandBuilder_throws_when_negative_CommandTimeout_is_used` | SetCommandTimeout(-5) throws ArgumentException | Command timeout | — | Exception |
| U `SqliteRelationalConnectionTest.Sets_DefaultTimeout_when_connection{,String}` | The configured command timeout becomes `SqliteConnection.DefaultTimeout`, including on a connection you supply | Connection timeout | `SqliteRelationalConnection` | Results |
| U `SqliteDbContextOptionsBuilderExtensionsTest` (12) | UseSqlite with a connection string, a DbConnection, an owned connection or no connection, and generic options. The last call between connection and connection string wins. Command timeout and max batch size are stored. The AddSqlite service extension works | Options | `UseSqlite`, `AddSqlite` | Model (options extension) |
| U `SqliteCodeGeneratorTest` (3) | Scaffolded code emits `.UseSqlite(...)`, `x => x.UseNetTopologySuite()` and provider options | Scaffolding code | `SqliteCodeGenerator` | SQL-like (generated C# text) |

### 2.8 Reverse engineering (scaffolding)

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `Scaffolding/SqliteDatabaseModelFactoryTest` (51): Create_{tables, columns, view_columns, primary_key, composite_primary_key, unique_constraints, composite_unique_constraint, indexes, composite_index, foreign_keys, composite_foreign_key(+default columns), multiple_foreign_key_in_same_table, foreign_key_referencing_unique_constraint}; Set_name_for_{PK, FK, unique}; Set_referential_action; Set_unique_for_unique_index; Filter_tables(+case-insensitive); Column_{storetype, nullability, default_value, computed_column_sql, collation, ValueGenerated}_is_set | The DDL-to-DatabaseModel reader recovers tables, views, keys, indexes, FKs, collation, generated columns and autoincrement from `sqlite_master` and PRAGMAs | Introspection | `SqliteDatabaseModelFactory` | Model |
| F `…Column_ClrType_is_set_when_{data, no_data, insane}` | With no or unknown declared type, the CLR type is inferred from stored values' storage classes | Dynamic typing | same | Model |
| F `…Simple_{bool, byte, short, int, long, float, double, decimal(+Danish locale), string, Guid, DateTime, DateOnly, TimeOnly, DateTimeOffset}_literals_are_parsed_for_HasDefaultValue`; `Non_literal_or_non_parsable_DateTime_default_values_are_passed_through` | Default-value SQL text is parsed back into typed CLR defaults, independent of culture | Default parsing | same | Model |
| F `…Warn_for_schema_filtering`, `Warn_missing_{table, principal_table_for_foreign_key, principal_column_for_foreign_key}` | Schema filters produce a warning (no schemas). Dangling references produce warnings | Diagnostics | `SqliteEventId`/scaffolding events | Model (log) |
| F `…Create_primary_key_when_integer_primary_key_aliased_to_rowid` | (skipped, issue 8802) A rowid-alias PK is scaffolded | rowid | — | (skipped) |

### 2.9 Metadata API (SRID, RETURNING)

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| U `SqliteBuilderExtensionsTest.Can_set_srid{,_non_generic,_convention}`; `SqliteMetadataExtensionsTest.Can_get_and_set_srid` | SRID annotation on spatial properties | Spatial metadata | `HasSrid`/`GetSrid` | Model |
| U `…Can_set_UseSqlReturningClause` (generic, non-generic, table name with one or two tables); `SqliteMetadataBuilderExtensionsTest.Can_change_entity_type_UseSqlReturningClause` | Per table (and per split table), you can opt out of `RETURNING` (for triggers) | RETURNING opt-out | `ToTable(tb => tb.UseSqlReturningClause(false))` | Model |
| U `SqliteModelValidatorTest.Detects_incompatible_sql_returning_clause_shared_table` | Entity types sharing a table must agree on the RETURNING setting | Validation | — | Model (error text) |
| U `…Detects_duplicate_column_names_within_hierarchy_with_different_srid` | Shared columns must have the same SRID | Validation | — | Model (error) |
| U `…Detects_schemas`, `Detects_sequences` | A schema or sequence in the model gives a warning (ignored), not an error | No schemas/sequences | `SqliteEventId.SchemaConfiguredWarning`, `SequenceConfiguredWarning` | Model (warning) |
| U `…Detects_{insert,update,delete}_stored_procedures` | Stored-procedure mapping is an error: `StoredProceduresNotSupported` | No sprocs | — | Model (error) |
| U `SqliteEventIdTest.Every_eventId_has_a_logger_method_and_logs_when_level_enabled` | Every SqliteEventId has a logger extension | Diagnostics | `SqliteEventId` | Model |

### 2.10 Miscellaneous provider-only runtime behaviours

| Tests | Behaviour | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| F `Query/BadDataSqliteTest` (8: invalid_cast / null × entity, key, no_tracking, projection) | When a column holds data of the wrong storage class or a NULL in a required column, materialization throws `ErrorMaterializingProperty{InvalidCast,NullReference}` / `ErrorMaterializingValue*` with entity and property names | Dynamic typing at read time | materializer | Exception/message |
| F `NorthwindMiscellaneousQuerySqliteTest.Single_Predicate_Cancellation` | Cancelling a running query surfaces OperationCanceledException (the native interrupt) | Cancellation | `SqliteCommand.Cancel` | Exception |
| F `LazyLoadProxySqliteTest.IsLoaded_is_not_set_if_loading_*_fails` (5) | If a lazy load fails (interceptor throws), IsLoaded stays false and a retry succeeds | Lazy loading | proxies | Exception and Results |
| F `LoggingSqliteTest.AmbientTransactionWarning_throws_by_default` | SaveChanges inside a TransactionScope throws (AmbientTransactionWarning is an error by default), because System.Transactions is unsupported | Ambient transactions | warning config | Exception/message |

---

## 3. Divergences from the shared spec

These are overrides whose expectation differs from the base test, as opposed to overrides that only add a SQL baseline. Counts are distinct test methods, and the same method is often repeated across the TPH/TPC/TPT, SharedType and Split variants.

### 3.1 No APPLY / LATERAL (the largest gap)

The engine has no CROSS APPLY / OUTER APPLY or LATERAL join. Any query the relational pipeline would translate to APPLY throws `InvalidOperationException(SqliteStrings.ApplyNotSupported)`. The tests call `AssertApplyNotSupported(() => base.X())`. There are about 290 assertions, roughly 170 distinct methods, in 40 files.

| Query shape (behaviour) | Representative tests | Files | Asserted by |
|---|---|---|---|
| Correlated collection projection with Take/Skip/Distinct/GroupBy inside the projection | `Take_Select_collection_Take`, `Skip_Take_Select_collection_Skip_Take`, `Correlated_collections_with_Distinct`, `Correlated_collection_after_distinct_3_levels`, `Correlated_collection_after_groupby_with_complex_projection_*`, `Project_collection_of_ints_with_distinct`, `Project_collection_of_nullable_ints_with_paging{,2,3}`, `Select_nested_collection_with_groupby`, `Complex_query_with_groupBy_in_subquery1-5` | GearsOfWar (all 3 inheritance variants), ComplexNavigationsCollections (4 variants), NorthwindSelect, NorthwindGroupBy, PrimitiveCollections | Exception/message |
| Filtered Include with Take/Skip, or with outer-parameter filters | `Filtered_include_Take_with_another_Take_on_top_level`, `Filtered_include_Skip_Take_*`, `Filtered_include_with_multiple_ordering`, `Filtered_include_skip_navigation_order_by_skip_take_*`, `Filtered_include_outer_parameter_used_inside_filter`, `Filtered_include_complex_three_level_*` | ComplexNavigationsCollections*, ManyToMany* (6 variants), NorthwindInclude* (5 variants) | Exception/message |
| SelectMany whose collection selector is correlated to the outer element (or has Take, or a non-equality predicate) | `SelectMany_correlated_with_outer_1-7`, `SelectMany_whose_selector_references_outer_source`, `SelectMany_with_selecting_outer_*`, `SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join`, `Reverse_in_SelectMany_with_Take`, `Column_collection_SelectMany*`, `Json_collection_of_primitives_SelectMany`, `SelectMany_associate_collection`, `SelectMany_nested_collection_on_*_associate` | NorthwindSelect, NorthwindJoin, PrimitiveCollections, Json, Associations/* | Exception/message |
| Include collection with an explicit cross/outer apply | `Include_collection_with_{cross,outer}_apply_with_filter*` | NorthwindInclude* | Exception/message |
| Scalar subquery with FirstOrDefault over a correlated collection, used in a projection | `Select_subquery_{required,optional}_related_FirstOrDefault`, `Select_subquery_FirstOrDefault_complex_collection`, `Correlated_projection_with_first`, `Multiple_collection_navigation_with_FirstOrDefault_chained`, `Take_on_correlated_collection_in_first` | Associations/*, ComplexNavigations, NorthwindMisc | Exception/message |
| Outer parameter in a join key / GroupJoin with DefaultIfEmpty | `Outer_parameter_in_join_key{,_inner_and_outer}`, `Outer_parameter_in_group_join_with_DefaultIfEmpty`, `Let_let_contains_from_outer_let`, `Let_multiple_references_with_reference_to_outer` | NorthwindJoin, ComplexNavigations | Exception/message |
| JSON collection queried with Skip/Take/Distinct/Where inside a projection | `Json_collection_skip_take_in_projection*`, `Json_collection_distinct_in_projection`, `Json_collection_filter_in_projection`, `Json_nested_collection_*_in_projection`, `Json_multiple_collection_projections`, `Json_collection_Select_entity_*_ElementAt` | JsonQuery, OwnedJson*, ComplexJson* | Exception/message |
| Bulk update/delete over APPLY | `Update_with_{cross,outer}_apply_set_constant`, `Update_with_cross_join_{cross,outer}_apply_set_constant`, `Delete_with_{cross,outer}_apply` | NorthwindBulkUpdates | Exception/message |
| Subquery membership with null expansion | `Subquery_projecting_{nullable,non_nullable}_scalar_contains_*_null_expansion{,_negated}` | ComplexNavigations* | Exception/message |
| Miscellaneous (owned collection in materialization interception, set operation in a pending collection, Odata empty-key groupby, `AsEnumerable_in_subquery_for_GroupBy`, `Project_shadow_properties`, `Distinct_projected`) | as named | MaterializationInterception, Northwind*, Associations | Exception/message |
| Same gap, but the base throws a different message, so the override only expects `EqualException` | `OwnedNavigationsSetOperations.Over_associate_collection_projected` | — | Exception |
| Same gap, so the override is a no-op | OwnedJsonProjection `Select_subquery_*`, `SelectMany_*` (6); OwnedJson/OwnedNavigationsCollection `Distinct_projected`; JsonQuery `*_AsNoTrackingWithIdentityResolution` (4) | — | (disabled) |

### 3.2 Engine limits on correlated subqueries (`SqliteException`)

| Behaviour | Tests | Asserted by |
|---|---|---|
| An outer column cannot be used as LIMIT/OFFSET in a correlated subquery (ElementAt(column), inline collection indexed by a column) | `GearsOfWar*.Where_subquery_with_ElementAt_using_column_as_index` (message `no such column: s.Id`); `PrimitiveCollections.Inline_collection_index_Column`, `Inline_collection_value_index_Column`, `Inline_collection_List_value_index_Column` | Exception/message + SQL |
| A deeply nested aggregate subquery references a column that is not visible | `NorthwindGroupBy.GroupBy_aggregate_from_multiple_query_in_same_projection_3` | Exception |
| A keyless entity with a defining query and a navigation, through FromSql (issue 21627) | `NorthwindKeylessEntities.KeylessEntity_with_nav_defining_query` | Exception |
| TPT ExecuteUpdate on a base property through a derived type or OfType generates invalid SQL (issue 31402) | `TPT{,Filters}InheritanceBulkUpdates.Update_base_property_on_derived_type`, `Update_base_type_with_OfType` | Exception |

### 3.3 Temporal types stored as TEXT: translation gaps

| Type / operation | Tests | Reason | Asserted by |
|---|---|---|---|
| DateTimeOffset: every component and Date, Now/UtcNow, ToUnixTime* | `DateTimeOffsetTranslations.{Date, Day, DayOfYear, Hour, Minute, Second, Millisecond, Microsecond, Nanosecond, Month, Year, Now, UtcNow, ToUnixTimeMilliseconds, ToUnixTimeSecond}` (15) | Stored as offset-bearing TEXT, with no translator | Exception (`AssertTranslationFailed`) |
| DateTimeOffset comparison, Contains with < >, `Now - TimeSpan`, `.Date` | `GearsOfWar*.DateTimeOffset_Contains_Less_than_Greater_than`, `DateTimeOffsetNow_minus_timespan`, `DateTimeOffset_Date_returns_datetime` (×3 variants) | TEXT comparison is not chronological across offsets | Exception |
| DateTimeOffset/TimeSpan/ulong Min/Max and OrderBy | BuiltInDataTypes `Cant_query_*` (see 2.3) | Same | Exception/message |
| TimeOnly: components, Add*, IsBetween, FromDateTime/FromTimeSpan comparisons, Subtract, OrderBy(FromTimeSpan) | `TimeOnlyTranslations.*` (17) | No time arithmetic translator | Exception |
| TimeSpan components | `TimeSpanTranslations.{Hours, Minutes, Seconds, Milliseconds, Microseconds, Nanoseconds}` | Stored as TEXT `d.hh:mm:ss` | Exception |
| DateOnly.ToDateTime(…) | `DateOnlyTranslations.ToDateTime_*` (5) | No combine translator | Exception |
| DateTime subtract → TotalDays; nanosecond/microsecond | `DateTimeTranslations.subtract_and_TotalDays`; `NorthwindMisc.Where_nanosecond_and_microsecond_component` | — | Exception |
| DateTime/DateOnly working translations (`strftime`, `date()`, `julianday`, `rtrim(rtrim(strftime('%Y-%m-%d %H:%M:%f'…),'0'),'.')`) | the remaining DateTime/DateOnly overrides | — | Both |
| JSON ExecuteUpdate from a JSON property into a non-JSON column, for DateTime/DateTimeOffset/DateOnly/TimeOnly/TimeSpan/Guid (issue 36688) | `Types/*TypeTest.ExecuteUpdate_within_json_to_nonjson_column` (7) | Only string/numeric/bool are supported | Exception/message |
| TimeOnly property inside JSON cannot be queried | `TimeOnlyTypeTest.Query_property_within_json` | — | Exception |
| `Array_of_DateOnly` primitive collection | NonSharedPrimitiveCollections (skipped, issue 30730) | element matching | (skipped) |

### 3.4 Decimal stored as TEXT

| Behaviour | Tests | Asserted by |
|---|---|---|
| Math.Abs/Floor/Round/Round(digits)/Truncate on decimal do not translate | `MathTranslations.{Abs,Floor,Round,Round_with_digits,Truncate}_decimal` | Exception |
| Sum over Round/Truncate in a projection | `NorthwindFunctions.Sum_over_{round,truncate}_works_correctly_in_projection{,_2}` | Exception |
| Nullable-decimal arithmetic projection | `BuiltInDataTypes.Projecting_arithmetic_operations_on_nullable_decimals` | Exception |
| Decimal aggregates and comparisons go through UDFs (`ef_sum`, `ef_avg`, `ef_min`, `ef_max`, `ef_compare`, `ef_divide`…) | NorthwindAggregateOperators, GroupBy, Ef6GroupBy, Functions, AdHocMiscellaneous overrides | Both |
| `Convert.ToX` (Boolean, Byte, Decimal, Double, Int16/32/64, String) | `MiscellaneousTranslations.Convert_To*` (8) | Exception |

### 3.5 Other translation gaps

| Behaviour | Tests | Asserted by |
|---|---|---|
| Comparing constructed anonymous types or tuples (row values) to a constant does not translate (issue 14672) | `NorthwindWhere.Where_compare_{constructed, tuple_constructed, tuple_create_constructed}_{equal, multi_value_equal, multi_value_not_equal}` (9) | Exception |
| `Contains` over a local array of tuples or anonymous types | `NorthwindAggregateOperators.Contains_with_local_{tuple,anonymous_type}_array_closure` | Exception |
| Bitwise shifts and XOR (no `^` operator in SQLite) | `BitwiseOperatorTranslations.{Left_shift, Right_shift, Xor, Xor_over_boolean}`; `Xor`, `And_over_boolean` skipped (issue 16645) | Exception / skipped |
| byte[] indexer and First() | `ByteArrayTranslations.{Index, First}` | Exception |
| Guid.NewGuid() server-side | `GuidTranslations.NewGuid` | Exception |
| string.Join on a non-aggregate | `StringTranslations.Join_non_aggregate` | Exception |
| Client method in a GroupJoin OrderBy; unknown client method; member binding after a ctor with client eval | `ComplexNavigations*.GroupJoin_client_method_in_OrderBy`, `NorthwindMisc.Client_code_unknown_method`, `NorthwindSelect.Member_binding_after_ctor_arguments_fails_with_client_eval` | Exception/message (same as other providers, re-asserted) |
| JSON element `Json_predicate_on_byte_array` returns different results (issue 33522) | JsonQuery | Exception (EqualException: results differ) |
| `Parameter_collection_Concat_column_collection` returns different results (issue 32561) | PrimitiveCollections | Exception (EqualException) + SQL |
| Nested collections in JSON are not mapped, so edits fail in different ways | `JsonUpdate.Edit_single_property_collection_of_collection_of_*` (≈10: NullReference, IndexOutOfRange, ArgumentOutOfRange, EqualException, NullException) | Exception |
| Spatial `Normalized` has no translation | SpatialQuery (no-op) | (disabled) |
| `Include_collection_with_last_no_orderby` throws `LastUsedWithoutOrderBy` | Northwind{StringInclude, EFPropertyInclude, IncludeNoTracking} | Exception/message |

### 3.6 Schemas, sequences, stored procedures, idempotent scripts

| Behaviour | Tests | Asserted by |
|---|---|---|
| Sequence DDL is not supported | `MigrationsSqlite.{Create_sequence(+long,short,all_settings), Alter_sequence_*(3), Drop_sequence, Rename_sequence, Move_sequence}` (10); `SqlGenerator.Sequence_restart_operation`; `UpdateSqlGenerator.GenerateNextSequenceValueOperation_*` (2) | Exception/message (`SequencesNotSupported`) |
| Schemas are ignored: `Create_schema` creates the table without a schema, `Move_table` emits no SQL, and `Alter_table_add_comment_non_default_schema` rebuilds without a schema | MigrationsSqlite overrides | Both |
| A multi-op rename-then-drop needs a model rebuild: `InvalidMigrationOperation(DropPrimaryKeyOperation)` | `Multiop_rename_table_and_drop` | Exception/message |
| AddForeignKey without principal columns, AlterColumn without a column type | `SqlGenerator.AddForeignKeyOperation_without_principal_columns`, `AlterColumnOperation_without_column_type` | Exception/message |
| Idempotent migration scripts are not supported | `MigrationsInfrastructure.Can_generate_idempotent_up_and_down_scripts{,_noTransactions}` | Exception |
| Stored-procedure mapping is rejected | `SqliteModelBuilder.Can_use_sproc_mapping_with_owned_reference`; validator overrides `Passes_for_stored_procedure_*`, `Passes_on_*_stored_procedure_in_TPT`, `Detects_unmapped_concurrency_token` (6 now expect the error); CompiledModel `ComplexTypes`, `Tpc_Sprocs`, `Sequences` no-op | Exception/message; (disabled) |
| Max length is not enforced (columns are TEXT with no length), so `MaxLengthAttribute_throws…`/`StringLengthAttribute_throws…` only check metadata; the migration `Add_column_with_max_length` emits `TEXT` (issue 3698) | DataAnnotation, Migrations | Model; SQL |
| No rowversion: `TimestampAttribute_throws_if_value_in_database_changed` only checks the IsConcurrencyToken flag; OptimisticConcurrency skips 11 tests (issue 2195) | DataAnnotation, OptimisticConcurrency | Model / (skipped) |
| Comments become `-- comment` lines inside a rebuilt CREATE TABLE | Migrations comment overrides | SQL |
| Required primitive collection with a custom default SQL on an existing table | `Add_required_primitive_collection_with_custom_default_value_sql_to_existing_table` (+ typo twin), skipped (issue 33038) | (skipped) |

### 3.7 Disabled or no-op overrides (behaviour not asserted on SQLite)

| Test | Reason given |
|---|---|
| `CompositeKeyEndToEnd.Can_use_generated_values_in_composite_key_end_to_end` | Generated value in a composite key is not supported (autoincrement only on a single integer PK) |
| `StoreGenerated.Fields_used_correctly_for_store_generated_values` | Computed columns are not supported (in that test's shape) |
| `Updates.Save_with_shared_foreign_key` | Store-generated GUIDs are not supported |
| `CustomConverters.Can_insert_and_read_back_with_case_insensitive_string_key` | The default collation is case-sensitive (BINARY) |
| `GraphUpdates*` owned collections (6, CLR and shadow key) | The default owned-collection pattern uses a composite key, so it cannot autoincrement |
| `FieldMapping` update tests (8) and some projection tests, in the enforce-property/field fixtures | "Cannot force property access when properties missing getter/setter": a fixture-model limitation, not a database one |
| `ProxyGraphUpdates.ChangeTracking.Save_two_entity_cycle_with_lazy_loading` | Needs lazy loading |
| `TPTTableSplitting.Can_insert_dependent_with_just_one_parent` | Not valid for TPT |
| `IncompleteMappingInheritance.Can_insert_update_delete` | Duplicate of another test (it causes a transaction failure) |
| `FromSqlQuery`/`SqlQuery.Bad_data_error_handling_invalid_cast*` (4 each) | Dynamic typing: no cast error is raised (covered instead by BadDataSqliteTest) |
| `Scaffolding: Create_composite_unique_constraint`, `Create_foreign_key_referencing_unique_constraint`, `Create_primary_key_when_integer_primary_key_aliased_to_rowid` | Skipped, issue 8802 |
| `NorthwindBulkUpdates.Update_Where_Join_set_property_from_joined_single_result_scalar`, `Update_with_outer_apply_set_constant` | Skipped, issue 28886 |

---

## 4. Conditions and flags

| Gate | Where | What it gates |
|---|---|---|
| `[SqliteVersionCondition(Min = "3.35.0")]` (class) | `Update/StoreValueGenerationSqliteTest` | The `RETURNING`-based store-value-generation baselines. Below 3.35 the provider would need the legacy SELECT-after-INSERT path, which `StoreValueGenerationWithoutReturningSqliteTest` (file `StoreValueGenerationLegacySqliteTest.cs`) covers instead, with RETURNING turned off. The attribute also supports `Max` and `Skip` (exact version), reading `SqliteConnection.ServerVersion` from a `:memory:` connection |
| `[SpatialiteRequired]` (class) | `SpatialSqliteTest`, `Query/SpatialQuerySqliteTest`, `Scaffolding/CompiledModelSqliteTest` | The `mod_spatialite` extension must load (`SpatialiteLoader.TryLoad`). Otherwise the class is skipped with "mod_spatialite not found" |
| `ConditionalFact/Theory(Skip = "...")` | 27 tests: OptimisticConcurrency (11, issue 2195), GraphUpdates owned collections (6), Scaffolding (3, issue 8802), Bitwise XOR (2, issue 16645), BulkUpdates (2, issue 28886), Migrations (2, issue 33038), NonSharedPrimitiveCollections (1, issue 30730) | See 3.7 |
| `Check_all_tests_overridden` (30 classes) | Query/Translations/*, BulkUpdates/*, GearsOfWar, ComplexType, PrimitiveCollections, ToSql, Owned*StructuralEquality | A meta-test that fails if any base test lacks an SQLite override, so a SQL baseline is mandatory |
| `UseQuerySplittingBehavior(SingleQuery)` default in the test store; split-query suites override it | `SqliteTestStore.AddProviderOptions` | Single-query mode is what triggers most APPLY failures. Split variants (`*SplitQuery*`) still hit APPLY in some shapes |
| `SetParameterizedCollectionMode` override | NonSharedPrimitiveCollections | Selects the parameter (JSON `json_each`) mode for parameterized collections |
| Implicit JSON requirement | all JSON/primitive-collection suites | Requires SQLite ≥ 3.38 (`json_each`, `->>`). There is no explicit gate; the bundled e_sqlite3 is assumed |
| No TransactionScope | `LoggingSqliteTest` | AmbientTransactionWarning is an error by default |

---

## 5. Test infrastructure

| Component | Role | SQLite-specific? |
|---|---|---|
| `SqliteTestStore : RelationalTestStore` | One file DB per store name (`<name>.db`, private or shared cache). `GetOrCreate` (shared, seeded), `GetExisting` (no seed: northwind), `Create` (non-shared). `CommandTimeout=30`, SingleQuery default. Clean via `EnsureClean`, then `EnsureCreatedResilientlyAsync`. Helpers `ExecuteNonQuery`/`ExecuteScalar<T>` with `@p0…` parameters | File-per-store and `Cache=Shared` are SQLite concepts. The rest maps to any "test database per fixture" |
| `SqliteTestStoreFactory` / `SharedCacheSqliteTestStoreFactory` / `SqliteNorthwindTestStoreFactory` | Factory used by every fixture. Adds `AddEntityFrameworkSqlite()` and `AddEntityFrameworkSqliteNetTopologySuite()` | NTS is registered globally for all fixtures |
| `northwind.db` (checked-in binary) | The Northwind seed is shipped as a prebuilt file, not created by script | Yes: other providers seed via SQL script |
| `SqliteDatabaseCleaner : RelationalDatabaseCleaner` | Drops all tables via the scaffolding model factory. Wraps the drop in `PRAGMA foreign_keys=OFF/ON`, skips FKs and indexes, and first calls `DiscardGeometryColumn` over `geometry_columns` for SpatiaLite | Yes (PRAGMA, SpatiaLite metadata) |
| `SqliteDatabaseFacadeTestExtensions.EnsureClean` | Entry point for the cleaner | thin |
| `SqliteTestHelpers : RelationalTestHelpers` | Unit-test service provider: `UseSqlite(new SqliteConnection("Data Source=:memory:"))`, `SqliteLoggingDefinitions` | `:memory:` |
| `SqlitePrecompiledQueryTestHelpers` | Roslyn metadata references for precompiled-query tests | references provider assembly |
| `SqliteVersionConditionAttribute`, `SpatialiteRequiredAttribute` | ITestCondition gates (section 4) | Yes |
| `NorthwindSqliteContext` | Northwind context with no SQLite customization | no |
| `SpatialSqliteFixture` and spatial fixtures | `UseNetTopologySuite()`, column types `POINTZ`/`POINTM`/`POINTZM` | Yes (SpatiaLite types) |
| `SqliteModelBuilderFixture` / `SqliteTestModelBuilderExtensions.UseAutoincrement` | Adapts the generic/non-generic test builders to the provider's `UseAutoincrement` | Yes |
| `F1SqliteFixture` | Concurrency-token model, with no rowversion, so tokens are configured manually | partly |
| Shared harness used unchanged | `TestSqlLoggerFactory.AssertBaseline` (SQL baselines, `GO`-separated for migrations), `AssertQuery`/`AssertCount` (results vs LINQ-to-Objects), `AssertTranslationFailed`, `NonSharedFixture`, `SharedStoreFixtureBase`, `TestStore.NormalizeDelimitersInInterpolatedString` (`[x]` → `"x"`), the `MigrationsTestBase.Test(…)` model differ plus `DatabaseModel` verification via the scaffolding factory | Database-neutral. The SQLite-specific inputs are the baselines (double-quoted identifiers, `@p` parameters, `ef_temp_` rebuild steps, `strftime`/`json_each`/`ef_*` UDFs) |
| SQLite-only test helper in base overrides | `AssertApplyNotSupported(Func<Task>)`: expects `InvalidOperationException` with `SqliteStrings.ApplyNotSupported` | Yes |
| Connection-level requirements the suite relies on | UDFs registered on open: `regexp`, `ef_add`/`ef_divide`/`ef_mod`/`ef_multiply`/`ef_negate`, `ef_compare`, `ef_sum`/`ef_avg`/`ef_min`/`ef_max`, the `EF_DECIMAL` collation; PRAGMA `foreign_keys`; WAL on create | Yes. A homogenized API must let the dialect register such functions on each connection open |
