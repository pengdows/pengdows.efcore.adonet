"""Generates the base suite's classes for every EF specification test base an upstream provider runs (docs/upstream-tests/spec-bases.tsv)."""
import json, os, re, sys

bases = json.load(open(sys.argv[1]))
out = sys.argv[2]
HOSTS = ['SqlServer', 'PostgreSql', 'MySql', 'Sqlite', 'DuckDb', 'FlatFile']
# Databases that take one writer at a time: their test classes run one after another (an xUnit collection).
# Server-backed fixtures share named databases (especially Northwind) across many EF fixture classes. Serialize the
# generated server suite as well so one fixture cannot drop/reseed a store while another is reading it.
SINGLE_WRITER = {'Sqlite', 'DuckDb', 'FlatFile', 'MySql'}

# Abstract fixtures that non-generic bases take, made concrete once per host in TestUtilities/GeneratedSupport.cs.
SHARED_FIXTURES = {
    'InterceptionFixtureBase': 'AdoNetInterceptionFixture<THost>',
    'RelationalModelBuilderFixture': 'AdoNetRelationalModelBuilderFixture<THost>',
    'PrecompiledQueryRelationalFixture': 'AdoNetPrecompiledQueryRelationalFixture<THost>',
    'PrecompiledSqlPregenerationQueryRelationalFixture': 'AdoNetPrecompiledSqlPregenerationQueryRelationalFixture<THost>',
}

# Virtual members of a base whose value is the database's: overridden from its dialect.
VIRTUALS = {
    'UpdateSqlGeneratorTestBase': ['''    public override void AppendInsertOperation_appends_insert_and_select_rowcount_if_no_store_generated_columns_exist_or_conditions_exist()
    {
        var stringBuilder = new global::System.Text.StringBuilder();
        CreateSqlGenerator().AppendInsertOperation(stringBuilder, CreateInsertCommand(false, false), 0);
        UpdateSqlGeneratorAssertions.Insert(stringBuilder, false);
    }

    public override void GenerateNextSequenceValueOperation_correctly_handles_schemas()
    {
        if (new THost().Dialect.Sql.SupportsSchemas)
        {
            base.GenerateNextSequenceValueOperation_correctly_handles_schemas();
        }
    }'''],
    # What the database reads back: schemas where it has them, constraint names and index filters where its reader keeps them.
    'MigrationsTestBase': [
        '    protected override bool AssertSchemaNames => new THost().Dialect.Sql.SupportsSchemas;',
        '    protected override bool AssertConstraintNames => new THost().Dialect.SchemaReader?.KeepsConstraintNames ?? true;',
        '    protected override bool AssertIndexFilters => new THost().Dialect.SchemaReader?.ReadsIndexFilters ?? true;'],
    # The provider, over a database it never opens: the tests only deserialize.
    'BadDataJsonDeserializationTestBase': ['    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)\n        => base.OnConfiguring(new THost().UseProvider(optionsBuilder, new THost().ConnectionStringFor("ef_bad_json"), o => TestServices.Use(new THost(), o)));'],
    'TransactionTestBase': ['    protected override bool AmbientTransactionsSupported => new THost().Dialect.Capabilities.SupportsAmbientTransactions;', '    protected override bool SavepointsSupported => new THost().Dialect.Sql.Savepoints != null;'],
    'SeedingTestBase': ['''    private sealed class AdoNetSeedingContext(string testId) : global::Microsoft.EntityFrameworkCore.SeedingTestBase.SeedingContext(testId)
    {
        protected override void OnConfiguring(global::Microsoft.EntityFrameworkCore.DbContextOptionsBuilder optionsBuilder)
            => AdoNetTestStoreFactory<THost>.Instance.GetOrCreate("Seeding").AddProviderOptions(optionsBuilder);
    }'''],
}
# Virtual members whose EF default does nothing but every provider overrides: emitted wherever a base declares them.
VIRTUAL_DEFAULTS = {
    # The inner contexts of EF's ExecuteWithStrategyInTransaction must join the outer one's transaction.
    'UseTransaction': '    protected override void UseTransaction(global::Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade facade, global::Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)\n        => facade.UseTransaction(global::Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(transaction));',
}
# EF fixtures given a hand-written subclass (under Fixtures/) that the generated fixtures derive from instead.
FIXTURE_BASES = {
    'global::Microsoft.EntityFrameworkCore.Query.Translations.BasicTypesQueryFixtureBase': 'BasicTypesAdoNetFixture<THost>',
    # SQL Server's and Npgsql's variant (SQLite's is ChangedOnly); the abstract fixture leaves CreateContext to it.
    'global::Microsoft.EntityFrameworkCore.MonsterFixupTestBase<AdoNetFixture>.MonsterFixupFixtureBase':
        'global::Microsoft.EntityFrameworkCore.MonsterFixupTestBase<AdoNetFixture>.MonsterFixupChangedChangingFixtureBase',
    'global::Microsoft.EntityFrameworkCore.Query.NorthwindQueryFixtureBase<global::Microsoft.EntityFrameworkCore.Query.NorthwindQueryFiltersCustomizer>':
        'NorthwindQueryFiltersAdoNetFixture<THost>',
}
# Members of a base the mapped EF fixture implements.
INHERITED = {('MonsterFixupTestBase', 'CreateContext'), ('MonsterFixupTestBase', 'UseDetectChanges')}
HELPER_CTOR = ['TestHelpers', 'IServiceCollection', 'DbContextOptions']
HAND_WRITTEN = {'NorthwindWhereQueryAdoNetTest', 'NorthwindSelectQueryAdoNetTest', 'ProxyGraphUpdatesAdoNetTest'}
NORTHWIND = re.compile(r'Northwind(Query(Relational)?Fixture|QueryFixtureBase)<global::Microsoft\.EntityFrameworkCore\.TestUtilities\.NoopModelCustomizer>$')
NOT_YET = 'throw new NotSupportedException("Not yet provided by the base suite: {0}.")'

