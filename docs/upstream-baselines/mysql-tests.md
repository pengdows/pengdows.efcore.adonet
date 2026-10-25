# Pomelo.EntityFrameworkCore.MySql (main, 9.0.1 / EF Core 9): test suite as a behaviour record

Source: `providers/pomelo/test` (EFCore.MySql.FunctionalTests 350 files, EFCore.MySql.Tests 28, EFCore.MySql.IntegrationTests 32, Shared 4). Read only; nothing was run.
Concept and API names follow `api-inventory/mysql.md`. **[M]** = MySQL-only behaviour or gate, **[MDB]** = MariaDB-only.

Method: class declarations, base chains and test attributes were parsed from the sources (partial classes merged). Inherited counts come from reading the `[Fact]/[Theory]/[ConditionalFact]/[ConditionalTheory]` methods in the EF Core **9.0.1** specification assemblies (`Microsoft.EntityFrameworkCore{,.Relational}.Specification.Tests.dll` in the NuGet cache), summed up the base chain. A theory counts once, whatever its data rows. "SQL baselines" counts `AssertSql(...)`/`AssertExecuteUpdateSql(...)`/`AssertBaseline(...)` calls that carry SQL (empty `AssertSql()` excluded).

Tools left in `scratchpad/tools/` (`specount/`, `pomelo_classes.py`, `divs.py`, `conds.py`, `sqlden.py`) for re-running against EF 10.

**Assertion style** (section 1) and **Asserted by** (sections 2 and 3):
- *SQL*: a SQL baseline (`AssertSql`) or generated-DDL string.
- *Results*: query results compared with expected data (`AssertQuery` evaluates the same LINQ in memory) or with .NET values; for model or type-mapping tests, the model or mapping.
- *Both*: results and a SQL baseline. This is the default for provider-only query tests.
- *Exception/message*: the test expects an exception (type and sometimes message).

---

## 1. Suite map

### Totals

| | Classes | Tests (method count) |
|---|---|---|
| Spec-derived, runnable (public) | ~161 | **~11,770 inherited** (EF 9.0.1 spec methods); 11,082 of the non-model-building ones still exist under the same base names in EF 10.0.11 |
| Spec-derived but `internal`, so xUnit never runs them | 9 (`CommandInterception*` x2, `TransactionInterception*` x2, `Logging`, `NorthwindQueryTagging`, `OptionalDependentQuery`, `AdHocJsonQuery`, `MySqlGenericOwnedTypes`) | ~230 (disabled) |
| Provider-only tests (new `[Fact]/[Theory]` methods, in provider-only classes **and** added to spec-derived classes) | 47 provider-only classes, plus 14 spec-derived classes that add their own tests | **~664** (excludes 46 commented-out Npgsql leftovers in `MySqlTypeMappingTest` and 22 `Check_all_tests_overridden` guards) |
| Overrides of spec tests | | ~3,130 (most only add a MySQL SQL baseline) |

Assertion style: **46 classes are SQL-heavy** (a baseline for most tests): the TPC suites, GroupBy, Functions, PrimitiveCollections, ManyToMany (TPC), EF.Property include, Migrations, ComplexNavigationsCollections(Split), BulkUpdates, Spatial, ComplexType, SqlQuery, Ef6GroupBy, StringComparison, MatchQuery, the SQL generators, StoreValueGeneration, the JSON DOM/POCO suites and DbFunctions. The plain (TPH) `GearsOfWarQueryMySqlTest`, `NorthwindIncludeQuery*`, `NorthwindMiscellaneous`, `NorthwindSelect`, `NorthwindAggregateOperators`, `ComplexNavigationsQuery`, `Load`, `GraphUpdates`, `PropertyValues`, `FieldMapping`, `ManyToMany(Query/NoTracking)` (TPH/TPT), `TPT*` and everything outside Query are **results-only or mostly results**. They inherit spec tests without SQL overrides.

Folder tables: "Overrides" is the number of overriding methods; "SQL baselines" is counted per source file.

### FunctionalTests

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| BadDataJsonDeserializationMySqlTest | BadDataJsonDeserializationTestBase | 5 | 0 | 1 | 0 | Results only |  |
| BuiltInDataTypesMySqlTest | BuiltInDataTypesTestBase | 28 | 12 | 6 | 0 | Results only |  |
| ChangeTracking | ProxyGraphUpdatesTestBase | 85 | 0 | 1 | 0 | Results only |  |
| ChangeTrackingAndLazyLoading | ProxyGraphUpdatesTestBase | 85 | 0 | 0 | 0 | Results only |  |
| CommandInterceptionMySqlTest | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | Results only | **internal: not run** |
| CommandInterceptionWithDiagnosticsMySqlTest | CommandInterceptionTestBase | 35 | 0 | 0 | 0 | Results only | **internal: not run** |
| ComplexTypesTrackingMySqlTest | ComplexTypesTrackingTestBase | 39 | 0 | 1 | 0 | Results only |  |
| CompositeKeyEndToEndMySqlTest | CompositeKeyEndToEndTestBase | 3 | 0 | 0 | 0 | Results only |  |
| ConcurrencyDetectorDisabledMySqlTest | ConcurrencyDetectorDisabledRelationalTestBase | 9 | 0 | 1 | 0 | Results only |  |
| ConcurrencyDetectorEnabledMySqlTest | ConcurrencyDetectorEnabledRelationalTestBase | 9 | 0 | 1 | 0 | Results only |  |
| ConferencePlannerMySqlTest | ConferencePlannerTestBase | 22 | 0 | 1 | 0 | Results only |  |
| ConnectionInterceptionMySqlTest | ConnectionInterceptionTestBase | 9 | 0 | 0 | 0 | Results only |  |
| ConnectionInterceptionWithDiagnosticsMySqlTest | ConnectionInterceptionTestBase | 9 | 0 | 0 | 0 | Results only |  |
| ConnectionMySqlTest | provider-only | 0 | 6 | 0 | 0 | Results only |  |
| ConnectionSettingsMySqlTest | provider-only | 0 | 1 | 0 | 0 | Results only |  |
| ConvertToProviderTypesMySqlTest | ConvertToProviderTypesTestBase | 30 | 0 | 5 | 0 | Results only |  |
| CustomConvertersMySqlTest | CustomConvertersTestBase | 57 | 0 | 5 | 0 | Results only |  |
| DataAnnotationMySqlTest | DataAnnotationRelationalTestBase | 97 | 0 | 3 | 5 | Mostly results |  |
| DatabindingMySqlTest | DataBindingTestBase | 37 | 0 | 0 | 0 | Results only |  |
| DefaultValuesTest | provider-only | 0 | 2 | 0 | 0 | Results only |  |
| DesignTimeMySqlTest | DesignTimeTestBase | 2 | 0 | 0 | 0 | Results only |  |
| EntitySplittingMySqlTest | EntitySplittingTestBase | 2 | 0 | 0 | 0 | Results only |  |
| FieldMappingMySqlTest | FieldMappingTestBase | 101 | 0 | 1 | 0 | Results only |  |
| FindMySqlTestContext | FindTestBase | 69 | 0 | 0 | 0 | Results only |  |
| FindMySqlTestNonGeneric | FindTestBase | 69 | 0 | 0 | 0 | Results only |  |
| FindMySqlTestSet | FindTestBase | 69 | 0 | 0 | 0 | Results only |  |
| FullInfrastructureMigrationsTest | provider-only | 0 | 4 | 0 | 0 | Results only |  |
| GraphUpdatesMySqlClientCascadeTest | GraphUpdatesTestBase | 127 | 0 | 1 | 0 | Results only |  |
| GraphUpdatesMySqlClientNoActionTest | GraphUpdatesTestBase | 127 | 0 | 1 | 0 | Results only |  |
| KeysWithConvertersMySqlTest | KeysWithConvertersTestBase | 47 | 0 | 0 | 0 | Results only |  |
| LazyLoadProxyMySqlTest | LazyLoadProxyTestBase | 75 | 0 | 3 | 0 | Results only |  |
| LazyLoading | ProxyGraphUpdatesTestBase | 85 | 0 | 0 | 0 | Results only |  |
| LoadMySqlTest | LoadTestBase | 202 | 0 | 0 | 0 | Results only |  |
| LoggingMySqlTest | LoggingRelationalTestBase | 13 | 0 | 0 | 0 | Results only | **internal: not run** |
| ManyToManyLoadMySqlTest | ManyToManyLoadTestBase | 48 | 0 | 0 | 0 | Results only |  |
| ManyToManyTrackingMySqlTest | ManyToManyTrackingRelationalTestBase | 67 | 0 | 0 | 0 | Results only |  |
| MaterializationInterceptionMySqlTest | MaterializationInterceptionTestBase | 7 | 0 | 0 | 0 | Results only |  |
| MigrationsInfrastructureMySqlTest | MigrationsInfrastructureTestBase | 25 | 0 | 24 | 0 | Results only |  |
| MigrationsMySqlTest | MigrationsTestBase | 120 | 23 | 120 | 112 | SQL-heavy | Check_all_tests_overridden |
| ModelBuilding101MySqlTest | ModelBuilding101RelationalTestBase | 124 | 0 | 0 | 0 | Results only |  |
| MonsterFixupChangedChangingMySqlTest | MonsterFixupTestBase | 12 | 0 | 0 | 0 | Results only |  |
| MusicStoreMySqlTest | MusicStoreTestBase | 16 | 0 | 0 | 0 | Results only |  |
| MySqlApiConsistencyTest | ApiConsistencyTestBase | 18 | 0 | 1 | 0 | Results only |  |
| MySqlComplianceTest | RelationalComplianceTestBase | 2 | 0 | 0 | 0 | Results only |  |
| MySqlMigrationsSqlGeneratorTest | MigrationsSqlGeneratorTestBase | 49 | 57 | 36 | 35 | Mixed |  |
| MySqlNetTopologySuiteApiConsistencyTest | ApiConsistencyTestBase | 18 | 0 | 1 | 0 | Results only |  |
| MySqlServiceCollectionExtensionsTest | RelationalServiceCollectionExtensionsTestBase | 3 | 0 | 0 | 0 | Results only |  |
| NorthwindQueryTaggingQueryMySqlTest | NorthwindQueryTaggingQueryTestBase | 9 | 0 | 0 | 0 | Results only | **internal: not run** |
| NotificationEntitiesMySqlTest | NotificationEntitiesTestBase | 2 | 0 | 0 | 0 | Results only |  |
| OptimisticConcurrencyMySqlTest | OptimisticConcurrencyRelationalTestBase | 34 | 0 | 3 | 0 | Results only |  |
| OverzealousInitializationMySqlTest | OverzealousInitializationTestBase | 1 | 0 | 0 | 0 | Results only |  |
| PropertyValuesMySqlTest | PropertyValuesTestBase | 142 | 0 | 0 | 0 | Results only |  |
| QueryExpressionInterceptionMySqlTest | QueryExpressionInterceptionTestBase | 3 | 0 | 0 | 0 | Results only |  |
| QueryExpressionInterceptionWithDiagnosticsMySqlTest | QueryExpressionInterceptionTestBase | 3 | 0 | 0 | 0 | Results only |  |
| SaveChangesInterceptionMySqlTest | SaveChangesInterceptionTestBase | 6 | 0 | 0 | 0 | Results only |  |
| SaveChangesInterceptionWithDiagnosticsMySqlTest | SaveChangesInterceptionTestBase | 6 | 0 | 0 | 0 | Results only |  |
| SeedingMySqlTest | SeedingTestBase | 2 | 0 | 0 | 0 | Results only |  |
| SerializationMySqlTest | SerializationTestBase | 1 | 0 | 0 | 0 | Results only |  |
| SpatialMySqlTest | SpatialTestBase | 5 | 0 | 3 | 0 | Results only |  |
| StoreGeneratedFixupMySqlTest | StoreGeneratedFixupRelationalTestBase | 118 | 1 | 1 | 0 | Results only |  |
| StoreGeneratedMySqlTest | StoreGeneratedTestBase | 60 | 0 | 1 | 0 | Results only |  |
| TPTTableSplittingMySqlTest | TPTTableSplittingTestBase | 26 | 0 | 1 | 0 | Results only |  |
| TableSplittingMySqlTest | TableSplittingTestBase | 26 | 0 | 1 | 0 | Results only |  |
| TransactionInterceptionMySqlTest | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | Results only | **internal: not run** |
| TransactionInterceptionWithDiagnosticsMySqlTest | TransactionInterceptionTestBase | 17 | 0 | 0 | 0 | Results only | **internal: not run** |
| TransactionMySqlTest | TransactionTestBase | 44 | 0 | 0 | 0 | Results only |  |
| TwoDatabasesMySqlTest | TwoDatabasesTestBase | 3 | 0 | 0 | 0 | Results only |  |
| UpdatesMySqlTest | UpdatesRelationalTestBase | 29 | 0 | 2 | 0 | Results only |  |
| ValueConvertersEndToEndMySqlTest | ValueConvertersEndToEndTestBase | 25 | 1 | 0 | 0 | Results only |  |
| WithConstructorsMySqlTest | WithConstructorsTestBase | 41 | 0 | 1 | 0 | Results only |  |

