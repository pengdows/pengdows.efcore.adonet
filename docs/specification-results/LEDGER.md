| Specification base | SqlServer | PostgreSql | MySql | Sqlite | DuckDb | FlatFile |
|---|---:|---:|---:|---:|---:|---:|
| AdHocAdvancedMappingsQuery | 44/46 | 19/46 | 44/46 | 46/46 | 46/46 | 46/46 |
| AdHocManyToManyQuery | 2/3 | 2/3 | 3/3 | 3/3 | 3/3 | 3/3 |
| AdHocMiscellaneousQuery | 66/72 (1 skipped) | 34/72 (1 skipped) | 64/72 (1 skipped) | 69/72 (1 skipped) | 70/72 (1 skipped) | 60/72 (1 skipped) |
| AdHocNavigationsQuery | 22/25 | 12/25 | 23/25 | 22/25 | 24/25 | 25/25 |
| AdHocQueryFiltersQuery | 25/25 | 13/25 | 25/25 | 24/25 | 24/25 | 25/25 |
| AdHocQuerySplittingQuery | 7/13 | 4/13 | 7/13 | 12/13 | 11/13 | 12/13 |
| ApiConsistency | 14/19 | 14/19 | 14/19 | 14/19 | 14/19 | 14/19 |
| BuiltInDataTypes | 11/30 | 20/30 | 0/30 | 11/30 | 11/30 | 0/30 |
| CommandInterception | 122/122 | 122/122 | 122/122 | 122/122 | 122/122 | 122/122 |
| ComplexNavigationsCollectionsQuery | 0/312 | 304/312 | 312/312 | 0/312 | 0/312 | 0/312 |
| ComplexNavigationsCollectionsSharedTypeQuery | 0/312 | 306/312 | 312/312 | 0/312 | 0/312 | 0/312 |
| ComplexNavigationsCollectionsSplitQuery | 0/312 | 34/312 | 34/312 | 0/312 | 0/312 | 0/312 |
| ComplexNavigationsCollectionsSplitSharedTypeQuery | 0/312 | 66/312 | 66/312 | 0/312 | 0/312 | 0/312 |
| ComplexNavigationsQuery | 0/616 | 558/616 | 574/616 | 0/616 | 0/616 | 0/616 |
| ComplexNavigationsSharedTypeQuery | 0/616 | 592/616 | 604/616 | 0/616 | 0/616 | 0/616 |
| ComplexTypeQuery | 146/147 (1 skipped) | 146/147 (1 skipped) | 146/147 (1 skipped) | 144/147 (1 skipped) | 146/147 (1 skipped) | 146/147 (1 skipped) |
| CompositeKeyEndToEnd | 2/3 | 2/3 | 2/3 | 2/3 | 2/3 | 2/3 |
| CompositeKeysQuery | 0/14 | 14/14 | 14/14 | 14/14 | 0/14 | 0/14 |
| CompositeKeysSplitQuery | 0/14 | 0/14 | 0/14 | 14/14 | 0/14 | 0/14 |
| ConcurrencyDetectorDisabled | 0/18 | 0/18 | 0/18 | 0/18 | 0/18 | 0/18 |
| ConcurrencyDetectorEnabled | 18/18 | 18/18 | 18/18 | 18/18 | 18/18 | 18/18 |
| ConnectionInterception | 18/18 | 8/18 | 18/18 | 18/18 | 18/18 | 18/18 |
| ConvertToProviderTypes | 31/32 | 13/32 | 0/32 | 13/32 | 13/32 | 0/32 |
| CustomConverters | 13/60 | 13/60 | 0/60 | 28/60 | 24/60 | 0/60 |
| DataAnnotation | 89/97 | 95/97 | 89/97 | 93/97 | 93/97 | 0/97 |
| DataBinding | 20/58 | 20/58 | 20/58 | 20/58 | 20/58 | 20/58 |
| DesignTime | 0/2 | 0/2 | 0/2 | 0/2 | 0/2 | 0/2 |
| Ef6GroupBy | 108/110 | 110/110 | 110/110 | 110/110 | 110/110 | 102/110 |
| EntitySplitting | 2/3 | 1/3 | 2/3 | 3/3 | 2/3 | 2/3 |
| EntitySplittingQuery | 0/116 | 0/116 | 0/116 | 0/116 | 0/116 | 0/116 |
| FieldsOnlyLoad | 713/713 | 713/713 | 713/713 | 713/713 | 713/713 | 713/713 |
| Find | 0/137 | 0/137 | 0/137 | 0/137 | 0/137 | 0/137 |
| FromSqlQuery | 144/150 | 140/150 | 144/150 | 140/150 | 116/150 | 110/150 |
| FunkyDataQuery | 14/38 | 14/38 | 14/38 | 14/38 | 14/38 | 14/38 |
| GearsOfWarFromSqlQuery | 0/1 | 0/1 | 1/1 | 0/1 | 0/1 | 1/1 |
| GearsOfWarQuery | 0/1183 (4 skipped) | 0/1183 (4 skipped) | 1071/1183 (4 skipped) | 0/1183 (4 skipped) | 0/1183 (4 skipped) | 919/1183 (4 skipped) |
| IncludeOneToOne | 12/12 | 12/12 | 12/12 | 12/12 | 12/12 | 12/12 |
| KeysWithConverters | 0/47 | 0/47 | 0/47 | 0/47 | 0/47 | 0/47 |
| Load | 3137/3137 | 3137/3137 | 3137/3137 | 3137/3137 | 3137/3137 | 3137/3137 |
| ManyToManyFieldsLoad | 0/124 | 124/124 | 124/124 | 124/124 | 124/124 | 124/124 |
| ManyToManyLoad | 0/358 | 358/358 | 358/358 | 358/358 | 358/358 | 352/358 |
| ManyToManyNoTrackingQuery | 0/244 (4 skipped) | 190/244 (4 skipped) | 194/244 (4 skipped) | 228/244 (4 skipped) | 228/244 (4 skipped) | 160/244 (4 skipped) |
| ManyToManyQuery | 0/242 (4 skipped) | 190/242 (4 skipped) | 194/242 (4 skipped) | 228/242 (4 skipped) | 228/242 (4 skipped) | 160/242 (4 skipped) |
| ManyToManyTracking | 0/201 | 191/201 | 191/201 | 191/201 | 122/201 | 191/201 |
| MappingQuery | 0/4 | 0/4 | 0/4 | 0/4 | 0/4 | 0/4 |
| Migrations | 0/134 (2 skipped) | 0/134 (2 skipped) | 0/134 (2 skipped) | 0/134 (2 skipped) | 0/134 (2 skipped) | 0/134 (2 skipped) |
| MigrationsInfrastructure | 1/25 | 1/25 | 1/25 | 1/25 | 0/25 | 1/25 |
| MonsterFixup | 0/12 | 0/12 | 0/12 | 0/12 | 0/12 | 0/12 |
| MusicStore | 18/18 | 8/18 | 18/18 | 18/18 | 18/18 | 17/18 |
| NonSharedModelBulkUpdates | 28/34 | 14/34 | 28/34 | 30/34 | 28/34 | 10/34 |
| NonSharedModelUpdates | 4/4 | 2/4 | 4/4 | 4/4 | 4/4 | 2/4 |
| NonSharedPrimitiveCollectionsQuery | 16/56 (1 skipped) | 12/56 (1 skipped) | 16/56 (1 skipped) | 26/56 (1 skipped) | 26/56 (1 skipped) | 26/56 (1 skipped) |
| NorthwindAggregateOperatorsQuery | 374/422 | 410/422 | 402/422 | 410/422 | 406/422 | 344/421 (1 skipped) |
| NorthwindAsNoTrackingQuery | 24/24 | 24/24 | 24/24 | 24/24 | 24/24 | 24/24 |
| NorthwindAsTrackingQuery | 6/6 | 6/6 | 6/6 | 6/6 | 6/6 | 6/6 |
| NorthwindBulkUpdates | 156/185 (3 skipped) | 162/185 (3 skipped) | 170/185 (3 skipped) | 160/185 (3 skipped) | 147/185 (3 skipped) | 48/185 (3 skipped) |
| NorthwindChangeTrackingQuery | 17/17 | 17/17 | 17/17 | 17/17 | 17/17 | 17/17 |
| NorthwindCompiledQuery | 32/32 | 32/32 | 32/32 | 32/32 | 32/32 | 32/32 |
| NorthwindDbFunctionsQuery | 14/30 | 12/30 | 14/30 | 14/30 | 12/30 | 14/30 |
| NorthwindEFPropertyIncludeQuery | 236/238 | 236/238 | 232/238 | 228/238 | 236/238 | 216/238 |
| NorthwindFunctionsQuery | 16/20 | 16/20 | 16/20 | 12/20 | 16/20 | 10/20 |
| NorthwindGroupByQuery | 488/512 (4 skipped) | 508/512 (4 skipped) | 508/512 (4 skipped) | 486/512 (4 skipped) | 506/512 (4 skipped) | 426/512 (4 skipped) |
| NorthwindIncludeNoTrackingQuery | 234/236 | 234/236 | 230/236 | 226/236 | 234/236 | 214/236 |
| NorthwindIncludeQuery | 236/236 | 236/236 | 232/236 | 228/236 | 236/236 | 216/236 |
| NorthwindJoinQuery | 124/132 (4 skipped) | 128/132 (4 skipped) | 126/132 (4 skipped) | 114/132 (4 skipped) | 126/132 (4 skipped) | 114/132 (4 skipped) |
| NorthwindKeylessEntitiesQuery | 30/40 | 34/40 | 34/40 | 34/40 | 34/40 | 34/40 |
| NorthwindMiscellaneousQuery | 849/938 (1 skipped) | 911/938 (1 skipped) | 907/938 (1 skipped) | 902/938 (1 skipped) | 908/938 (1 skipped) | 892/938 (1 skipped) |
| NorthwindNavigationsQuery | 146/146 | 146/146 | 146/146 | 146/146 | 146/146 | 136/146 |
| NorthwindQueryFiltersQuery | 33/33 | 33/33 | 33/33 | 33/33 | 33/33 | 33/33 |
| NorthwindQueryTaggingQuery | 8/9 | 9/9 | 9/9 | 9/9 | 9/9 | 9/9 |
| NorthwindSelectQuery | 372/372 | 372/372 | 372/372 | 372/372 | 370/371 (1 skipped) | 358/365 (7 skipped) |
| NorthwindSetOperationsQuery | 180/182 | 178/182 | 180/182 | 172/182 | 178/182 | 170/182 |
| NorthwindSplitIncludeNoTrackingQuery | 108/236 | 108/236 | 108/236 | 228/236 | 236/236 | 216/236 |
| NorthwindSplitIncludeQuery | 80/236 | 80/236 | 80/236 | 228/236 | 236/236 | 216/236 |
| NorthwindSqlQuery | 8/8 | 8/8 | 8/8 | 8/8 | 8/8 | 8/8 |
| NorthwindStringIncludeQuery | 236/238 | 236/238 | 232/238 | 230/238 | 236/238 | 216/238 |
| NorthwindWhereQuery | 408/408 | 408/408 | 408/408 | 408/408 | 408/408 | 408/408 |
| NotificationEntities | 2/2 | 2/2 | 2/2 | 2/2 | 2/2 | 2/2 |
| NullKeys | 0/5 | 5/5 | 5/5 | 5/5 | 5/5 | 5/5 |
| NullSemanticsQuery | 290/322 (1 skipped) | 290/322 (1 skipped) | 290/322 (1 skipped) | 290/322 (1 skipped) | 290/322 (1 skipped) | 268/322 (1 skipped) |
| OperatorsQuery | 12/14 | 0/14 | 12/14 | 12/14 | 10/14 | 10/14 |
| OptionalDependentQuery | 0/20 | 0/20 | 0/20 | 0/20 | 0/20 | 0/20 |
| OverzealousInitialization | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 | 1/1 |
| OwnedEntityQuery | 35/37 | 18/37 | 33/37 | 37/37 | 37/37 | 37/37 |
| OwnedQuery | 0/212 | 196/212 | 196/212 | 212/212 | 212/212 | 208/212 |
| PrimitiveCollectionsQuery | 74/156 (1 skipped) | 0/156 (1 skipped) | 74/156 (1 skipped) | 92/156 (1 skipped) | 93/156 (1 skipped) | 93/156 (1 skipped) |
| QueryExpressionInterception | 8/8 | 8/8 | 8/8 | 8/8 | 8/8 | 8/8 |
| QueryFilterFuncletization | 28/28 | 28/28 | 28/28 | 28/28 | 28/28 | 28/28 |
| QueryNoClientEval | 13/14 (1 skipped) | 13/14 (1 skipped) | 13/14 (1 skipped) | 13/14 (1 skipped) | 13/14 (1 skipped) | 13/14 (1 skipped) |
| RelationalComplexType | 54/96 (42 skipped) | 54/96 (42 skipped) | 54/96 (42 skipped) | 54/96 (42 skipped) | 54/96 (42 skipped) | 54/96 (42 skipped) |
| RelationalCompliance | 0/2 | 0/2 | 0/2 | 0/2 | 0/2 | 0/2 |
| RelationalInheritance | 31/31 | 31/31 | 31/31 | 31/31 | 31/31 | 31/31 |
| RelationalManyToMany | 36/36 | 36/36 | 36/36 | 36/36 | 36/36 | 36/36 |
| RelationalManyToOne | 59/59 | 59/59 | 59/59 | 59/59 | 59/59 | 59/59 |
| RelationalNonRelationship | 173/173 | 173/173 | 173/173 | 173/173 | 173/173 | 173/173 |
| RelationalOneToMany | 90/90 | 90/90 | 90/90 | 90/90 | 90/90 | 90/90 |
| RelationalOneToOne | 142/142 | 142/142 | 142/142 | 142/142 | 142/142 | 142/142 |
| RelationalOwnedTypes | 75/75 | 75/75 | 75/75 | 75/75 | 75/75 | 75/75 |
| RelationalServiceCollectionExtensions | 0/3 | 0/3 | 0/3 | 0/3 | 0/3 | 0/3 |
| SaveChangesInterception | 56/56 | 56/56 | 56/56 | 56/56 | 56/56 | 56/56 |
| Serialization | 0/6 | 0/6 | 0/6 | 0/6 | 0/6 | 0/6 |
| SharedTypeQuery | 6/6 | 2/6 | 6/6 | 6/6 | 6/6 | 6/6 |
| Spatial | 0/5 | 0/5 | 0/5 | 0/5 | 0/5 | 0/5 |
| SpatialQuery | 0/168 | 0/168 | 0/168 | 0/168 | 0/168 | 0/168 |
| SqlQuery | 95/119 | 95/119 | 95/119 | 89/119 | 83/119 | 69/119 |
| StoreGenerated | 0/211 | 0/211 | 0/211 | 0/211 | 0/211 | 26/211 |
| StoreGeneratedFixup | 0/118 | 34/118 | 18/118 | 34/118 | 34/118 | 34/118 |
| StoreValueGeneration | 0/38 | 0/38 | 32/38 | 38/38 | 38/38 | 0/38 |
| TPCFiltersInheritanceBulkUpdates | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) |
| TPCFiltersInheritanceQuery | 0/22 | 0/22 | 0/22 | 0/22 | 0/22 | 0/22 |
| TPCGearsOfWarQuery | 0/1183 (4 skipped) | 0/1183 (4 skipped) | 1165/1183 (4 skipped) | 0/1183 (4 skipped) | 0/1183 (4 skipped) | 993/1183 (4 skipped) |
| TPCInheritanceBulkUpdates | 0/37 (3 skipped) | 0/37 (3 skipped) | 0/37 (3 skipped) | 0/37 (3 skipped) | 0/37 (3 skipped) | 0/37 (3 skipped) |
| TPCInheritanceQuery | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) |
| TPCManyToManyNoTrackingQuery | 0/244 (4 skipped) | 184/244 (4 skipped) | 202/244 (4 skipped) | 236/244 (4 skipped) | 236/244 (4 skipped) | 168/244 (4 skipped) |
| TPCManyToManyQuery | 0/242 (4 skipped) | 182/242 (4 skipped) | 200/242 (4 skipped) | 234/242 (4 skipped) | 234/242 (4 skipped) | 166/242 (4 skipped) |
| TPCRelationshipsQuery | 0/138 | 94/138 | 94/138 | 138/138 | 138/138 | 138/138 |
| TPHInheritanceBulkUpdates | 26/37 (3 skipped) | 26/37 (3 skipped) | 24/37 (3 skipped) | 26/37 (3 skipped) | 20/37 (3 skipped) | 0/37 (3 skipped) |
| TPHInheritanceQuery | 96/100 (4 skipped) | 94/100 (4 skipped) | 96/100 (4 skipped) | 96/100 (4 skipped) | 96/100 (4 skipped) | 43/100 (4 skipped) |
| TPTFiltersInheritanceBulkUpdates | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) | 0/33 (3 skipped) |
| TPTFiltersInheritanceQuery | 0/22 | 0/22 | 0/22 | 0/22 | 0/22 | 0/22 |
| TPTGearsOfWarQuery | 0/1183 (4 skipped) | 0/1183 (4 skipped) | 1165/1183 (4 skipped) | 0/1183 (4 skipped) | 0/1183 (4 skipped) | 997/1183 (4 skipped) |
| TPTInheritanceBulkUpdates | 0/35 (5 skipped) | 0/35 (5 skipped) | 0/35 (5 skipped) | 0/35 (5 skipped) | 0/35 (5 skipped) | 0/35 (5 skipped) |
| TPTInheritanceQuery | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) | 0/98 (4 skipped) |
| TPTManyToManyNoTrackingQuery | 0/244 (4 skipped) | 198/244 (4 skipped) | 202/244 (4 skipped) | 236/244 (4 skipped) | 236/244 (4 skipped) | 168/244 (4 skipped) |
| TPTManyToManyQuery | 0/242 (4 skipped) | 196/242 (4 skipped) | 200/242 (4 skipped) | 234/242 (4 skipped) | 234/242 (4 skipped) | 166/242 (4 skipped) |
| TPTRelationshipsQuery | 0/138 | 94/138 | 94/138 | 138/138 | 138/138 | 138/138 |
| TPTTableSplitting | 23/29 (2 skipped) | 14/29 (2 skipped) | 26/29 (2 skipped) | 26/29 (2 skipped) | 22/29 (2 skipped) | 23/29 (2 skipped) |
| TableSplitting | 5/29 (2 skipped) | 3/29 (2 skipped) | 5/29 (2 skipped) | 5/29 (2 skipped) | 5/29 (2 skipped) | 4/29 (2 skipped) |
| ToSqlQuery | 2/2 | 0/2 | 2/2 | 0/2 | 0/2 | 2/2 |
| Transaction | 109/111 | 44/111 | 110/111 | 93/111 | 77/111 | 86/111 |
| TransactionInterception | 26/36 | 26/36 | 0/36 | 4/36 | 2/36 | 2/36 |
| Updates | 0/36 | 34/36 | 34/36 | 34/36 | 0/36 | 0/36 |
| ValueConvertersEndToEnd | 27/27 | 24/27 | 27/27 | 27/27 | 24/27 | 24/27 |
| Warnings | 11/11 | 11/11 | 11/11 | 11/11 | 11/11 | 11/11 |
| WithConstructors | 39/41 | 39/41 | 39/41 | 39/41 | 39/41 | 39/41 |
| **Total** | **10906/22059** (131 skipped) | **14861/22059** (131 skipped) | **18594/22059** (131 skipped) | **13911/22059** (131 skipped) | **13787/22058** (132 skipped) | **15633/22051** (139 skipped) |