def short(name):
    return re.sub(r'(Relational)?TestBase$', '', name)

def impl(m, base, fixture='TFixture'):
    """C# for one abstract member, or a not-yet-provided body."""
    n, acc = m['name'], m['access']
    ret = m['returns'].replace('<TFixture>', '<' + fixture + '>')
    params = [p.replace('<TFixture>', '<' + fixture + '>') for p in m['parameters']]
    args = ', '.join(params)
    def prop(body):
        return f'    {acc} override {ret} {n} => {body};'
    def meth(body):
        return f'    {acc} override {ret} {n}({args})\n        => {body};'
    table = {
        'TestStoreFactory': lambda: prop('AdoNetTestStoreFactory<THost>.Instance'),
        'StoreName': lambda: prop(f'"{short(base)}"'),
        'TestHelpers': lambda: prop('AdoNetTestHelpers<THost>.Instance'),
        'CreateDbParameter': lambda: meth('DbParameters.Create(new THost(), name, value)'),
        'EnforcesFKs': lambda: prop('true'),
        'SnapshotSupported': lambda: prop('false'),
        'UseTransaction': lambda: meth('facade.UseTransaction(global::Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(transaction))'),
        'ClearLog': lambda: meth('Fixture.TestSqlLoggerFactory.Clear()'),
        'SetQuerySplittingBehavior': lambda: meth('QuerySplitting.Set(optionsBuilder, splittingBehavior)'),
        'ClearQuerySplittingBehavior': lambda: meth('QuerySplitting.Clear(optionsBuilder)'),
        'SetParameterizedCollectionMode': lambda: meth('ParameterizedCollections.Set(optionsBuilder, parameterizedCollectionMode)'),
        'CreateContextWithConnectionString': lambda: meth('new DbContext(Fixture.AddOptions(new THost().UseProvider(new DbContextOptionsBuilder(), ((RelationalTestStore)Fixture.TestStore).ConnectionString, _ => { })).UseInternalServiceProvider(Fixture.ServiceProvider).Options)'),
        'TenMostExpensiveProductsSproc': lambda: prop('Sprocs.Of<THost>().TenMostExpensiveProducts'),
        'CustomerOrderHistorySproc': lambda: prop('Sprocs.Of<THost>().' + ('CustomerOrderHistoryNamed' if base == 'SqlExecutorTestBase' else 'CustomerOrderHistory')),
        'CustomerOrderHistoryWithGeneratedParameterSproc': lambda: prop('Sprocs.Of<THost>().CustomerOrderHistory'),
        'NonDefaultCollation': lambda: prop('new THost().NonDefaultCollation ?? throw new NotSupportedException("The host names no non-default collation.")'),
        'CaseInsensitiveCollation': lambda: prop('new THost().CaseInsensitiveCollation ?? throw new NotSupportedException("The host names no case-insensitive collation.")'),
        'CaseSensitiveCollation': lambda: prop('new THost().CaseSensitiveCollation ?? throw new NotSupportedException("The host names no case-sensitive collation.")'),
        # EF's mapping fixture uses null for databases without schemas (MySQL, SQLite, DuckDB, and the flat-file host).
        'DatabaseSchema': lambda: prop('new THost().DatabaseSchema!'),
        'StrictEquality': lambda: prop('new THost().StrictEquality'),
        'SupportsAnsi': lambda: prop('false'),
        'SupportsUnicodeToAnsiConversion': lambda: prop('true'),
        'SupportsLargeStringComparisons': lambda: prop('true'),
        'SupportsBinaryKeys': lambda: prop('true'),
        'SupportsDecimalComparisons': lambda: prop('true'),
        'DefaultDateTime': lambda: prop('new global::System.DateTime()'),
        'PreservesDateTimeKind': lambda: prop('false'),
        'UseDetectChanges': lambda: prop('true'),
        'Finder': lambda: prop('new FindViaSetFinder()'),
        'ShouldSubscribeToDiagnosticListener': lambda: prop('false'),
        'CreateModelBuilder': lambda: meth('new global::Microsoft.EntityFrameworkCore.ModelBuilding.ModelBuilderTest.GenericTestModelBuilder(Fixture, configure)'),
        'ConfigureProvider': lambda: meth('new THost().UseProvider(optionsBuilder, new THost().ConnectionStringFor("Interception"), _ => { })'),
        'CreateBadUniverse': lambda: meth('new(\n            new THost().UseProvider(optionsBuilder, FailingConnection.Create(), _ => { }).Options)'),
        'AddServices': lambda: meth('serviceCollection.AddEntityFrameworkAdoNet(new THost().Dialect)'),
        'TargetAssembly': lambda: prop('typeof(global::pengdows.efcore.adonet.Infrastructure.AdoNetOptionsExtension).Assembly'),
        'ProviderAssembly': lambda: prop('new THost().Dialect.GetType().Assembly'),
        'PrecompiledQueryTestHelpers': lambda: prop('AdoNetPrecompiledQueryTestHelpers<THost>.Instance'),
        'CreateContextWithEmptyDatabase': lambda: meth('new AdoNetSeedingContext(testId)'),
        'TestStore': lambda: prop('AdoNetTestStoreFactory<THost>.Instance.GetOrCreate("Seeding")'),
        'GetGeometryCollectionStoreType': lambda: meth('"geometry"'),
        'CreateSqlGenerator': lambda: meth('UpdateSqlGeneratorSupport.Create<THost>(TestHelpers)'),
        'RowsAffected': lambda: prop('(new THost().Dialect.Sql.Keys as global::pengdows.efcore.adonet.SelectAfterKeyRetrieval)?.RowsAffectedFunction ?? "ROW_COUNT()"'),
        'ExecuteSqlAsync': lambda: meth('MigrationsSqlSupport.ExecuteAsync(Fixture, value)'),
        'AppendDeleteOperation_creates_full_delete_command_text_verification': lambda: meth('UpdateSqlGeneratorAssertions.Delete(stringBuilder, false)'),
        'AppendDeleteOperation_creates_full_delete_command_text_with_concurrency_check_verification': lambda: meth('UpdateSqlGeneratorAssertions.Delete(stringBuilder, true)'),
        'AppendInsertOperation_insert_if_store_generated_columns_exist_verification': lambda: meth('UpdateSqlGeneratorAssertions.Insert(stringBuilder, true)'),
        'AppendInsertOperation_for_store_generated_columns_but_no_identity_verification': lambda: meth('UpdateSqlGeneratorAssertions.Insert(stringBuilder, true)'),
        'AppendInsertOperation_for_only_identity_verification': lambda: meth('UpdateSqlGeneratorAssertions.Insert(stringBuilder, true, true)'),
        'AppendInsertOperation_for_all_store_generated_columns_verification': lambda: meth('UpdateSqlGeneratorAssertions.Insert(stringBuilder, true)'),
        'AppendInsertOperation_for_only_single_identity_columns_verification': lambda: meth('UpdateSqlGeneratorAssertions.Insert(stringBuilder, true, true)'),
        'AppendUpdateOperation_if_store_generated_columns_exist_verification': lambda: meth('UpdateSqlGeneratorAssertions.Update(stringBuilder, true, true)'),
        'AppendUpdateOperation_if_store_generated_columns_dont_exist_verification': lambda: meth('UpdateSqlGeneratorAssertions.Update(stringBuilder, false, true)'),
        'AppendUpdateOperation_appends_where_for_concurrency_token_verification': lambda: meth('UpdateSqlGeneratorAssertions.Update(stringBuilder, false, true)'),
        'AppendUpdateOperation_for_computed_property_verification': lambda: meth('UpdateSqlGeneratorAssertions.Update(stringBuilder, true, true)'),
        'Seed30028': lambda: meth('JsonFixtureSeeds.Seed30028<THost>(ctx)'),
        'Seed2951': lambda: meth('MiscellaneousSeeds.SeedNullKey<THost>(context)'),
        'Seed33046': lambda: meth('JsonFixtureSeeds.Seed33046<THost>(ctx)'),
        'SeedJunkInJson': lambda: meth('JsonFixtureSeeds.SeedJunkInJson<THost>(ctx)'),
        'SeedTrickyBuffering': lambda: meth('JsonFixtureSeeds.SeedTrickyBuffering<THost>(ctx)'),
        'SeedShadowProperties': lambda: meth('JsonFixtureSeeds.SeedShadowProperties<THost>(ctx)'),
        'SeedNotICollection': lambda: meth('JsonFixtureSeeds.SeedNotICollection<THost>(ctx)'),
        'Identifiers_are_generated_correctly': lambda: meth('UpdateIdentifierAssertions.Assert<THost>(CreateContext())'),
    }
    # NullSemantics: a context over the fixture's options, with relational nulls when asked (TestUtilities/GeneratedSupport.cs).
    if n == 'CreateContext' and 'useRelationalNulls' in args:
        return meth('NullSemantics.CreateContext(Fixture, useRelationalNulls)')
    # A required primitive collection added with a default: the JSON array as the dialect's string literal, as each upstream
    # provider passes its own.
    if base == 'MigrationsTestBase' and n.endswith('_with_custom_default_value_sql_to_existing_table'):
        return meth(f'{n}_core(new THost().Dialect.Sql.StringLiteral("[3, 2, 1]"))')
    if base == 'RelationalComplianceTestBase' and n == 'TargetAssembly':
        return prop('typeof(RelationalComplianceAdoNetTest<THost>).Assembly')
    # EF's stored-procedure update tests, each with the host's procedures for it.
    if base == 'StoredProcedureUpdateTestBase':
        if n == 'ConfigureStoreGeneratedConcurrencyToken':
            return meth('UpdateProcedureSql.ConcurrencyToken<THost>(entityTypeBuilder, propertyName)')
        if args == 'bool async':
            return meth(f'{n}(async, UpdateProcedureSql.Of<THost>("{n}"))')
    # A fixture mapped to an EF fixture that implements the member (FIXTURE_BASES) keeps EF's.
    if (base, n) in INHERITED:
        return None
    if n in table:
        return table[n]()
    body = NOT_YET.format(n)
    if m['kind'] == 'property':
        return prop(body)
    return meth(body)