### FunctionalTests/BulkUpdates

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| ComplexTypeBulkUpdatesMySqlTest | ComplexTypeBulkUpdatesRelationalTestBase | 15 | 0 | 15 | 17 | SQL-heavy | base gone in EF10, Check_all_tests_overridden |
| NonSharedModelBulkUpdatesMySqlTest | NonSharedModelBulkUpdatesRelationalTestBase | 14 | 0 | 14 | 16 | SQL-heavy | Check_all_tests_overridden |
| NorthwindBulkUpdatesMySqlTest | NorthwindBulkUpdatesRelationalTestBase | 86 | 0 | 86 | 79 | SQL-heavy | Check_all_tests_overridden |
| TPCFiltersInheritanceBulkUpdatesMySqlTest | TPCFiltersInheritanceBulkUpdatesTestBase | 18 | 0 | 19 | 14 | SQL-heavy | Check_all_tests_overridden |
| TPCInheritanceBulkUpdatesMySqlTest | TPCInheritanceBulkUpdatesTestBase | 20 | 0 | 21 | 16 | SQL-heavy | Check_all_tests_overridden |
| TPHFiltersInheritanceBulkUpdatesMySqlTest | FiltersInheritanceBulkUpdatesTestBase | 16 | 0 | 16 | 16 | SQL-heavy | Check_all_tests_overridden |
| TPHInheritanceBulkUpdatesMySqlTest | TPHInheritanceBulkUpdatesTestBase | 20 | 0 | 21 | 19 | SQL-heavy | Check_all_tests_overridden |
| TPTFiltersInheritanceBulkUpdatesMySqlTest | TPTFiltersInheritanceBulkUpdatesTestBase | 18 | 0 | 19 | 12 | SQL-heavy | Check_all_tests_overridden |
| TPTInheritanceBulkUpdatesMySqlTest | TPTInheritanceBulkUpdatesTestBase | 22 | 0 | 21 | 12 | SQL-heavy | Check_all_tests_overridden |

