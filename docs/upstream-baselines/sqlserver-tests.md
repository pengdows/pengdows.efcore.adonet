# Microsoft.EntityFrameworkCore.SqlServer 10.0.11: the test suite as a behaviour record

Sources (read only, nothing run): `ef10011/test/EFCore.SqlServer.FunctionalTests` (508 files) and `ef10011/test/EFCore.SqlServer.Tests` (37 files). Base-class test counts were resolved against `EFCore.Specification.Tests`, `EFCore.Relational.Specification.Tests`, `EFCore.Tests` and `EFCore.Relational.Tests` at tag `v10.0.11` (extracted to `scratchpad/spec10011`). The counts are static: each `[ConditionalFact]/[ConditionalTheory]/[Fact]/[Theory]` method counts once, partial classes are merged, and theory data rows are not expanded. Expect ±2% error from signature parsing. Concept and API names follow `api-inventory/sqlserver.md`.

Abbreviations: "spec" means a shared test base from the EF Core repo. "Provider-added" means a test method the SQL Server suite declares itself.

## 1. Suite map

Totals (static count of `[ConditionalFact]/[ConditionalTheory]/[Fact]/[Theory]` methods; a theory counts once regardless of data rows):

| Measure | Count |
|---|---:|
| Concrete test classes | 392 |
| … deriving from a shared specification/test base (EFCore.Specification.Tests, EFCore.Relational.Specification.Tests, EFCore(.Relational).Tests) | 315 |
| … provider-only classes | 77 |
| Test methods inherited from shared bases (summed per concrete class) | 19413 |
| Of those, overridden by the provider (usually to add `AssertSql` baselines) | 12241 |
| Provider-added test methods inside spec-derived classes | 976 |
| Test methods in provider-only classes | 621 |
| `Check_all_tests_overridden` guard tests (infrastructure, excluded above) | 124 |

### Functional: `.`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| AddConfigureDbContextWithRetry | provider-only | 0 | 6 | 0 | 0 | Results only |
| BatchingTest | provider-only | 0 | 7 | 0 | 1 | Results + some SQL |
| BuiltInDataTypesSqlServerTest | BuiltInDataTypesTestBase | 28 | 54 | 1 | 11 | Results + some SQL |
| CommandConfigurationTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| CommandInterceptionSqlServerTest | CommandInterceptionTestBase (via CommandInterceptionSqlServerTestBase) | 28 | 0 | 0 | 3 | Results + some SQL |
| CommandInterceptionWithDiagnosticsSqlServerTest | CommandInterceptionTestBase (via CommandInterceptionSqlServerTestBase) | 28 | 0 | 0 | 3 | Results + some SQL |
| ComplexTypesTrackingProxiesSqlServerTest | ComplexTypesTrackingRelationalTestBase (via ComplexTypesTrackingSqlServerTestBase) | 128 | 0 | 74 | 0 | Results only |
| ComplexTypesTrackingSqlServerTest | ComplexTypesTrackingRelationalTestBase (via ComplexTypesTrackingSqlServerTestBase) | 128 | 0 | 0 | 0 | Results only |
| CompositeKeyEndToEndSqlServerTest | CompositeKeyEndToEndTestBase | 3 | 0 | 0 | 0 | Results only |
| ComputedColumnTest | provider-only | 0 | 3 | 0 | 0 | Results only |
| ConcurrencyDetectorDisabledSqlServerTest | ConcurrencyDetectorDisabledRelationalTestBase | 9 | 0 | 0 | 0 | Results only |
| ConcurrencyDetectorEnabledSqlServerTest | ConcurrencyDetectorEnabledRelationalTestBase | 9 | 0 | 0 | 0 | Results only |
| ConferencePlannerSqlServerTest | ConferencePlannerTestBase | 22 | 0 | 0 | 0 | Results only |
| ConfigureTwoEngines | provider-only | 0 | 1 | 0 | 0 | Exceptions/messages |
| ConnectionInterceptionSqlServerTest | ConnectionInterceptionTestBase (via ConnectionInterceptionSqlServerTestBase) | 9 | 0 | 0 | 0 | Results only |
| ConnectionInterceptionWithConnectionStringSqlServerTest | ConnectionInterceptionTestBase (via ConnectionInterceptionSqlServerTestBase) | 9 | 0 | 0 | 0 | Results only |
| ConnectionInterceptionWithDiagnosticsSqlServerTest | ConnectionInterceptionTestBase (via ConnectionInterceptionSqlServerTestBase) | 9 | 0 | 0 | 0 | Results only |
| ConnectionSpecificationTest | provider-only | 0 | 22 | 0 | 0 | Results only |
| ConstructorArgsToBuilder | provider-only | 0 | 1 | 0 | 0 | Results only |
| ConstructorArgsToOnConfiguring | provider-only | 0 | 1 | 0 | 0 | Results only |
| ConvertToProviderTypesSqlServerTest | ConvertToProviderTypesTestBase | 30 | 1 | 1 | 0 | Results only |
| CustomConvertersSqlServerTest | CustomConvertersTestBase | 53 | 2 | 10 | 10 | SQL baselines (heavy) |
| DataAnnotationSqlServerTest | DataAnnotationRelationalTestBase | 97 | 5 | 4 | 5 | SQL baselines (heavy) |
| DataBindingSqlServerTest | DataBindingTestBase | 38 | 0 | 0 | 0 | Results only |
| DbContextPoolingTest | provider-only | 0 | 48 | 0 | 0 | Results only |
| DefaultValuesTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| DesignTimeSqlServerTest | DesignTimeTestBase | 2 | 0 | 0 | 0 | Results only |
| EntitySplittingSqlServerTest | EntitySplittingTestBase | 2 | 1 | 1 | 1 | Results + some SQL |
| EverythingIsBytesSqlServerTest | BuiltInDataTypesTestBase | 28 | 1 | 5 | 0 | Results only |
| EverythingIsStringsSqlServerTest | BuiltInDataTypesTestBase | 28 | 1 | 4 | 0 | Results only |
| ExecutionStrategyTest | provider-only | 0 | 10 | 0 | 0 | Results only |
| ExplicitServicesAndConfig | provider-only | 0 | 1 | 0 | 0 | Results only |
| ExplicitServicesAndNoConfig | provider-only | 0 | 1 | 0 | 0 | Exceptions/messages |
| ExplicitServicesImplicitConfig | provider-only | 0 | 1 | 0 | 0 | Results only |
| FieldMappingSqlServerTest | FieldMappingTestBase | 101 | 0 | 0 | 0 | Results only |
| FieldsOnlyLoadSqlServerTest | FieldsOnlyLoadTestBase | 120 | 0 | 0 | 0 | Results only |
| FindSqlServerTestContext | FindTestBase (via FindSqlServerTest) | 69 | 0 | 0 | 18 | SQL baselines (heavy) |
| FindSqlServerTestNonGeneric | FindTestBase (via FindSqlServerTest) | 69 | 0 | 0 | 18 | SQL baselines (heavy) |
| FindSqlServerTestSet | FindTestBase (via FindSqlServerTest) | 69 | 0 | 0 | 18 | SQL baselines (heavy) |
| ImplicitConfigButNoServices | provider-only | 0 | 1 | 0 | 0 | Exceptions/messages |
| ImplicitServicesAndConfig | provider-only | 0 | 1 | 0 | 0 | Results only |
| ImplicitServicesExplicitConfig | provider-only | 0 | 1 | 0 | 0 | Results only |
| InjectContext | provider-only | 0 | 1 | 0 | 0 | Results only |
| InjectContextAndConfiguration | provider-only | 0 | 1 | 0 | 0 | Results only |
| JsonTypesCustomMappingSqlServerTest | JsonTypesRelationalTestBase (via JsonTypesSqlServerTestBase) | 244 | 0 | 0 | 0 | Results only |
| JsonTypesSqlServerTest | JsonTypesRelationalTestBase (via JsonTypesSqlServerTestBase) | 244 | 0 | 0 | 0 | Results only |
| KeysWithConvertersSqlServerTest | KeysWithConvertersTestBase | 47 | 0 | 0 | 0 | Results only |
| LazyLoadProxySqlServerTest | LazyLoadProxyRelationalTestBase | 74 | 0 | 38 | 40 | SQL baselines (heavy) |
| LoadSqlServerTest | LoadTestBase | 203 | 0 | 131 | 133 | SQL baselines (heavy) |
| LoggingSqlServerTest | LoggingRelationalTestBase | 13 | 1 | 0 | 0 | Exceptions/messages |
| ManyToManyFieldsLoadSqlServerTest | ManyToManyFieldsLoadTestBase | 23 | 0 | 7 | 9 | SQL baselines (heavy) |
| ManyToManyLoadSqlServerTest | ManyToManyLoadTestBase | 48 | 0 | 7 | 9 | SQL baselines (heavy) |
| ManyToManyTrackingGeneratedKeysSqlServerTest | ManyToManyTrackingRelationalTestBase (via ManyToManyTrackingSqlServerTestBase) | 67 | 0 | 0 | 0 | Results only |
| ManyToManyTrackingProxyGeneratedKeysSqlServerTest | ManyToManyTrackingRelationalTestBase (via ManyToManyTrackingSqlServerTestBase) | 67 | 0 | 5 | 0 | Results only |
| ManyToManyTrackingProxySqlServerTest | ManyToManyTrackingRelationalTestBase (via ManyToManyTrackingSqlServerTestBase) | 67 | 0 | 5 | 0 | Results only |
| ManyToManyTrackingSqlServerTest | ManyToManyTrackingRelationalTestBase (via ManyToManyTrackingSqlServerTestBase) | 67 | 0 | 0 | 0 | Results only |
| MaterializationInterceptionSqlServerTest | MaterializationInterceptionTestBase | 7 | 0 | 0 | 0 | Results only |
| MemoryOptimizedTablesTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| ModelBuilding101SqlServerTest | ModelBuilding101RelationalTestBase | 124 | 0 | 0 | 0 | Results only |
| MonsterFixupChangedChangingSqlServerTest | MonsterFixupTestBase | 12 | 0 | 0 | 0 | Results only |
| MusicStoreSqlServerTest | MusicStoreTestBase | 16 | 0 | 0 | 0 | Results only |
| NavigationTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| NestedContext | provider-only | 0 | 1 | 0 | 0 | Results only |
| NoEngineConfigured | provider-only | 0 | 1 | 0 | 0 | Exceptions/messages |
| NoServicesAndNoConfig | provider-only | 0 | 1 | 0 | 0 | Exceptions/messages |
| NotificationEntitiesSqlServerTest | NotificationEntitiesTestBase | 2 | 0 | 0 | 0 | Results only |
| OptimisticConcurrencySqlServerTest | OptimisticConcurrencyRelationalTestBase (via OptimisticConcurrencySqlServerTestBase) | 34 | 9 | 0 | 2 | Results + some SQL |
| OptimisticConcurrencyULongSqlServerTest | OptimisticConcurrencyRelationalTestBase (via OptimisticConcurrencySqlServerTestBase) | 34 | 10 | 0 | 2 | Results + some SQL |
| OverzealousInitializationSqlServerTest | OverzealousInitializationTestBase | 1 | 0 | 0 | 0 | Results only |
| PropertyValuesSqlServerTest | PropertyValuesRelationalTestBase | 171 | 0 | 0 | 0 | Results only |
| QueryExpressionInterceptionSqlServerTest | QueryExpressionInterceptionTestBase (via QueryExpressionInterceptionSqlServerTestBase) | 4 | 0 | 0 | 0 | Results only |
| QueryExpressionInterceptionWithDiagnosticsSqlServerTest | QueryExpressionInterceptionTestBase (via QueryExpressionInterceptionSqlServerTestBase) | 4 | 0 | 0 | 0 | Results only |
| SaveChangesInterceptionSqlServerTest | SaveChangesInterceptionTestBase (via SaveChangesInterceptionSqlServerTestBase) | 6 | 1 | 0 | 0 | Results only |
| SaveChangesInterceptionWithDiagnosticsSqlServerTest | SaveChangesInterceptionTestBase (via SaveChangesInterceptionSqlServerTestBase) | 6 | 1 | 0 | 0 | Results only |
| SeedingSqlServerTest | SeedingTestBase | 2 | 0 | 0 | 0 | Results only |
| SequenceEndToEndTest | provider-only | 0 | 7 | 0 | 0 | Results only |
| SequentialGuidEndToEndTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SerializationSqlServerTest | SerializationTestBase | 1 | 0 | 0 | 0 | Results only |
| SpatialSqlServerTest | SpatialTestBase | 5 | 0 | 0 | 0 | Results only |
| SqlServerApiConsistencyTest | ApiConsistencyTestBase | 19 | 0 | 0 | 0 | Results only |
| SqlServerComplianceTest | RelationalComplianceTestBase | 2 | 0 | 0 | 0 | Results only |
| SqlServerDatabaseCreatorCreateTablesTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 3 | 0 | 0 | Results only |
| SqlServerDatabaseCreatorCreateTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 2 | 0 | 0 | Results only |
| SqlServerDatabaseCreatorDeleteTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 3 | 0 | 0 | Exceptions/messages |
| SqlServerDatabaseCreatorEnsureCreatedTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 8 | 0 | 0 | Results only |
| SqlServerDatabaseCreatorEnsureDeletedTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 4 | 0 | 0 | Results only |
| SqlServerDatabaseCreatorExistsTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 4 | 0 | 0 | Results only |
| SqlServerDatabaseCreatorHasTablesTest | provider-only (via SqlServerDatabaseCreatorTestBase) | 0 | 3 | 0 | 0 | Results only |
| SqlServerEndToEndTest | provider-only | 0 | 25 | 0 | 0 | Results only |
| SqlServerQueryTriggersTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerSentinelValueGenerationScenariosTest | provider-only (via SqlServerValueGenerationScenariosTestBase) | 0 | 32 | 0 | 0 | Results only |
| SqlServerServiceCollectionExtensionsTest | RelationalServiceCollectionExtensionsTestBase | 3 | 0 | 0 | 0 | Results only |
| SqlServerTriggersTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerTypeAliasTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlServerValueGenerationScenariosTest | provider-only (via SqlServerValueGenerationScenariosTestBase) | 0 | 32 | 0 | 0 | Results only |
| SqlServerValueGenerationStrategyNoThrowTest | provider-only (via SqlServerValueGenerationConflictTest) | 0 | 1 | 0 | 0 | Results only |
| SqlServerValueGenerationStrategyThrowTest | provider-only (via SqlServerValueGenerationConflictTest) | 0 | 2 | 0 | 0 | Exceptions/messages |
| StoreGeneratedFixupSqlServerTest | StoreGeneratedFixupRelationalTestBase | 118 | 1 | 0 | 0 | Results only |
| StoreGeneratedSentinelSqlServerTest | StoreGeneratedTestBase (via StoreGeneratedSqlServerTestBase) | 60 | 3 | 0 | 0 | Results only |
| StoreGeneratedSqlServerTest | StoreGeneratedTestBase (via StoreGeneratedSqlServerTestBase) | 60 | 3 | 0 | 0 | Results only |
| TPTTableSplittingSqlServerTest | TPTTableSplittingTestBase | 26 | 0 | 11 | 10 | SQL baselines (heavy) |
| TableSplittingSqlServerTest | TableSplittingTestBase | 26 | 0 | 11 | 11 | SQL baselines (heavy) |
| TpcManyToManyTrackingSqlServerTest | ManyToManyTrackingRelationalTestBase (via ManyToManyTrackingSqlServerTestBase) | 67 | 0 | 0 | 0 | Results only |
| TptManyToManyTrackingSqlServerTest | ManyToManyTrackingRelationalTestBase (via ManyToManyTrackingSqlServerTestBase) | 67 | 0 | 0 | 0 | Results only |
| TransactionInterceptionSqlServerTest | TransactionInterceptionTestBase (via TransactionInterceptionSqlServerTestBase) | 17 | 0 | 1 | 0 | Results only |
| TransactionInterceptionWithDiagnosticsSqlServerTest | TransactionInterceptionTestBase (via TransactionInterceptionSqlServerTestBase) | 17 | 0 | 1 | 0 | Results only |
| TransactionSqlServerTest | TransactionTestBase | 44 | 1 | 3 | 0 | Results only |
| TwoDatabasesSqlServerTest | TwoDatabasesTestBase | 2 | 0 | 1 | 0 | Results only |
| ValueConvertersEndToEndSqlServerTest | ValueConvertersEndToEndTestBase | 25 | 4 | 0 | 0 | SQL/DDL text |
| WithConstructorsSqlServerTest | WithConstructorsTestBase | 41 | 0 | 0 | 0 | Results only |

