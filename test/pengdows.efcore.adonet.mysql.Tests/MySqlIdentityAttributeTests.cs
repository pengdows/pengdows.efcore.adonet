using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.mysql.Tests;

/// <summary>
///     [DatabaseGenerated(Identity)] on a date and time, with no fluent call: the server sets it on insert, as Pomelo's
///     convention does with the same attribute (DEFAULT CURRENT_TIMESTAMP(n), not AUTO_INCREMENT). The fluent
///     UseMySqlIdentityColumn does the same (MySqlMigrationApiTests). Model and SQL only; no connection is opened. Behaviour
///     reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Metadata/Conventions/MySqlValueGenerationStrategyConvention.cs
/// </summary>
public class MySqlIdentityAttributeTests
{
    public sealed class Stamp
    {
        public int Id { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedAt { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTimeOffset OffsetAt { get; set; }
    }

    public sealed class StampContext(DbContextOptions<StampContext> options) : DbContext(options)
    {
        public DbSet<Stamp> Stamps => Set<Stamp>();
    }

    private static StampContext Context()
        => new(new DbContextOptionsBuilder<StampContext>().AllowManyDialects().UseAdoNetMySql("Server=localhost").Options);

    [Fact]
    public void An_identity_date_and_time_attribute_defaults_to_the_current_timestamp()
    {
        using var context = Context();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("`CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),", script);
        Assert.Contains("`OffsetAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),", script);
    }

    [Fact]
    public void An_identity_date_and_time_attribute_is_the_identity_column_strategy()
    {
        using var context = Context();

        Assert.Equal(
            MySQLValueGenerationStrategy.IdentityColumn,
            context.Model.FindEntityType(typeof(Stamp))!.FindProperty(nameof(Stamp.CreatedAt))!.GetValueGenerationStrategy());
    }
}
