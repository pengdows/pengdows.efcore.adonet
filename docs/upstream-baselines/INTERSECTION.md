# Tests every provider runs

The EF Core specification test bases that all five upstream providers derive from (Microsoft SQL Server and SQLite 10.0.11, Npgsql 10.0.3, Pomelo 9.0.1, skuirrels DuckDB): the behaviour common to every database, and the first porting target for all six of this project's databases.

Computed from the providers' test source: each provider test class is followed through its local base classes to the EF Core 10.0.11 specification class it derives from. Test methods are `[Fact]`/`[Theory]` (and `Conditional` forms) declared in the base and its specification ancestors; theories count once. Pomelo targets EF Core 9, so specification suites new in EF Core 10 (e.g. `Translations`) fall outside the strict intersection.

| Area | Specification base | Test methods |
|---|---|---:|
| (top level) | `ApiConsistencyTestBase` | 16 |
| (top level) | `BuiltInDataTypesTestBase` | 28 |
| (top level) | `CommandInterceptionTestBase` | 1 |
| (top level) | `CompositeKeyEndToEndTestBase` | 3 |
| (top level) | `ConcurrencyDetectorDisabledRelationalTestBase` | 9 |
| (top level) | `ConcurrencyDetectorEnabledRelationalTestBase` | 9 |
| (top level) | `ConnectionInterceptionTestBase` | 9 |
| (top level) | `ConvertToProviderTypesTestBase` | 30 |
| (top level) | `CustomConvertersTestBase` | 29 |
| (top level) | `DataAnnotationRelationalTestBase` | 1 |
| (top level) | `DataBindingTestBase` | 38 |
| (top level) | `DesignTimeTestBase` | 2 |
| (top level) | `EntitySplittingTestBase` | 2 |
| (top level) | `FieldsOnlyLoadTestBase` | 120 |
| (top level) | `FindTestBase` | 69 |
| (top level) | `KeysWithConvertersTestBase` | 47 |
| (top level) | `LoadTestBase` | 106 |
| (top level) | `LoggingRelationalTestBase` | 11 |
| (top level) | `ManyToManyFieldsLoadTestBase` | 23 |
| (top level) | `ManyToManyLoadTestBase` | 22 |
| (top level) | `ManyToManyTrackingRelationalTestBase` | 21 |
| (top level) | `MaterializationInterceptionTestBase` | 7 |
| (top level) | `MonsterFixupTestBase` | 12 |
| (top level) | `MusicStoreTestBase` | 4 |
| (top level) | `NotificationEntitiesTestBase` | 2 |
| (top level) | `OptimisticConcurrencyRelationalTestBase` | 34 |
| (top level) | `OverzealousInitializationTestBase` | 1 |
| (top level) | `QueryExpressionInterceptionTestBase` | 4 |
| (top level) | `RelationalComplianceTestBase` | 2 |
| (top level) | `RelationalServiceCollectionExtensionsTestBase` | 3 |
| (top level) | `SaveChangesInterceptionTestBase` | 1 |
| (top level) | `SerializationTestBase` | 1 |
| (top level) | `SpatialTestBase` | 5 |
| (top level) | `StoreGeneratedFixupRelationalTestBase` | 118 |
| (top level) | `StoreGeneratedTestBase` | 15 |
| (top level) | `TPTTableSplittingTestBase` | 26 |
| (top level) | `TableSplittingTestBase` | 26 |
| (top level) | `TransactionInterceptionTestBase` | 5 |
| (top level) | `TransactionTestBase` | 44 |
| (top level) | `ValueConvertersEndToEndTestBase` | 25 |
| (top level) | `WithConstructorsTestBase` | 41 |
| BulkUpdates | `NonSharedModelBulkUpdatesRelationalTestBase` | 10 |
| BulkUpdates | `NorthwindBulkUpdatesRelationalTestBase` | 52 |
| BulkUpdates | `TPCFiltersInheritanceBulkUpdatesTestBase` | 18 |
| BulkUpdates | `TPCInheritanceBulkUpdatesTestBase` | 20 |
| BulkUpdates | `TPHInheritanceBulkUpdatesTestBase` | 20 |
| BulkUpdates | `TPTFiltersInheritanceBulkUpdatesTestBase` | 18 |
| BulkUpdates | `TPTInheritanceBulkUpdatesTestBase` | 22 |
| Migrations | `MigrationsInfrastructureTestBase` | 25 |
| Migrations | `MigrationsTestBase` | 25 |
| ModelBuilding | `RelationalComplexTypeTestBase` | 41 |
| ModelBuilding | `RelationalInheritanceTestBase` | 31 |
| ModelBuilding | `RelationalManyToManyTestBase` | 21 |
| ModelBuilding | `RelationalManyToOneTestBase` | 31 |
| ModelBuilding | `RelationalNonRelationshipTestBase` | 43 |
| ModelBuilding | `RelationalOneToManyTestBase` | 90 |
| ModelBuilding | `RelationalOneToOneTestBase` | 142 |
| ModelBuilding | `RelationalOwnedTypesTestBase` | 45 |
| Query | `AdHocAdvancedMappingsQueryRelationalTestBase` | 4 |
| Query | `AdHocManyToManyQueryRelationalTestBase` | 1 |
| Query | `AdHocMiscellaneousQueryRelationalTestBase` | 2 |
| Query | `AdHocNavigationsQueryRelationalTestBase` | 2 |
| Query | `AdHocQueryFiltersQueryRelationalTestBase` | 9 |
| Query | `AdHocQuerySplittingQueryTestBase` | 5 |
| Query | `ComplexNavigationsCollectionsQueryRelationalTestBase` | 14 |
| Query | `ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase` | 14 |
| Query | `ComplexNavigationsCollectionsSplitQueryRelationalTestBase` | 14 |
| Query | `ComplexNavigationsCollectionsSplitSharedTypeQueryRelationalTestBase` | 14 |
| Query | `ComplexNavigationsQueryRelationalTestBase` | 76 |
| Query | `ComplexNavigationsSharedTypeQueryRelationalTestBase` | 76 |
| Query | `ComplexTypeQueryRelationalTestBase` | 76 |
| Query | `CompositeKeysQueryRelationalTestBase` | 7 |
| Query | `CompositeKeysSplitQueryRelationalTestBase` | 7 |
| Query | `Ef6GroupByTestBase` | 55 |
| Query | `EntitySplittingQueryTestBase` | 58 |
| Query | `FromSqlQueryTestBase` | 75 |
| Query | `FunkyDataQueryTestBase` | 19 |
| Query | `GearsOfWarFromSqlQueryTestBase` | 1 |
| Query | `GearsOfWarQueryRelationalTestBase` | 331 |
| Query | `IncludeOneToOneTestBase` | 12 |
| Query | `ManyToManyNoTrackingQueryRelationalTestBase` | 124 |
| Query | `ManyToManyQueryRelationalTestBase` | 123 |
| Query | `MappingQueryTestBase` | 4 |
| Query | `NonSharedPrimitiveCollectionsQueryRelationalTestBase` | 35 |
| Query | `NorthwindAggregateOperatorsQueryRelationalTestBase` | 1 |
| Query | `NorthwindAsNoTrackingQueryTestBase` | 11 |
| Query | `NorthwindAsTrackingQueryTestBase` | 5 |
| Query | `NorthwindChangeTrackingQueryTestBase` | 17 |
| Query | `NorthwindCompiledQueryTestBase` | 32 |
| Query | `NorthwindDbFunctionsQueryRelationalTestBase` | 15 |
| Query | `NorthwindEFPropertyIncludeQueryTestBase` | 119 |
| Query | `NorthwindFunctionsQueryRelationalTestBase` | 10 |
| Query | `NorthwindGroupByQueryRelationalTestBase` | 29 |
| Query | `NorthwindIncludeNoTrackingQueryTestBase` | 118 |
| Query | `NorthwindIncludeQueryRelationalTestBase` | 118 |
| Query | `NorthwindJoinQueryRelationalTestBase` | 58 |
| Query | `NorthwindKeylessEntitiesQueryRelationalTestBase` | 20 |
| Query | `NorthwindMiscellaneousQueryRelationalTestBase` | 7 |
| Query | `NorthwindNavigationsQueryRelationalTestBase` | 73 |
| Query | `NorthwindQueryFiltersQueryTestBase` | 17 |
| Query | `NorthwindQueryTaggingQueryTestBase` | 9 |
| Query | `NorthwindSelectQueryRelationalTestBase` | 132 |
| Query | `NorthwindSetOperationsQueryRelationalTestBase` | 25 |
| Query | `NorthwindSplitIncludeNoTrackingQueryTestBase` | 118 |
| Query | `NorthwindSplitIncludeQueryTestBase` | 118 |
| Query | `NorthwindSqlQueryTestBase` | 4 |
| Query | `NorthwindStringIncludeQueryTestBase` | 119 |
| Query | `NorthwindWhereQueryRelationalTestBase` | 23 |
| Query | `NullKeysTestBase` | 5 |
| Query | `NullSemanticsQueryTestBase` | 168 |
| Query | `OperatorsQueryTestBase` | 10 |
| Query | `OptionalDependentQueryTestBase` | 10 |
| Query | `OwnedEntityQueryRelationalTestBase` | 2 |
| Query | `OwnedQueryRelationalTestBase` | 104 |
| Query | `PrimitiveCollectionsQueryRelationalTestBase` | 81 |
| Query | `QueryFilterFuncletizationTestBase` | 28 |
| Query | `QueryNoClientEvalTestBase` | 14 |
| Query | `SharedTypeQueryRelationalTestBase` | 4 |
| Query | `SpatialQueryRelationalTestBase` | 84 |
| Query | `SqlQueryTestBase` | 61 |
| Query | `TPCFiltersInheritanceQueryTestBase` | 11 |
| Query | `TPCGearsOfWarQueryRelationalTestBase` | 331 |
| Query | `TPCInheritanceQueryTestBase` | 53 |
| Query | `TPCManyToManyNoTrackingQueryRelationalTestBase` | 124 |
| Query | `TPCManyToManyQueryRelationalTestBase` | 123 |
| Query | `TPCRelationshipsQueryTestBase` | 70 |
| Query | `TPHInheritanceQueryTestBase` | 55 |
| Query | `TPTFiltersInheritanceQueryTestBase` | 11 |
| Query | `TPTGearsOfWarQueryRelationalTestBase` | 331 |
| Query | `TPTInheritanceQueryTestBase` | 53 |
| Query | `TPTManyToManyNoTrackingQueryRelationalTestBase` | 124 |
| Query | `TPTManyToManyQueryRelationalTestBase` | 123 |
| Query | `TPTRelationshipsQueryTestBase` | 70 |
| Query | `ToSqlQueryTestBase` | 1 |
| Query | `WarningsTestBase` | 11 |
| Update | `NonSharedModelUpdatesTestBase` | 1 |
| Update | `StoreValueGenerationTestBase` | 19 |
| Update | `UpdatesRelationalTestBase` | 29 |
| **Total** | **138 bases** | **6093** |