### Functional: `BulkUpdates`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| NonSharedModelBulkUpdatesSqlServerTest | NonSharedModelBulkUpdatesRelationalTestBase | 17 | 0 | 17 | 19 | SQL baselines (heavy) |
| NorthwindBulkUpdatesSqlServerTest | NorthwindBulkUpdatesRelationalTestBase | 92 | 0 | 94 | 96 | SQL baselines (heavy) |
| TPCFiltersInheritanceBulkUpdatesSqlServerTest | TPCFiltersInheritanceBulkUpdatesTestBase | 18 | 0 | 18 | 20 | SQL baselines (heavy) |
| TPCInheritanceBulkUpdatesSqlServerTest | TPCInheritanceBulkUpdatesTestBase | 20 | 0 | 20 | 22 | SQL baselines (heavy) |
| TPHFiltersInheritanceBulkUpdatesSqlServerTest | FiltersInheritanceBulkUpdatesRelationalTestBase | 18 | 0 | 18 | 20 | SQL baselines (heavy) |
| TPHInheritanceBulkUpdatesSqlServerTest | TPHInheritanceBulkUpdatesTestBase | 20 | 0 | 20 | 22 | SQL baselines (heavy) |
| TPTFiltersInheritanceBulkUpdatesSqlServerTest | TPTFiltersInheritanceBulkUpdatesTestBase | 18 | 0 | 18 | 20 | SQL baselines (heavy) |
| TPTInheritanceBulkUpdatesSqlServerTest | TPTInheritanceBulkUpdatesTestBase | 20 | 0 | 20 | 22 | SQL baselines (heavy) |

### Functional: `GraphUpdates`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| ChangeTracking | ProxyGraphUpdatesTestBase (via ProxyGraphUpdatesSqlServerTestBase) | 85 | 0 | 1 | 0 | Results only |
| ChangeTrackingAndLazyLoading | ProxyGraphUpdatesTestBase (via ProxyGraphUpdatesSqlServerTestBase) | 85 | 0 | 0 | 0 | Results only |
| GraphUpdatesSqlServerClientCascadeTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 0 | 0 | Results only |
| GraphUpdatesSqlServerClientNoActionTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 0 | 0 | Results only |
| GraphUpdatesSqlServerHiLoTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 0 | 0 | Results only |
| GraphUpdatesSqlServerIdentityTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 0 | 0 | Results only |
| GraphUpdatesSqlServerOwnedTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 22 | 0 | Results only |
| GraphUpdatesSqlServerSequenceTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 0 | 0 | Results only |
| GraphUpdatesSqlServerTptIdentityTest | GraphUpdatesTestBase (via GraphUpdatesSqlServerTestBase) | 127 | 1 | 0 | 0 | Results only |
| LazyLoading | ProxyGraphUpdatesTestBase (via ProxyGraphUpdatesSqlServerTestBase) | 85 | 0 | 0 | 0 | Results only |

### Functional: `Migrations`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| MigrationsInfrastructureSqlServerTest | MigrationsInfrastructureTestBase | 25 | 11 | 14 | 0 | Exceptions/messages |
| MigrationsSqlServerTest | MigrationsTestBase | 105 | 216 | 120 | 175 | SQL baselines (heavy) |
| SqlServerMigrationsSqlGeneratorTest | MigrationsSqlGeneratorTestBase | 48 | 66 | 34 | 93 | SQL baselines (heavy) |

### Functional: `ModelBuilding`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerGenericComplexCollection | RelationalComplexCollectionTestBase (via SqlServerComplexCollection) | 46 | 0 | 0 | 0 | Results only |
| SqlServerGenericComplexType | RelationalComplexTypeTestBase (via SqlServerComplexType) | 96 | 0 | 0 | 0 | Results only |
| SqlServerGenericInheritance | RelationalInheritanceTestBase (via SqlServerInheritance) | 31 | 11 | 0 | 0 | SQL/DDL text |
| SqlServerGenericManyToMany | RelationalManyToManyTestBase (via SqlServerManyToMany) | 36 | 2 | 0 | 0 | Results only |
| SqlServerGenericManyToOne | RelationalManyToOneTestBase (via SqlServerManyToOne) | 59 | 0 | 0 | 0 | Results only |
| SqlServerGenericNonRelationship | RelationalNonRelationshipTestBase (via SqlServerNonRelationship) | 173 | 9 | 0 | 0 | Exceptions/messages |
| SqlServerGenericOneToMany | RelationalOneToManyTestBase (via SqlServerOneToMany) | 90 | 1 | 0 | 0 | Results only |
| SqlServerGenericOneToOne | RelationalOneToOneTestBase (via SqlServerOneToOne) | 142 | 0 | 0 | 0 | Results only |
| SqlServerGenericOwnedTypes | RelationalOwnedTypesTestBase (via SqlServerOwnedTypes) | 63 | 23 | 0 | 0 | Results only |
| SqlServerNonGenericComplexCollection | RelationalComplexCollectionTestBase (via SqlServerComplexCollection) | 46 | 0 | 0 | 0 | Results only |
| SqlServerNonGenericComplexType | RelationalComplexTypeTestBase (via SqlServerComplexType) | 96 | 0 | 0 | 0 | Results only |
| SqlServerNonGenericInheritance | RelationalInheritanceTestBase (via SqlServerInheritance) | 31 | 11 | 0 | 0 | SQL/DDL text |
| SqlServerNonGenericManyToMany | RelationalManyToManyTestBase (via SqlServerManyToMany) | 36 | 2 | 0 | 0 | Results only |
| SqlServerNonGenericManyToOne | RelationalManyToOneTestBase (via SqlServerManyToOne) | 59 | 0 | 0 | 0 | Results only |
| SqlServerNonGenericNonRelationship | RelationalNonRelationshipTestBase (via SqlServerNonRelationship) | 173 | 9 | 0 | 0 | Exceptions/messages |
| SqlServerNonGenericOneToMany | RelationalOneToManyTestBase (via SqlServerOneToMany) | 90 | 1 | 0 | 0 | Results only |
| SqlServerNonGenericOneToOne | RelationalOneToOneTestBase (via SqlServerOneToOne) | 142 | 0 | 0 | 0 | Results only |
| SqlServerNonGenericOwnedTypes | RelationalOwnedTypesTestBase (via SqlServerOwnedTypes) | 63 | 23 | 0 | 0 | Results only |