def members(ms, base, fixture='TFixture'):
    seen, lines = set(), []
    for m in ms:
        if m['name'] in seen or m['generic']:
            continue
        seen.add(m['name'])
        code = impl(m, base, fixture)
        if code is not None:
            lines.append(code)
    return lines

generated, skipped = [], []
present = {b['name'] for b in bases if not b.get('missing')}
for b in bases:
    if b.get('missing'):
        skipped.append((b['name'], 'missing'))
        continue
    # UdfFixtureBase is an EF fixture discovered alongside test bases, not a test base itself; its concrete test base is
    # UdfDbFunctionTestBase and generating both produces an invalid constructor chain and no additional coverage.
    if b['name'] == 'UdfFixtureBase':
        skipped.append((b['name'], 'fixture, not a test base'))
        continue
    if short(b['name']) + 'AdoNetTest' in HAND_WRITTEN:
        skipped.append((b['name'], 'hand-written'))
        continue
    has_relational_counterpart = (
        not b['name'].endswith('RelationalTestBase')
        and b['name'].replace('TestBase', 'RelationalTestBase') in present)
    if any(g['Name'] != 'TFixture' for g in b['generic']):
        skipped.append((b['name'], 'type parameters ' + ', '.join(g['Name'] for g in b['generic'])))
        continue
    name, cs = b['name'], b['cs']
    # ADO.NET is relational even for EF's provider-independent JSON base.  The
    # relational base supplies the store initialization and required JSON
    # shaping behavior that the non-relational base deliberately omits.
    if name == 'AdHocJsonQueryTestBase':
        cs = 'global::Microsoft.EntityFrameworkCore.Query.AdHocJsonQueryRelationalTestBase'
    # Keep both EF's provider-independent base and its relational base. Upstream providers expose both layers, and the
    # compliance suite requires a concrete derived class for every exported TestBase. The Core suffix prevents the two
    # generated classes from colliding while preserving the same shared host implementation.
    cls = short(name) + ('Core' if has_relational_counterpart else '') + 'AdoNetTest'
    ns_sub = b['namespace'].replace('Microsoft.EntityFrameworkCore', '').strip('.')
    gens = b['generic']
    ctor = b['ctors'][0] if b['ctors'] else []
    has_output = any('ITestOutputHelper' in p for p in ctor)
    lines = []
    abstract_fixture = next((f for f in b.get('ctorFixtures') or [] if f['isAbstract']), None) if not gens else None
    if abstract_fixture and abstract_fixture['cs'].split('.')[-1] not in SHARED_FIXTURES:
        # Its fixture isn't made concrete in TestUtilities/GeneratedSupport.cs yet: listed, so it isn't silently dropped.
        skipped.append((b['name'], 'fixture not provided yet: ' + abstract_fixture['cs'].split('.')[-1]))
        continue
    if not gens:
        # A non-generic base: its constructor takes a fixture, which, when abstract, is made concrete here and declared.
        shared = None
        if abstract_fixture:
            fixture_cs = abstract_fixture['cs']
            shared = SHARED_FIXTURES[fixture_cs.split('.')[-1]]
            ctor = [f'{shared} fixture' if p.split(' ')[0] == fixture_cs else p for p in ctor]
        params = ', '.join(ctor)
        args = ', '.join(p.split(' ')[-1] for p in ctor)
        # Bases built from test helpers, services and options rather than a fixture (EF's providers pass their own): the
        # host's, so the class needs no constructor parameters.
        if [p.split(' ')[0].split('.')[-1] for p in ctor] == HELPER_CTOR:
            ctor, params = [], ''
            args = ('AdoNetTestHelpers<THost>.Instance, new global::Microsoft.Extensions.DependencyInjection.ServiceCollection(), '
                    'AdoNetTestHelpers<THost>.Instance.AddProviderOptions(new DbContextOptionsBuilder()).Options')
        declared = f', global::Xunit.IClassFixture<{shared}>' if abstract_fixture else ''
        lines.append(f'public abstract partial class {cls}<THost>({params}) : {cs}({args}){declared}')
        lines.append('    where THost : IDatabaseHost, new()')
        lines.append('{')
        lines += members(b['abstracts'], name)
        lines += VIRTUALS.get(name, [])
        lines.append('}')
        generated.append(dict(name=name, cls=cls, sub=ns_sub, code=lines, fixture=None, ctor=ctor, nonshared=True))
        continue
    if len(gens) != 1:
        skipped.append((name, f'{len(gens)} type parameters'))
        continue
    cons = [c for c in gens[0]['constraints'] if c['isClass']]
    if len(cons) != 1:
        skipped.append((name, 'no single fixture constraint'))
        continue
    c = cons[0]
    helper_only = [p.split(' ')[0].split('.')[-1] for p in ctor] == ['TestHelpers']
    extra = ', '.join(p.split(' ')[0] + ' ' + p.split(' ')[-1] for p in ctor[1:])
    extra_args = ', '.join(p.split(' ')[-1] for p in ctor[1:])
    if NORTHWIND.search(c['cs']):
        fixture = 'NorthwindQueryAdoNetFixture<THost>'
        nested = []
    else:
        fixture = f'{cls}<THost>.AdoNetFixture'
        # EF's own fixture for the base when it has one (its own store name), else the one the constraint names.
        base_fixture = (b.get('preferredFixture')
                        or (f"{cs.replace('<TFixture>', '<AdoNetFixture>')}.{b['nestedFixture']}" if b.get('nestedFixture') else None)
                        or c['cs'].replace('<TFixture>', '<AdoNetFixture>'))
        base_fixture = FIXTURE_BASES.get(base_fixture, base_fixture)
        # EF's query asserter casts the fixture to ITestSqlLoggerFactory to show a failed query's SQL; without it a
        # failure is reported as an InvalidCastException.
        logger = ['        public global::Microsoft.EntityFrameworkCore.TestUtilities.TestSqlLoggerFactory TestSqlLoggerFactory',
                  '            => (global::Microsoft.EntityFrameworkCore.TestUtilities.TestSqlLoggerFactory)ListLoggerFactory;']
        if not c.get('listLogger'):
            logger = []
        # The Filters variants of EF's inheritance suites run their base's model with query filters on, in a store of their own.
        if 'FiltersInheritance' in name:
            logger = logger + ['        public override bool EnableFilters => true;',
                               f'        protected override string StoreName => "{short(name)}";']
        interface = ', global::Microsoft.EntityFrameworkCore.TestUtilities.ITestSqlLoggerFactory' if logger else ''
        nested = ['', f'    public partial class AdoNetFixture : {base_fixture}{interface}', '    {'] + ['    ' + l for l in members(c['abstracts'], name, 'AdoNetFixture')] + logger + ['    }']
    params = '' if helper_only else f'{fixture} fixture' + (f', {extra}' if extra else '')
    args = 'AdoNetTestHelpers<THost>.Instance' if helper_only else 'fixture' + (f', {extra_args}' if extra_args else '')
    lines.append(f'public abstract partial class {cls}<THost>({params}) : {cs.replace("<TFixture>", "<" + fixture + ">")}({args})')
    lines.append('    where THost : IDatabaseHost, new()')
    lines.append('{')
    lines += members(b['abstracts'], name, fixture)
    lines += VIRTUALS.get(name, [])
    lines += [code for member, code in VIRTUAL_DEFAULTS.items() if member in (b.get('virtuals') or [])]
    lines += nested
    lines.append('}')
    generated.append(dict(name=name, cls=cls, sub=ns_sub, code=lines, fixture=fixture, ctor=ctor, nonshared=False, has_output=has_output, extra=extra, extra_args=extra_args, helper_only=helper_only))

