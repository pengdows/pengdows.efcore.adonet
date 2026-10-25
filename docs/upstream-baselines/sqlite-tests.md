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