### Functional: `Query`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| AdHocAdvancedMappingsQuerySqlServerTest | AdHocAdvancedMappingsQueryRelationalTestBase | 19 | 0 | 19 | 19 | SQL baselines (heavy) |
| AdHocComplexTypeQuerySqlServerTest | AdHocComplexTypeQueryRelationalTestBase | 14 | 0 | 3 | 4 | Results + some SQL |
| AdHocJsonQuerySqlServerJsonTypeTest | AdHocJsonQueryRelationalTestBase (via AdHocJsonQuerySqlServerTestBase) | 46 | 3 | 9 | 0 | Results only |
| AdHocJsonQuerySqlServerTest | AdHocJsonQueryRelationalTestBase (via AdHocJsonQuerySqlServerTestBase) | 46 | 3 | 0 | 0 | Results only |
| AdHocManyToManyQuerySqlServerTest | AdHocManyToManyQueryRelationalTestBase | 2 | 0 | 2 | 2 | Results + some SQL |
| AdHocMiscellaneousQuerySqlServerTest | AdHocMiscellaneousQueryRelationalTestBase | 46 | 23 | 43 | 64 | SQL baselines (heavy) |
| AdHocNavigationsQuerySqlServerTest | AdHocNavigationsQueryRelationalTestBase | 21 | 1 | 21 | 23 | SQL baselines (heavy) |
| AdHocPrecompiledQuerySqlServerTest | AdHocPrecompiledQueryRelationalTestBase | 7 | 0 | 7 | 7 | SQL baselines (heavy) |
| AdHocQueryFiltersQuerySqlServerTest | AdHocQueryFiltersQueryRelationalTestBase | 21 | 2 | 18 | 20 | SQL baselines (heavy) |
| AdHocQuerySplittingQuerySqlServerTest | AdHocQuerySplittingQueryTestBase | 10 | 1 | 6 | 7 | SQL baselines (heavy) |
| ComplexNavigationsCollectionsQuerySqlServerTest | ComplexNavigationsCollectionsQueryRelationalTestBase | 156 | 0 | 156 | 157 | SQL baselines (heavy) |
| ComplexNavigationsCollectionsSharedTypeQuerySqlServerTest | ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase | 156 | 0 | 156 | 157 | SQL baselines (heavy) |
| ComplexNavigationsCollectionsSplitQuerySqlServerTest | ComplexNavigationsCollectionsSplitQueryRelationalTestBase | 156 | 0 | 156 | 157 | SQL baselines (heavy) |
| ComplexNavigationsCollectionsSplitSharedTypeQuerySqlServerTest | ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase | 156 | 0 | 0 | 0 | Results only |
| ComplexNavigationsQuerySqlServer160Test | ComplexNavigationsQueryRelationalTestBase | 308 | 3 | 308 | 312 | SQL baselines (heavy) |
| ComplexNavigationsQuerySqlServerTest | ComplexNavigationsQueryRelationalTestBase | 308 | 3 | 308 | 312 | SQL baselines (heavy) |
| ComplexNavigationsSharedTypeQuerySqlServer160Test | ComplexNavigationsSharedTypeQueryRelationalTestBase | 306 | 2 | 308 | 311 | SQL baselines (heavy) |
| ComplexNavigationsSharedTypeQuerySqlServerTest | ComplexNavigationsSharedTypeQueryRelationalTestBase | 306 | 2 | 308 | 311 | SQL baselines (heavy) |
| ComplexTypeQuerySqlServerTest | ComplexTypeQueryRelationalTestBase | 74 | 8 | 74 | 77 | SQL baselines (heavy) |
| CompositeKeysQuerySqlServerTest | CompositeKeysQueryRelationalTestBase | 7 | 0 | 7 | 8 | SQL baselines (heavy) |
| CompositeKeysSplitQuerySqlServerTest | CompositeKeysSplitQueryRelationalTestBase | 7 | 0 | 7 | 8 | SQL baselines (heavy) |
| Ef6GroupBySqlServerTest | Ef6GroupByTestBase | 55 | 0 | 55 | 56 | SQL baselines (heavy) |
| EntitySplittingQuerySqlServerTest | EntitySplittingQueryTestBase | 39 | 0 | 58 | 58 | SQL baselines (heavy) |
| FromSqlQuerySqlServerTest | FromSqlQueryTestBase | 75 | 1 | 57 | 58 | SQL baselines (heavy) |
| FromSqlSprocQuerySqlServerTest | FromSqlSprocQueryTestBase | 24 | 0 | 11 | 12 | SQL baselines (heavy) |
| FunkyDataQueryAzureSynapseTest | FunkyDataQueryTestBase (via FunkyDataQuerySqlServerBaseTest) | 19 | 0 | 19 | 19 | SQL baselines (heavy) |
| FunkyDataQuerySqlServerTest | FunkyDataQueryTestBase (via FunkyDataQuerySqlServerBaseTest) | 19 | 0 | 19 | 19 | SQL baselines (heavy) |
| GearsOfWarFromSqlQuerySqlServerTest | GearsOfWarFromSqlQueryTestBase | 1 | 0 | 1 | 0 | Results only |
| GearsOfWarQuerySqlServerTest | GearsOfWarQueryRelationalTestBase | 595 | 8 | 596 | 604 | SQL baselines (heavy) |
| IncludeOneToOneSqlServerTest | IncludeOneToOneTestBase | 12 | 0 | 4 | 5 | SQL baselines (heavy) |
| IncompleteMappingInheritanceQuerySqlServerTest | TPHInheritanceQueryTestBase | 55 | 1 | 55 | 55 | SQL baselines (heavy) |
| InheritanceRelationshipsQuerySqlServerTest | InheritanceRelationshipsQueryRelationalTestBase | 70 | 0 | 70 | 71 | SQL baselines (heavy) |
| JsonQueryJsonTypeSqlServerTest | JsonQueryRelationalTestBase | 226 | 0 | 206 | 221 | SQL baselines (heavy) |
| JsonQuerySqlServerTest | JsonQueryRelationalTestBase | 226 | 0 | 206 | 221 | SQL baselines (heavy) |
| ManyToManyNoTrackingQuerySqlServerTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 74 | 75 | SQL baselines (heavy) |
| ManyToManyQuerySqlServerTest | ManyToManyQueryRelationalTestBase | 123 | 0 | 76 | 77 | SQL baselines (heavy) |
| MappingQuerySqlServerTest | MappingQueryTestBase | 4 | 0 | 4 | 5 | SQL baselines (heavy) |
| NonSharedPrimitiveCollectionsQuerySqlServerTest | NonSharedPrimitiveCollectionsQueryRelationalTestBase | 36 | 22 | 38 | 59 | SQL baselines (heavy) |
| NorthwindAggregateOperatorsQuerySqlServerTest | NorthwindAggregateOperatorsQueryRelationalTestBase | 211 | 0 | 211 | 211 | SQL baselines (heavy) |
| NorthwindAsNoTrackingQuerySqlServerTest | NorthwindAsNoTrackingQueryTestBase | 11 | 0 | 11 | 12 | SQL baselines (heavy) |
| NorthwindAsTrackingQuerySqlServerTest | NorthwindAsTrackingQueryTestBase | 5 | 0 | 5 | 6 | SQL baselines (heavy) |
| NorthwindChangeTrackingQuerySqlServerTest | NorthwindChangeTrackingQueryTestBase | 17 | 0 | 17 | 18 | SQL baselines (heavy) |
| NorthwindCompiledQuerySqlServerTest | NorthwindCompiledQueryTestBase | 32 | 0 | 32 | 33 | SQL baselines (heavy) |
| NorthwindDbFunctionsQuerySqlServer160Test | NorthwindDbFunctionsQueryRelationalTestBase | 15 | 64 | 15 | 71 | SQL baselines (heavy) |
| NorthwindDbFunctionsQuerySqlServerTest | NorthwindDbFunctionsQueryRelationalTestBase | 15 | 64 | 15 | 67 | SQL baselines (heavy) |
| NorthwindEFPropertyIncludeQuerySqlServerTest | NorthwindEFPropertyIncludeQueryTestBase | 119 | 0 | 119 | 120 | SQL baselines (heavy) |
| NorthwindFunctionsQuerySqlServer160Test | NorthwindFunctionsQueryRelationalTestBase | 10 | 2 | 10 | 13 | SQL baselines (heavy) |
| NorthwindFunctionsQuerySqlServerTest | NorthwindFunctionsQueryRelationalTestBase | 10 | 2 | 10 | 13 | SQL baselines (heavy) |
| NorthwindGroupByQuerySqlServerTest | NorthwindGroupByQueryRelationalTestBase | 258 | 0 | 258 | 259 | SQL baselines (heavy) |
| NorthwindIncludeNoTrackingQuerySqlServerTest | NorthwindIncludeNoTrackingQueryTestBase | 118 | 0 | 118 | 118 | SQL baselines (heavy) |
| NorthwindIncludeQuerySqlServerTest | NorthwindIncludeQueryRelationalTestBase | 118 | 1 | 118 | 119 | SQL baselines (heavy) |
| NorthwindJoinQuerySqlServerTest | NorthwindJoinQueryRelationalTestBase | 68 | 0 | 68 | 69 | SQL baselines (heavy) |
| NorthwindKeylessEntitiesQuerySqlServerTest | NorthwindKeylessEntitiesQueryRelationalTestBase | 20 | 0 | 20 | 21 | SQL baselines (heavy) |
| NorthwindMiscellaneousQuerySqlServerTest | NorthwindMiscellaneousQueryRelationalTestBase | 471 | 5 | 471 | 471 | SQL baselines (heavy) |
| NorthwindNavigationsQuerySqlServerTest | NorthwindNavigationsQueryRelationalTestBase | 73 | 0 | 73 | 74 | SQL baselines (heavy) |
| NorthwindQueryFiltersQuerySqlServerTest | NorthwindQueryFiltersQueryTestBase | 17 | 2 | 17 | 20 | SQL baselines (heavy) |
| NorthwindQueryTaggingQuerySqlServerTest | NorthwindQueryTaggingQueryTestBase | 9 | 1 | 9 | 11 | SQL baselines (heavy) |
| NorthwindSelectQuerySqlServerTest | NorthwindSelectQueryRelationalTestBase | 186 | 0 | 186 | 186 | SQL baselines (heavy) |
| NorthwindSetOperationsQuerySqlServerTest | NorthwindSetOperationsQueryRelationalTestBase | 91 | 2 | 91 | 94 | SQL baselines (heavy) |
| NorthwindSplitIncludeNoTrackingQuerySqlServerTest | NorthwindSplitIncludeNoTrackingQueryTestBase | 118 | 0 | 118 | 119 | SQL baselines (heavy) |
| NorthwindSplitIncludeQuerySqlServerTest | NorthwindSplitIncludeQueryTestBase | 118 | 1 | 118 | 119 | SQL baselines (heavy) |
| NorthwindSqlQuerySqlServerTest | NorthwindSqlQueryTestBase | 4 | 0 | 4 | 5 | SQL baselines (heavy) |
| NorthwindStringIncludeQuerySqlServerTest | NorthwindStringIncludeQueryTestBase | 119 | 0 | 119 | 120 | SQL baselines (heavy) |
| NorthwindWhereQuerySqlServerTest | NorthwindWhereQueryRelationalTestBase | 203 | 0 | 203 | 205 | SQL baselines (heavy) |
| NullKeysSqlServerTest | NullKeysTestBase | 5 | 0 | 0 | 0 | Results only |
| NullSemanticsQuerySqlServerTest | NullSemanticsQueryTestBase | 168 | 0 | 165 | 166 | SQL baselines (heavy) |
| OperatorsQuerySqlServerTest | OperatorsQueryTestBase | 10 | 4 | 10 | 15 | SQL baselines (heavy) |
| OptionalDependentQuerySqlServerTest | OptionalDependentQueryTestBase | 10 | 0 | 10 | 11 | SQL baselines (heavy) |
| OwnedEntityQuerySqlServerTest | OwnedEntityQueryRelationalTestBase | 23 | 3 | 20 | 24 | SQL baselines (heavy) |
| OwnedQuerySqlServerTest | OwnedQueryRelationalTestBase | 104 | 0 | 93 | 94 | SQL baselines (heavy) |
| PrecompiledQuerySqlServerTest | PrecompiledQueryRelationalTestBase | 120 | 1 | 120 | 122 | SQL baselines (heavy) |
| PrecompiledSqlPregenerationQuerySqlServerTest | PrecompiledSqlPregenerationQueryRelationalTestBase | 14 | 1 | 14 | 15 | SQL baselines (heavy) |
| PrimitiveCollectionsQueryOldSqlServerTest | PrimitiveCollectionsQueryRelationalTestBase | 158 | 1 | 158 | 91 | SQL baselines (heavy) |
| PrimitiveCollectionsQuerySqlServer160Test | PrimitiveCollectionsQueryRelationalTestBase | 158 | 1 | 158 | 147 | SQL baselines (heavy) |
| PrimitiveCollectionsQuerySqlServerJsonTypeTest | PrimitiveCollectionsQueryRelationalTestBase | 158 | 1 | 158 | 147 | SQL baselines (heavy) |
| PrimitiveCollectionsQuerySqlServerTest | PrimitiveCollectionsQueryRelationalTestBase | 158 | 4 | 158 | 147 | SQL baselines (heavy) |
| QueryFilterFuncletizationSqlServerTest | QueryFilterFuncletizationTestBase | 28 | 0 | 27 | 28 | SQL baselines (heavy) |
| QueryLoggingSqlServerTest | provider-only | 0 | 10 | 0 | 0 | Results only |
| QueryNoClientEvalSqlServerTest | QueryNoClientEvalTestBase | 14 | 0 | 0 | 0 | Results only |
| SharedTypeQuerySqlServerTest | SharedTypeQueryRelationalTestBase | 4 | 0 | 2 | 2 | Results + some SQL |
| SpatialQuerySqlServerGeographyTest | SpatialQueryRelationalTestBase | 84 | 1 | 84 | 64 | SQL baselines (heavy) |
| SpatialQuerySqlServerGeometryTest | SpatialQueryRelationalTestBase | 84 | 1 | 84 | 79 | SQL baselines (heavy) |
| SqlExecutorSqlServerTest | SqlExecutorTestBase | 14 | 0 | 10 | 11 | SQL baselines (heavy) |
| SqlQuerySqlServerTest | SqlQueryTestBase | 61 | 0 | 46 | 47 | SQL baselines (heavy) |
| TPCFiltersInheritanceQuerySqlServerTest | TPCFiltersInheritanceQueryTestBase | 11 | 0 | 11 | 12 | SQL baselines (heavy) |
| TPCGearsOfWarQuerySqlServerTest | TPCGearsOfWarQueryRelationalTestBase | 595 | 1 | 596 | 597 | SQL baselines (heavy) |
| TPCInheritanceQueryHiLoSqlServerTest | TPCInheritanceQueryTestBase (via TPCInheritanceQuerySqlServerTestBase) | 53 | 0 | 0 | 0 | Results only |
| TPCInheritanceQuerySqlServerTest | TPCInheritanceQueryTestBase (via TPCInheritanceQuerySqlServerTestBase) | 53 | 0 | 0 | 0 | Results only |
| TPCManyToManyNoTrackingQuerySqlServerTest | TPCManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 124 | 125 | SQL baselines (heavy) |
| TPCManyToManyQuerySqlServerTest | TPCManyToManyQueryRelationalTestBase | 123 | 0 | 123 | 124 | SQL baselines (heavy) |
| TPCRelationshipsQuerySqlServerTest | TPCRelationshipsQueryTestBase | 70 | 0 | 70 | 71 | SQL baselines (heavy) |
| TPHFiltersInheritanceQuerySqlServerTest | FiltersInheritanceQueryTestBase | 11 | 0 | 11 | 12 | SQL baselines (heavy) |
| TPHInheritanceQuerySqlServerTest | TPHInheritanceQueryTestBase | 55 | 1 | 55 | 55 | SQL baselines (heavy) |
| TPHTemporalFiltersInheritanceQuerySqlServerTest | FiltersInheritanceQueryTestBase | 11 | 0 | 11 | 11 | SQL baselines (heavy) |
| TPTFiltersInheritanceQuerySqlServerTest | TPTFiltersInheritanceQueryTestBase | 11 | 0 | 11 | 12 | SQL baselines (heavy) |
| TPTGearsOfWarQuerySqlServerTest | TPTGearsOfWarQueryRelationalTestBase | 595 | 1 | 596 | 597 | SQL baselines (heavy) |
| TPTInheritanceQuerySqlServerTest | TPTInheritanceQueryTestBase | 53 | 0 | 53 | 53 | SQL baselines (heavy) |
| TPTManyToManyNoTrackingQuerySqlServerTest | TPTManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 124 | 125 | SQL baselines (heavy) |
| TPTManyToManyQuerySqlServerTest | TPTManyToManyQueryRelationalTestBase | 123 | 0 | 123 | 124 | SQL baselines (heavy) |
| TPTRelationshipsQuerySqlServerTest | TPTRelationshipsQueryTestBase | 70 | 0 | 70 | 71 | SQL baselines (heavy) |
| TemporalComplexNavigationsCollectionsQuerySqlServerTest | ComplexNavigationsCollectionsQueryRelationalTestBase | 156 | 0 | 156 | 157 | SQL baselines (heavy) |
| TemporalComplexNavigationsCollectionsSharedTypeQuerySqlServerTest | ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase | 152 | 0 | 118 | 114 | SQL baselines (heavy) |
| TemporalGearsOfWarQuerySqlServerTest | GearsOfWarQueryRelationalTestBase | 595 | 3 | 596 | 598 | SQL baselines (heavy) |
| TemporalManyToManyQuerySqlServerTest | ManyToManyQueryRelationalTestBase | 123 | 0 | 57 | 58 | SQL baselines (heavy) |
| TemporalOwnedQuerySqlServerTest | OwnedQueryRelationalTestBase | 104 | 2 | 81 | 78 | SQL baselines (heavy) |
| TemporalTableSqlServerTest | NonSharedModelTestBase | 0 | 12 | 0 | 12 | SQL baselines (heavy) |
| ToSqlQuerySqlServerTest | ToSqlQueryTestBase | 1 | 0 | 1 | 2 | Results + some SQL |
| UdfDbFunctionSqlServerTests | UdfDbFunctionTestBase | 106 | 0 | 71 | 72 | SQL baselines (heavy) |
| WarningsSqlServerTest | WarningsTestBase | 11 | 0 | 0 | 0 | Results only |

### Functional: `Query/Associations/ComplexJson`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| ComplexJsonBulkUpdateSqlServerTest | ComplexJsonBulkUpdateRelationalTestBase | 33 | 0 | 33 | 49 | SQL baselines (heavy) |
| ComplexJsonCollectionSqlServerTest | ComplexJsonCollectionRelationalTestBase | 13 | 0 | 14 | 22 | SQL baselines (heavy) |
| ComplexJsonMiscellaneousSqlServerTest | ComplexJsonMiscellaneousRelationalTestBase | 7 | 0 | 7 | 12 | SQL baselines (heavy) |
| ComplexJsonPrimitiveCollectionSqlServerTest | ComplexJsonPrimitiveCollectionRelationalTestBase | 6 | 0 | 6 | 7 | SQL baselines (heavy) |
| ComplexJsonProjectionSqlServerTest | ComplexJsonProjectionRelationalTestBase | 28 | 0 | 28 | 36 | SQL baselines (heavy) |
| ComplexJsonSetOperationsSqlServerTest | ComplexJsonSetOperationsRelationalTestBase | 5 | 0 | 5 | 6 | SQL baselines (heavy) |
| ComplexJsonStructuralEqualitySqlServerTest | ComplexJsonStructuralEqualityRelationalTestBase | 16 | 0 | 16 | 29 | SQL baselines (heavy) |

### Functional: `Query/Associations/ComplexTableSplitting`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| ComplexTableSplittingBulkUpdateSqlServerTest | ComplexTableSplittingBulkUpdateRelationalTestBase | 33 | 0 | 33 | 33 | SQL baselines (heavy) |
| ComplexTableSplittingMiscellaneousSqlServerTest | ComplexTableSplittingMiscellaneousRelationalTestBase | 7 | 0 | 7 | 7 | SQL baselines (heavy) |
| ComplexTableSplittingPrimitiveCollectionSqlServerTest | ComplexTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 0 | 6 | 6 | SQL baselines (heavy) |
| ComplexTableSplittingProjectionSqlServerTest | ComplexTableSplittingProjectionRelationalTestBase | 28 | 0 | 28 | 28 | SQL baselines (heavy) |
| ComplexTableSplittingStructuralEqualitySqlServerTest | ComplexTableSplittingStructuralEqualityRelationalTestBase | 16 | 0 | 16 | 16 | SQL baselines (heavy) |

### Functional: `Query/Associations/Navigations`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| NavigationsCollectionSqlServerTest | NavigationsCollectionRelationalTestBase | 13 | 0 | 14 | 14 | SQL baselines (heavy) |
| NavigationsIncludeSqlServerTest | NavigationsIncludeRelationalTestBase | 9 | 0 | 9 | 9 | SQL baselines (heavy) |
| NavigationsMiscellaneousSqlServerTest | NavigationsMiscellaneousRelationalTestBase | 4 | 0 | 4 | 4 | Results + some SQL |
| NavigationsPrimitiveCollectionSqlServerTest | NavigationsPrimitiveCollectionRelationalTestBase | 6 | 0 | 6 | 6 | SQL baselines (heavy) |
| NavigationsProjectionSqlServerTest | NavigationsProjectionRelationalTestBase | 24 | 0 | 24 | 24 | SQL baselines (heavy) |
| NavigationsSetOperationsSqlServerTest | NavigationsSetOperationsRelationalTestBase | 5 | 0 | 5 | 5 | SQL baselines (heavy) |
| NavigationsStructuralEqualitySqlServerTest | NavigationsStructuralEqualityRelationalTestBase | 15 | 0 | 15 | 15 | SQL baselines (heavy) |

### Functional: `Query/Associations/OwnedJson`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| OwnedJsonBulkUpdateSqlServerTest | OwnedJsonBulkUpdateRelationalTestBase | 3 | 0 | 0 | 0 | Results only |
| OwnedJsonCollectionSqlServerTest | OwnedJsonCollectionRelationalTestBase | 13 | 0 | 14 | 25 | SQL baselines (heavy) |
| OwnedJsonMiscellaneousSqlServerTest | OwnedJsonMiscellaneousRelationalTestBase | 4 | 0 | 4 | 7 | SQL baselines (heavy) |
| OwnedJsonPrimitiveCollectionSqlServerTest | OwnedJsonPrimitiveCollectionRelationalTestBase | 6 | 0 | 6 | 7 | SQL baselines (heavy) |
| OwnedJsonProjectionSqlServerTest | OwnedJsonProjectionRelationalTestBase | 24 | 0 | 24 | 32 | SQL baselines (heavy) |
| OwnedJsonStructuralEqualitySqlServerTest | OwnedJsonStructuralEqualityRelationalTestBase | 15 | 0 | 15 | 15 | SQL baselines (heavy) |

### Functional: `Query/Associations/OwnedNavigations`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| OwnedNavigationsCollectionSqlServerTest | OwnedNavigationsCollectionRelationalTestBase | 13 | 0 | 14 | 14 | SQL baselines (heavy) |
| OwnedNavigationsMiscellaneousSqlServerTest | OwnedNavigationsMiscellaneousRelationalTestBase | 4 | 0 | 4 | 4 | Results + some SQL |
| OwnedNavigationsPrimitiveCollectionTestBase | OwnedNavigationsPrimitiveCollectionRelationalTestBase | 6 | 0 | 6 | 6 | SQL baselines (heavy) |
| OwnedNavigationsProjectionSqlServerTest | OwnedNavigationsProjectionRelationalTestBase | 24 | 0 | 24 | 24 | SQL baselines (heavy) |
| OwnedNavigationsSetOperationsSqlServerTest | OwnedNavigationsSetOperationsRelationalTestBase | 5 | 0 | 5 | 4 | Results + some SQL |
| OwnedNavigationsStructuralEqualitySqlServerTest | OwnedNavigationsStructuralEqualityRelationalTestBase | 15 | 0 | 15 | 15 | SQL baselines (heavy) |