### FunctionalTests/Query

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| AdHocAdvancedMappingsQueryMySqlTest | AdHocAdvancedMappingsQueryRelationalTestBase | 19 | 0 | 3 | 0 | Results only |  |
| AdHocJsonQueryMySqlTest | AdHocJsonQueryTestBase | 21 | 0 | 8 | 0 | Results only | **internal: not run** |
| AdHocManyToManyQueryMySqlTest | AdHocManyToManyQueryRelationalTestBase | 2 | 0 | 0 | 0 | Results only |  |
| AdHocMiscellaneousQueryMySqlTest | AdHocMiscellaneousQueryRelationalTestBase | 43 | 0 | 2 | 0 | Results only |  |
| AdHocNavigationsQueryMySqlTest | AdHocNavigationsQueryRelationalTestBase | 21 | 0 | 0 | 0 | Results only |  |
| AdHocQueryFiltersQueryMySqlTest | AdHocQueryFiltersQueryRelationalTestBase | 12 | 0 | 2 | 2 | Mixed |  |
| AdHocQuerySplittingQueryMySqlTest | AdHocQuerySplittingQueryTestBase | 10 | 0 | 0 | 0 | Results only |  |
| BoolIndexingOptimizationDisabledMySqlTest | provider-only | 0 | 8 | 0 | 12 | SQL-heavy |  |
| ComplexNavigationsCollectionsQueryMySqlTest | ComplexNavigationsCollectionsQueryRelationalTestBase | 156 | 0 | 94 | 97 | SQL-heavy |  |
| ComplexNavigationsCollectionsSharedTypeQueryMySqlTest | ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase | 156 | 0 | 8 | 10 | Mostly results |  |
| ComplexNavigationsCollectionsSplitQueryMySqlTest | ComplexNavigationsCollectionsSplitQueryRelationalTestBase | 156 | 0 | 102 | 99 | SQL-heavy |  |
| ComplexNavigationsCollectionsSplitSharedTypeQueryMySqlTest | ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase | 156 | 0 | 0 | 0 | Results only |  |
| ComplexNavigationsQueryMySqlTest | ComplexNavigationsQueryRelationalTestBase | 308 | 0 | 7 | 6 | Mostly results |  |
| ComplexNavigationsSharedTypeQueryMySqlTest | ComplexNavigationsSharedTypeQueryRelationalTestBase | 308 | 0 | 9 | 5 | Mostly results |  |
| ComplexTypeQueryMySqlTest | ComplexTypeQueryRelationalTestBase | 76 | 0 | 76 | 68 | SQL-heavy | Check_all_tests_overridden |
| CompositeKeysQueryMySqlTest | CompositeKeysQueryRelationalTestBase | 7 | 0 | 7 | 9 | SQL-heavy |  |
| CompositeKeysSplitQueryMySqlTest | CompositeKeysSplitQueryRelationalTestBase | 7 | 0 | 7 | 9 | SQL-heavy |  |
| DateOnlyQueryMySqlTest | provider-only | 0 | 7 | 0 | 9 | SQL-heavy |  |
| Ef6GroupByMySqlTest | Ef6GroupByTestBase | 55 | 0 | 55 | 45 | SQL-heavy | Check_all_tests_overridden |
| EntitySplittingQueryMySqlTest | EntitySplittingQueryTestBase | 57 | 0 | 57 | 38 | SQL-heavy | Check_all_tests_overridden |
| EscapesMySqlNoBackslashesTest | provider-only | 0 | 4 | 4 | 6 | SQL-heavy |  |
| EscapesMySqlTest | provider-only | 0 | 4 | 4 | 6 | SQL-heavy |  |
| FieldsOnlyLoadMySqlTest | FieldsOnlyLoadTestBase | 120 | 0 | 0 | 0 | Results only |  |
| FromSqlQueryMySqlTest | FromSqlQueryTestBase | 73 | 0 | 1 | 0 | Results only |  |
| FromSqlSprocQueryMySqlTest | FromSqlSprocQueryTestBase | 24 | 0 | 10 | 0 | Results only |  |
| FunkyDataQueryMySqlTest | FunkyDataQueryTestBase | 19 | 0 | 5 | 5 | Mixed |  |
| GearsOfWarFromSqlQueryMySqlTest | GearsOfWarFromSqlQueryTestBase | 1 | 0 | 0 | 0 | Results only |  |
| GearsOfWarQueryMySqlTest | GearsOfWarQueryRelationalTestBase | 674 | 10 | 39 | 23 | Mostly results |  |
| IncludeOneToOneMySqlTest | IncludeOneToOneTestBase | 12 | 0 | 0 | 0 | Results only |  |
| InheritanceRelationshipsQueryMySqlTest | InheritanceRelationshipsQueryRelationalTestBase | 70 | 0 | 0 | 0 | Results only |  |
| JsonMicrosoftDomChangeTrackingTest | provider-only | 0 | 12 | 0 | 0 | Results only |  |
| JsonMicrosoftDomQueryTest | provider-only | 0 | 29 | 0 | 26 | SQL-heavy |  |
| JsonMicrosoftPocoChangeTrackingTest | provider-only | 0 | 14 | 0 | 0 | Results only |  |
| JsonMicrosoftPocoQueryTest | provider-only | 0 | 28 | 1 | 0 | Results only |  |
| JsonMicrosoftStringChangeTrackingTest | provider-only | 0 | 12 | 0 | 0 | Results only |  |
| JsonMicrosoftStringQueryTest | provider-only | 0 | 11 | 0 | 0 | Results only |  |
| JsonNewtonsoftDomChangeTrackingTest | provider-only | 0 | 12 | 0 | 0 | Results only |  |
| JsonNewtonsoftDomQueryTest | provider-only | 0 | 31 | 0 | 28 | SQL-heavy |  |
| JsonNewtonsoftPocoChangeTrackingTest | provider-only | 0 | 14 | 0 | 0 | Results only |  |
| JsonNewtonsoftPocoQueryTest | provider-only | 0 | 28 | 1 | 0 | Results only |  |
| JsonNewtonsoftStringChangeTrackingTest | provider-only | 0 | 12 | 0 | 0 | Results only |  |
| JsonNewtonsoftStringQueryTest | provider-only | 0 | 11 | 0 | 0 | Results only |  |
| ManyToManyFieldsLoadMySqlTest | ManyToManyFieldsLoadTestBase | 23 | 0 | 0 | 0 | Results only |  |
| ManyToManyNoTrackingQueryMySqlTest | ManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 0 | 0 | Results only |  |
| ManyToManyQueryMySqlTest | ManyToManyQueryRelationalTestBase | 123 | 0 | 0 | 0 | Results only |  |
| MappingQueryMySqlTest | MappingQueryTestBase | 4 | 0 | 4 | 0 | Results only |  |
| MatchQueryMySqlTest | provider-only | 0 | 37 | 0 | 37 | SQL-heavy |  |
| NonSharedPrimitiveCollectionsQueryMySqlTest | NonSharedPrimitiveCollectionsQueryRelationalTestBase | 36 | 0 | 35 | 29 | SQL-heavy | Check_all_tests_overridden |
| NorthwindAggregateOperatorsQueryMySqlTest | NorthwindAggregateOperatorsQueryRelationalTestBase | 211 | 0 | 7 | 3 | Mostly results |  |
| NorthwindAsNoTrackingQueryMySqlTest | NorthwindAsNoTrackingQueryTestBase | 11 | 0 | 0 | 0 | Results only |  |
| NorthwindAsTrackingQueryMySqlTest | NorthwindAsTrackingQueryTestBase | 5 | 0 | 0 | 0 | Results only |  |
| NorthwindChangeTrackingQueryMySqlTest | NorthwindChangeTrackingQueryTestBase | 17 | 0 | 0 | 0 | Results only |  |
| NorthwindCompiledQueryMySqlTest | NorthwindCompiledQueryTestBase | 32 | 0 | 0 | 2 | Mostly results |  |
| NorthwindDbFunctionsQueryMySqlTest | NorthwindDbFunctionsQueryRelationalTestBase | 17 | 27 | 3 | 32 | SQL-heavy |  |
| NorthwindEFPropertyIncludeQueryMySqlTest | NorthwindEFPropertyIncludeQueryTestBase | 118 | 0 | 118 | 116 | SQL-heavy | Check_all_tests_overridden |
| NorthwindFunctionsQueryMySqlTest | NorthwindFunctionsQueryRelationalTestBase | 171 | 5 | 172 | 142 | SQL-heavy | Check_all_tests_overridden |
| NorthwindGroupByQueryMySqlTest | NorthwindGroupByQueryRelationalTestBase | 257 | 0 | 258 | 241 | SQL-heavy | Check_all_tests_overridden |
| NorthwindIncludeNoTrackingQueryMySqlTest | NorthwindIncludeNoTrackingQueryTestBase | 117 | 0 | 6 | 2 | Mostly results |  |
| NorthwindIncludeQueryMySqlTest | NorthwindIncludeQueryRelationalTestBase | 117 | 0 | 6 | 2 | Mostly results |  |
| NorthwindJoinQueryMySqlTest | NorthwindJoinQueryRelationalTestBase | 61 | 0 | 3 | 3 | Mostly results |  |
| NorthwindKeylessEntitiesQueryMySqlTest | NorthwindKeylessEntitiesQueryRelationalTestBase | 20 | 0 | 1 | 3 | Mixed |  |
| NorthwindMiscellaneousQueryMySqlTest | NorthwindMiscellaneousQueryRelationalTestBase | 482 | 1 | 31 | 23 | Mostly results |  |
| NorthwindNavigationsQueryMySqlTest | NorthwindNavigationsQueryRelationalTestBase | 73 | 0 | 4 | 2 | Mostly results |  |
| NorthwindQueryFiltersQueryMySqlTest | NorthwindQueryFiltersQueryTestBase | 17 | 0 | 1 | 3 | Mixed |  |
| NorthwindSelectQueryMySqlTest | NorthwindSelectQueryRelationalTestBase | 182 | 2 | 26 | 14 | Mostly results |  |
| NorthwindSetOperationsQueryMySqlTest | NorthwindSetOperationsQueryRelationalTestBase | 89 | 0 | 13 | 12 | Mostly results |  |
| NorthwindSplitIncludeNoTrackingQueryMySqlTest | NorthwindSplitIncludeNoTrackingQueryTestBase | 117 | 0 | 7 | 0 | Results only |  |
| NorthwindSplitIncludeQueryMySqlTest | NorthwindSplitIncludeQueryTestBase | 117 | 0 | 11 | 2 | Mostly results |  |
| NorthwindSqlQueryMySqlTest | NorthwindSqlQueryTestBase | 4 | 0 | 4 | 6 | SQL-heavy | Check_all_tests_overridden |
| NorthwindStringComparisonFunctionsQueryMySqlTest | provider-only | 0 | 44 | 0 | 41 | SQL-heavy |  |
| NorthwindStringIncludeQueryMySqlTest | NorthwindStringIncludeQueryTestBase | 118 | 0 | 6 | 2 | Mostly results |  |
| NorthwindWhereQueryMySqlTest | NorthwindWhereQueryRelationalTestBase | 239 | 17 | 37 | 38 | Mostly results |  |
| NullKeysMySqlTest | NullKeysTestBase | 5 | 0 | 0 | 0 | Results only |  |
| NullSemanticsQueryMySqlTest | NullSemanticsQueryTestBase | 166 | 1 | 2 | 3 | Mostly results |  |
| OperatorsQueryMySqlTest | OperatorsQueryTestBase | 10 | 0 | 11 | 11 | SQL-heavy |  |
| OptionalDependentQueryMySqlTest | OptionalDependentQueryTestBase | 10 | 0 | 10 | 2 | Mixed | **internal: not run** |
| OwnedEntityQueryMySqlTest | OwnedEntityQueryRelationalTestBase | 23 | 0 | 4 | 2 | Mostly results |  |
| OwnedQueryMySqlTest | OwnedQueryRelationalTestBase | 104 | 0 | 2 | 0 | Results only |  |
| PrimitiveCollectionsQueryMySqlTest | PrimitiveCollectionsQueryRelationalTestBase | 131 | 0 | 128 | 121 | SQL-heavy | Check_all_tests_overridden |
| QueryFilterFuncletizationMySqlTest | QueryFilterFuncletizationTestBase | 28 | 0 | 1 | 4 | Mostly results |  |
| QueryNoClientEvalMySqlTest | QueryNoClientEvalTestBase | 14 | 0 | 1 | 0 | Results only |  |
| SharedTypeQueryMySqlTest | SharedTypeQueryRelationalTestBase | 4 | 0 | 2 | 2 | SQL-heavy |  |
| SpatialGeographyQueryMySqlTest | provider-only | 0 | 6 | 0 | 2 | Mixed |  |
| SpatialQueryMySqlTest | SpatialQueryRelationalTestBase | 84 | 0 | 84 | 69 | SQL-heavy | Check_all_tests_overridden |
| SqlExecutorMySqlTest | SqlExecutorTestBase | 14 | 0 | 13 | 5 | Mixed |  |
| SqlQueryMySqlTest | SqlQueryTestBase | 61 | 0 | 60 | 57 | SQL-heavy | Check_all_tests_overridden |
| TPCFiltersInheritanceQueryMySqlTest | TPCFiltersInheritanceQueryTestBase | 11 | 0 | 11 | 13 | SQL-heavy | Check_all_tests_overridden |
| TPCGearsOfWarQueryMySqlTest | TPCGearsOfWarQueryRelationalTestBase | 674 | 0 | 669 | 642 | SQL-heavy | Check_all_tests_overridden |
| TPCInheritanceQueryMySqlTest | TPCInheritanceQueryTestBase | 53 | 0 | 1 | 0 | Results only |  |
| TPCManyToManyNoTrackingQueryMySqlTest | TPCManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 124 | 117 | SQL-heavy | Check_all_tests_overridden |
| TPCManyToManyQueryMySqlTest | TPCManyToManyQueryRelationalTestBase | 123 | 0 | 123 | 119 | SQL-heavy | Check_all_tests_overridden |
| TPCRelationshipsQueryMySqlTest | TPCRelationshipsQueryTestBase | 70 | 0 | 70 | 71 | SQL-heavy | Check_all_tests_overridden |
| TPHInheritanceQueryMySqlTest | TPHInheritanceQueryTestBase | 55 | 0 | 0 | 0 | Results only |  |
| TPTFiltersInheritanceQueryMySqlTest | TPTFiltersInheritanceQueryTestBase | 11 | 0 | 0 | 0 | Results only |  |
| TPTGearsOfWarQueryMySqlTest | TPTGearsOfWarQueryRelationalTestBase | 674 | 0 | 35 | 7 | Mostly results |  |
| TPTInheritanceQueryMySqlTest | TPTInheritanceQueryTestBase | 53 | 0 | 0 | 0 | Results only |  |
| TPTManyToManyNoTrackingQueryMySqlTest | TPTManyToManyNoTrackingQueryRelationalTestBase | 124 | 0 | 0 | 0 | Results only |  |
| TPTManyToManyQueryMySqlTest | TPTManyToManyQueryRelationalTestBase | 123 | 0 | 0 | 0 | Results only |  |
| TPTRelationshipsQueryMySqlTest | TPTRelationshipsQueryTestBase | 70 | 0 | 0 | 0 | Results only |  |
| ToSqlQueryMySqlTest | ToSqlQueryTestBase | 1 | 0 | 1 | 3 | SQL-heavy | Check_all_tests_overridden |
| WarningsMySqlTest | WarningsTestBase | 11 | 0 | 0 | 0 | Results only |  |

### FunctionalTests/Scaffolding

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| CompiledModelMySqlTest | CompiledModelRelationalTestBase | 15 | 0 | 8 | 0 | Results only |  |
| MySqlDatabaseModelFactoryTest | provider-only | 0 | 49 | 0 | 0 | Results only |  |

### FunctionalTests/Storage

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlJsonMicrosoftTypeMappingTest | provider-only | 0 | 6 | 0 | 0 | Results only |  |
| MySqlJsonNewtonsoftTypeMappingTest | provider-only | 0 | 4 | 0 | 0 | Results only |  |
| MySqlTypeMappingTest | provider-only | 0 | 5 | 0 | 0 | Results only |  |

### FunctionalTests/Update

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlUpdateSqlGeneratorTest | UpdateSqlGeneratorTestBase | 14 | 4 | 15 | 21 | SQL-heavy |  |
| NonSharedModelUpdatesMySqlTest | NonSharedModelUpdatesTestBase | 2 | 0 | 1 | 4 | SQL-heavy |  |
| StoreValueGenerationMySqlTest | StoreValueGenerationTestBase | 19 | 0 | 19 | 29 | SQL-heavy |  |
| StoredProcedureUpdateMySqlTest | StoredProcedureUpdateTestBase | 28 | 0 | 30 | 15 | SQL-heavy |  |

### IntegrationTests/Tests

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| BatchTest | provider-only | 0 | 1 | 0 | 0 | Results only |  |
| ConnectionTest | provider-only | 0 | 1 | 0 | 0 | Results only |  |
| CrmTest | provider-only | 0 | 2 | 0 | 0 | Results only |  |
| DataTypesTest | provider-only | 0 | 2 | 0 | 0 | Results only |  |
| ExpressionTest | provider-only | 0 | 26 | 0 | 0 | Results only |  |
| GeneratedTypesTest | provider-only | 0 | 3 | 0 | 0 | Results only |  |
| SequenceTest | provider-only | 0 | 1 | 0 | 0 | Results only |  |

### Tests

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlDbContextOptionsBuilderExtensionsTest | provider-only | 0 | 26 | 0 | 0 | Results only |  |
| MySqlOptionsExtensionTest | provider-only | 0 | 1 | 0 | 0 | Results only |  |
| MySqlRelationalConnectionTest | provider-only | 0 | 9 | 0 | 0 | Results only |  |

### Tests/Behaviors

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| HavingBehavior | provider-only | 0 | 5 | 0 | 0 | Results only |  |
| MySql8WithSridConstraintSpatialBehavior | provider-only | 0 | 2 | 0 | 0 | Results only |  |
| MySql8WithoutSridConstraintSpatialBehavior | provider-only | 0 | 2 | 0 | 0 | Results only |  |
| SpatialBehavior | provider-only | 0 | 2 | 0 | 0 | Results only |  |

### Tests/Bugs

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlBug96947 | provider-only | 0 | 2 | 0 | 0 | Results only |  |

