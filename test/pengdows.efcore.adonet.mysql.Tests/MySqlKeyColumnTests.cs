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
        using var context = new KeyedContext(new DbContextOptionsBuilder<KeyedContext>().AllowManyDialects().UseAdoNetMySql("Server=localhost").Options);
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

/// <summary>
///     MySQL counts every VARCHAR and VARBINARY byte toward a 65,535-byte row: a string longer than fits (16,383 characters at
///     utf8mb4's 4 bytes) is LONGTEXT, and binary of 8,000 bytes or more is LONGBLOB, as Pomelo maps them. Behaviour
///     reference: Pomelo's MySqlTypeMappingSource and MySqlByteArrayTypeMapping (adapted).
/// </summary>
public class MySqlLongColumnTests
{
    [Theory]
    [InlineData(typeof(string), 255, "VARCHAR(255)")]
    [InlineData(typeof(string), 16383, "VARCHAR(16383)")]
    [InlineData(typeof(string), 16384, "LONGTEXT")]
    [InlineData(typeof(byte[]), 7999, "VARBINARY(7999)")]
    [InlineData(typeof(byte[]), 8000, "LONGBLOB")]
    [InlineData(typeof(byte[]), 9000, "LONGBLOB")]
    public void Sized_columns_too_long_for_a_row_are_unbounded(Type clrType, int size, string expected)
        => Assert.Equal(expected, MySqlDialect.Instance.TypeMappings.GetStoreType(clrType, new StoreTypeFacets(Size: size)));
}

/// <summary>
///     MySQL has one datetime type, so a DateTime parameter binds as DbType.DateTime, as both MySQL EF providers bind it.
///     DbType.DateTime2 is right elsewhere, but Oracle's MySql.Data maps it to a BLOB and sends current-culture text
///     (UPSTREAM_ISSUES.md), so this keeps the dialect working with either driver.
/// </summary>
public class MySqlDateTimeParameterTests
{
    [Fact]
    public void A_date_time_parameter_binds_as_DateTime()
    {
        using var context = new pengdows.efcore.adonet.Testing.BlogContext(new DbContextOptionsBuilder<pengdows.efcore.adonet.Testing.BlogContext>().AllowManyDialects().UseAdoNetMySql("Server=localhost").Options);

        var mapping = context.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalTypeMappingSource>().FindMapping(typeof(DateTime))!;

        Assert.Equal(System.Data.DbType.DateTime, mapping.DbType);
    }
}