### Functional: `Query/Associations/OwnedTableSplitting`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| OwnedTableSplittingMiscellaneousSqlServerTest | OwnedTableSplittingMiscellaneousRelationalTestBase | 4 | 0 | 4 | 4 | Results + some SQL |
| OwnedTableSplittingPrimitiveCollectionSqlServerTest | OwnedTableSplittingPrimitiveCollectionRelationalTestBase | 6 | 0 | 6 | 6 | SQL baselines (heavy) |
| OwnedTableSplittingProjectionSqlServerTest | OwnedTableSplittingProjectionRelationalTestBase | 24 | 0 | 24 | 24 | SQL baselines (heavy) |
| OwnedTableSplittingStructuralEqualitySqlServerTest | OwnedTableSplittingStructuralEqualityRelationalTestBase | 15 | 0 | 15 | 15 | SQL baselines (heavy) |

### Functional: `Query/Translations`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| ByteArrayTranslationsSqlServerTest | ByteArrayTranslationsTestBase | 7 | 0 | 7 | 8 | SQL baselines (heavy) |
| EnumTranslationsSqlServerTest | EnumTranslationsTestBase | 18 | 0 | 18 | 19 | SQL baselines (heavy) |
| GuidTranslationsSqlServerTest | GuidTranslationsTestBase | 4 | 0 | 4 | 5 | SQL baselines (heavy) |
| MathTranslationsSqlServer160Test | MathTranslationsTestBase | 66 | 0 | 15 | 9 | SQL baselines (heavy) |
| MathTranslationsSqlServerTest | MathTranslationsTestBase | 66 | 0 | 66 | 56 | SQL baselines (heavy) |
| MiscellaneousTranslationsSqlServerTest | MiscellaneousTranslationsRelationalTestBase | 18 | 0 | 18 | 19 | SQL baselines (heavy) |
| StringTranslationsSqlServer160Test | StringTranslationsRelationalTestBase | 104 | 0 | 9 | 8 | SQL baselines (heavy) |
| StringTranslationsSqlServerTest | StringTranslationsRelationalTestBase | 104 | 0 | 104 | 99 | SQL baselines (heavy) |
| VectorTranslationsSqlServerTest | provider-only | 0 | 3 | 0 | 4 | Results + some SQL |

### Functional: `Query/Translations/Operators`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| ArithmeticOperatorTranslationsSqlServerTest | ArithmeticOperatorTranslationsTestBase | 5 | 0 | 5 | 6 | SQL baselines (heavy) |
| BitwiseOperatorTranslationsSqlServerTest | BitwiseOperatorTranslationsTestBase | 15 | 0 | 15 | 14 | SQL baselines (heavy) |
| ComparisonOperatorTranslationsSqlServerTest | ComparisonOperatorTranslationsTestBase | 6 | 0 | 6 | 7 | SQL baselines (heavy) |
| LogicalOperatorTranslationsSqlServerTest | LogicalOperatorTranslationsTestBase | 6 | 0 | 6 | 7 | SQL baselines (heavy) |
| MiscellaneousOperatorTranslationsSqlServerTest | MiscellaneousOperatorTranslationsTestBase | 2 | 0 | 2 | 3 | Results + some SQL |

### Functional: `Query/Translations/Temporal`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| DateOnlyTranslationsSqlServerTest | DateOnlyTranslationsTestBase | 18 | 0 | 18 | 19 | SQL baselines (heavy) |
| DateTimeOffsetTranslationsSqlServerTest | DateTimeOffsetTranslationsTestBase | 24 | 2 | 24 | 27 | SQL baselines (heavy) |
| DateTimeTranslationsSqlServerTest | DateTimeTranslationsTestBase | 19 | 2 | 19 | 21 | SQL baselines (heavy) |
| TimeOnlyTranslationsSqlServerTest | TimeOnlyTranslationsTestBase | 17 | 0 | 17 | 18 | SQL baselines (heavy) |
| TimeSpanTranslationsSqlServerTest | TimeSpanTranslationsTestBase | 6 | 0 | 6 | 7 | SQL baselines (heavy) |

### Functional: `Scaffolding`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| CompiledModelSqlServerTest | CompiledModelRelationalTestBase | 14 | 3 | 0 | 0 | Results only |
| SqlServerDatabaseModelFactoryTest | provider-only | 0 | 105 | 0 | 0 | Results only |

### Functional: `SqlAzure`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlAzureBatchingTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlAzureConnectionTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlAzureDatabaseCreationTest | provider-only | 0 | 3 | 0 | 0 | Results only |
| SqlAzureFundamentalsTest | provider-only | 0 | 4 | 0 | 0 | Results only |

### Functional: `Types`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| BoolTypeTest | RelationalTypeTestBase | 7 | 0 | 1 | 10 | SQL baselines (heavy) |
| ByteArrayTypeTest | RelationalTypeTestBase | 7 | 0 | 2 | 10 | SQL baselines (heavy) |
| ByteTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| DateOnlyTypeTest | RelationalTypeTestBase | 7 | 0 | 3 | 25 | SQL baselines (heavy) |
| DateTimeOffsetTypeTest | RelationalTypeTestBase | 7 | 0 | 3 | 25 | SQL baselines (heavy) |
| DateTimeTypeTest | RelationalTypeTestBase | 7 | 0 | 3 | 25 | SQL baselines (heavy) |
| DecimalTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| DoubleTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| FloatTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| GeometryCollectionTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 2 | 11 | SQL baselines (heavy) |
| GuidTypeTest | RelationalTypeTestBase | 7 | 0 | 2 | 10 | SQL baselines (heavy) |
| IntTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| LineStringTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 1 | 11 | SQL baselines (heavy) |
| LongTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| MultiLineStringTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 1 | 11 | SQL baselines (heavy) |
| MultiPointTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 1 | 11 | SQL baselines (heavy) |
| MultiPolygonTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 1 | 11 | SQL baselines (heavy) |
| PointTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 1 | 11 | SQL baselines (heavy) |
| PolygonTypeTest | RelationalTypeTestBase (via GeometryTypeTestBase) | 7 | 0 | 1 | 11 | SQL baselines (heavy) |
| ShortTypeTest | RelationalTypeTestBase | 7 | 0 | 0 | 0 | Results only |
| StringTypeTest | RelationalTypeTestBase | 7 | 0 | 1 | 10 | SQL baselines (heavy) |
| TimeOnlyTypeTest | RelationalTypeTestBase | 7 | 0 | 3 | 25 | SQL baselines (heavy) |
| TimeSpanTypeTest | RelationalTypeTestBase | 7 | 0 | 3 | 25 | SQL baselines (heavy) |

### Functional: `Update`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| ComplexCollectionJsonUpdateSqlServerTest | ComplexCollectionJsonUpdateTestBase | 20 | 0 | 19 | 19 | SQL baselines (heavy) |
| JsonUpdateJsonTypeSqlServerTest | JsonUpdateTestBase | 132 | 0 | 136 | 131 | SQL baselines (heavy) |
| JsonUpdateSqlServerTest | JsonUpdateTestBase | 134 | 0 | 105 | 100 | SQL baselines (heavy) |
| MismatchedKeyTypesSqlServerTest | provider-only | 0 | 6 | 0 | 0 | Results only |
| NonSharedModelUpdatesSqlServerTest | NonSharedModelUpdatesTestBase | 2 | 1 | 2 | 3 | Results + some SQL |
| SqlServerUpdateSqlGeneratorTest | UpdateSqlGeneratorTestBase | 14 | 5 | 0 | 0 | SQL/DDL text |
| StoreValueGenerationIdentitySqlServerTest | StoreValueGenerationTestBase | 18 | 0 | 19 | 19 | SQL baselines (heavy) |
| StoreValueGenerationIdentityWithoutOutputSqlServerTest | StoreValueGenerationTestBase (via StoreValueGenerationWithoutOutputSqlServerTestBase) | 19 | 2 | 18 | 19 | SQL baselines (heavy) |
| StoreValueGenerationSequenceSqlServerTest | StoreValueGenerationTestBase | 19 | 0 | 18 | 18 | SQL baselines (heavy) |
| StoreValueGenerationSequenceWithoutOutputSqlServerTest | StoreValueGenerationTestBase (via StoreValueGenerationWithoutOutputSqlServerTestBase) | 19 | 2 | 18 | 19 | SQL baselines (heavy) |
| StoredProcedureUpdateSqlServerTest | StoredProcedureUpdateTestBase | 28 | 0 | 28 | 26 | SQL baselines (heavy) |
| UpdatesSqlServerTPCTest | UpdatesRelationalTestBase (via UpdatesSqlServerTestBase) | 29 | 0 | 2 | 1 | Results + some SQL |
| UpdatesSqlServerTPTTest | UpdatesRelationalTestBase (via UpdatesSqlServerTestBase) | 29 | 0 | 2 | 1 | Results + some SQL |
| UpdatesSqlServerTest | UpdatesRelationalTestBase (via UpdatesSqlServerTestBase) | 29 | 0 | 2 | 1 | Results + some SQL |

### Unit: `.`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| DbSetAsTableNameSqlServerTest | DbSetAsTableNameTest | 14 | 0 | 0 | 0 | Results only |
| SqlServerNTSApiConsistencyTest | ApiConsistencyTestBase | 19 | 0 | 0 | 0 | Results only |
| SqlServerOptionsExtensionTest | provider-only | 0 | 2 | 0 | 0 | Results only |

### Unit: `Design`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerDesignTimeProviderServicesTest | DesignTimeProviderServicesTest | 1 | 0 | 0 | 0 | Results only |

### Unit: `Design/Internal`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerAnnotationCodeGeneratorTest | provider-only | 0 | 21 | 0 | 0 | Results only |

### Unit: `Diagnostics`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerEventIdTest | EventIdTestBase | 0 | 1 | 0 | 0 | Results only |

### Unit: `Extensions`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| AzureSqlDbContextOptionsExtensionsTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlServerDatabaseFacadeExtensionsTest | provider-only | 0 | 16 | 0 | 0 | Results only |
| SqlServerDbContextOptionsExtensionsTest | provider-only | 0 | 11 | 0 | 0 | Results only |

### Unit: `Infrastructure`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerModelValidatorTest | RelationalModelValidatorTest | 380 | 69 | 3 | 0 | Results only |

### Unit: `Metadata`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerBuilderExtensionsTest | provider-only | 0 | 58 | 0 | 0 | Results only |
| SqlServerMetadataBuilderExtensionsTest | provider-only | 0 | 14 | 0 | 0 | Results only |
| SqlServerMetadataExtensionsTest | provider-only | 0 | 22 | 0 | 0 | Results only |

### Unit: `Metadata/Conventions`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| RelationalForeignKeyIndexConventionTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerConventionSetBuilderTests | ConventionSetBuilderTests | 4 | 0 | 0 | 0 | Results only |
| SqlServerMemoryOptimizedTablesConventionTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlServerOnDeleteConventionTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlServerOutputClauseConventionTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerValueGenerationStrategyConventionTest | provider-only | 0 | 3 | 0 | 0 | Results only |

### Unit: `Migrations`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerHistoryRepositoryTest | provider-only | 0 | 9 | 0 | 0 | SQL/DDL text |
| SqlServerMigrationBuilderTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerMigrationsAnnotationProviderTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerModelDifferTest | MigrationsModelDifferTestBase | 0 | 42 | 0 | 0 | Results only |

### Unit: `Scaffolding`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerCodeGeneratorTest | provider-only | 0 | 3 | 0 | 0 | Results only |

### Unit: `Storage`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerRetryingExecutionStrategyTests | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlServerSqlGenerationHelperTest | provider-only | 0 | 1 | 0 | 0 | Results only |
| SqlServerStringTypeMappingTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerTypeMappingSourceTest | RelationalTypeMappingSourceTestBase | 0 | 80 | 0 | 0 | Results only |
| SqlServerTypeMappingTest | RelationalTypeMappingTest | 32 | 11 | 12 | 0 | Results only |

### Unit: `Storage/Internal`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerConnectionTest | provider-only | 0 | 7 | 0 | 0 | SQL/DDL text |
| SqlServerDatabaseCreatorTest | provider-only | 0 | 12 | 0 | 0 | Results only |
| SqlServerGeometryTypeMappingTests | provider-only | 0 | 1 | 0 | 0 | Results only |

### Unit: `Update`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerModificationCommandBatchFactoryTest | provider-only | 0 | 2 | 0 | 0 | Results only |
| SqlServerModificationCommandBatchTest | provider-only | 0 | 3 | 0 | 0 | Results only |

### Unit: `ValueGeneration`

| Class | Derives from (spec base) | Inherited spec tests | Provider-added tests | Overrides | AssertSql calls | Assertion mode |
|---|---|---:|---:|---:|---:|---|
| SqlServerSequenceValueGeneratorTest | provider-only | 0 | 10 | 0 | 0 | Results only |
| SqlServerValueGeneratorCacheTest | provider-only | 0 | 21 | 0 | 0 | Results only |
| SqlServerValueGeneratorSelectorTest | provider-only | 0 | 9 | 0 | 0 | Results only |