### Tests/Migrations

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlHistoryRepositoryTest | provider-only | 0 | 2 | 0 | 0 | Results only |  |
| ServerVersionTest | provider-only | 0 | 2 | 0 | 0 | Results only |  |

### Tests/Query

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlCollationTest | provider-only | 0 | 3 | 0 | 0 | Results only |  |
| MySqlTimeZoneTest | provider-only | 0 | 2 | 0 | 0 | Results only |  |

### Tests/ValueGeneration

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MysqlSequentialGuidValueGeneratorTest | provider-only | 0 | 8 | 0 | 0 | Results only |  |

### FunctionalTests/ModelBuilding

(Nested classes of `MySqlModelBuilderGenericTest : MySqlModelBuilderTestBase : RelationalModelBuilderTest`; counts are the nested spec base plus its core parent.)

| Class | Spec base (EF 9.0.1) | Inherited tests | Provider-only tests | Overrides | SQL baselines | Assertion style | Notes |
|---|---|---|---|---|---|---|---|
| MySqlGenericNonRelationship | RelationalNonRelationshipTestBase | 173 | 0 | 0 | 0 | Model only |  |
| MySqlGenericComplexType | RelationalComplexTypeTestBase | 86 | 0 | 0 | 0 | Model only |  |
| MySqlGenericInheritance | RelationalInheritanceTestBase | 31 | 0 | 0 | 0 | Model only |  |
| MySqlGenericOneToMany | RelationalOneToManyTestBase | 90 | 0 | 0 | 0 | Model only |  |
| MySqlGenericManyToOne | RelationalManyToOneTestBase | 59 | 0 | 0 | 0 | Model only |  |
| MySqlGenericOneToOne | RelationalOneToOneTestBase | 142 | 0 | 0 | 0 | Model only |  |
| MySqlGenericManyToMany | RelationalManyToManyTestBase | 36 | 0 | 0 | 0 | Model only |  |
| MySqlGenericOwnedTypes | RelationalOwnedTypesTestBase | 63 | 0 | 1 | 0 | Model only | **internal: not run**; overrides `Can_use_sproc_mapping_with_owned_reference` to expect InvalidOperationException |

---

## 2. Provider-only behaviours

Grouped by concept. "Where" names the class (abbreviated: `…MySqlTest` dropped). Near-identical tests are grouped, with the count in brackets.

### 2.1 Charset / collation

| Behaviour asserted (database-neutral) | Where | Provider API | Asserted by |
|---|---|---|---|
| Create/alter database with an explicit charset or collation emits the clause [5] | MySqlMigrationsSqlGenerator: `CreateDatabaseOperation(_with_charset/_with_collation)`, `AlterDatabaseOperation_with_charset/_collation` | `HasCharSet`/`UseCollation` on the model; database annotations | SQL |
| Table-level charset, collation and table options: create, alter, and reset to default [8] | MySqlMigrationsSqlGenerator: `CreateTableOperation_with_charset/_collation/_table_options`, `AlterTableOperation_with_charset(_reset)/_collation(_reset)/_table_options` | `HasCharSet`, `UseCollation`, `HasTableOption` (entity) | SQL |
| A column charset is implicit or comes from an annotation [2] | MySqlMigrationsSqlGenerator: `AddColumnOperation_with_charset_implicit/_annotation` | `HasCharSet` (property) | SQL |
| Delegation: model, table and column charset/collation inherit down the hierarchy; an explicit column value beats the inherited one; "table-only" delegation in between; upgrading from the legacy charset to the annotation charset emits no ALTERs [9] | Migrations: `Add_columns_with_collations`, `Alter_column_collations_with_delegation(2/_columns_only/_with_inbetween_tableonly_collation)`, `Alter_column_charsets_using_delegated_charset_with_tableonly_charset_inbetween`, `Create_table_explicit_column_{charset,collation}_takes_precedence_over_inherited_*`, `Upgrade_legacy_charset_to_annotation_charset_only_does_not_generate_alter_column_operations` | `DelegationModes`, `HasCharSet(…, DelegationModes)`, `UseCollation(…, DelegationModes)` | Both (DDL baseline and scaffolded model) |
| A legacy charset written inside the column type (`longtext CHARACTER SET …`) and an upper-case `NVARCHAR` store type round-trip [2] | Migrations: `Create_table_longtext_column_with_string_length_and_legacy_charset_definition_in_column_type`, `Create_table_NVARCHAR_UPPERCASE_column` | `HasColumnType` | Both |
| Guid columns get a default binary-safe collation (ascii_general_ci), which can be explicit, the default, or disabled [4] | Migrations: `Add_guid_columns(_with_collation/_with_explicit_default_collation/_with_disabled_default_collation)` | `MySqlGuidFormat.Char36`, `UseGuidCollation` option | Both |
| Scaffolding reads the table collation [1] | MySqlDatabaseModelFactory: `Create_table_with_collation` | reverse engineering | Results (DatabaseModel) |
| Comparing columns whose collations are incompatible fails; an explicit `EF.Functions.Collate` (case-sensitive or case-insensitive) makes it succeed [3] [MDB < 11.4 gate on the failing test] | Tests/Query/MySqlCollation | `EF.Functions.Collate`, `SET NAMES` session collation | Exception/message + Results |
| Concatenation and string comparison under the server's default utf8mb4 collation (MariaDB 11.4.2–11.5 "illegal mix of collations" is excluded) | NorthwindWhere: `Where_concat_string_int_comparison1-4`, `Using_same_parameter_twice…` | `DefaultUtf8CiCollation`/`CsCollation` per server | Both |
| The test store creates every database with charset `utf8mb4`; the default factories (`InstanceCi`) use `ServerVersion.DefaultUtf8CiCollation`, and `InstanceCs` the case-sensitive one | TestUtilities/MySqlTestStore(Factory) | `CHARACTER SET … COLLATE …` | (infrastructure) |

### 2.2 String comparison (StringComparison overloads) and string functions

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| `string.Equals`, `Contains`, `StartsWith`, `EndsWith` and `IndexOf` (instance and static), each with every `StringComparison`: Ordinal, Invariant and Current give a case-sensitive comparison (`COLLATE utf8mb4_bin`); the `*IgnoreCase` variants give `LCASE()` on both sides; a comparison passed as a parameter becomes a CASE over the value; `IndexOf` with a start index [44] | NorthwindStringComparisonFunctionsQuery | translation of StringComparison overloads | Both |
| Equals with `InvariantCulture`, used directly on a column, fails translation with a provider-specific message | NorthwindMiscellaneous: `Using_(static_)string_Equals_with_StringComparison_throws_informative_error` (overrides) | `MySqlStrings.QueryUnableToTranslateMethodWithStringComparison` | Exception/message |
| `Remove(n)`/`Remove(i,n)` become SUBSTRING; `string.Concat` over objects, arrays, params, enumerables and single elements becomes CONCAT, or is parameterized when the whole value is client-side [16] | NorthwindWhere: `Where_string_remove(_count)`, `Where_string_concat_method_comparison_*` [14] | String translators | Both |
| `PadLeft`/`PadRight` with and without a pad char become LPAD/RPAD [4] | NorthwindFunctions (.MySql partial) | String translators | Both |
| Contains/StartsWith/EndsWith with a pattern that contains wildcard or escape characters: a constant pattern is escaped inline, a parameter pattern is rewritten client-side (`@p_contains`) [6] | NorthwindDbFunctions: `{Contains,StartsWith,EndsWith}_with_{escape,wild}_char` | LIKE escaping | Both |
| `EF.Functions.Like` over non-string columns (int, uint, short, DateTime) casts to char; Like with an escape character [5] | NorthwindDbFunctions: `Like_{Int,Uint,Short,DateTime}_literal`, `Like_Int_literal_with_escape` | `EF.Functions.Like(object…)` | Both |
| Backslash and quote escaping in literals, parameters and IN lists, with and without `NO_BACKSLASH_ESCAPES` [4 x 2 fixtures] | EscapesMySql / EscapesMySqlNoBackslashes (base: EscapesMySqlTestBase) | `DisableBackslashEscaping()`, sql_mode | Both |
| `CREATE DATABASE` works with backslash escaping disabled | ConnectionMySql: `Can_create_database_with_disablebackslashescaping` | `DisableBackslashEscaping` | Results |
| String-function translators each return the correct results: Contains/StartsWith/EndsWith (optimized forms), Equals, Length, Replace, Substring, ToLower, ToUpper, Regex.IsMatch, ToString, Convert [~12] | IntegrationTests/ExpressionTest | translators | Results |

### 2.3 Full-text search

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| `IsMatch` (predicate) and `Match` (relevance score) in natural-language mode, boolean mode, a search mode passed as a parameter, and query expansion; one or many columns; keywords separated, several or excluded; score evaluation [37] | MatchQuery | `EF.Functions.Match`, `EF.Functions.IsMatch`, `MySqlMatchSearchMode` | Both |
| A FULLTEXT index, also with a parser (ngram), is created [2] and scaffolded [2] | MySqlMigrationsSqlGenerator: `CreateIndexOperation_fulltext(_with_parser)`; MySqlDatabaseModelFactory: `Set_fulltext_for_fulltext_index`, `Set_fulltextparser…` (gate FullTextParser) | `IsFullText(…, parser)` | SQL / Results |

### 2.4 Guid formats and Guid generation

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| Insert and read back a Guid under each connection `GuidFormat` (Char36, Char32, Binary16, TimeSwapBinary16, LittleEndianBinary16, None), and find it with raw SQL in the matching storage form (string, `X'…'` byte order, `UUID_TO_BIN(x, swap)` [M ≥ 8.0]) [8 data rows] | ConnectionSettings: `Insert_and_read_Guid_value` | `MySqlGuidFormat` (connection string), type mapping | Results |
| Sequential Guid generator: new, unique, loosely sorted, valid RFC 4122 random version; each in the default and the LittleEndianBinary16 layout [8] | Tests/ValueGeneration/MysqlSequentialGuidValueGenerator | `MySqlSequentialGuidValueGenerator` | Results |
| `Guid.NewGuid()` becomes `UUID()`; a Guid parameter compared against it | NorthwindWhere: `Where_guid`; IntegrationTests `MySqlNewGuidTranslator` | Guid translators | Both |
| A Guid column with a database-generated `UUID()` default; scaffolding Guid columns [2] | MySqlMigrationsSqlGenerator: `AddColumnOperation_with_dbgenerated_uuid`; MySqlDatabaseModelFactory: `Create_guid_columns` (gate DefaultExpression) | database-generated Guid default | SQL / Results |

### 2.5 Bool mapping and bool indexing

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| `bool` maps to `tinyint(1)` by default and with `TinyInt1`, to `bit(1)` with `Bit1`, to `int` with `None`; with the default setting a `bit(1)` store type maps to `ulong` [5] | Storage/MySqlTypeMapping (live part) | `DefaultDataTypeMappings(m => m.WithClrBoolean(MySqlBooleanType.*))` | Results (mapping) |
| `TreatTinyAsBoolean` true, false or unset in the connection string decides the bool mapping; explicit `DefaultDataTypeMappings` take precedence [5] | Tests/MySqlDbContextOptionsBuilderExtensions | `TreatTinyAsBoolean`, `DefaultDataTypeMappings` | Results |
| Bool predicates are emitted bare (`WHERE col`, `WHERE NOT (col)`) for `==true`, `!=false`, negation and parenthesized forms, both with index-optimized bool columns **on** (in GearsOfWar, with a `FORCE INDEX` key-usage check) and **off** [8 + 8] | GearsOfWarQuery (.MySql partial), BoolIndexingOptimizationDisabled | `EnableIndexOptimizedBooleanColumns` | Both |
| `prm == e.NullableBool.HasValue` | NullSemanticsQuery: `Compare_left_bool_parameter_with_right_nullable_hasvalue` | null semantics | Both |