### SqlServer: largest failure groups

- 4195 × `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.`
- 2929 × `Microsoft.Data.SqlClient.SqlException : Incorrect syntax near the keyword '…'.`
- 2155 × `Microsoft.Data.SqlClient.SqlException : Introducing FOREIGN KEY constraint '…' on table '…' may cause cycles or multiple cascade paths. Specify ON DELETE NO ACTION or ON UPDATE NO ACTION, or modify ot`
- 302 × `System.InvalidOperationException : There is already an open DataReader associated with this Connection which must be closed first.`
- 211 × `Microsoft.Data.SqlClient.SqlException : Identity column '…' must be of data type int, bigint, smallint, tinyint, or decimal or numeric with a scale of N, unencrypted, and constrained to be nonnullable`
- 180 × `System.InvalidOperationException : The instance of entity type '…' cannot be tracked because another instance with the key value '…' is already being tracked. When attaching existing entities, ensure `
- 173 × `System.InvalidOperationException : No suitable constructor was found for the type '…'. The following constructors had parameters that could not be bound to properties of the type: `
- 140 × `Assert.Equal() Failure: Values differ`
- 122 × `System.InvalidOperationException : Unable to find expected assembly attribute [DesignTimeProviderServices] in provider assembly '…'. This attribute is required to identify the class which acts as the `
- 110 × `Microsoft.Data.SqlClient.SqlException : No column name was specified for column N of '…'.`
- 47 × `System.InvalidOperationException : The LINQ expression 'DbSet<PrimitiveCollectionsEntity>()`
- 47 × `System.InvalidOperationException : An error was generated for warning '…': The property '…' is a collection or enumeration type with a value converter but with no value comparer. Set a value comparer `
- 41 × `System.NotSupportedException : Not yet provided by the base suite: CreateContext.`
- 38 × `Microsoft.Data.SqlClient.SqlException : Incorrect syntax near '…'.`
- 30 × `Microsoft.Data.SqlClient.SqlException : Cannot perform an aggregate function on an expression containing an aggregate or a subquery.`

