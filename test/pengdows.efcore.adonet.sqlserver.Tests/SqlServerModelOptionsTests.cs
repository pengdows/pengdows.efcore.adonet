using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     Covers Microsoft's SQL Server model options and the DDL its migrations write for them: clustered and nonclustered keys
///     and indexes, included columns, fill factor, online and tempdb index builds, data compression, sparse columns, identity
///     seed and increment, hi-lo and sequence keys, and memory-optimized tables. The annotations keep Microsoft's names, so a
///     model or migration written for its provider means the same here. SQL-shape tests, and fakeDb where keys are drawn;
///     no server. Provider-only: these are SQL Server's DDL. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Migrations/SqlServerMigrationsSqlGenerator.cs
/// </summary>
public class SqlServerModelOptionsTests
{
    public sealed class Blog
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public int Rank { get; set; }
        public string? Note { get; set; }
    }

    public sealed class Counter
    {
        public int Id { get; set; }
    }

    public sealed class HiLoRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class SequenceRow
    {
        public long Id { get; set; }
        public int Number { get; set; }
    }

    public sealed class Hot
    {
        public int Id { get; set; }
        public int Value { get; set; }
    }

    private sealed class OptionsContext(DbContextOptions options, Action<ModelBuilder> build) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => build(modelBuilder);
    }

    private static string Script(Action<ModelBuilder> build)
    {
        using var context = new OptionsContext(
            new DbContextOptionsBuilder().UseAdoNetSqlServer("Server=.;Database=x").ReplaceService<IModelCacheKeyFactory, PerContextModelCacheKey>().Options,
            build);
        return context.Database.GenerateCreateScript().ReplaceLineEndings("\n");
    }

    private static string Sql(params MigrationOperation[] operations)
    {
        using var context = new OptionsContext(new DbContextOptionsBuilder().UseAdoNetSqlServer("Server=.;Database=x").Options, _ => { });
        return string.Join("", context.GetService<IMigrationsSqlGenerator>().Generate(operations).Select(c => c.CommandText)).ReplaceLineEndings("\n");
    }

    [Fact]
    public void Keys_take_clustering_and_fill_factor()
    {
        var script = Script(m => m.Entity<Blog>().HasKey(b => b.Id).IsClustered(false).HasFillFactor(80));

        Assert.Contains("CONSTRAINT [PK_Blog] PRIMARY KEY NONCLUSTERED ([Id]) WITH (FILLFACTOR = 80)", script);
    }

    [Fact]
    public void Indexes_take_clustering_included_columns_and_options()
    {
        var script = Script(m => m.Entity<Blog>().HasIndex(b => b.Name)
            .IsClustered()
            .IncludeProperties(b => new { b.Title, b.Rank })
            .HasFillFactor(90)
            .IsCreatedOnline()
            .SortInTempDb()
            .UseDataCompression(DataCompressionType.Page));

        Assert.Contains(
            "CREATE CLUSTERED INDEX [IX_Blog_Name] ON [Blog] ([Name]) INCLUDE ([Title], [Rank]) "
            + "WITH (FILLFACTOR = 90, ONLINE = ON, SORT_IN_TEMPDB = ON, DATA_COMPRESSION = PAGE);",
            script);
    }

    /// <summary>Included columns are named by property and written by column, delimited however hostile the name.</summary>
    [Fact]
    public void Included_columns_follow_column_names_and_a_filter()
    {
        var script = Script(m =>
        {
            m.Entity<Blog>().Property(b => b.Title).HasColumnName("Ti]tle");
            m.Entity<Blog>().HasIndex(b => b.Name).IsUnique().HasFilter("[Rank] > 0").IncludeProperties("Title").UseDataCompression(DataCompressionType.Row);
        });

        Assert.Contains("CREATE UNIQUE INDEX [IX_Blog_Name] ON [Blog] ([Name]) INCLUDE ([Ti]]tle]) WHERE [Rank] > 0 WITH (DATA_COMPRESSION = ROW);", script);
    }

    [Fact]
    public void Sparse_columns_are_marked_sparse()
        => Assert.Contains("[Note] nvarchar(max) SPARSE,", Script(m => m.Entity<Blog>().Property(b => b.Note).IsSparse()));

    [Fact]
    public void An_identity_column_takes_its_seed_and_increment()
    {
        Assert.Contains("[Id] int IDENTITY(100, 5) NOT NULL", Script(m => m.Entity<Counter>().Property(c => c.Id).UseIdentityColumn(100, 5)));
        Assert.Contains("[Id] int IDENTITY(1000, 10) NOT NULL", Script(m => m.UseIdentityColumns(1000, 10).Entity<Counter>()));
        Assert.Contains("[Id] int IDENTITY NOT NULL", Script(m => m.Entity<Counter>()));
    }

    [Fact]
    public void A_hilo_key_is_no_identity_and_has_its_sequence()
    {
        var script = Script(m => m.Entity<HiLoRow>().Property(r => r.Id).UseHiLo("RowSeq", "app"));
        var modelWide = Script(m => m.UseHiLo().Entity<HiLoRow>());

        Assert.Contains("CREATE SEQUENCE [app].[RowSeq]", script);
        Assert.Contains("INCREMENT BY 10", script);
        Assert.Contains("[Id] int NOT NULL", script);
        Assert.DoesNotContain("IDENTITY", script);
        Assert.Contains("CREATE SEQUENCE [EntityFrameworkHiLoSequence]", modelWide);
        Assert.DoesNotContain("IDENTITY", modelWide);
    }

    [Fact]
    public void A_sequence_key_defaults_to_the_sequences_next_value()
    {
        var script = Script(m => m.Entity<SequenceRow>().Property(r => r.Id).UseSequence("Row]Ids", "app"));
        var modelWide = Script(m => m.UseKeySequences().Entity<SequenceRow>());

        Assert.Contains("CREATE SEQUENCE [app].[Row]]Ids]", script);
        Assert.Contains("[Id] bigint DEFAULT (NEXT VALUE FOR [app].[Row]]Ids]) NOT NULL", script);
        Assert.Contains("CREATE SEQUENCE [SequenceRowSequence]", modelWide);
        Assert.Contains("DEFAULT (NEXT VALUE FOR [SequenceRowSequence])", modelWide);
        Assert.DoesNotContain("IDENTITY", modelWide);
    }

    [Fact]
    public void A_memory_optimized_table_is_created_with_nonclustered_keys()
    {
        var script = Script(m => m.Entity<Hot>().IsMemoryOptimized().HasIndex(h => h.Value));

        Assert.Contains("CONSTRAINT [PK_Hot] PRIMARY KEY NONCLUSTERED ([Id])\n) WITH (MEMORY_OPTIMIZED = ON);", script);
        Assert.Contains("ALTER TABLE [Hot] ADD INDEX [IX_Hot_Value] NONCLUSTERED ([Value]);", script);
    }

    /// <summary>Operations a migration scaffolded by Microsoft's provider carries (its annotation names) mean the same here.</summary>
    [Fact]
    public void Migrations_written_for_microsofts_provider_keep_their_meaning()
    {
        var addColumn = new AddColumnOperation { Table = "T", Name = "Id", ClrType = typeof(int), ColumnType = "int" };
        addColumn.AddAnnotation("SqlServer:Identity", "10, 2");
        var sparse = new AddColumnOperation { Table = "T", Name = "Note", ClrType = typeof(string), ColumnType = "nvarchar(max)", IsNullable = true };
        sparse.AddAnnotation("SqlServer:Sparse", true);
        var index = new CreateIndexOperation { Table = "T", Name = "IX", Columns = ["A"] };
        index.AddAnnotation("SqlServer:Clustered", false);
        index.AddAnnotation("SqlServer:Include", new[] { "B" });
        index.AddAnnotation("SqlServer:FillFactor", 70);

        Assert.Equal("ALTER TABLE [T] ADD [Id] int IDENTITY(10, 2) NOT NULL;\n", Sql(addColumn));
        Assert.Equal("ALTER TABLE [T] ADD [Note] nvarchar(max) SPARSE;\n", Sql(sparse));
        Assert.Equal("CREATE NONCLUSTERED INDEX [IX] ON [T] ([A]) INCLUDE ([B]) WITH (FILLFACTOR = 70);\n", Sql(index));
    }

    [Fact]
    public void A_malformed_identity_annotation_is_refused()
    {
        var addColumn = new AddColumnOperation { Table = "T", Name = "Id", ClrType = typeof(int), ColumnType = "int" };
        addColumn.AddAnnotation("SqlServer:Identity", "1; DROP TABLE [T]");

        Assert.Throws<InvalidOperationException>(() => Sql(addColumn));
    }

    [Fact]
    public void Fill_factor_outside_0_to_100_is_refused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Script(m => m.Entity<Blog>().HasIndex(b => b.Name).HasFillFactor(101)));

    /// <summary>A hi-lo key draws one block from its sequence for many new rows, and the INSERT writes the key.</summary>
    [Fact]
    public void Hilo_keys_are_drawn_in_blocks_from_the_sequence()
    {
        var connection = new fakeDbConnection { ConnectionString = $"Data Source=fake;EmulatedProduct={SupportedDatabase.SqlServer}" };
        var commands = new List<string>();
        using var context = new OptionsContext(
            new DbContextOptionsBuilder()
                .UseAdoNetSqlServer(connection)
                .ReplaceService<IModelCacheKeyFactory, PerContextModelCacheKey>()
                .LogTo(m => commands.Add(m), [RelationalEventId.CommandExecuted])
                .Options,
            m => m.Entity<HiLoRow>().Property(r => r.Id).UseHiLo("RowSeq", "app"));
        connection.EnqueueScalarResult(41L);

        var first = new HiLoRow { Name = "a" };
        var second = new HiLoRow { Name = "b" };
        context.Add(first);
        context.Add(second);

        Assert.Equal(41, first.Id);
        Assert.Equal(42, second.Id);
        Assert.Single(commands, c => c.Contains("SELECT NEXT VALUE FOR [app].[RowSeq]", StringComparison.Ordinal));
    }

    [Fact]
    public void Sequence_keys_are_drawn_before_the_insert()
    {
        var connection = new fakeDbConnection { ConnectionString = $"Data Source=fake;EmulatedProduct={SupportedDatabase.SqlServer}" };
        using var context = new OptionsContext(
            new DbContextOptionsBuilder().UseAdoNetSqlServer(connection).ReplaceService<IModelCacheKeyFactory, PerContextModelCacheKey>().Options,
            m => m.Entity<SequenceRow>().Property(r => r.Id).UseSequence("RowIds"));
        connection.EnqueueScalarResult(7L);

        var row = new SequenceRow();
        context.Add(row);

        Assert.Equal(7L, row.Id);
    }

    /// <summary>Each test builds its own model.</summary>
    private sealed class PerContextModelCacheKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => new object();
    }
}