### 2.6 Date and time: DateDiff, DateOnly, time zones, precision

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| `DateDiffYear` … `DateDiffNanosecond` (Year, Quarter, Week, Month, Day, Hour, Minute, Second, Millisecond, Microsecond, Tick, Nanosecond) become TIMESTAMPDIFF, with the unit scaled for tick and nanosecond [12] | NorthwindDbFunctions (.MySql partial) | `EF.Functions.DateDiff*` | Both |
| `DateOnly.DayNumber` becomes `TO_DAYS(x) - 366` and matches the CLR value; `ToDateTime(TimeOnly)` becomes `ADDTIME(CAST … datetime(6), TIME '…')`, or a plain CAST for midnight; `DateOnly.FromDateTime(DateTime.Today)`; `DateDiffDay` over DateOnly and over `ToDateTime` [7] | DateOnlyQuery | DateOnly translators, `EF.Functions.DateDiffDay` | Both |
| `CONVERT_TZ` of a DateTimeOffset to a time zone from a column, and `DateTimeOffset.LocalDateTime` through `@@session.time_zone` [2] | Tests/Query/MySqlTimeZone | `EF.Functions.ConvertTimeZone` | Both |
| `DateTimeOffset.DateTime` and `.UtcDateTime` are no-ops (values are stored in UTC) [2] | GearsOfWarQuery (.MySql) | DateTimeOffset mapping | Both |
| Default store types: DateTime and DateTimeOffset map to `datetime(6)`, TimeSpan to `time(6)`, decimal to `decimal(65,30)`, char to `varchar(1)`, bool to `tinyint(1)`; every mapped type inserts, reads back (nullable, null and batched variants) and queries with constants and parameters [12] | BuiltInDataTypes (`Columns_have_expected_data_types`, `Can_*_all_mapped_*`, `Sql_translation_uses_type_mapper_when_*`) | type mapping | Results (+ store-type dump) |
| `datetime(6)` column add; a datetime default with `ON UPDATE` [2] | MySqlMigrationsSqlGenerator: `AddColumnOperation_with_datetime6`, `AddDefaultDatetimeOperation_with_valueOnUpdate` | `HasPrecision`, `ValueGeneratedOnAddOrUpdate`/CURRENT_TIMESTAMP ON UPDATE | SQL |
| Database-generated time, concurrency token and rowversion columns update on save [3] | IntegrationTests/GeneratedTypes | `timestamp(6)` ON UPDATE | Results |
| The DateAdd, DatePart, DateTime.Now and Date-to-DateTime translators [4] | IntegrationTests/ExpressionTest | translators | Results |

### 2.7 JSON (provider JSON types, not EF 7+ `ToJson`)

All JSON classes are gated by `Supports.Json`. Each base suite runs once for Microsoft (System.Text.Json) and once for Newtonsoft.

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| DOM mapping (`JsonDocument`/`JsonElement` or `JToken`/`JObject`): round trip; literal, document and element parameters; text, int, Guid and bool output; nested and twice-nested navigation; arrays of objects, nested arrays, array index from a parameter; array length; LIKE on JSON text; nullable Guid; root value [~22 each] | JsonMicrosoftDomQuery [29], JsonNewtonsoftDomQuery [31] | DOM path translation to `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_LENGTH` | Both |
| `EF.Functions.JsonContains`, `JsonContainsPath(Any/All)`, `JsonType`, `JsonSearchAny/All` (with path), `JsonOverlaps` (string, element, through JsonExtract) [M ≥ 8.0.17 gate JsonOverlaps] | same | `EF.Functions.Json*` | Both |
| POCO mapping: round trip, literal, parameter, text, int, Guid and bool output, nullable, nested, arrays (top-level, nested, parameter index, Length, Any), LIKE, nullable Guid, `JsonQuote/JsonUnquote/JsonExtract/JsonContains/JsonContainsPath(Any)/JsonExistAll/JsonType` [28 x 2] | JsonPocoQueryTestBase, via JsonMicrosoftPocoQuery and JsonNewtonsoftPocoQuery | POCO-to-JSON mapping, `EF.Functions.Json*` | Both |
| String-as-JSON mapping: round trip, literal, parameter, `MySqlJsonString` cast, JsonExtract/Unquote/Quote, JsonContains, JsonExists/ExistAny/ExistAll [11 x 2] | JsonStringQueryTestBase | `HasColumnType("json")` on string, `MySqlJsonString` | Both / Results |
| Change tracking of JSON properties: `CompareRootPropertyOnly`, `None` (per property and global), `CompareStringRootPropertyByEquals`; root and inner changes; whitespace-only changes; semantically equal values; top-level array elements; the snapshot calls Clone or DeepClone [12–14 per mapping, 6 classes] | Json{Microsoft,Newtonsoft}{Dom,Poco,String}ChangeTracking | `MySqlJsonChangeTrackingOptions`, `UseMicrosoftJson(options)`/`UseNewtonsoftJson` | Results (EntityState and saved value) |
| Old enum values stored in JSON entities are read [3] (internal class, not run) | AdHocJsonQuery | — | Results |
| Scaffolding finds JSON columns through their CHECK constraints [MDB] | MySqlDatabaseModelFactory: `Create_json_column` (gate IdentifyJsonColumsByCheckConstraints) | reverse engineering | Results |