### PostgreSql: largest failure groups

- 4043 × `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.`
- 400 × `Npgsql.PostgresException : 57P01: terminating connection due to administrator command`
- 211 × `Npgsql.PostgresException : N: identity column type must be smallint, integer, or bigint`
- 173 × `System.InvalidOperationException : No suitable constructor was found for the type '…'. The following constructors had parameters that could not be bound to properties of the type: `
- 153 × `Assert.Equal() Failure: Values differ`
- 128 × `System.InvalidCastException : Unable to cast object of type '…' to type '…'.`
- 128 × `System.InvalidOperationException : The instance of entity type '…' cannot be tracked because another instance with the key value '…' is already being tracked. When attaching existing entities, ensure `
- 122 × `System.InvalidOperationException : Unable to find expected assembly attribute [DesignTimeProviderServices] in provider assembly '…'. This attribute is required to identify the class which acts as the `
- 120 × `Npgsql.NpgsqlOperationInProgressException : A command is already in progress: SELECT c."…", c."…", c."…", c."…", c."…", c."…", c."…", c."…", c."…", c."…", c."…"`
- 96 × `Npgsql.NpgsqlOperationInProgressException : A command is already in progress: SELECT l."…"`
- 92 × `Npgsql.NpgsqlOperationInProgressException : A command is already in progress: SELECT l."…", l."…", l."…"`
- 92 × `Npgsql.NpgsqlOperationInProgressException : A command is already in progress: SELECT l."…", l."…", l."…", l."…", l."…", l."…"`
- 86 × `System.InvalidOperationException : A transaction is already in progress; nested/concurrent transactions aren't supported.`
- 72 × `Npgsql.NpgsqlOperationInProgressException : A command is already in progress: SELECT e."…", e."…", e."…", e."…"`
- 47 × `Assert.Throws() Failure: Exception type was not an exact match`

