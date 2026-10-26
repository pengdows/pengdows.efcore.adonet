using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary>
///     A key or index column has a bounded type: SQL Server can't index nvarchar(max) or varbinary(max), and its index key
///     holds 900 bytes. Unsized strings that are keys or indexed are nvarchar(450) (or varchar(900), ANSI); binary,
///     varbinary(900). Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerTypeMappingSource.cs
/// </summary>
public class SqlServerKeyColumnTests
{
    [Fact]
    public void Key_and_index_columns_are_bounded()
    {
        using var context = new KeyedContext(new DbContextOptionsBuilder<KeyedContext>().UseAdoNetSqlServer("Server=localhost").Options);
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Keyed))!;
        string Column(string property)
            => entity.FindProperty(property)!.GetColumnType();

        Assert.Equal("nvarchar(450)", Column(nameof(Keyed.Code)));
        Assert.Equal("nvarchar(450)", Column(nameof(Keyed.Indexed)));
        Assert.Equal("varchar(900)", Column(nameof(Keyed.AnsiIndexed)));
        Assert.Equal("varbinary(900)", Column(nameof(Keyed.Hash)));
        Assert.Equal("nvarchar(max)", Column(nameof(Keyed.Plain)));
        Assert.Equal("nvarchar(20)", Column(nameof(Keyed.Sized)));
    }

    public class Keyed
    {
        [Key]
        public string Code { get; set; } = "";

        public string Indexed { get; set; } = "";
        public string AnsiIndexed { get; set; } = "";
        public byte[] Hash { get; set; } = [];
        public string Plain { get; set; } = "";

        [MaxLength(20)]
        public string Sized { get; set; } = "";
    }

    public class KeyedContext(DbContextOptions<KeyedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Keyed>(b =>
            {
                b.HasIndex(k => k.Indexed);
                b.HasIndex(k => k.AnsiIndexed);
                b.Property(k => k.AnsiIndexed).IsUnicode(false);
                b.HasIndex(k => k.Hash);
                b.HasIndex(k => k.Sized);
            });
    }
}
