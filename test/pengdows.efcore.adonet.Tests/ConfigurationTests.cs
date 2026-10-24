using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Tests;

/// <summary>The ways a context can be configured, and what the configuration exposes.</summary>
public class ConfigurationTests
{
    private const string ConnectionString = "Data Source=fake;EmulatedProduct=PostgreSql";

    [Fact]
    public void A_factory_can_be_chosen_by_invariant_name()
    {
        DbProviderFactories.RegisterFactory("pengdows.efcore.adonet.Tests.fake", new fakeDbFactory(SupportedDatabase.PostgreSql));

        using var nonGeneric = new ItemContext(
            (DbContextOptions<ItemContext>)((DbContextOptionsBuilder)new DbContextOptionsBuilder<ItemContext>().AllowManyDialects())
            .UseAdoNet("pengdows.efcore.adonet.Tests.fake", ConnectionString).Options);
        using var generic = new ItemContext(
            new DbContextOptionsBuilder<ItemContext>().AllowManyDialects().UseAdoNet("pengdows.efcore.adonet.Tests.fake", ConnectionString).Options);

        Assert.IsType<fakeDbConnection>(nonGeneric.Database.GetDbConnection());
        Assert.IsType<fakeDbConnection>(generic.Database.GetDbConnection());
    }

    [Fact]
    public void Generic_connection_and_data_source_overloads_keep_the_context_type()
    {
        var factory = new fakeDbFactory(SupportedDatabase.PostgreSql);
        using var dataSource = factory.CreateDataSource(ConnectionString);
        using var connection = new fakeDbConnection { ConnectionString = ConnectionString };

        DbContextOptions<ItemContext> fromDataSource = new DbContextOptionsBuilder<ItemContext>().AllowManyDialects().UseAdoNet(dataSource).Options;
        DbContextOptions<ItemContext> fromConnection = new DbContextOptionsBuilder<ItemContext>().AllowManyDialects().UseAdoNet(connection).Options;

        using var first = new ItemContext(fromDataSource);
        using var second = new ItemContext(fromConnection);
        Assert.Same(connection, second.Database.GetDbConnection());
        Assert.IsType<fakeDbConnection>(first.Database.GetDbConnection());
    }

    [Fact]
    public void A_factory_without_a_connection_string_is_rejected()
    {
        var builder = new DbContextOptionsBuilder<ItemContext>().AllowManyDialects();
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
            new AdoNetOptionsExtension().WithProviderFactory(new fakeDbFactory(SupportedDatabase.PostgreSql)));
        using var context = new ItemContext(builder.Options);

        Assert.Contains("requires a connection string", Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection()).Message);
    }

    [Fact]
    public void The_dialect_appears_in_the_log_fragment_and_debug_info()
    {
        var extension = new FakeDatabase().Context(TestDialects.Top).GetService<IDbContextOptions>().FindExtension<AdoNetOptionsExtension>()!;
        var debugInfo = new Dictionary<string, string>();
        extension.Info.PopulateDebugInfo(debugInfo);

        Assert.Same(TestDialects.Top, extension.Dialect);
        Assert.Contains("Dialect=Top ", extension.Info.LogFragment);
        Assert.Equal(TestDialects.Top.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture), debugInfo["AdoNet:Dialect"]);
        Assert.Equal("Top", TestDialects.Top.ToString());
        Assert.Equal("ANSI", AdoNetDialect.Ansi.Name);
    }

    [Fact]
    public void An_explicit_column_type_keeps_the_dialects_literals()
    {
        using var context = new TypedContext(new DbContextOptionsBuilder<TypedContext>().AllowManyDialects()
            .UseAdoNet(new fakeDbConnection { ConnectionString = ConnectionString }, contextOwnsConnection: true, o => o.UseDialect(TestDialects.Bit))
            .Options);

        var mapping = context.Model.FindEntityType(typeof(Item))!.FindProperty(nameof(Item.Active))!.GetRelationalTypeMapping();

        Assert.Equal("bit", mapping.StoreType);
        Assert.Equal("1", mapping.GenerateSqlLiteral(true));
        Assert.Equal("0", mapping.GenerateSqlLiteral(false));
    }

    [Fact]
    public void Mappings_are_found_by_store_type_name()
    {
        using var context = new FakeDatabase().Context(AdoNetDialect.Ansi);
        var source = context.GetService<IRelationalTypeMappingSource>();

        Assert.Equal(typeof(long), source.FindMapping("BIGINT")!.ClrType);
        Assert.Equal("DECIMAL(10,4)", source.FindMapping("DECIMAL(10,4)")!.StoreType);
        Assert.Null(source.FindMapping("GEOMETRY"));
    }

    [Fact]
    public void Types_a_dialect_has_no_store_type_for_are_unmapped()
    {
        using var context = new FakeDatabase().Context(NoOffsetsDialect.Instance);
        var source = context.GetService<IRelationalTypeMappingSource>();

        // No store type of its own: EF falls back to storing it through a value converter.
        Assert.NotNull(source.FindMapping(typeof(DateTimeOffset))!.Converter);
        Assert.Null(source.FindMapping(typeof(DateTime))!.Converter);
    }

    [Fact]
    public void Mapping_plugins_win()
    {
        using var context = new FakeDatabase().Context(PluginDialect.Instance);
        var source = context.GetService<IRelationalTypeMappingSource>();

        Assert.Equal("JSON", source.FindMapping(typeof(string))!.StoreType);
    }

    [Fact]
    public void Identifiers_and_parameter_names_can_be_appended_to_a_builder()
    {
        using var context = new FakeDatabase().Context(TestDialects.ColonParameters);
        var helper = context.GetService<ISqlGenerationHelper>();
        var builder = new StringBuilder();

        helper.GenerateParameterName(builder, ":p");
        builder.Append(' ');
        helper.GenerateParameterNamePlaceholder(builder, "p");
        builder.Append(' ');
        ((RelationalSqlGenerationHelper)helper).EscapeIdentifier(builder, "a\"b");
        builder.Append(' ');
        helper.DelimitIdentifier(builder, "T", "dbo");

        Assert.Equal("p :p a\"\"b \"T\"", builder.ToString());
    }

    private sealed class TypedContext(DbContextOptions<TypedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Item>(e =>
            {
                e.Property(i => i.Active).HasColumnType("bit");
                e.Ignore(i => i.Parts);
            });
    }

    private sealed class NoOffsetsDialect : AdoNetDialect
    {
        public static readonly NoOffsetsDialect Instance = new();

        public override string Name => "No offsets";
        public override ITypeMappingDialect TypeMappings { get; } = new NoOffsetTypes();

        private sealed class NoOffsetTypes : AnsiTypeMappings
        {
            public override string? GetStoreType(Type clrType)
                => clrType == typeof(DateTimeOffset) ? null : base.GetStoreType(clrType);
        }
    }

    private sealed class PluginDialect : AdoNetDialect
    {
        public static readonly PluginDialect Instance = new();

        public override string Name => "Plugin";

        public override void ConfigureServices(IServiceCollection services)
            => services.AddSingleton<IRelationalTypeMappingSourcePlugin, JsonStrings>();

        private sealed class JsonStrings : IRelationalTypeMappingSourcePlugin
        {
            public RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
                => mappingInfo.ClrType == typeof(string) ? new StringTypeMapping("JSON", System.Data.DbType.String) : null;
        }
    }
}
