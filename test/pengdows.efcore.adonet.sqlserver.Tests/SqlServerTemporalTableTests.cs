using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     Covers Microsoft's SQL Server temporal tables: <c>IsTemporal</c> with its period columns and history table in the model,
///     the DDL (PERIOD FOR SYSTEM_TIME, SYSTEM_VERSIONING) its migrations write and drop, and the TemporalAll, TemporalAsOf,
///     TemporalFromTo, TemporalBetween and TemporalContainedIn queries as FOR SYSTEM_TIME. SQL-shape tests; no server.
///     Provider-only: system-versioned tables are SQL Server's. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerTableBuilderExtensions.cs
/// </summary>
public class SqlServerTemporalTableTests
{
    public sealed class Blog
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class Plain
    {
        public int Id { get; set; }
    }

    private sealed class TemporalContext(DbContextOptions options, Action<ModelBuilder> build) : DbContext(options)
    {
        public DbSet<Blog> Blogs => Set<Blog>();
        public DbSet<Plain> Plains => Set<Plain>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => build(modelBuilder);
    }

    private static TemporalContext Context(Action<ModelBuilder> build)
        => new(
            new DbContextOptionsBuilder().UseAdoNetSqlServer("Server=.;Database=x").ReplaceService<IModelCacheKeyFactory, PerContextModelCacheKey>().Options,
            build);