### MySql: largest failure groups

- 1082 × `System.InvalidOperationException : This MySqlConnection is already in use. See https://mysqlconnector.net/conn-reuse`
- 358 × `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.`
- 211 × `MySqlConnector.MySqlException : Incorrect column specifier for column '…'`
- 196 × `Assert.Equal() Failure: Values differ`
- 180 × `System.InvalidOperationException : The instance of entity type '…' cannot be tracked because another instance with the key value '…' is already being tracked. When attaching existing entities, ensure `
- 173 × `System.InvalidOperationException : No suitable constructor was found for the type '…'. The following constructors had parameters that could not be bound to properties of the type: `
- 135 × `Assert.Throws() Failure: Exception type was not an exact match`
- 124 × `System.InvalidCastException : Unable to cast object of type '…' to type '…'.`
- 124 × `System.InvalidOperationException : Transactions may not be nested.`
- 122 × `System.InvalidOperationException : Unable to find expected assembly attribute [DesignTimeProviderServices] in provider assembly '…'. This attribute is required to identify the class which acts as the `
- 122 × `MySqlConnector.MySqlException : Row size too large. The maximum row size for the used table type, not counting BLOBs, is N. This includes storage overhead, check the manual. You have to change some co`
- 47 × `System.InvalidOperationException : The LINQ expression 'DbSet<PrimitiveCollectionsEntity>()`
- 47 × `System.InvalidOperationException : An error was generated for warning '…': The property '…' is a collection or enumeration type with a value converter but with no value comparer. Set a value comparer `
- 41 × `System.NotSupportedException : Not yet provided by the base suite: CreateContext.`
- 28 × `MySqlConnector.MySqlException : Unknown column '…' in '…'`