header = '// Generated by tools/genspec.py from EF Core 10.0.11\'s specification test bases; edit the generator, not this file.\n'
os.makedirs(os.path.join(out, 'Generated'), exist_ok=True)
with open(os.path.join(out, 'Generated', 'Bases.cs'), 'w') as f:
    f.write(header + '\n#nullable disable\n\nnamespace pengdows.efcore.adonet.Specification.Tests;\n\n')
    for g in generated:
        f.write(f'/// <summary>EF\'s {g["name"]}.</summary>\n' + '\n'.join(g['code']) + '\n\n')
for host in HOSTS:
    with open(os.path.join(out, 'Generated', f'{host}.cs'), 'w') as f:
        f.write(header + f'\n#nullable disable\n\nnamespace pengdows.efcore.adonet.Specification.Tests.{host};\n\n')
        collection = f'[Collection(SingleWriter.{host})]\n' if host in SINGLE_WRITER else ''
        for g in generated:
            cls = g['cls'].replace('AdoNetTest', f'{host}Test')
            f.write(collection)
            if g['nonshared']:
                params = ', '.join(g['ctor']).replace('<THost>', f'<{host}Host>')
                args = ', '.join(p.split(' ')[-1] for p in g['ctor'])
                f.write(f'public partial class {cls}({params}) : {g["cls"]}<{host}Host>({args});\n\n')
            else:
                fixture = g['fixture'].replace('THost', f'{host}Host')
                params = '' if g.get('helper_only') else f'{fixture} fixture' + (f', {g["extra"]}' if g['extra'] else '')
                args = 'AdoNetTestHelpers<%sHost>.Instance' % host if g.get('helper_only') else 'fixture' + (f', {g["extra_args"]}' if g['extra_args'] else '')
                f.write(f'public partial class {cls}({params}) : {g["cls"]}<{host}Host>({args});\n\n')
print(f'{len(generated)} generated; skipped:')
for s in skipped:
    print('  ', s)
