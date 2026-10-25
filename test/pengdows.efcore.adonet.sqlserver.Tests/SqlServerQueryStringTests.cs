using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.sqlserver.Tests;

/// <summary><c>ToQueryString</c> on SQL Server is runnable T-SQL: run on the server, it finds what the query finds.</summary>
public class SqlServerQueryStringTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private const string Hostile = "it's '' -- ; DROP TABLE [Samples]; /* \r\n \u0000 🚀 @p0 */";

    private static readonly byte[] Bytes = [0x00, 0xFF, 0x27, 0x5D];
    private static readonly Guid Id = Guid.Parse("8f14e45f-ceea-467f-a0e6-4f4c2b0a0c55");
    private static readonly DateTime At = new DateTime(2024, 2, 29, 23, 59, 59, 999).AddTicks(9999);
    private static readonly DateTimeOffset AtOffset = new(2024, 2, 29, 23, 59, 59, TimeSpan.FromHours(-9.5));

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Query_strings_run_on_the_server_and_find_the_same_rows()
    {
        fixture.SkipIfNotRunnable();
        await fixture.CreateTablesOnceAsync<SampleContext>(o => new(o));
        await using (var context = fixture.CreateContext<SampleContext>(o => new(o)))
        {
            context.Add(Expected());
            await context.SaveChangesAsync(Ct);
        }

        await using (var context = fixture.CreateContext<SampleContext>(o => new(o)))
        {
            var (text, ansi, fixedText, money, big, ratio, flag, id, at, atOffset, bytes) =
                (Hostile, "ascii", "abc", 12345678.1234567890m, long.MinValue, double.MaxValue, true, Id, At, AtOffset, Bytes);
            var query = context.Samples.Where(
                s => (s.Text == text) && (s.Ansi == ansi) && (s.FixedText == fixedText) && (s.Money == money) && (s.Big == big)
                    && (s.Ratio == ratio) && (s.Flag == flag) && (s.ExternalId == id) && (s.At == at) && (s.AtOffset == atOffset)
                    && (s.Bytes == bytes));

            Assert.Equal(await query.CountAsync(Ct), await CountRowsAsync(query.ToQueryString()));
            Assert.Equal(1, await query.CountAsync(Ct));
        }
    }

    private async Task<int> CountRowsAsync(string sql)
    {
        await using DbConnection connection = new Microsoft.Data.SqlClient.SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = 0;
        while (await reader.ReadAsync(Ct))
        {
            rows++;
        }

        return rows;
    }

    private static Sample Expected()
        => new()
        {
            Text = Hostile,
            Ansi = "ascii",
            FixedText = "abc",
            Money = 12345678.1234567890m,
            Big = long.MinValue,
            Ratio = double.MaxValue,
            Flag = true,
            ExternalId = Id,
            At = At,
            AtOffset = AtOffset,
            Bytes = Bytes
        };

    public class Sample
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";

        [MaxLength(20)]
        [Unicode(false)]
        public string Ansi { get; set; } = "";

        [MaxLength(3)]
        public string FixedText { get; set; } = "";

        public decimal Money { get; set; }
        public long Big { get; set; }
        public double Ratio { get; set; }
        public bool Flag { get; set; }
        public Guid ExternalId { get; set; }
        public DateTime At { get; set; }
        public DateTimeOffset AtOffset { get; set; }

        [MaxLength(4)]
        public byte[] Bytes { get; set; } = [];
    }

    public class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options)
    {
        public DbSet<Sample> Samples => Set<Sample>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Sample>(b =>
            {
                b.ToTable("QueryStringSamples");
                b.Property(s => s.FixedText).IsFixedLength();
                b.Property(s => s.Money).HasPrecision(28, 10);
                b.Property(s => s.At).HasColumnType("datetime2(7)");
            });
    }
}