### Sqlite: largest failure groups

- 6457 × `Microsoft.Data.Sqlite.SqliteException : SQLite Error N: '…'.`
- 199 × `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.`
- 196 × `Assert.Equal() Failure: Values differ`
- 180 × `System.InvalidOperationException : The instance of entity type '…' cannot be tracked because another instance with the key value '…' is already being tracked. When attaching existing entities, ensure `
- 173 × `System.InvalidOperationException : No suitable constructor was found for the type '…'. The following constructors had parameters that could not be bound to properties of the type: `
- 134 × `System.InvalidOperationException : This query joins a subquery correlated with the rows before it (a lateral join, EF'…'t have.`
- 122 × `System.InvalidOperationException : Unable to find expected assembly attribute [DesignTimeProviderServices] in provider assembly '…'. This attribute is required to identify the class which acts as the `
- 115 × `System.InvalidOperationException : SqliteConnection does not support nested transactions.`
- 47 × `System.InvalidOperationException : An error was generated for warning '…': The property '…' is a collection or enumeration type with a value converter but with no value comparer. Set a value comparer `
- 47 × `System.InvalidOperationException : The LINQ expression 'DbSet<PrimitiveCollectionsEntity>()`
- 41 × `System.NotSupportedException : Not yet provided by the base suite: CreateContext.`
- 26 × `System.InvalidOperationException : The LINQ expression 'DbSet<TestEntity>()`
- 23 × `System.InvalidOperationException : The store type '…' specified for JSON column '…' in table '…' is not supported by the current provider. JSON columns require a provider-specific JSON store type.`
- 20 × `System.NotSupportedException : Deleting a database is not supported by the '…' ADO.NET dialect.`
- 20 × `System.InvalidOperationException : Cannot create a DbSet for '…' because this type is not included in the model for the context.`