**Reading the "Assertion mode" column:** "SQL baselines (heavy)" means the class has at least one `AssertSql` per test (≥5 overall). Porting it to another engine means re-baselining the SQL text, while the result checks in the spec base carry over. "Results only" classes compare data or metadata with expected .NET values, so they port without SQL baselines. "SQL/DDL text" classes are unit tests that compare generated SQL or scripts verbatim. Every Query suite (Northwind*, GearsOfWar*, ComplexNavigations*, Associations/*, Translations/*, Json*, PrimitiveCollections*, Temporal*, TPx*, BulkUpdates/*) is SQL-heavy. Most of the root folder, GraphUpdates, ModelBuilding, Scaffolding, Types (except where JSON is involved) and the unit tests check results only.

## 2. Provider-only behaviours

Every row below is asserted only by the SQL Server suite (a provider-only class, or a test method the provider added to a spec-derived class). Behaviours are in database-neutral words; "Provider API" uses the concept names from `api-inventory/sqlserver.md`. "Asserted by": **SQL** = `AssertSql` baseline or generated DDL/script text compared verbatim; **Results** = data read back / metadata compared with expected .NET values; **Both**; **Exception/message** = expects an exception type and usually an exact resource message. `Check_all_tests_overridden` (124 copies) is a guard that every virtual spec test has an override; it is infrastructure, not behaviour.

### 2.1 Key generation and store-generated values

| Behaviour (neutral) | Tests (class: methods) | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Auto-increment key column is filled on insert, including for `byte`/`decimal` keys, keys behind a value converter (uint, ulong enum, int enum, string), and TPT dependents | SqlServerValueGenerationScenariosTest (+ Sentinel variant, run with non-default CLR sentinels): `Insert_with_Identity_column`, `Insert_uint/string_to_Identity_column_using_value_converter`, `Insert_int_enum/ulong_enum_to_Identity_column`; SqlServerEndToEndTest: `Can_use_decimal_and_byte_as_identity_columns`, `Can_insert_TPT_dependents_with_identity` | identity / auto-increment | `UseIdentityColumn(seed, increment)` | Results |
| Explicit key values can be inserted into an auto-increment column (provider toggles identity-insert), explicit defaults vs non-defaults, "keys by default" | `Insert_with_explicit_non_default_keys`, `…_by_default`, `Insert_with_explicit_with_default_keys`, `Insert_with_implicit_default_keys`, `Insert_with_explicit_default_keys` | identity insert | `IDENTITY_INSERT` handled by update pipeline | Results |
| Sequence-backed key generated by a server default (`NEXT VALUE FOR`), non-key sequence default, string default from a sequence, read-only sequence key | `Insert_with_key_sequence`, `Insert_with_non_key_sequence`, `Insert_with_default_value_from_sequence`, `Insert_with_default_string_value_from_sequence`, `Insert_with_key_default_value_from_sequence`, `Insert_explicit_value_throws_when_readonly_sequence_before_save`; SequenceEndToEndTest (5: end-to-end, async, concurrent contexts, multiple databases, explicit values) | sequence default | `UseSequence`, `HasSequence` | Results |
| Client-side block (HiLo) key allocation from a sequence; block size, sequence name/schema resolution, one generator per sequence/database/server; thread-safe; sequential values for every integer CLR type; never temporary | `Insert_with_sequence_HiLo`; StoreGeneratedSqlServerTest `Insert_update_and_delete_with_wrapped_int_key_using_hi_lo`; unit: SqlServerValueGeneratorCacheTest (21), SqlServerSequenceValueGeneratorTest (10), SqlServerValueGeneratorSelectorTest (9); CompiledModelSqlServerTest `Key_HiLo_sequence`, `Key_sequence`; TPCInheritanceQueryHiLoSqlServerTest | HiLo | `UseHiLo`, `UseKeySequences`/`UseHiLo` on model | Results |
| Client-generated GUID key vs server-generated sequential GUID (`NEWSEQUENTIALID()`), GUID non-key with ValueGeneratedOnAdd throws | `Insert_with_client_generated_GUID_key`, `Insert_with_server_generated_GUID_key`, `Insert_with_ValueGeneratedOnAdd_GUID_nonkey_property_throws`; SequentialGuidEndToEndTest (2) | GUID keys | `HasDefaultValueSql("newsequentialid()")` | Results / Exception |
| Computed columns are read back after insert and update; explicit value into computed column fails; computed via scalar function and querying function; computed with nullable enum; columns computed without metadata configuration | `Insert_and_update_with_computed_column*`, `Insert_explicit_value_into_computed_column`, `Update_explicit_value_in_computed_column`, `Insert_with_computed_column_with_function_without_metadata_configuration`; ComputedColumnTest (3) | computed columns | `HasComputedColumnSql(sql, stored)` | Results / Exception |
| Non-key server default values (incl. spatial default, read-only default) are read back | `Insert_with_non_key_default_value`, `…_readonly`, `Insert_with_non_key_default_spatial_value`; DefaultValuesTest `Can_use_SQL_Server_default_values` | server defaults | `HasDefaultValue`, `HasDefaultValueSql` | Results |
| Conflicting value-generation strategies (identity + default, identity + sequence) warn or throw depending on warning config | SqlServerValueGenerationStrategyThrowTest (2), NoThrowTest (1); unit: SqlServerModelValidatorTest `SqlServerValueGenerationStrategy_warns_when_setting_conflicting_*`, `Metadata_throws_when_setting_conflicting_serverGenerated_values` | strategy conflicts | `ConflictingValueGenerationStrategiesWarning` | Exception/message |
| Model validation for key generation: identity only on integer/decimal types, single identity per table, byte identity warning, sequence/HiLo only on numeric types, same column in hierarchy must agree on seed/increment/strategy/sequence name/schema | SqlServerModelValidatorTest: `Throws_for_identity_on_bad_type`, `Throws_for_sequence(_HiLo)_on_bad_type`, `Detects_multiple_identity_properties`, `Detects_(nullable_)byte_identity_column`, `Detects_duplicate_column_names_*_different_identity_seed/increment/value_generation_strategy/HiLoSequence/KeySequence_*`, `Passes_for_*` (≈25) | key generation validation | validator | Exception/message |
| Value-generation strategy convention annotates the model (identity by default, sequences when configured); builder API sets/reset seed, increment, sequence name/schema when strategy changes | unit: SqlServerValueGenerationStrategyConventionTest (3), SqlServerBuilderExtensionsTest (`Can_set_identities_*`, `Can_set_*sequence*`, ≈30), SqlServerMetadataBuilderExtensionsTest (`*_are_reset_when_changing_strategy`), SqlServerMetadataExtensionsTest (`TryGetSequence_*`, `Can_get_and_set_value_generation_*`) | key generation metadata | `UseIdentityColumns`, `UseHiLo`, `UseKeySequences`, `HasValueGenerationStrategy` | Results |
| Temp values are replaced with store values on save; failure in SaveChanges reverts store values; long→decimal converted key round-trips | StoreGeneratedFixupSqlServerTest `Temp_values_are_replaced_on_save`; StoreGeneratedSqlServerTest `Exception_in_SaveChanges_causes_store_values_to_be_reverted`, `Insert_update_and_delete_with_long_to_decimal_conversion` | store-generated fixup | update pipeline | Results |

### 2.2 Concurrency tokens / row version

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Binary row-version column (`rowversion`) used as concurrency token across TPH/TPT/TPC with owned types and table splitting; token mapped as `ulong` via converter; empty array from database handled; only-token modification is a no-op; token updated for all entities sharing the row | OptimisticConcurrencySqlServerTest (6+3), OptimisticConcurrencyULongSqlServerTest (7+3) | row version | `IsRowVersion()` → `rowversion`, `ulong` converter | Results |
| Row version is marked modified only if it actually changed; `rowversion` only for computed concurrency tokens | unit: SqlServerTypeMappingTest `Row_version_is_marked_as_modified_only_if_it_really_changed`; SqlServerTypeMappingSourceTest `Does_non_key_SQL_Server_(required_)rowversion_mapping`, `Does_not_do_rowversion_mapping_for_non_computed_concurrency_tokens` | row version mapping | type mapping source | Results |
| `Resolve_concurrency` with row version in value-generation scenarios | SqlServerValueGenerationScenariosTest `Resolve_concurrency` | concurrency resolution | | Results |

### 2.3 Temporal (system-versioned) tables

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| The whole GearsOfWar / ComplexNavigationsCollections(SharedType) / ManyToMany / Owned / TPH-filters spec suites are re-run against system-versioned tables with every root query rewritten to "as of point in time" | TemporalGearsOfWarQuerySqlServerTest, TemporalComplexNavigationsCollections(SharedType)QuerySqlServerTest, TemporalManyToManyQuerySqlServerTest, TemporalOwnedQuerySqlServerTest, TPHTemporalFiltersInheritanceQuerySqlServerTest (≈1,500 inherited, via `TemporalPointInTimeQueryRewriter`) | point-in-time query | `TemporalAsOf(DateTime)`; `IsTemporal()` | Both |
| Point-in-time query operators: as-of, all, between; navigation expansion only allowed for as-of (others throw); owned types in same/different table; set operations on temporal sources with same vs different dates | TemporalTableSqlServerTest (12: `Temporal_owned_*`, `Temporal_can_query_shared_derived_hierarchy`); TemporalOwnedQuerySqlServerTest `Navigation_on_owned_entity_mapped_to_different_table_fails_for_non_asof`, `…same_table_works_with_all_temporal_methods`; TemporalGearsOfWarQuerySqlServerTest `Set_operation_on_temporal_*` (3) | temporal query operators | `TemporalAsOf`, `TemporalAll`, `TemporalBetween`, (`TemporalFromTo`, `TemporalContainedIn` not exercised) | Both / Exception |
| Temporal queries over JSON-mapped owned data | AdHocMiscellaneousQuerySqlServerTest `TemporalAll_with_json_basic_query`, `TemporalAsOf_with_json_basic_query`, `TemporalAsOf_project_json_entity_reference/collection` | temporal + JSON | `TemporalAll`, `TemporalAsOf` | Both |
| Migrations: create temporal table (default/custom period columns, history table name/schema), convert regular↔temporal, rename table/schema/period columns, add/drop/alter columns (incl. sparse, defaults) on temporal tables, multi-operation migrations, drop temporal table with custom history | MigrationsSqlServerTest.TemporalTables.cs (≈131: `Create_temporal_table_with_*`, `Convert_*`, `Temporal_multiop_*`, `Rename_temporal_table_*`, `Remove_columns_from_temporal_table*`, `History_table_schema_*`) | temporal DDL | `IsTemporal`, `UseHistoryTable`, `HasPeriodStart/End`, `SqlServerAnnotationNames.IsTemporal`… | SQL (+ model snapshot assertions) |
| Model building: temporal defaults (shadow `PeriodStart`/`PeriodEnd`, `{Table}History`), custom period/history config, switching temporal on/off, implicit many-to-many becoming temporal | SqlServer(Non)GenericOwnedTypes `Temporal_table_*` (6), `Switching_from_temporal_to_non_temporal_default_settings`, `Implicit_many_to_many_converted_from_non_temporal_to_temporal` | temporal metadata | `IsTemporal(Action<TemporalTableBuilder>)` | Results |
| Validation: temporal only on root types, not TPH, consistent across table splitting, period properties must be shadow, non-nullable `datetime2`, no default, value-generated on add-or-update, explicit period precision allowed | SqlServerModelValidatorTest `Temporal_*` (13) | temporal validation | validator | Exception/message |
| Scaffolding: period columns and hidden columns are not reverse-engineered as properties | SqlServerDatabaseModelFactoryTest `Period_columns_are_not_created`, `Hidden_period_columns_are_not_created` | temporal scaffolding | database model factory | Results |
| Memory-optimized + temporal table creation | MigrationsSqlServerTest `Create_memory_optimized_temporal_table` | temporal + in-memory | `IsMemoryOptimized` + `IsTemporal` | SQL |

### 2.4 JSON (documents, primitive collections, native `json` type)

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Primitive collections stored as JSON text arrays; booleans serialized as `[true,false]` | PrimitiveCollectionsQuery(Old/160/JsonType)SqlServerTest `Json_representation_of_bool_array` | primitive-collection JSON format | `nvarchar(max)` / `json` + OPENJSON | Results |
| Ordered element semantics preserved when querying a JSON array (ordering by array index) for every element CLR type; split query with ordered collection | NonSharedPrimitiveCollectionsQuerySqlServerTest `Ordered_array_of_*` (16), `Ordered_collection_with_split_query` | JSON array ordering | `OPENJSON … ORDER BY CAST([key] AS int)` | Both |
| Inline/parameter collection type-mapping inference; same collection with conflicting mappings throws | `Infer_inline_collection_type_mapping`, `Same_collection_with_*` (3), `Same_parameter_with_different_type_mappings` | collection type inference | translator | Both / Exception |
| Parameter collections at the 2,098–2,100 element boundary of the server's parameter limit still execute | PrimitiveCollectionsQuerySqlServerTest `Parameter_collection_of_ints_Contains_int_parameters_limit`, `Parameter_collection_Count_parameters_limit`, `Parameter_collection_of_ints_Contains_int_2071_values` | parameter-limit fallback | `TranslateParameterizedCollectionsTo…`, 2100-param limit | Results |
| Legacy enum values stored as strings inside JSON are read | AdHocJsonQuerySqlServerTest (+JsonType) `Read_*_with_legacy_values` (3) | JSON enum compat | JSON reader | Results |
| Native `json` column type (compat level 170) chosen by `UseAzureSql`, not by `UseSqlServer` unless compat level explicitly 170; vector inside JSON rejected | unit: SqlServerTypeMappingSourceTest `Json_is_mapped_to_*` (4); SqlServerModelValidatorTest `Throws_for_vector_property_inside_JSON`; scaffold `Handles_native_JSON_type` | json store type | `UseCompatibilityLevel(170)`, `UseAzureSql` | Results / Exception |
| Model building: JSON owned entity with custom names, views, TPH, nested enums converted to int, coexisting with table-split owned | SqlServer(Non)GenericOwnedTypes `Json_entity_*` (8), `Entity_mapped_to_json_and_unwound_*` | `ToJson()` metadata | `OwnedNavigationBuilder.ToJson()` | Results |
| Migrations: column changes from `nvarchar(max)` to `json` for complex/owned JSON | SqlServerModelDifferTest `Alter_column_from_nvarchar_max_to_json_for_*` | json migration | differ | Results |

### 2.5 Full-text search

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Full-text CONTAINS predicate: literal, prefix term, proximity term, logical operators, language term (literal and non-literal), through navigation, over binary column | NorthwindDbFunctionsQuerySqlServer(160)Test `Contains_*` (8); GearsOfWarQuerySqlServerTest `Contains_with_binary_column*` | full-text | `EF.Functions.Contains(prop, term[, languageTerm])` | Both |
| Full-text FREETEXT predicate: literal, multiple words, multiple predicates, language term, navigation, binary column | `FreeText_*` (9); `FreeText_with_binary_column*` | full-text | `EF.Functions.FreeText` | Both |
| Full-text functions refuse client evaluation, non-column property argument, non-parameter/constant search text, and fail on a column without a full-text index | `Contains_should_throw_*` (3), `FreeText_throws_*` (3), `FreeText_client_eval_throws` | full-text guards | translator | Exception/message |

### 2.6 Date/time functions

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Datepart difference counts (year, month, week for datetime and datetimeoffset, day, hour, minute, second, ms, µs, ns; null parameters) | NorthwindDbFunctionsQuerySqlServer(160)Test `DateDiff_*` (13); BuiltInDataTypesSqlServerTest `Can_query_using_DateDiff*_using_TimeSpan` (6) | date diff | `EF.Functions.DateDiffYear…DateDiffNanosecond`, `DateDiffWeek` | Both |
| Construct date/time values from parts in predicates and projections (date, datetime, datetime2, datetimeoffset, smalldatetime, time) | `DateFromParts_*`, `DateTimeFromParts_*`, `DateTime2FromParts_*`, `DateTimeOffsetFromParts_*`, `SmallDateTimeFromParts_*`, `TimeFromParts_*` (17) | from-parts | `EF.Functions.DateFromParts`… | Both |
| Time-zone conversion of datetime/datetimeoffset columns, constants, parameters; null handling | GearsOfWarQuerySqlServerTest `Where_AtTimeZone_datetime_*` (3); OperatorsQuerySqlServerTest `Where_AtTimeZone_datetimeoffset_*`, `Where_AtTimeZone_is_null` | AT TIME ZONE | `EF.Functions.AtTimeZone` | Both |
| `IsDate` validation function | `IsDate_*` (4) | validation fn | `EF.Functions.IsDate` | Both / Exception |
| `DateTime.Now/UtcNow` compared to constants uses the right store type (datetime2 vs datetimeoffset) | DateTime/DateTimeOffsetTranslationsSqlServerTest `Now_/UtcNow_has_proper_type_mapping_for_constant_comparison` | current time | `GETDATE()/SYSDATETIME()/SYSUTCDATETIME()` | Both |
| Date literal generation: `smalldatetime` literal in Contains, DateTime literals/Now/new DateTime in predicates | AdHocMiscellaneousQuerySqlServerTest `DateTime_Contains_with_smalldatetime_generates_correct_literal`, `Where_contains_DateTime_literals`, `Where_(not_)equals_DateTime_Now`, `Where_equals_new_DateTime` | date literals | type mappings | Both |

### 2.7 Other provider functions and operators

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Byte length of string/binary values | `DataLength_*` (4), GearsOfWar/TPC/TPT `DataLength_function_for_string_parameter` | data length | `EF.Functions.DataLength` | Both |
| Numeric-string validation; pattern index | `IsNumeric_*`/`IsNummeric_valid` (4); `PatIndex_literal` | string functions | `EF.Functions.IsNumeric`, `PatIndex` | Both |
| Statistical aggregates (stdev/var, sample and population) | NorthwindFunctionsQuerySqlServer(160)Test `StandardDeviation`, `Variance` | aggregates | `EF.Functions.StandardDeviationSample/Population`, `VarianceSample/Population` | Both |
| Precompiled query with provider aggregate expression | PrecompiledQuerySqlServerTest `SqlServerAggregateFunctionExpression` | precompiled queries | `SqlServerAggregateFunctionExpression` | Both |
| Vector distance ordering (constant and parameter vectors) and vector length | VectorTranslationsSqlServerTest (3) | vector search | `EF.Functions.VectorDistance`, `SqlVector<float>`, `vector(n)` | Both |
| Vector type mapping requires dimensions > 0; vector comparer | unit: `Vector_is_properly_mapped`, `Vector_requires_positive_dimensions`, `Throws_for_vector_property_without_dimensions`, `Vector_comparer_compares_Memory`; scaffold `Vector_type` | vector type | `HasColumnType("vector(n)")` | Results / Exception |
| Spatial `CurveToLine` (geometry and geography) | SpatialQuerySqlServerGeometry/GeographyTest `CurveToLine` | spatial | NTS + `STCurveToLine` | Both |
| `sql_variant` batch insert with different runtime types; `sql_variant` literals | AdHocMiscellaneousQuerySqlServerTest `Batch_insert_with_sqlvariant_different_types`; unit `SqlVariant_literal_generated_correctly` | sql_variant | `HasColumnType("sql_variant")` | Results |
| Set operation over columns with different store types allowed when server can coerce | NorthwindSetOperationsQuerySqlServerTest `Union_with_different_store_types_is_fine_if_database_can_translate_it` | set ops | | Both |
| Distinct + Skip/Take without ORDER BY generates valid paging | ComplexNavigations(SharedType)QuerySqlServer(160)Test `Distinct_skip/take_without_orderby` | paging | `OFFSET … FETCH` requires ORDER BY | Both |
| Bulk update with PK push-down, join and multiple setters | NorthwindBulkUpdatesSqlServerTest `Update_with_PK_pushdown_and_join_and_multiple_setters` | ExecuteUpdate | `UPDATE … FROM` | Both |

### 2.8 Raw SQL, stored procedures, functions, triggers

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Raw SQL composed with LINQ for complex types, query filters, includes; out-parameter from a stored procedure; out-parameter with transient-error retry; multiple raw SQL sources in GROUP BY | ComplexTypeQuerySqlServerTest `*_with_FromSql` (8); NorthwindQueryFiltersQuerySqlServerTest `FromSql_is_composed*`; ComplexNavigationsQuerySqlServer(160)Test `Multiple_complex_includes_from_sql`; AdHocMiscellaneousQuerySqlServerTest `From_sql_gets_value_of_out_parameter_in_stored_procedure`, `Muliple_occurrences_of_FromSql_in_group_by_aggregate`, `From_sql_expression_compares_correctly`; FromSqlQuerySqlServerTest `FromSql_output_parameter_works_with_transient_errors` | raw SQL | `FromSql`, `SqlQuery`, `SqlParameter(Direction=Output)` | Both |
| Table-valued function with Take/SelectMany; default schema applied to functions without schema | AdHocMiscellaneousQuerySqlServerTest `Subquery_take_SelectMany_with_TVF`, `Default_schema_applied_when_no_function_schema` | TVF/UDF | `HasDbFunction` | Both |
| Triggers on tables still work with batching (OUTPUT clause disabled when a trigger is declared); triggers with subqueries; entity splitting with triggers | SqlServerTriggersTest (2), SqlServerQueryTriggersTest (2), EntitySplittingSqlServerTest `Can_roundtrip_with_triggers`; unit SqlServerOutputClauseConventionTest (2) | triggers / OUTPUT | `HasTrigger`, `UseSqlOutputClause(false)` | Results |
| Stored-procedure concurrency token not mapped throws by default | LoggingSqlServerTest `StoredProcedureConcurrencyTokenNotMapped_throws_by_default` | sproc mapping | `InsertUsingStoredProcedure` | Exception/message |

### 2.9 Update pipeline and batching

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Inserts are batched, batched only when needed, keep insertion order, mixed inserts+updates batched; deadlock-prone delete/insert patterns ordered safely; different store types in one batch | BatchingTest (7) | batching | `MaxBatchSize` (default 42, max 1000) | Results (+ SQL count) |
| Batch header `SET NOCOUNT ON`; multi-row insert uses `MERGE … OUTPUT` when store-generated columns exist, plain `INSERT` otherwise, `DEFAULT VALUES` forms | SqlServerUpdateSqlGeneratorTest (5); StoreValueGeneration*WithoutOutputSqlServerTest `Four_Add_use_merge_output_into`, `Three_Add_use_batched_inserts` | insert SQL shape | update SQL generator, `UseSqlOutputClause` | SQL |
| Batch closes at max batch size or at 2,100 parameters | unit SqlServerModificationCommandBatchTest (3), …BatchFactoryTest (2); SqlAzureBatchingTest `AddWithBatchSize` | batch limits | `MaxBatchSize` | Results |
| Key types mismatched with store: queries work, SaveChanges fails | MismatchedKeyTypesSqlServerTest (2) | type mismatch | | Results / Exception |
| End-to-end CRUD with change-tracking strategies, byte-array/string/enum keys, table splitting, many-to-many replacement | SqlServerEndToEndTest (22), NavigationTest (2) | CRUD | | Results |

### 2.10 Type mappings and column types

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Every CLR type maps to the expected store type (catalog check via `INFORMATION_SCHEMA`), including sized, precision/scale, unicode/ANSI, fixed-length, max | BuiltInDataTypesSqlServerTest `Columns_have_expected_data_types`, `Can_get_column_types_from_built_model`; EverythingIsBytes/Strings, ConvertToProviderTypes, CustomConverters `Columns_have_expected_data_types` | store type catalog | type mapping source | Results (text dump) |
| Insert/read back all mapped types: sized, scaled, precision+scale, nullable, null, with identity, in batches, with square-bracket names; double precision; varchar(max) IndexOf; debug-string query for each fixture entity | BuiltInDataTypesSqlServerTest (≈45) | type round-trip | `HasColumnType`, `HasPrecision`, `IsUnicode`, `IsFixedLength` | Results (+ SQL for debug-string) |
| Type mapping source rules: string/binary key and FK lengths (450/900), indexed column lengths, ANSI vs unicode, fixed length thresholds, datetime/time precision, decimal default (18,2), `rowversion`, plugins override, unknown types throw | unit SqlServerTypeMappingSourceTest (78) | type mapping rules | `SqlServerTypeMappingSource` | Results / Exception |
| Literal generation for DateOnly/TimeOnly/sql_variant/geometry/strings (unicode `N''`), UTF-8 collation types, UDT clones | unit SqlServerTypeMappingTest (11), SqlServerStringTypeMappingTest (2), SqlServerGeometryTypeMappingTests (1) | SQL literals | type mappings | SQL |
| Type aliases (`CREATE TYPE`) usable as column types; scaffold resolves alias and `sysname` to underlying type | SqlServerTypeAliasTest; scaffold `Column_with_type_alias_*`, `Column_with_sysname_*`, `Sequence_using_type_alias` | user-defined type alias | | Results |
| DbType, fixed-length hints and nullability of converted columns respected; converters without property | ValueConvertersEndToEndSqlServerTest (4) | converters | | Results |
| Decimal key/default mapping warnings | SqlServerModelValidatorTest `Detects_decimal_keys`, `Detects_default_(nullable_)decimal_mapping`, `*_decimal_*` (5) | decimal precision | validator | Exception/message (warning) |

### 2.11 Indexes, keys, tables (physical options)

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Clustered / non-clustered primary keys, unique constraints and indexes | MigrationsSqlServerTest `Add_primary_key_nonclustered`, `Create_index_clustered`, `Create_index_unique_clustered`; differ `Alter_*_clustering`; builder/metadata tests | clustering | `IsClustered` | SQL |
| Included (covering) columns, filters (auto filter for unique nullable), fill factor, sort-in-tempdb, data compression, online index build; rebuild index only when options change | Migrations `Create_index_*with_include*`, `*fill_factor*`, `*sortintempdb*`, `*datacompression*`, `*online*`; differ `Rebuild_*`/`Dont_rebuild_*` (17); validator `Detects_duplicate_index_names_within_hierarchy_different_*` (6), `Detects_*include_properties` (3); model builder `Index_has_a_filter_if_nonclustered_unique_with_nullable_properties` | index options | `IncludeProperties`, `HasFilter`, `HasFillFactor`, `SortInTempDb`, `UseDataCompression`, `IsCreatedOnline` | SQL / Exception |
| Memory-optimized tables: create, keys/indexes forced non-clustered, alter, unique nullable indexes, database filegroup operation | MemoryOptimizedTablesTest; Migrations `Create_memory_optimized_table`, `Create_index_memoryOptimized_*` (3), `Alter_column_memoryOptimized_with_index`; generator `AlterDatabaseOperation_memory_optimized`; convention test | in-memory OLTP | `IsMemoryOptimized` | SQL / Results |
| Sparse columns | Migrations `Create_table_with_sparse_column`, `Alter_column_make_sparse`; scaffold `Column_sparseness_is_set`; validator sparseness consistency | sparse | `IsSparse` | SQL |
| Named default constraints (explicit and opt-in implicit names, rename, dedupe, trimming, TPT/TPC) | MigrationsSqlServerTest.NamedDefaultConstraints.cs (26); validator `DefaultValue*_constraint_name_*` (5) | named defaults | `HasDefaultValue(value, constraintName)`, `UseNamedDefaultConstraints` | SQL / Exception |
| Collation on columns with index; database collation | Migrations `Alter_column_set_collation_with_index`; generator `AlterDatabaseOperation_collation*`; model builder `Can_set_collation_*` | collation | `UseCollation` | SQL |

### 2.12 Migrations SQL generation and infrastructure

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Idempotent scripts wrap DDL/DML in `EXEC(N'…')` blocks (computed column, check constraint, filtered index, insert/update/delete data, alter column required) | Migrations `*_generates_exec_when_idempotent` (6); generator `CreateIndex_generates_exec_when_legacy_filter_and_idempotent`, `AlterColumn_make_required_with_idempotent` | idempotent scripts | `MigrationsSqlGenerationOptions.Idempotent` | SQL |
| Raw SQL operation splitting on batch separator `GO` (counts, comments, strings, brackets, quotes) | generator `SqlOperation_handles_*` (35) | batch separator | `SqlOperation` / `GO` | SQL |
| Identity changes: add/remove/change seed via column rebuild; changing identity in place throws | Migrations `Add_column_identity*`, `Alter_column_add/remove_identity`, `Alter_column_change_identity_seed`, `Alter_column_change_type_with_identity`, `Create_table_with_identity_column_value_converter`; generator `*_identity_legacy` (6); differ `Alter_column_*identity` | identity DDL | `UseIdentityColumn` | SQL / Exception |
| Sequences: create byte/decimal sequences, sequence + dependent column, move sequence/table into default schema, `dbo` schema creation ignored, HiLo column | Migrations `Create_sequence_*`, `Drop_sequence_and_dependent_column`, `Move_*_into_default_schema`, `Create_schema_dbo_is_ignored`, `Add_column_hilo`, `Add_column_sequence` | sequence DDL | `HasSequence`, `UseHiLo` | SQL |
| Database create/drop with file name, data directory, collation; drop index/rename index without table throws | generator `CreateDatabaseOperation*` (5), `DropDatabaseOperation`, `*_throws_when_no_table` | database DDL | `SqlServerCreateDatabaseOperation` | SQL / Exception |
| Default value DDL for datetime/smalldatetime/datetimeoffset/time with precision; rowversion columns with/without converter | Migrations `Add_column_with_defaultValue_*` (6), `Add_column_with_rowversion*` | defaults/rowversion DDL | | SQL |
| Insert data respects max batch size; unmappable CLR type error | generator `InsertDataOperation_max_batch_size_is_respected`, `Invalid_column_type_for_unmappable_clr_type_throws_meaningful_exception` | seed data | | SQL / Exception |
| History repository scripts (create, if-exists/if-not-exists, insert, delete, schema) | unit SqlServerHistoryRepositoryTest (9) | migrations history | `SqlServerHistoryRepository` | SQL |
| Non-transactional migration is retried; pending model changes / nondeterministic seed / missing migrations / missing snapshot throw; empty migration creates database | MigrationsInfrastructureSqlServerTest (11) | migration runner | `Migrate()` + retry | Results / Exception |
| `IsSqlServer()` detection in migrations and DatabaseFacade; active provider name | unit SqlServerMigrationBuilderTest (2), SqlServerDatabaseFacadeExtensionsTest (`IsSqlServer_*`, 10) | provider detection | `IsSqlServer()` | Results / Exception |

### 2.13 Scaffolding (reverse engineering)

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Read tables, views, columns, PKs, unique constraints, FKs (composite, referencing unique constraint, referential actions), indexes (filter, include, clustered, fill factor), sequences with facets, triggers | SqlServerDatabaseModelFactoryTest `Create_*`, `Set_*`, `Index_fill_factor`, `Triggers`, `Create_sequences_with_facets`… (≈40) | database model | `SqlServerDatabaseModelFactory` | Results |
| Table/schema/sequence filters including quoted and schema-qualified names | `Filter_*` (9) | scaffold filters | | Results |
| Store type facets: default max lengths, precision/scale, datetime precision, `max`, type alias, sysname, vector, native json | `Default_max_length_*`, `Specific_max_length_*`, `Decimal_numeric_*`, `Datetime_types_*`, `Store_types_without_any_facets`, `Vector_type`, `Handles_native_JSON_type` | scaffold types | | Results |
| Default value literal parsing to CLR values (bool/int/decimal (incl. locale)/Guid/DateTime/DateOnly/TimeOnly/DateTimeOffset/string), non-literal defaults passed through, computed columns, identity/computed value generation, rowversion concurrency | `Simple_*_literals_are_parsed_for_HasDefaultValue` (15), `Non_literal_*` (4), `Default_and_computed_values_are_stored`, `ValueGenerated_is_set_*`, `ConcurrencyToken_is_set_for_rowVersion` | scaffold defaults | | Results |
| Skip/warn: columnstore and hypothetical indexes, duplicate/reflexive FKs, missing principal table/schema/table, default database collation not emitted | `Ignore_*`, `Skip_*`, `Warn_*` (5), `Default_database_collation_is_not_scaffolded` | scaffold warnings | | Results (log) |
| Annotation code generation: fluent API for memory-optimized, clustering, fill factor, includes, identity seed/increment, HiLo, sequences, sparse, Azure edition options | unit SqlServerAnnotationCodeGeneratorTest (21), SqlServerCodeGeneratorTest (3: `UseSqlServer(...)` with/without NTS/options) | scaffold codegen | `SqlServerAnnotationCodeGenerator` | Results (generated code) |
| Compiled model baselines for HiLo key, sequence key, spatial types | CompiledModelSqlServerTest `Key_HiLo_sequence`, `Key_sequence`, `SpatialTypesTest` | compiled model | | SQL-like (C# baselines) |

### 2.14 Connection, configuration, provider selection

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Connection supplied as string vs `DbConnection` (owned/not owned), in `OnConfiguring`, via DI or configuration; switching connection when closed; cannot switch when owned and open | ConnectionSpecificationTest (22); unit SqlServerDbContextOptionsExtensionsTest (11) | connection ownership | `UseSqlServer(string | DbConnection, bool contextOwnsConnection)` | Results / Exception |
| DI configuration patterns (implicit/explicit services, injected options, nested contexts) | SqlServerConfigPatternsTest nested classes (≈14) | configuration | `AddDbContext`, `UseSqlServer` | Results / Exception |
| Choosing exactly one engine flavour; configuring two engines throws; no engine configured throws; library-level engine-agnostic config | `ConfigureTwoEngines.Throws_when_two_engines_used`, `NoEngineConfigured.Throws_when_no_engine_configured` | engine flavour | `UseSqlServer`/`UseAzureSql`/`UseAzureSynapse`/`ConfigureSqlEngine` | Exception/message |
| Application name injected into connection string unless user-defined; master connection derived (keeps file name, default timeout) | unit SqlServerConnectionTest (7) | connection string | `SqlServerConnection` | Results |
| Command timeout default null, set, infinite → 0, negative throws | unit SqlServerDatabaseFacadeExtensionsTest (3); CommandConfigurationTest | command timeout | `CommandTimeout` | Results / Exception |
| Options extension registers services; compiled model thread safety; NTS with Azure SQL | unit SqlServerOptionsExtensionTest (2), AzureSqlDbContextOptionsExtensionsTest | services | `SqlServerOptionsExtension` | Results |
| Context pooling: reset of state/config/provider services, pool size validation, double dispose, constructor rules | DbContextPoolingTest (42) | pooling (core behaviour, provider-hosted) | `AddDbContextPool`, `AddPooledDbContextFactory` | Results / Exception |
| Azure SQL: encrypted connection, basic CRUD/include, batching, database creation in tiers/elastic pool | SqlAzure* (9) | Azure flavour | `UseAzureSql`, `HasServiceTier`, `HasPerformanceLevel`, `HasDatabaseMaxSize`, `HasServiceTierSql` | Results |
| Azure database edition options in metadata/migrations | unit metadata `Can_*_MaxSize/PerformanceLevel/ServiceTier`; differ `Alter_database_edition_options` | Azure edition | `HasDatabaseMaxSize` etc. | Results / SQL |

### 2.15 Transient-fault retry (execution strategy)

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Retries SaveChanges, query, raw SQL, opening connection, beginning transaction on transient errors; commit failure verified and retried with the same limit; multiple SaveChanges | ExecutionStrategyTest (9) | retry | `EnableRetryOnFailure`, `SqlServerRetryingExecutionStrategy` | Results |
| Retry strategy registration precedence (Add vs Configure, fallback strategy) | AddConfigureDbContextWithRetry (6) | retry config | `EnableRetryOnFailure`, `ExecutionStrategy` | Results |
| In-memory OLTP transient errors get a shorter delay | unit SqlServerRetryingExecutionStrategyTests | retry delay | | Results |
| Database creation waits/retries for "cannot open/attach/timeout/no process" errors, then gives up | unit SqlServerDatabaseCreatorTest (12) | create retry | `SqlServerDatabaseCreator` | Results / Exception |
| Concurrent async queries serialized without MARS; split query without MARS; savepoints disabled with MARS | NorthwindMiscellaneousQuerySqlServerTest (5); AdHocQuerySplittingQuerySqlServerTest `Using_AsSplitQuery_without_multiple_active_result_sets_works`; TransactionSqlServerTest `Savepoints_are_disabled_with_MARS` | MARS | connection string `MultipleActiveResultSets` | Results |

### 2.16 Database lifecycle

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Exists/HasTables/Create/CreateTables/Delete/EnsureCreated/EnsureDeleted, including file-attached databases, missing initial catalog, missing seed, script generation | SqlServerDatabaseCreator*Test (27) | database creator | `IRelationalDatabaseCreator` (+ `AttachDBFilename`) | Results / Exception |

### 2.17 Logging / diagnostics

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Logged SQL with/without sensitive parameter values; warnings for paging without ORDER BY, FirstOrDefault without ORDER BY, Distinct after OrderBy; include/split logs | QueryLoggingSqlServerTest (10) | query logging | relational logging | Results (log text incl. SQL) |
| Provider event IDs all have logger methods | unit SqlServerEventIdTest | event ids | `SqlServerEventId` | Results |
| Query tags on split include | NorthwindQueryTaggingQuerySqlServerTest `Tag_on_split_include_query` | tags | `TagWith` | SQL |
| `ToQueryString` for include reference+collection | NorthwindInclude/SplitInclude `ToQueryString_for_include_reference_and_collection` | ToQueryString | | SQL |
| Command interception can read statistics (`SET STATISTICS IO`); SaveChanges concurrency interception gets relational data | CommandInterceptionSqlServerTest `Intercept_query_to_get_statistics`; SaveChangesInterception `Intercept_concurrency_with_relational_specific_data` | interception | interceptors | Results |

### 2.18 Model building and validation (other)

| Behaviour | Tests | Concept | Provider API | Asserted by |
|---|---|---|---|---|
| Inheritance mapping: check constraints on derived types (conflicts throw), TPC/TPT FK placement, inherited properties share column, TPT index on inherited props | SqlServer(Non)GenericInheritance (9) | inheritance + constraints | `HasCheckConstraint` | Results / Exception |
| Store type/collation/fixed-length set by property type convention (also primitive collections); index names unique per table | SqlServer(Non)GenericNonRelationship (9) | conventions | `Properties<T>().HaveColumnType…` | Results |
| Join entity schema follows related types; shadow FK names to generic types | SqlServer*ManyToMany (2), *OneToMany (1) | naming | | Results |
| Owned types use table splitting by default, can map to other tables/views | SqlServer*OwnedTypes `Owned_*` (6) | owned mapping | | Results |
| Default constraint rules for keys: default on whole composite key or string key throws | DataAnnotationSqlServerTest (5) | defaults on keys | | Exception/message |
| Shared-table compatibility: memory-optimized, non-clustered key, OUTPUT clause, trigger definitions | validator `Detects_incompatible_*` (3), `Passes_for_shared_table_with_only_one_entity_trigger_definition` | table sharing | validator | Exception/message |
| Relational FK index convention removes unused indexes | unit RelationalForeignKeyIndexConventionTest (2) | conventions | | Results |
| Query filters: keyless type in defining query; DbSet in filter doesn't block others; nested include not populated twice; owned to separate table | AdHocQueryFilters (2), AdHocNavigations (1), OwnedEntityQuerySqlServerTest (3), Incomplete/TPHInheritance `Common_property_shares_column` | misc query | | Both |
| Thread-safety / concurrency of query compilation and command cache; cancellation; context disposed during query | AdHocMiscellaneous `Thread_safety_in_relational_command_cache`, `Nested_queries_does_not_cause_concurrency_exception_sync`, `Include_group_join_is_per_query_context*`; NorthwindMiscellaneous `Query_compiler_concurrency`, `Race_when_context_disposed_before_query_termination`, `Single_Predicate_Cancellation` | concurrency | | Results |
| Precompiled SQL pregeneration honours do-not-cache | PrecompiledSqlPregenerationQuerySqlServerTest `Do_not_cache_is_respected` | precompiled | | SQL |
| Value converter projections: ulong ↔ byte[] row version projection, converter + include, point via buffered reader | AdHocMiscellaneous (3) | converters | | Results |

## 3. Divergences from the shared spec

Only overrides whose expectation differs from the spec base are listed (≈12,000 other overrides call `base` and add an `AssertSql` baseline; their divergence is the SQL text itself, the SQL-dialect layer). "Asserted by" refers to the override.

### 3.1 Translation failures (capability gaps: `AssertTranslationFailed` / `QueryUnableToTranslate…`)

| Spec test(s) | Class(es) | SQL Server expectation | Reason / gap | Asserted by |
|---|---|---|---|---|
| `Where_compare_constructed_(multi_value_)(not_)equal`, `Where_compare_tuple_constructed_*`, `Where_compare_tuple_create_constructed_*` (9) | NorthwindWhereQuerySqlServerTest | translation failed, no SQL | No row-value (tuple) comparison in T-SQL | Exception + empty SQL |
| `Contains_with_local_tuple_array_closure`, `Contains_with_local_anonymous_type_array_closure` | NorthwindAggregateOperatorsQuerySqlServerTest | translation failed | No tuple `IN` / row values | Exception |
| `Contains_with_local_enumerable_inline(_closure_mix)` | same | `InvalidOperationException` | inline enumerable of non-constant not translatable | Exception |
| `Parameter_collection_of_nullable_ints_Contains_nullable_int_with_EF_Parameter` | PrimitiveCollectionsQueryOldSqlServerTest (compat 120) | `InvalidOperationException` | forcing a single JSON parameter needs OPENJSON | Exception |
| `Max`, `Min`, `Max_nested(_twice)`, `Min_nested(_twice)` (Math.Max/Min) | MathTranslationsSqlServerTest (compat 150) | translation failed; **pass** in MathTranslationsSqlServer160Test | `GREATEST`/`LEAST` need compat ≥160 | Exception |
| `Log2`, `Acosh`, `Asinh`, `Atanh`, `Cosh`, `Sinh`, `Tanh` | MathTranslationsSqlServer(160)Test | translation failed | No such built-in functions | Exception |
| `Least`, `Greatest`, `*_with_nullable_value_type` | NorthwindDbFunctionsQuerySqlServerTest (compat 150) | translation failed; pass in …160Test | compat-level gated | Exception |
| `TrimStart/TrimEnd_with_char(_array)_argument`, `Trim_with_char(_array)_argument_in_predicate` | StringTranslationsSqlServerTest | translation failed (160 variant: only the `Trim_…in_predicate` pair fails) | `LTRIM/RTRIM(…, chars)` need compat ≥160; `TRIM(chars FROM)` limits | Exception |
| `Regex_IsMatch`, `Regex_IsMatch_constant_input` | StringTranslationsSqlServer(160)Test | translation failed | No regex before SQL Server 2025 (`REGEXP_LIKE`) | Exception |
| `Left_shift`, `Right_shift` | BitwiseOperatorTranslationsSqlServerTest | translation failed | No shift operators (pre-2022 `LEFT_SHIFT`) | Exception |
| `DayOfWeek`, `ToDateTime_with_complex_DateTime/TimeOnly` | DateOnlyTranslationsSqlServerTest | translation failed | not mapped | Exception + empty SQL |
| `Add_TimeSpan`, `Subtract` | TimeOnlyTranslationsSqlServerTest | translation failed | no time arithmetic | Exception + empty SQL |
| `subtract_and_TotalDays` | DateTimeTranslationsSqlServerTest | translation failed | TimeSpan from datetime subtraction unsupported | Exception |
| `DateTimeOffsetNow_minus_timespan` | GearsOfWar (+TPC/TPT/Temporal) | translation failed | same | Exception |
| 14 column/projected primitive-collection tests (`Column_collection_*` incl. `Column_collection_SelectMany`, `Project_collection_*`, `Project_multiple_collections`, `Project_empty_collection_of_nullables_*`) | PrimitiveCollectionsQueryOldSqlServerTest (compat 120) | translation failed | No `OPENJSON` below compat 130 → JSON array queries impossible | Exception |
| `GroupJoin_client_method_in_OrderBy` | ComplexNavigations(SharedType)QuerySqlServer(160)Test | `QueryUnableToTranslateMethod` | client method in ORDER BY | Exception/message |
| `Member_binding_after_ctor_arguments_fails_with_client_eval`, `SelectMany_…_references_non_mapped_properties…` | NorthwindSelectQuerySqlServerTest | translation failed / `AssertUnableToTranslateEFProperty` | client eval | Exception |
| `Client_code_unknown_method`, `Client_code_using_instance_*` (3), `Entity_equality_through_subquery_composite_key` | NorthwindMiscellaneousQuerySqlServerTest | specific CoreStrings messages | same as base but message re-asserted | Exception/message |
| `Json_collection_index_in_projection_using_untranslatable_client_method(2)` | JsonQuery(JsonType)SqlServerTest | `QueryUnableToTranslateMethod` | client method as JSON index | Exception/message |
| `Value_conversion_on_enum_collection_contains` | CustomConvertersSqlServerTest | `TranslationFailed` | converted enum collection Contains | Exception/message |

### 3.2 Server-side errors expected instead of results (`SqlException` / server limitation)

| Spec test(s) | Class(es) | Expectation | Reason | Asserted by |
|---|---|---|---|---|
| `Distinct`, `Distinct_projected`, `Distinct_over_projected_nested_collection`, `Json_collection_Distinct_Count_with_predicate`, `Json_collection_distinct_in_projection`, `Json_multiple_collection_projections`, `Json_branch_collection_distinct_and_other_collection` | ComplexJsonCollection / OwnedJsonCollection / JsonQueryJsonType SqlServerTest (when `json` type is in use) | `SqlException` + SQL baseline | `json` type is not comparable → `DISTINCT` over JSON columns fails | Both |
| `Json_subquery_reference_pushdown_*` (5) | JsonQueryJsonTypeSqlServerTest | `SqlException` | same (`json` in DISTINCT/GROUP BY) | Exception |
| `Project_entity_with_json_null_values` | AdHocJsonQuerySqlServerJsonTypeTest | `SqlException` | SQL Server 2025 cannot `CAST('8' AS json)` / `CAST('null' AS json)` (scalar JSON) | Exception |
| `KeylessEntity_with_nav_defining_query` | NorthwindKeylessEntitiesQuerySqlServerTest | `SqlException` + SQL | raw-SQL (FromSql) mapping of the keyless type, issue #21627 | Both |
| `EnvelopeCombine_aggregate` | SpatialQuerySqlServerGeographyTest | NTS `ParseException` | geography returns a full-globe/curve result NTS cannot parse | Exception |
| `Add_primary_key_int`, `Drop_primary_key_int` | MigrationsSqlServerTest | `InvalidOperationException(AlterIdentityColumn)` | cannot alter a column to/from IDENTITY in place | Exception/message |
| `FromSqlRaw_composed_with_common_table_expression`, `SqlQueryRaw_composed_with_common_table_expression` | FromSqlQuery / SqlQuery SqlServerTest | `FromSqlNonComposable` | `WITH …` cannot be wrapped in a subquery in T-SQL | Exception/message |
| `Can_generate_*_script*` (4), `Can_get_active_provider` | MigrationsInfrastructureSqlServerTest | `WarningAsErrorTemplate` / provider name | pending-model-changes warning raised for this model; name assertion | Exception/message |

### 3.3 Different results (test asserts the base *fails*)

| Spec test(s) | Class | Expectation | Reason | Asserted by |
|---|---|---|---|---|
| `Average_over_max_subquery` | NorthwindAggregateOperatorsQuerySqlServerTest | base's equality assert fails (`EqualException`) + SQL | query rewritten (#34256) to avoid SQL Server's "cannot aggregate an expression containing an aggregate or subquery"; result 59.843333 vs expected 59.8412698… (decimal(18,2)/float precision) | Both |
| `Edit_single_property_collection_of_collection_of_*` (10: bool, char, double, int16, int32, single, nullable int32/enum…) | JsonUpdate(JsonType)SqlServerTest | various assertion/IndexOutOfRange exceptions | nested collections are not mapped in the relational model, so no data is stored for them | Exception |
| `Include_where_list_contains_navigation(2)`, `Navigation_accessed_twice_outside_and_inside_subquery`, `Query_reusing_parameter_with_inner_query_*`, `Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result1/2`, `Multiple_includes_with_client_method_around_entity_*` | TemporalGearsOfWarQuerySqlServerTest | `EqualException`/`SingleException` | point-in-time rewrite changes the data the query sees, so base expected results no longer match | Both |
| `SelectMany_with_predicate_and_DefaultIfEmpty_projecting_root_collection_element_and_another_collection` | TemporalComplexNavigationsCollectionsSharedTypeQuerySqlServerTest | `ExpressionParameterizationExceptionSensitive` | temporal rewrite interacts with parameterization | Exception/message |
| `Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval` | NorthwindMiscellaneousQuerySqlServerTest | `TrueException` | binding after client-eval projection not supported (#24478) | Exception |
| `Max_on_empty_sequence_throws` | same | `InvalidOperationException` | re-asserted | Exception |
| `Detects_duplicate_columns_in_derived_types_with_different_types` | SqlServerModelValidatorTest | different message args (store types `nvarchar(max)` vs `int`) | provider store type names | Exception/message |
| Insert_* stored-procedure tests (28) | StoredProcedureUpdateSqlServerTest | provider supplies its own `CREATE PROCEDURE` text, then asserts `EXEC … OUTPUT` | sproc DDL is dialect-specific | SQL |

### 3.4 Disabled overrides (no-op `=> Task.CompletedTask`) — capability gaps

| Spec test(s) | Class | Reason given | Asserted by |
|---|---|---|---|
| `Boundary`, `Buffer_quadrantSegments`, `Centroid`, `CoveredBy`, `Covers`, `Crosses`, `Envelope`, `InteriorPoint`, `IsRing`, `IsSimple`, `Normalized`, `PointOnSurface`, `Relate`, `Reverse`, `Touches`, `Union_void` (geography 16; geometry: `Buffer_quadrantSegments`, `CoveredBy`, `Covers`, `Normalized`, `Reverse`, `Union_void`) | SpatialQuerySqlServerGeography/GeometryTest | "No SqlServer Translation" | — |
| `Distance_constant(_lhs)`, `Distance_on_converted_geometry_type_constant(_lhs)` | SpatialQuerySqlServerGeographyTest | "Mixing SRIDs not supported" | — |
| `GetGeometryN_with_null_argument` | both spatial | `STGeometryN` rejects null | — |
| `Savepoint_can_be_released` | TransactionSqlServerTest | savepoints cannot be released | — |
| `SaveChanges_implicitly_creates_savepoint`, `SaveChanges_uses_explicit_transaction_with_failure_behavior` | TransactionSqlServerTest | skipped when MARS on (savepoints disabled) | — |
| (1 each) | TransactionInterception(WithDiagnostics)SqlServerTest | savepoint release | — |
| `Can_read_back_mapped_enum_from_collection_first_or_default`, `Can_read_back_bool_mapped_as_int_through_navigation`, `Can_compare_enum_to_constant/parameter`, `Object_to_string_conversion` | EverythingIsBytes/Strings SqlServerTest | "needs TOP(1)", "column mapped as int" | — |
| 74 tests | ComplexTypesTrackingProxiesSqlServerTest | proxies don't support complex types | — |
| 21 graph tests (collection replacement, stable dependents, sentinel bool/int composite keys, required one-to-one cascade, AK over FK, shadow skip navigation) | GraphUpdatesSqlServerOwnedTest | "No owned types" in that model variant / owned dependents always loaded | — |
| `Can_insert_dependent_with_just_one_parent` | TPTTableSplittingSqlServerTest | not valid for TPT | — |
| `Can_use_IgnoreQueryFilters_and_GetDatabaseValues` | TPHTemporalFiltersInheritanceQuerySqlServerTest | temporal | — |
| 5 tests | TemporalOwnedQuerySqlServerTest | not `AssertQuery`, so un-rewritten query runs against modified data | — |
| JSON no-ops (6) | AdHocJsonQuerySqlServerJsonTypeTest | with the `json` type, bad JSON seed data is rejected at insert, so read-side error tests can't run | — |
| `Can_insert/update_many_to_many_shared_with_payload(_unidirectional)`, `Can_insert_update_delete_shared_type_entity_type` (5 each) | ManyToManyTrackingProxy(GeneratedKeys)SqlServerTest | mutable properties aren't proxyable on `Dictionary` shared-type entities | — |
| `Save_two_entity_cycle_with_lazy_loading` | ProxyGraphUpdates `ChangeTracking` variant | needs lazy loading | — |
| `Parameter_collection_in_subquery_and_Convert_as_compiled_query` | PrimitiveCollectionsQueryOldSqlServerTest | compat 120 | — |

### 3.5 Skipped via `Skip =` (known bugs, not capability)

| Spec test(s) | Class | Skip reason |
|---|---|---|
| 19 entity-splitting-with-owned tests (`*_owning_a_split_*`) | EntitySplittingQuerySqlServerTest | Issue 29075 |
| `ToString_boolean_computed_nullable` | GearsOfWar, TPC/TPT/Temporal GearsOfWar | #34001 SQL Server never returns null for `bool?` |
| 5 `Temporal_multiop_*` | MigrationsSqlServerTest (TemporalTables) | #36161 |
| `Complex_query_with_let_collection_projection_FirstOrDefault_*`, `Filtered_include_*parameter*` (2), `Include_inside_subquery` | TemporalComplexNavigationsCollectionsSharedTypeQuerySqlServerTest | #28058, #26922 |
| `Add_required_primitive_collection_with_custom_converter_to_existing_table` (2 spellings) | MigrationsSqlServerTest | #33038 |
| `Edit_single_property_collection_of_char`, `Edit_single_property_relational_collection_of_char` | JsonUpdateJsonTypeSqlServerTest | json type hang / investigate |
| `Can_set_connection_string_in_interceptor` | TwoDatabasesSqlServerTest | Application Name injection (#36548) |
| 2 create-retry theories | SqlServerDatabaseCreatorTest | #36578 |
| `SqlQuery_with_converted_type_using_model_configuration_builder_works` | CustomConvertersSqlServerTest | #30730 |
| `Concurrency_test` | DbContextPoolingTest | #32700 |
| 2 online-index migrations | MigrationsSqlServerTest | #19668 Enterprise-only (also condition-gated) |

### 3.6 Condition-gated spec overrides (run only on capable servers)

| Spec test(s) | Class(es) | Gate | Why |
|---|---|---|---|
| ~29 `Json_collection_index_*`, `Json_projection_*_collection_indexer_*`, `*_AsNoTrackingWithIdentityResolution` (3, re-implemented) | JsonQuery(JsonType)SqlServerTest | `SupportsJsonPathExpressions` | non-constant JSON path in `JSON_VALUE` needs 2017+ |
| `Parameter_collection_index_Column_equal_Column/constant`, `Parameter_collection_with_type_inference_for_JsonScalarExpression` | PrimitiveCollectionsQuerySqlServer(160/JsonType)Test | `SupportsJsonPathExpressions` | same |
| `Index_no_evaluatability`, `Index_with_captured_variable` | AdHocPrecompiledQuerySqlServerTest | `SupportsJsonPathExpressions` | same |
| `Join_over_(non_)nullable_column`, `Join_with_predicate`, `Join_with_ordering`, `Join_non_aggregate`, `Concat_aggregate` | StringTranslationsSqlServerTest | `SupportsFunctions2017` | `STRING_AGG`/`CONCAT_WS` |
| `ExecuteUpdate_within_json_to_nonjson_column` | Bool/ByteArray/DateOnly/DateTime/DateTimeOffset/Guid/TimeOnly/TimeSpan TypeTest | `SupportsFunctions2022` | JSON_VALUE … RETURNING / compat 160 |
| `Nav_rewrite_doesnt_apply_null_protection_for_function_arguments` | ComplexNavigationsSharedTypeQuerySqlServer160Test | `SupportsFunctions2022` | |
| whole classes | Spatial*, TemporalTable*/Temporal*Query*, *JsonType*, *160Test, SqlAzure* | see §4 | |

## 4. Conditions and flags

`[SqlServerCondition(...)]` (TestUtilities/SqlServerConditionAttribute.cs) is an `ITestCondition`; flags AND together. The probes live in `TestEnvironment` and query the live server once: `SERVERPROPERTY('ProductVersion')` (major), `SERVERPROPERTY('EngineEdition')`, `compatibility_level` of `master`, `FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')`, `SERVERPROPERTY('IsXTPSupported')`. Each can be forced via config key of the same name (`GetFlag`). The assembly carries `[SqlServerConfiguredCondition]` (skip everything when no `DefaultConnection` is configured).

| Condition | Probe (true when) | Gates | Uses |
|---|---|---|---|
| `IsAzureSql` | EngineEdition 5 or 8 | SqlAzure folder classes (batching, encrypted connect, fundamentals, database creation in tiers/elastic pool) | 4 |
| `IsNotAzureSql` | !IsAzureSql | EverythingIsBytes/Strings, ConvertToProviderTypes, CustomConverters classes; database creation tests; `Ignore_columnstore_index`; GUID non-key generation; one Migrations-infrastructure test; UTF-8 type test | 11 |
| `SupportsMemoryOptimized` | `IsXTPSupported` = 1 and not LocalDB | MemoryOptimizedTablesTest; memory-optimized migrations (6); scaffold annotation | 8 |
| `SupportsAttach` | data source is `(localdb)` or user instance | database-creator tests that attach an `.mdf` file name (7) | 7 |
| `SupportsHiddenColumns` | major ≥13 (not edition 6) or Azure, compat ≥130 | scaffold of (hidden) period columns | 2 |
| `IsNotCI` | env var `PIPELINE_WORKSPACE` absent | database-creator classes, multi-database sequence test, migrations infra | 10 |
| `SupportsFullTextSearch` | full-text installed | all `Contains`/`FreeText` full-text tests (Northwind DbFunctions, GearsOfWar binary columns) | 44 |
| `SupportsOnlineIndexes` | EngineEdition 3 (Enterprise/Developer) or Azure | `ONLINE = ON` index migrations | 2 |
| `SupportsTemporalTablesCascadeDelete` | major ≥14 or Azure, compat ≥140 | all temporal query classes (TemporalTable, TemporalGearsOfWar, TemporalOwned, TemporalManyToMany, TemporalComplexNavigationsCollections, TPHTemporalFilters) | 6 classes |
| `SupportsUtf8` | major ≥15 or Azure, compat ≥150 | UTF-8 collation round-trip in BuiltInDataTypes | 1 |
| `SupportsJsonPathExpressions` | major ≥14 or Azure, compat ≥140 | non-constant JSON path indexing (Json collection index tests, parameter collection index, precompiled index tests) | 63 |
| `SupportsSqlClr` | EngineEdition ≠ 9 (not Azure SQL Edge) | spatial classes (geometry/geography need CLR types), `AT TIME ZONE` tests, spatial default value, compiled-model spatial baseline | 13 |
| `SupportsFunctions2017` | major ≥14 or Azure, compat ≥140 | `STRING_AGG`/`CONCAT_WS` string join/concat aggregate tests | 6 |
| `SupportsFunctions2019` | major ≥15 or Azure, compat ≥150 | (defined, unused) | 0 |
| `SupportsFunctions2022` | major ≥16 or Azure, compat ≥160 | all `*SqlServer160Test` classes (GREATEST/LEAST, LTRIM/RTRIM with chars and other 2022 functions), JSON-type primitive collections, `ExecuteUpdate_within_json_to_nonjson_column` on Types, precompiled aggregate expression | 17 |
| `SupportsJsonType` | major ≥17 or Azure, compat ≥170 | native `json` column classes (`*JsonType*` query/update/adhoc), scaffold of `json` | 5 |
| `SupportsVectorType` | (not LocalDB and major ≥17) or Azure, compat ≥170 | VectorTranslationsSqlServerTest, scaffold `Vector_type` | 2 |

**Version / compatibility-level switches that are not attributes** (the same spec suite run several times under different provider options):

| Mechanism | Classes | Effect on expectations |
|---|---|---|
| `UseSqlServerCompatibilityLevel(120)` | PrimitiveCollectionsQueryOldSqlServerTest, PrecompiledSqlPregenerationQuerySqlServerTest | no `OPENJSON`: parameter collections expand to `IN (@p1,…)`, column collections fail to translate |
| default 150 | most suites | baseline expectations; GREATEST/LEAST, `LTRIM(x, chars)`, Math.Max/Min fail |
| `UseSqlServerCompatibilityLevel(160)` | *160Test variants (ComplexNavigations(SharedType), NorthwindDbFunctions, NorthwindFunctions, PrimitiveCollections, MathTranslations, StringTranslations), BasicTypesQuerySqlServerFixture (all Translations suites) | GREATEST/LEAST, LTRIM/RTRIM with chars and other compat-160 translations available |
| `SqlServerMajorVersion ≥ 17 → UseSqlServerCompatibilityLevel(170)`; `Fixture.UsingJsonType` branches | ComplexJson*, OwnedJson* association suites; Types fixtures (`SetCompatibilityLevelFromEnvironment`) | same tests assert two SQL baselines (`nvarchar(max)` + `OPENJSON` vs `json` type + `JSON_VALUE … RETURNING`) and some flip to `SqlException` (DISTINCT over json) |
| `UseAzureSql` when `TestEnvironment.IsAzureSql` | every store (`SqlServerTestStore.AddProviderOptions`) | Azure defaults (retry on, compat 170 → json type) |
| `MultipleActiveResultSets` in connection string | TransactionSqlServerTest | savepoint tests become no-ops |
| `PIPELINE_WORKSPACE` (CI) | `SqlServerTestStore` re-seeds scripted databases only in CI | — |

## 5. Test infrastructure

| Component | Role | SQL Server-specific parts |
|---|---|---|
| `SqlServerTestStore : RelationalTestStore` | create/clean/seed named databases; `ExecuteScript`, `ExecuteScalar/NonQuery/Query` helpers; command timeout 300 s | creates databases by connecting to `master` (`CREATE DATABASE`, optional `.mdf` file name and `AttachDBFilename`), `sys.databases` existence check, `WaitForExists`, `DROP DATABASE` with single-user/pool clearing; `GO`-separated scripts (Northwind.sql); chooses `UseSqlServer` vs `UseAzureSql`; ignores `SavepointsDisabledBecauseOfMARS`; MARS option in `CreateConnectionString` |
| `SqlServerTestStoreFactory`, `SqlServerNorthwindTestStoreFactory` (shared `Northwind` DB from script), `SqlServerAdventureWorksTestStoreFactory` | store factories for fixtures; `AddProviderServices` = `AddEntityFrameworkSqlServer` | pre-built Northwind/AdventureWorks scripts |
| `AzureSynapseTestStore` | Synapse variant | `UseAzureSynapse` |
| `SqlServerDatabaseCleaner : RelationalDatabaseCleaner` | drops everything between tests (`EnsureClean`) using the scaffolding model factory | T-SQL that drops views in dependency order, then functions, CLR aggregates, procedures, user types, non-default schemas; adds temporal/memory-optimized annotations to drop operations; ignores views/indexes |
| `SqlServerDbContextOptionsBuilderExtensions.ApplyConfiguration` | uniform provider options for all tests | `TestSqlServerRetryingExecutionStrategy`, single-query splitting, command timeout, optional `MaxBatchSize` from config; `UseSqlServerCompatibilityLevel(n)` picks SQL Server vs Azure |
| `TestSqlServerRetryingExecutionStrategy` | retries with extra error numbers (debug mode throws on unretried errors) | SqlClient error numbers; "Internal .Net Framework Data Provider error 6" |
| `SqlExceptionFactory` | build `SqlException` with a given error number (for retry/creator tests) | reflection into Microsoft.Data.SqlClient |
| `TestSqlServerConnection`, `TestRelationalTransaction`, `TestRelationalCommandBuilderFactory` | fault injection: fail open/commit/execute N times with given error numbers | SqlServer connection subclass |
| `TestEnvironment` | config (`config.json`, `config.test.json`, env vars, section `Test:SqlServer`), `DefaultConnection`, capability probes, `SqlServerMajorVersion`, `ElasticPoolName` | `SERVERPROPERTY`, `FULLTEXTSERVICEPROPERTY`, `sys.databases` |
| `SqlServerCondition(Attribute)`, `SqlServerConfiguredConditionAttribute` | test gating | see §4 |
| `SqlServerTestHelpers : RelationalTestHelpers` | unit-test service provider, `UseSqlServer("Database=DummyDatabase")`, logging definitions | |
| `SqlServerPrecompiledQueryTestHelpers` | precompiled-query compilation references | references SqlServer assembly |
| `SqlServerDatabaseFacadeExtensions.EnsureClean` | entry point for the cleaner | |
| `TemporalPointInTimeQueryRewriter` (Query/) | rewrites every spec query root to `TemporalAsOf(date)` for the temporal re-runs | temporal tables are SQL Server-only among the five engines |
| Fixtures: `*SqlServerFixture` per suite (Northwind, GearsOfWar(+TPC/TPT/Temporal), ComplexNavigations, ComplexJson/OwnedJson (compat-switching), SqlServerTypeFixture, Spatial (NTS), Json (json type), BuiltInDataTypes (all store types incl. `money`, `smalldatetime`, `xml`, `sql_variant`), Migrations) | seed data and model per suite; `TestSqlLoggerFactory` captures SQL for `AssertSql` baselines | model configs use SQL Server store types, `UseIdentityColumn`, `IsTemporal`, `HasColumnType("json")`, `vector(n)` |
| `Scaffolding/Baselines/**` | compiled-model C# baselines compared verbatim | |
| `SqlServerNTSApiConsistencyTest`, `*ApiConsistencyTest` (unit) | public API surface consistency | |

Test-time requirements: a reachable SQL Server (LocalDB by default: `Data Source=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True`), permission to create/drop databases, optionally Full-Text, In-Memory OLTP, CLR types (spatial), Enterprise edition (online indexes), SQL Server 2025 (json/vector). Isolation is database-per-fixture (named databases, cleaned with `EnsureClean`, or dropped with `SET SINGLE_USER WITH ROLLBACK IMMEDIATE` + `ClearAllPools`).
