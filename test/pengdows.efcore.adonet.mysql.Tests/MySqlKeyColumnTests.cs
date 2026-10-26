using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     A key or index column has a bounded type: MySQL can't index TEXT or BLOB without a prefix length, and InnoDB's index
///     key holds 3072 bytes. Unsized strings that are keys or indexed are varchar(255) (half the key at 4 bytes a character,
///     at most 255); binary, varbinary(3072). Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Storage/Internal/MySqlTypeMappingSource.cs
/// </summary>
public class MySqlKeyColumnTests
{
    [Fact]
    public void Key_and_index_columns_are_bounded()
    {
        using var context = new KeyedContext(new DbContextOptionsBuilder<KeyedContext>().UseAdoNetMySql("Server=localhost").Options);
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Keyed))!;
        string Column(string property)
            => entity.FindProperty(property)!.GetColumnType();

        Assert.Equal("VARCHAR(255)", Column(nameof(Keyed.Code)));
        Assert.Equal("VARCHAR(255)", Column(nameof(Keyed.Indexed)));
        Assert.Equal("VARCHAR(255)", Column(nameof(Keyed.AnsiIndexed)));
        Assert.Equal("VARBINARY(3072)", Column(nameof(Keyed.Hash)));
        Assert.Equal("LONGTEXT", Column(nameof(Keyed.Plain)));
        Assert.Equal("VARCHAR(20)", Column(nameof(Keyed.Sized)));
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