### DuckDb: largest failure groups

- 6408 × `DuckDB.NET.Data.DuckDBException : Not implemented Error: No support for that ALTER TABLE option yet!`
- 310 × `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.`
- 209 × `Assert.Equal() Failure: Values differ`
- 180 × `System.InvalidOperationException : The instance of entity type '…' cannot be tracked because another instance with the key value '…' is already being tracked. When attaching existing entities, ensure `
- 173 × `System.InvalidOperationException : No suitable constructor was found for the type '…'. The following constructors had parameters that could not be bound to properties of the type: `
- 122 × `System.InvalidOperationException : Unable to find expected assembly attribute [DesignTimeProviderServices] in provider assembly '…'. This attribute is required to identify the class which acts as the `
- 119 × `System.InvalidOperationException : Already in a transaction.`
- 47 × `System.InvalidOperationException : The LINQ expression 'DbSet<PrimitiveCollectionsEntity>()`
- 47 × `System.InvalidOperationException : An error was generated for warning '…': The property '…' is a collection or enumeration type with a value converter but with no value comparer. Set a value comparer `
- 41 × `System.NotSupportedException : Not yet provided by the base suite: CreateContext.`
- 36 × `DuckDB.NET.Data.DuckDBException : Invalid type Error: Invalid Type [INTERVAL]: Invalid type for index key.`
- 30 × `System.InvalidOperationException : Connection is already closed.`
- 26 × `System.InvalidOperationException : The LINQ expression 'DbSet<TestEntity>()`
- 26 × `Assert.Equal() Failure: Strings differ`
- 23 × `System.InvalidOperationException : The store type '…' specified for JSON column '…' in table '…' is not supported by the current provider. JSON columns require a provider-specific JSON store type.`