### 2.8 Spatial

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| Geographic distance: `Distance` on SRID 4326 is within 1% of `ST_Distance_Sphere`; `IsWithinDistance`; `SpatialDistancePlanar`; `SpatialDistanceSphere` with the Native, Andoyer and Haversine algorithms [6] | SpatialGeographyQuery | `EF.Functions.SpatialDistanceSphere`, `SpatialDistancePlanar`, `SpatialDistanceAlgorithm` | Results (with a tolerance) |
| Axis order: NTS keeps x = longitude; MySQL 8 with SRID 4326 swaps to x = latitude when the column has an SRID constraint, and depends on the SRID otherwise; MySQL 5.7 and MariaDB keep x = longitude [2+2+2] | Tests/Behaviors/*SpatialBehavior (gate SpatialSetSridFunction / LessThan) | — | Results |
| Column SRID in DDL (`/*!80003 SRID 4326 */`), also for derived geometry types; spatial index; scaffolded SRID [4] | MySqlMigrationsSqlGenerator `CreateTable_uses_srid(_geometry_derived)`, `CreateIndexOperation_spatial`; MySqlDatabaseModelFactory `Column_srid_value_is_set`, `Set_spatial_for_spatial_index` | `HasSpatialReferenceSystem`, `IsSpatialIndex` | SQL / Results |

### 2.9 Schema behaviour, identifiers, DDL dialect (migrations and scaffolding)

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| A schema is either ignored or mapped into the table name (`schema_table`) by a translator; the history-table "exists" SQL follows the setting [1+2] | MySqlMigrationsSqlGenerator `CreateTable_with_SchemaNameTranslator`; Tests/Migrations/MySqlHistoryRepository `ExistsSql_respects_SchemaBehavior(_explicit)` | `SchemaBehavior(MySqlSchemaBehavior.Ignore/Translate, translator)` | SQL |
| Identifier length limit of 64: long FK, index and table names are truncated [3] | MySqlMigrationsSqlGenerator `AddForeignKeyOperation_with_long_name`, `CreateIndexOperation_with_long_name`; Updates `Identifiers_are_generated_correctly` (override) | identifier truncation | SQL / Results |
| Dropping a PK or unique constraint temporarily drops and recreates the FKs that depend on it (provider operations) [4]; the same through MigrationBuilder, and in full migrations [3+1] | MySqlMigrationsSqlGenerator `DropPrimaryKeyOperation`, `MySqlDropPrimaryKeyAndRecreateForeignKeysOperation…`, `DropUniqueConstraintOperation`, `MySqlDropUniqueConstraintAndRecreateForeignKeys…`; Migrations `Drop_unique_constraint_{with,without}_recreating_foreign_keys(_MigrationBuilder)`; FullInfrastructureMigrations `Drop_primary_key_with_recreating_foreign_keys` | `MySqlDropPrimaryKeyAndRecreateForeignKeysOperation`, `DropPrimaryKey(recreateForeignKeys:)` | SQL / Results |
| An auto-increment PK is changed through helper stored procedures (`POMELO_BEFORE_DROP_PRIMARY_KEY`, `POMELO_AFTER_ADD_PRIMARY_KEY`); idempotent scripts can contain them; a script needs no custom `DELIMITER` statements; altering the PK column emits no default value [4] | FullInfrastructureMigrations | migrations SQL generator, `MySqlMigrator` | SQL / Results |
| Index prefix lengths on indexes and PKs (generation, scaffolding, several indexes on the same columns); renaming an index keeps the prefix [6] | MySqlMigrationsSqlGenerator `CreateIndexOperation_with_prefix_lengths`, `CreateTableOperation_primary_key_with_prefix_lengths`; MySqlDatabaseModelFactory `Prefix_lengths_*` [4]; Migrations `Rename_index_with_prefix_length` | `HasPrefixLength` | SQL / Results |
| Rename index or column: native `RENAME INDEX/COLUMN`, or a DROP+CREATE or `CHANGE` fallback that needs the model (and throws without the table) [6] | MySqlMigrationsSqlGenerator `RenameIndexOperation(s_throws_when_no_table)`, `DropIndexOperations_throws_when_no_table`, `RenameColumnOperation(_with_model, _with_model_required_without_default_value)` | gates RenameIndex/RenameColumn | SQL / Exception |
| Computed columns: VIRTUAL vs STORED, altering the computed SQL when an index exists; scaffolding virtual, stored and constant-string computed columns [5] | MySqlMigrationsSqlGenerator `AddColumnOperation_with_computed_column`, `AlterColumnOperation_ComputedColumnSql_{with_index,stored}`, `AlterColumnOperation_type_with_index`; MySqlDatabaseModelFactory `Computed_value_*` [3] | `HasComputedColumnSql(stored)` | SQL / Results |
| Auto-increment: `serial` for an int key, a ulong key, an int default that is not serial, `ValueGenerationStrategy` for int [4] | MySqlMigrationsSqlGenerator `AddColumnOperation_serial`, `…int_defaultValue_isnt_serial`, `CreateTableUlongAutoincrement`, `CreateTable_with_ValueGenerationStrategy_int_value` | `MySqlValueGenerationStrategy.IdentityColumn` | SQL |
| Sequences [MDB only]: create/alter with and without min/max; drop [5] | MySqlMigrationsSqlGenerator `*SequenceOperation*` (gate Sequences) | `HasSequence` | SQL |
| Scaffolding (live tests only): missing principal table, a view whose column type has a comment after a cast [MDB ≥ 10.4], default-value columns (literal, simple function, expression, `curdate()` [MDB]), column store type, CLR default ignored, case-mismatched constraints [~10 live, 27 skipped] | MySqlDatabaseModelFactory | reverse engineering | Results (DatabaseModel) |

### 2.10 Default values and text columns

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| Text/blob columns get a literal default only when the server supports default expressions (MySQL ≥ 8.0.13 `DefaultExpression`, MariaDB `AlternativeDefaultExpression`); otherwise no default is generated, with a warning [4+3] | MySqlMigrationsSqlGenerator `DefaultValue_{generated,not_generated}_for_*_text_column*`, `DefaultValue_formats_literal_correctly`, `AddDefaultBooleanOperation`, `AlterColumnOperation_with_no_default_value_column_types`; Migrations `Add_column_with_defaultValue_string_{limited,unlimited}_length…`, `Add_column_with_defaultValueSql_simple` | gates DefaultExpression/AlternativeDefaultExpression | SQL / Results |
| Making a string column required emits an UPDATE that fills NULLs, not a DEFAULT | Migrations `Alter_string_column_make_required_generates_update_statement_instead_of_default_value` | migrations generator | SQL |
| Server defaults read back after insert, depending on expression syntax [2] | DefaultValuesTest | `HasDefaultValueSql` | Results |

### 2.11 Options, ServerVersion and connection

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| `UseMySql` with `MySqlServerVersion` or `MariaDbServerVersion` from a `Version`, a version-only string, a full string or a ServerVersion; the wrong type throws; `LatestSupportedServerVersion`; `ServerVersion.FromString`/`Parse` [13]; ServerVersion parsing, valid and invalid [2] | Tests/MySqlDbContextOptionsBuilderExtensions; Tests/Migrations/ServerVersion | `ServerVersion`, `MySqlServerVersion`, `MariaDbServerVersion` | Results / Exception |
| `ServerVersion.AutoDetect` from a connection string, a closed connection, an open connection or a DbDataSource [4] | same | `ServerVersion.AutoDetect` | Results |
| Several UseMySql calls all apply; no connection string; the service-collection extension; options hash code [4] | same; MySqlOptionsExtension | `AddMySql`, options extension | Results |
| DbDataSource from the options or from the application service provider, with and without mandatory settings (the provider forces `AllowUserVariables` and `UseAffectedRows=false`); named connection string; admin/master connection; ambient transaction with AutoEnlist [9+5] | Tests/MySqlRelationalConnection, ConnectionMySql (`SetConnectionString`, `Can_create_admin_connection_*`) | `MySqlRelationalConnection`, mandatory connection-string settings | Results / Exception |
| `UseAffectedRows=false` is required for correct concurrency counts [1] | IntegrationTests/ConnectionTest `AffectedRowsFalse` | connection settings | Results |
| A batch insert mixing rows with and without server-generated values [1] | IntegrationTests/BatchTest | batching | Results |

### 2.12 Query-shape dialect (HAVING, LIMIT, APPLY, window functions)

| Behaviour asserted | Where | Provider API | Asserted by |
|---|---|---|---|
| HAVING without an aggregate must refer to a projection alias; referring to the column or the function directly fails with "Unknown column" [5] | Tests/Behaviors/HavingBehavior | (server behaviour) | Results / Exception/message |
| A projected function used in a HAVING clause, also concatenated [2] | NorthwindSelect (.MySql) `Select_with_function_using_having_clause(_concatenated)` | HAVING alias rewriting | Both |
| Without CROSS APPLY or OUTER APPLY (LATERAL) support, the correlated SelectMany tests throw; without window functions, `ROW_NUMBER() OVER(PARTITION BY)` throws [3] (LessThan gates) | NorthwindSelect (.MySql) `CrossApply_/OuterApply_not_supported_throws`; NorthwindMiscellaneous (.MySql) `RowNumberOverPartitionBy_not_supported_throws` | gates CrossApply/OuterApply/WindowFunctions | Exception |
| MySQL bug 96947 (a constant in a derived table becomes wrong without a CAST) still exists [2] | Tests/Bugs/MySqlBug96947 | `MySqlBug96947Workaround` | Results |
| Multi-row INSERT for bulk inserts, with and without store-generated columns, and default-values-only rows [4] | MySqlUpdateSqlGenerator `AppendBulkInsertOperation_*` | update SQL generator | SQL |
| Math: `Math.Log(x, 2)`, HEX/UNHEX, DEGREES/RADIANS functions; Abs, Ceiling, Floor, Power, Round, Truncate, Acos, Cos and Sin translators [5 + ~9] | NorthwindFunctions `Where_math_log_new_base2`; NorthwindDbFunctions `Hex`, `Unhex`, `Degrees`, `Radians`; IntegrationTests ExpressionTest | `EF.Functions.Hex/Unhex/Degrees/Radians` | Both / Results |
| Store-generated temp values made permanent; value-converted properties map to nullable columns correctly [2] | StoreGeneratedFixup, ValueConvertersEndToEnd | — | Results |
| CRM and data-type end-to-end scenarios (eager and explicit loading, simple and variable-length types, sequence increment) [5] | IntegrationTests Crm/DataTypes/Sequence | — | Results |

---

## 3. Divergences from the shared spec

"Gap" is the capability reason. MySQL/MariaDB differences are called out. "Asserted by" describes the override.

### 3.1 Translation failed or Exception instead of a result (permanent capability gaps)

| Spec test(s) | Expected instead | Reason / capability gap | MySQL vs MariaDB | Asserted by |
|---|---|---|---|---|
| NorthwindWhere `Where_compare_{constructed,tuple_constructed,tuple_create_constructed}_{equal,multi_value_equal,multi_value_not_equal}` [9] | AssertTranslationFailed | Tuple or anonymous-object (row value) equality is not translated | both | Exception |
| NorthwindAggregateOperators `Contains_with_local_{anonymous_type,tuple}_array_closure`, `Contains_with_local_enumerable_inline` [3] | InvalidOperationException | Contains over a composite or inline enumerable is not translated | both | Exception |
| NorthwindFunctions `Trim/TrimStart/TrimEnd_with_char_array_argument_in_predicate` [3] | InvalidOperationException | TRIM takes only one char or string | both | Exception |
| NorthwindFunctions `Datetime_subtraction_TotalDays` | AssertTranslationFailed | No TimeSpan arithmetic mapping (TIME is limited to ±838h) | both | Exception |
| NorthwindSelect `Member_binding_after_ctor_arguments_fails_with_client_eval`; ComplexNavigations `GroupJoin_client_method_in_OrderBy`; NorthwindMiscellaneous `Client_code_unknown_method` [3] | translation failed, with a provider message | spec expects a different message or behaviour | both | Exception/message |
| CustomConverters `Value_conversion_on_enum_collection_contains` | TranslationFailed | Contains over a value-converted collection | both | Exception/message |
| NorthwindMiscellaneous `Using_(static_)string_Equals_with_StringComparison_throws_informative_error` [2] | Provider message `QueryUnableToTranslateMethodWithStringComparison` | Pomelo translates most StringComparison values; it only rejects culture-specific ones on a column | both | Exception/message |
| GearsOfWar and TPCGearsOfWar `Non_string_concat_uses_appropriate_type_mapping` [2] | InvalidCastException (decimal to TimeSpan) | TimeSpan addition yields decimal | both | Exception/message |
| OperatorsQuery `Concat_and_json_scalar` | InvalidOperationException | EF JSON-column (`ToJson`) mapping is unsupported | both | Exception |
| NonSharedPrimitiveCollections `Column_collection_inside_json_owned_entity`; CompiledModel `BigModel_with_JSON_columns` [2] | `MySqlStrings.Ef7CoreJsonMappingNotSupported` | No EF 7+ `ToJson()` owned-JSON support | both | Exception/message |
| Migrations `*json*` (create, rename, add, drop, convert to and from owned or JSON, string-to-JSON conversion) [11] | NullReferenceException | Same: ToJson is unsupported in migrations | both | Exception |
| OptionalDependentQuery [10]; AdHocJsonQuery [21]; Json* bases listed in MySqlComplianceTest (`JsonQuery*`, `JsonTypes*`, `JsonUpdate*`) | not run (internal, or listed as ignored) | Same | both | Exception |
| StoredProcedureUpdate: result columns, two result columns, output parameter plus result column, rows-affected result column or return value (with concurrency failure), TPH, TPT [11]; CompiledModel `ComplexTypes` | `MySqlStrings.StoredProcedureResultColumnsNotSupported` / `…ReturnValueNotSupported` | MySQL procedures can only return values through OUT parameters | both | Exception/message |
| ModelBuilder `Can_use_sproc_mapping_with_owned_reference` | InvalidOperationException | Same | both | Exception |
| FromSqlSproc: composing over a stored procedure (projection, re-projection, Where, Take, Min, Include, several procedures, procedure plus SELECT) [10] | MySqlException | `CALL` cannot be used as a subquery | both | Exception |
| MigrationsInfrastructure `Can_generate_idempotent_up_and_down_scripts(_noTransactions)` [2] | MySqlException "'DELIMITER' should not be used with MySqlConnector" | Idempotent scripts use DELIMITER, which the driver cannot execute | both | Exception/message |
| Migrations `Alter_column_change_computed_type` | MySqlException "Changing the STORED status…" / "not yet supported…" | VIRTUAL and STORED cannot be switched in place | messages differ (MySQL / MariaDB) | Exception/message |
| Migrations `Alter_column_make_computed` (stored only), `Alter_column_make_non_computed`, `Rename_table`, `Add_column_with_defaultValueSql`, `Create_sequence_all_settings` | rewritten test body | Generated-column and sequence differences (sequences have no schema) | MDB sequences | SQL |
| ComplexType `Project_(struct_)complex_type_via_optional_navigation` [2] | `CannotProjectNullableComplexType` | Same as the relational base (asserted explicitly) | both | Exception/message |
| NorthwindKeylessEntities `KeylessEntity_with_nav_defining_query` | MySqlException | A defining query over a view with a query filter fails on the server | both | Exception + SQL |
| NonSharedModelBulkUpdates `Delete_with_owned_collection_and_non_natively_translatable_query` | MySqlException | DELETE whose subquery selects from the target table (error 1093) | both | Exception |
| NorthwindBulkUpdates `Update_Where_GroupBy_First_set_constant_3` | MySqlException on **MySQL**, success on MariaDB | Same, error 1093 | **[M] throws, [MDB] works** | Exception / SQL |
| TPHInheritanceBulkUpdates `Delete_GroupBy_Where_Select_First_3`; TPHFilters variant | MySqlException; on MariaDB ≥ 11.1 the Filters variant succeeds | Same | **[MDB ≥ 11.1] works** | Exception + SQL |
| NorthwindMiscellaneous `Complex_nested_query_doesnt_try_binding_to_grandparent…` | MySqlException with OuterApply support, InvalidOperationException without | LATERAL subquery limits | depends on version | Exception |
| AdHocQueryFilters `Group_by_multiple_aggregate_joining_different_tables(_with_query_filter)` [2] | MySqlException when OuterReferenceInMultiLevelSubquery is unsupported | No outer reference two levels deep (MySQL < 8.0.14; MariaDB) | **[M ≥ 8.0.14] works** | Exception |
| NorthwindSelect `Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier`; PrimitiveCollections `Parameter_collection_in_subquery_…_as_compiled_query` [2]; NorthwindJoin `Join_local_collection_int_closure_is_cached_correctly` | message chosen by `HasPrimitiveCollectionsSupport` (JSON_TABLE) | Primitive collections need `JSON_TABLE` (MySQL ≥ 8.0.4; not MariaDB < 10.6) | version | Exception/message |
| PrimitiveCollections `Inline_collection_of_nullable_value_type_(with_null_)Min/Max` [4] | succeed only when `FieldReferenceInTableValueConstructor` is supported | VALUES rows cannot reference outer columns on some versions | version | Both |
| ComplexNavigations `Join_with_result_selector_returning_queryable_throws_validation_error` (x2 classes), `Nested_SelectMany_correlated_with_join_table_correctly_translated_to_apply` (EqualException: wrong results) | Exception | Wrong result with APPLY emulation | both | Exception |
| NorthwindSetOperations `Client_eval_Union_FirstOrDefault` | `SetOperationsNotAllowedAfterClientEvaluation` | as base (asserted explicitly) | both | Exception/message |
| Include, EF.Property, string include, NoTracking `Include_collection_with_last_no_orderby` [3] | `LastUsedWithoutOrderBy` | as base | both | Exception/message |
| NorthwindMiscellaneous `Client_code_using_instance_*` [3], `Entity_equality_through_subquery_composite_key`, `Max_on_empty_sequence_throws` | message asserted with the provider's type names | — | both | Exception/message |
| TPCManyToMany(NoTracking) `Throws_when_different_filtered_*` [8] | base plus empty AssertSql | — | both | Exception + SQL |

### 3.2 Same intent, different expected data or rewritten test (non-determinism, precision)

| Spec test(s) | Change | Reason | Asserted by |
|---|---|---|---|
| BuiltInDataTypes, ConvertToProviderTypes, CustomConverters `Can_insert_and_read_back_{all_non_nullable,non_nullable_backed,nullable_backed,object_backed}_data_types` (+ `…_with_values_set_to_non_null`, `Object_to_string_conversion`) [~15] | own body with MySQL-compatible values | `datetime(6)`/`time(6)` precision, no `DateTimeOffset` offset (stored as UTC), unsigned limits | Results |
| ConvertToProviderTypes `Can_perform_query_with_ansi_strings_test`; TPTTableSplitting `Can_insert_dependent_with_just_one_parent`; SqlQuery `*multiple_composed_with_closure_parameters*` [3]; SpatialQuery `Buffer_quadrantSegments`, `CoveredBy`, `Covers`, `M`, `Z`, `Normalized`, `Reverse`, `Union_void` [8] | no-op (`Task.CompletedTask` or empty) | No ANSI strings; no Z/M and no NTS functions on the server; a fixture quirk | none (silent pass) |
| Spatial `Can_roundtrip_Z_and_M` | Z and M come back as NaN | MySQL drops Z/M | Results |
| SpatialQuery `GeometryType` (lower-cased), `Combine_aggregate` (ordered) | rewritten | Server naming and order | Results |
| SpatialQuery Distance/IsWithinDistance/IsRing [7] | baseline chosen by `SpatialDistanceSphereFunction`/`SpatialFunctionAdditions` | `ST_Distance_Sphere` on SRID 4326 (MySQL 8) | SQL |
| GearsOfWar and TPC `DateTimeOffset_Contains_Less_than_Greater_than`, `Where_datetimeoffset_milliseconds_parameter_and_constant`, `Where_datetimeoffset_hour_component` (8 instead of 10) | expected values normalized to UTC (`GetExpectedValue`) | DateTimeOffset is stored in UTC with no offset | Both |
| AdHocAdvancedMappings `Query_generates_correct_{datetime2,datetimeoffset,timespan}_parameter_definition` | skipped when fractional seconds > 6 | Maximum precision is 6 | Both |
| NorthwindAggregateOperators `Average_over_max_subquery`, `Average_over_nested_subquery`, `Type_casting_inside_sum` | adds a decimal-precision tolerance | `decimal(65,30)` arithmetic | Both |
| Include, SplitInclude, StringInclude, EF.Property include `Include_collection_with_multiple_conditional_order_by`, `Include_duplicate_collection_result_operator(2)`, `Repro9735`, `*GroupBy_Select*`; ComplexNavigations `SelectMany_subquery_with_custom_projection`, `Max_in_multi_level_nested_subquery`, `Filtered_include_multiple_multi_level…`; NorthwindMiscellaneous `Entity_equality_orderby_subquery`; OwnedEntity [3]; FunkyData `String_Contains_and_StartsWith_with_same_parameter`; NorthwindBulkUpdates `Update_Where_Take_set_constant` [~30] | adds an explicit ORDER BY or filter | MySQL and MariaDB give no implicit order; MariaDB needs explicit filtering in some versions | Results (+ SQL) |
| NullSemantics `From_sql_composed_with_relational_null_comparison`; QueryNoClientEval `Doesnt_throw_when_from_sql_not_composed`; AdHocMiscellaneous `Multiple_different_entity_type_from_different_namespaces` | raw SQL rewritten with backticks, `cast(null as signed)` | SQL dialect | Results |
| MySqlUpdateSqlGenerator `AppendInsertOperation_*` [4] | own SQL verification | `SELECT … WHERE ROW_COUNT() = 1 AND id = LAST_INSERT_ID()` instead of OUTPUT; RETURNING on MariaDB | SQL |
| StoreValueGeneration [10], NonSharedModelUpdates, DataAnnotation `DatabaseGeneratedAttribute_autogenerates…`, MigrationsSqlGenerator and Migrations `DeleteData*` [6] | baseline chosen by `Supports.Returning` | MariaDB ≥ 10.5 has `INSERT/DELETE … RETURNING`; MySQL reads back with `LAST_INSERT_ID()` | SQL |
| PrimitiveCollections, NonSharedPrimitiveCollections index and inline-collection tests [~15] | baseline chosen by `Supports.JsonValue` (JSON_VALUE vs JSON_UNQUOTE(JSON_EXTRACT)) or `ValuesWithRows` (`VALUES ROW(…)` [M] vs `VALUES (…)` [MDB]) | Dialect differs between MySQL and MariaDB | SQL |
| NorthwindGroupBy `GroupBy_aggregate_after_skip_0_take_0`, `GroupBy_skip_0_take_0_aggregate` | baseline chosen by `MySqlBugLimit0Offset0ExistsWorkaround` | MySQL bug with `LIMIT 0 OFFSET 0` inside EXISTS | SQL |
| Migrations `Rename_index` (RENAME INDEX vs drop/create), `Add_column_computed_with_collation`, `Add_column_with_computedSql`, `Create_table_with_computed_column` (nullable generated-column syntax) | baseline chosen by a flag | Version DDL | SQL |

### 3.3 Skipped (unconditional `Skip=`)

| Spec test(s) | Skip reason (summarized) | Class of gap |
|---|---|---|
| GearsOfWar, TPT and TPC `Take_without_orderby_followed_by_orderBy_is_pushed_down1/2(/3)` [7] | LIMIT cannot take a parameter outside a prepared statement | **LIMIT parameter** |
| NorthwindSetOperations `Union_Take_Union_Take` | No ORDER BY or LIMIT directly inside a UNION branch | set-operation syntax |
| GearsOfWar, TPT and TPC `Correlated_collection_with_groupby_with_complex_grouping_key…` [3] | MySQL LATERAL JOIN bug | server bug |
| NorthwindSelect `Take_on_top_level_and_on_collection_projection_with_outer_apply`; `SelectMany_correlated_with_outer_2/4` [3] | MySQL bug / non-deterministic on 8.0.17 | server bug |
| NorthwindSelect `Projection_containing_DateTime_subtraction` | TIME range too small for TimeSpan | **TimeSpan range** |
| GearsOfWar, TPT and TPC `DateTimeOffsetNow_minus_timespan` [3] | test-definition issue | — |
| GearsOfWar, TPT and TPC `Client_member_and_unsupported_string_Equals_in_the_same_query` [3] | provider issue #996 | — |
| NorthwindWhere `Where_as_queryable_expression`, NorthwindSelect `Project_single_element_from_collection_with_*Take_and_FirstOrDefault*` [6], Navigations `Where_subquery_on_navigation(2)` [9] | issue #573 (outer reference in a nested subquery) | **outer-reference depth** |
| NorthwindWhere `Where_multiple_contains_in_subquery_with_and/or` [2] | issue #552 | — |
| NorthwindSplitInclude(NoTracking) `Include_collection_{skip,take,skip_take}_no_order_by`, `Include_duplicate_collection_result_operator` [7] | EF #21202 (split queries without ORDER BY are non-deterministic) | ordering |
| ComplexNavigationsSharedType `GroupBy_aggregate_where_required_relationship(_2)` [2] (EF #26104), `Nested_SelectMany…apply` | — | — |
| NorthwindJoin `SelectMany_with_client_eval_with_constructor` | Needs an explicit view order on MySQL and MariaDB | ordering |
| Ef6GroupBy `Whats_new_2021_sample_3/5/6` [3] | Throws locally but not in CI | non-deterministic |
| EntitySplittingQuery: owned split references and collections (normal, split, TPH, TPT, TPC) [20] | EF Issue29075 | EF bug |
| OptimisticConcurrency `Simple_concurrency_exception_can_be_resolved_with_store_values`, `Updating_then_deleting…` [2] | #588 | affected rows |
| Spatial `Translators_handle_static_members` | `Point.Empty` is unsupported | spatial |
| MigrationsInfrastructure `Can_diff_against_2_2_model`, the ASP.NET Identity diffs [4] | not implemented | — |
| Migrations `Add_primary_key_string/_with_name`, `Drop_column_primary_key`, `Drop_primary_key_int/_string`, `Rename_table_with_primary_key` [6] | The PK helper procedures need `MySqlMigrator` | DDL |
| Migrations `Add_unique_constraint(_composite_with_name)`, `Create_table_all_settings` [3] | Unique constraints are not scaffolded | scaffolding |
| Migrations `Create_index_with_filter`, `Create_unique_index_with_filter` [2] | **No filtered indexes** | DDL |
| Migrations `Move_sequence` | No schemas to move a sequence between | **no schemas** |
| MySqlDatabaseModelFactory [27] | Issue #582 (ported SQL Server tests never adapted) | scaffolding |

### 3.4 Capability gaps, consolidated (for a homogenized API)

| Gap | MySQL | MariaDB |
|---|---|---|
| Row-value or tuple comparison translation | no | no |
| `ToJson()` owned JSON columns (query, update, migrations, compiled model) | no (provider JSON types instead) | no |
| Stored-procedure result columns and return values | no (OUT only) | no |
| Composing over `CALL` | no | no |
| Filtered or partial indexes | no | no |
| Schemas | no (Ignore/Translate) | no |
| Sequences | no | yes (≥ 10.3) |
| RETURNING | no | yes (≥ 10.5) |
| LIMIT with a parameter | no (constant only) | no |
| Outer reference in a multi-level subquery | ≥ 8.0.14 | no |
| LATERAL / APPLY | ≥ 8.0.14 | no |
| INTERSECT / EXCEPT | ≥ 8.0.31 | ≥ 10.3 (precedence ≥ 10.5) |
| JSON_TABLE (primitive collections) | ≥ 8.0.4 (bugs until later) | ≥ 10.6 |
| `VALUES ROW()` | yes | no (`VALUES (…)`) |
| DML whose subquery targets the same table (error 1093) | fails | works in some cases (≥ 11.1) |
| TIME/TimeSpan range ±838h; DateTime precision ≤ 6; DateTimeOffset offset not kept | yes | yes |
| `CHANGE`/`RENAME COLUMN`, `RENAME INDEX`, default expressions on text, descending indexes | version-gated (8.0) | version-gated (10.x) |

---

## 4. Conditions and flags

`SupportedServerVersionCondition(params string[])` is met when `AppConfig.ServerVersion.Supports.<Property>` is true, or the server version is at least the given version string (`"8.0.18-mysql"`, `"0.0.0-mariadb"`); several arguments mean OR, several attributes mean AND. `Skip=` is only the message used when the condition is not met. `SupportedServerVersionLessThanCondition` is the inverse (it runs only on servers **without** the feature, to assert the failure path). `SupportedServerVersionBetweenCondition(min, max, Invert)` gives a range. Class-level gates work because of the custom `MySqlXunitTestFramework` discoverer. Runtime branches (`if (AppConfig.ServerVersion.Supports.X)`) choose between baselines.

| Gate | Tests gated (count) | What it gates |
|---|---|---|
| `Json` | 12 JSON classes (class level), BuiltInDataTypes (class), IntegrationTests GeneratedTypes | JSON column type at all |
| `JsonOverlaps` | 8 (DOM suites) | `JSON_OVERLAPS` [M ≥ 8.0.17] |
| `JsonTable`, `JsonTableImplementationStable`, `…WithoutMySqlBugs`, `…UsingParameterAsSourceWithoutEngineCrash`, `JsonValue` | 3 + 2 + 5 + 2 + 1 (PrimitiveCollections, NorthwindSelect) | Primitive collections through JSON_TABLE; MySQL crash and bug versions |
| `OuterReferenceInMultiLevelSubquery` | 45 (GearsOfWar, TPT and TPC `*subquery_distinct*`, Navigations, GroupBy, Select, ComplexNavigations) | Correlation depth |
| `"8.0.18-mysql"` | 26 (GearsOfWar and TPT `Where_subquery_*_boolean`, Concat and Union with navigations) | Outer column in a WHERE subquery (MySQL only; MariaDB never) |
| `"8.0.22-mysql"` (± MariaDB) | 5 | Null expansion in subquery Contains; `GroupBy_group_Where_Select_Distinct_aggregate` |
| `"8.0.0-mysql"` (± MariaDB) | 2 (class NorthwindSplitIncludeQuery; `Member_pushdown_with_multiple_collections`) | Implicit ordering of split queries (whole class off on MariaDB) |
| `"0.0.0-mysql"` | 1 (`Update_Where_Skip_Take_set_constant`) | Effectively "MySQL only" |
| `"10.4.0-mariadb"` | 1 | Scaffolding a view with a comment |
| `OuterApply` / `CrossApply` | 4 / 0 (+ LessThan 2) | LATERAL |
| `WindowFunctions` | LessThan 1 | ROW_NUMBER |
| `ExceptIntercept`, `ExceptInterceptPrecedence` | 9 + 1 (SetOperations) | INTERSECT/EXCEPT |
| `CommonTableExpressions` | 2 (FromSql/SqlQuery composed with a CTE) | WITH |
| `LimitWithinInAllAnySomeSubquery` | 4 (PrimitiveCollections Take/Skip, TPH Delete subquery) | LIMIT inside IN |
| `LimitWithNonConstantValue` | 3 (`Where_subquery_with_ElementAt_using_column_as_index`) | LIMIT from a column |
| `WhereSubqueryReferencesOuterQuery` | 6 | Inline-collection index of a column; owned Distinct/Union |
| `DefaultExpression` OR `AlternativeDefaultExpression` | 9 (+ LessThan 2) | Text/blob defaults, defaults as expressions |
| `GeneratedColumns` | 7 (incl. class TableSplitting) | Computed columns |
| `DescendingIndexes` | 3 | DESC index keys |
| `RenameIndex`, `RenameColumn` | 1 + 2 (class MigrationsInfrastructure) | Native rename |
| `Sequences` | 16 | [MDB] sequences |
| `Returning` | 1 attribute + ~20 runtime branches | RETURNING |
| `SpatialIndexes`, `SpatialFunctionAdditions`, `SpatialSupportFunctionAdditions`, `SpatialSetSridFunction` (+ LessThan), runtime `SpatialDistanceSphereFunction` | 2 + 5 + 1 + 2 + 7 runtime | MySQL 8 spatial functions, axis order |
| `FullTextParser` | 1 | ngram parser |
| `IdentifyJsonColumsByCheckConstraints` | 1 | [MDB] JSON alias detection |
| `AlternativeDefaultExpression` alone | 2 | [MDB] scaffolding defaults |
| Between `11.4.2-mariadb`–`11.5.0-mariadb` (Invert) | 5 (Where concat comparisons) | MariaDB 11.4 collation bug |
| LessThan `11.4.0-mariadb` | 1 (incompatible collations fail) | default collation change in MariaDB 11.4 |
| Runtime: `ValuesWithRows`, `JsonValue`, `FieldReferenceInTableValueConstructor`, `MySqlBugLimit0Offset0ExistsWorkaround`, `MySqlBug96947Workaround` (CAST wrapping in baselines), `ServerType.MariaDb` and version checks (BulkUpdates), `HasPrimitiveCollectionsSupport` | ~45 | SQL-baseline variants |
| ConnectionSettings `Insert_and_read_Guid_value` | data-row version `"8.0.0-mysql"` | `UUID_TO_BIN` |
| `SkippableTheory` with `Skip.If(fractionalSeconds > 6)` | 3 | precision |

---

## 5. Test infrastructure

| Piece | What it does | MySQL-specific? | Needs change for EF Core 10 spec package |
|---|---|---|---|
| `Shared/AppConfig` | Connection string from `config.json`/`appsettings.json` (+ `EF_PORT`, `EF_DATABASE`, `EF_BATCH_SIZE`, `EF_RETRY_ON_FAILURE`); **ServerVersion** from `SERVER_VERSION` env or `Data:ServerVersion`, else `ServerVersion.AutoDetect` | yes: the server-version oracle that every gate reads | none, but the homogenized API needs an equivalent "dialect + version capability set" |
| `SupportedServerVersion{,LessThan,Between}ConditionAttribute` | `ITestCondition` on `ServerVersion.Supports.PropertyOrVersion(string)` | yes | `ITestCondition` still exists in EF 10 (`Microsoft.EntityFrameworkCore.TestUtilities.Xunit`); check whether the spec package moved to xUnit v3 (`IsMetAsync` signature) |
| `TestUtilities/Xunit/MySqlXunitTestFramework` (+ discoverer, runners, orderers) | Class-level skip by condition, optional fixed test order | partly (generic mechanism) | Depends on xUnit v2 internals (`XunitTestFrameworkDiscoverer`, `XunitTestCaseRunner`). **Must be rewritten if EF 10 spec tests use xUnit v3** |
| `MySqlTestStore : RelationalTestStore` | Backtick delimiters; CREATE/ALTER DATABASE with `utf8mb4` + collation (factories default to `DefaultUtf8CiCollation`); `NO_BACKSLASH_ESCAPES` appended to sql_mode on open; GuidFormat in the connection string; script execution (`Northwind.sql`, split on `/* GO */` comments); command timeout 600; `TestMySqlRetryingExecutionStrategy` | yes | `InitializeAsync`/`CleanAsync` signatures changed in EF 9 and are async-only in 10; check for overrides that no longer exist |
| `MySqlTestStore.AddOptions` | `TranslateParameterizedCollectionsToParameters()` (Pomelo's default is constants), `SingleQuery`, retrying strategy | yes | EF 10 changed the parameterized-collection default to multiple parameters (`ParameterTranslationMode`). Baselines with `JSON_TABLE(@p)` vs `IN (@p1, @p2)` will change across the board |
| `MySqlTestStoreFactory` (`Instance`=`InstanceCi`, `InstanceCs`, `NoBackslashEscapesInstance`, `GuidBinary16Instance`), `MySqlNorthwindTestStoreFactory`, `MySqlConnectionStringTestStoreFactory` | Store variants | yes | — |
| `MySqlDatabaseCleaner : RelationalDatabaseCleaner` | Drops routines (procedures and functions) and views through custom SQL; ignores indexes and views in the model | yes | API stable |
| `MySqlTestHelpers` | `GetExpectedValue(DateTimeOffset)` (UTC normalization), `CastAsDouble`, `MySqlBug96947Workaround`, `HasPrimitiveCollectionsSupport`, its own `AssertAllMethodsOverridden` | yes | — |
| `DebugServices` (DebugRelationalCommand / DataReader) | diagnostics | no | — |
| Fixtures: `NorthwindQueryMySqlFixture`, `GearsOfWarQueryMySqlFixture`, `F1MySqlFixture`, `SpatialMySqlFixture`, `UpdatesMySqlFixture`, `ServiceProviderPerContextFixtureBase`, Json fixtures | Model customizations (charset, Guid format, JSON) | yes | — |
| Compliance: `MySqlComplianceTest.IgnoredTestBases` (27 bases: UDF, interception, tagging, logging, precompiled queries, all `Json*` relational bases, OptionalDependent, AdHocComplexType) | — | — | Lists bases by type; EF 10 adds new bases (Query/Translations/*, `Associations/*`, `ComplexJson*`, `BasicTypes`) that will fail compliance until mapped or ignored |

### EF Core 9 to 10 porting notes (from the spec assemblies)

- Every base class still exists in 10.0.11 **except** `ComplexTypeBulkUpdatesRelationalTestBase` (15 overrides).
- **302 overrides target spec methods that no longer exist** in EF 10: NorthwindFunctionsQuery 145 (string, math, DateTime, DateOnly, TimeOnly, bitwise and enum tests moved to the new `Query/Translations/*TranslationsTestBase` suites on the BasicTypes model), TPCGearsOfWar 82 (same move of DateTimeOffset, TimeSpan and byte-array tests), NorthwindWhere 23, ComplexTypeBulkUpdates 15, NorthwindMiscellaneous 13, NonSharedPrimitiveCollections 8 (`*_with_default_constants/parameters` renamed for the new parameter-translation modes), GearsOfWar 5, TPTGearsOfWar 4, NorthwindBulkUpdates 3 (`*left_join*`), ComplexTypeQuery 3, SqlQuery 1. These must be re-homed onto the Translations suites, where the MySQL-specific expectations (TRIM, LOCATE, LOG base, CONVERT_TZ, StringComparison) are now asserted.
- Base test-method counts drop from about 11,314 to 11,082 for the same bases, and the moved tests reappear under new bases.
- Parameter-name format in baselines changes (`@__p_0` becomes `@p`), and collection parameters change (see AddOptions above). Essentially every SQL baseline needs regenerating.
- `MySqlStrings.Ef7CoreJsonMappingNotSupported` expectations: EF 10 makes complex types mapped to JSON (and JSON owned types) a core relational feature. A homogenized API must either provide JSON columns on MySQL/MariaDB or keep these tests as declared gaps.