    private static void Temporal(ModelBuilder m)
        => m.Entity<Blog>().ToTable("Blogs", "dbo", t => t.IsTemporal());

    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = Context(_ => { });
        return string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(c => c.CommandText)).ReplaceLineEndings("\n");
    }

    [Fact]
    public void A_temporal_table_has_hidden_period_columns_maintained_by_the_database()
    {
        using var context = Context(Temporal);
        var entityType = context.Model.FindEntityType(typeof(Blog))!;

        var start = entityType.FindProperty("PeriodStart")!;
        var end = entityType.FindProperty("PeriodEnd")!;

        Assert.Equal(typeof(DateTime), start.ClrType);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, start.ValueGenerated);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, end.ValueGenerated);
        Assert.True(start.IsShadowProperty());
    }

    [Fact]
    public void A_temporal_table_is_created_system_versioned()
    {
        using var context = Context(Temporal);
        var script = context.Database.GenerateCreateScript().ReplaceLineEndings("\n");

        Assert.Contains("[PeriodStart] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL", script);
        Assert.Contains("[PeriodEnd] datetime2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL", script);
        Assert.Contains(",\n    PERIOD FOR SYSTEM_TIME([PeriodStart], [PeriodEnd])\n) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [dbo].[BlogsHistory]));", script);
    }

    [Fact]
    public void Period_columns_and_the_history_table_can_be_named()
    {
        using var context = Context(m => m.Entity<Blog>().ToTable("Blogs", "dbo", t => t.IsTemporal(tt =>
        {
            tt.HasPeriodStart("ValidFrom").HasColumnName("Valid]From");
            tt.HasPeriodEnd("ValidTo");
            tt.UseHistoryTable("BlogArchive", "hist");
        })));
        var script = context.Database.GenerateCreateScript().ReplaceLineEndings("\n");

        Assert.NotNull(context.Model.FindEntityType(typeof(Blog))!.FindProperty("ValidFrom"));
        Assert.Contains("[Valid]]From] datetime2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL", script);
        Assert.Contains("PERIOD FOR SYSTEM_TIME([Valid]]From], [ValidTo])", script);
        Assert.Contains("WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [hist].[BlogArchive]))", script);
        Assert.Contains("CREATE SCHEMA [hist]", script);
    }

    /// <summary>HISTORY_TABLE needs a schema: without one, the user's default schema is read when the table is created.</summary>
    [Fact]
    public void A_history_table_without_a_schema_goes_in_the_default_schema()
    {
        using var context = Context(m => m.Entity<Blog>().ToTable("Blo'gs", t => t.IsTemporal()));
        var script = context.Database.GenerateCreateScript().ReplaceLineEndings("\n");

        Assert.Contains("DECLARE @historyTableSchema sysname = SCHEMA_NAME();\nEXEC(N'CREATE TABLE [Blo''gs]", script);
        Assert.Contains("SYSTEM_VERSIONING = ON (HISTORY_TABLE = ' + QUOTENAME(@historyTableSchema) + N'.[Blo''gsHistory]))');", script);
    }

    [Fact]
    public void Dropping_a_temporal_table_drops_its_history()
    {
        var drop = new DropTableOperation { Name = "Blogs", Schema = "dbo" };
        drop.AddAnnotation("SqlServer:IsTemporal", true);
        drop.AddAnnotation("SqlServer:TemporalHistoryTableName", "BlogsHistory");
        drop.AddAnnotation("SqlServer:TemporalHistoryTableSchema", "hist");

        Assert.Equal(
            "ALTER TABLE [dbo].[Blogs] SET (SYSTEM_VERSIONING = OFF);\nDROP TABLE [dbo].[Blogs];\nDROP TABLE [hist].[BlogsHistory];\n",
            Sql(drop));
    }

    /// <summary>Turning versioning on or off for an existing table isn't generated yet; it is refused rather than skipped.</summary>
    [Fact]
    public void Changing_whether_a_table_is_temporal_is_refused()
    {
        var alter = new AlterTableOperation { Name = "Blogs" };
        alter.AddAnnotation("SqlServer:IsTemporal", true);

        Assert.Throws<NotSupportedException>(() => Sql(alter));
    }

    [Fact]
    public void Temporal_queries_read_for_system_time()
    {
        using var context = Context(Temporal);
        var from = new DateTime(2020, 1, 1);
        var to = new DateTime(2021, 1, 1);

        Assert.Contains("FROM [dbo].[Blogs] FOR SYSTEM_TIME ALL", context.Blogs.TemporalAll().ToQueryString());
        Assert.Contains("FOR SYSTEM_TIME AS OF @p0", context.Blogs.TemporalAsOf(from).ToQueryString());
        Assert.Contains("FOR SYSTEM_TIME FROM @p0 TO @p1", context.Blogs.TemporalFromTo(from, to).ToQueryString());
        Assert.Contains("FOR SYSTEM_TIME BETWEEN @p0 AND @p1", context.Blogs.TemporalBetween(from, to).ToQueryString());
        Assert.Contains("FOR SYSTEM_TIME CONTAINED IN (@p0, @p1)", context.Blogs.TemporalContainedIn(from, to).ToQueryString());
    }

    /// <summary>The hidden period columns are named, so a query can read them, and the temporal rows compose like any query.</summary>
    [Fact]
    public void Temporal_queries_read_the_period_columns_and_compose()
    {
        using var context = Context(Temporal);

        var sql = context.Blogs.TemporalAll()
            .Where(b => b.Name == "x")
            .OrderBy(b => EF.Property<DateTime>(b, "PeriodStart"))
            .Select(b => new { b.Name, Start = EF.Property<DateTime>(b, "PeriodStart") })
            .ToQueryString();

        var inner = sql[sql.IndexOf("FROM (", StringComparison.Ordinal)..sql.IndexOf("FOR SYSTEM_TIME ALL", StringComparison.Ordinal)];
        Assert.Contains("[PeriodStart]", inner);
        Assert.Contains("[PeriodEnd]", inner);
        Assert.Contains("[Name]", inner);
        Assert.Contains("ORDER BY [p].[PeriodStart]", sql);
    }

    [Fact]
    public void Temporal_queries_need_a_temporal_table()
    {
        using var context = Context(Temporal);

        Assert.Contains("temporal", Assert.Throws<InvalidOperationException>(() => context.Plains.TemporalAll()).Message);
    }

    /// <summary>Each test builds its own model.</summary>
    private sealed class PerContextModelCacheKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => new object();
    }
}