### FlatFile: largest failure groups

- 2508 × `Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes. See the inner exception for details.`
- 629 × `Assert.Equal() Failure: Values differ`
- 490 × `System.InvalidCastException : Unable to cast object of type '…' to type '…'.`
- 422 × `pengdows.flatfile.FlatFileException : Expected token '…' at position N, but found '…'.`
- 320 × `System.InvalidOperationException : Sequence '…' does not exist.`
- 300 × `System.NotSupportedException : Column '…' is generated by the database, but the '…' dialect cannot read generated values back. Generate the value on the client or use a key sequence.`
- 180 × `System.InvalidOperationException : The instance of entity type '…' cannot be tracked because another instance with the key value '…' is already being tracked. When attaching existing entities, ensure `
- 173 × `System.InvalidOperationException : No suitable constructor was found for the type '…'. The following constructors had parameters that could not be bound to properties of the type: `
- 124 × `System.InvalidOperationException : A transaction is already active on this connection.`
- 122 × `System.InvalidOperationException : Unable to find expected assembly attribute [DesignTimeProviderServices] in provider assembly '…'. This attribute is required to identify the class which acts as the `
- 120 × `Assert.Throws() Failure: Exception type was not an exact match`
- 99 × `System.InvalidOperationException : Unsupported value expression '…'.`
- 49 × `Assert.Equal() Failure: Strings differ`
- 47 × `System.InvalidOperationException : An error was generated for warning '…': The property '…' is a collection or enumeration type with a value converter but with no value comparer. Set a value comparer `
- 47 × `System.InvalidOperationException : The LINQ expression 'DbSet<PrimitiveCollectionsEntity>()`
