using System.Net;
using System.Net.NetworkInformation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     Npgsql's range, network address, pg_trgm and fuzzystrmatch translations: the types mapped to PostgreSQL's (int4range,
///     daterange, inet, cidr, macaddr), their operators and functions, and the trigram and fuzzy match functions. Provider-only:
///     each is PostgreSQL's. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlRangeTranslator.cs,
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlNetworkTranslator.cs
/// </summary>
public sealed class PostgreSqlRangeNetworkTests
{
    private sealed class Booking
    {
        public int Id { get; set; }
        public NpgsqlRange<int> Seats { get; set; }
        public NpgsqlRange<DateOnly> Days { get; set; }
        public IPAddress Address { get; set; } = IPAddress.Loopback;
        public NpgsqlCidr Network { get; set; }
        public PhysicalAddress Mac { get; set; } = PhysicalAddress.None;
        public string Name { get; set; } = "";
    }

    private sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Booking> Bookings => Set<Booking>();
    }

    private static Context Create()
        => new(new DbContextOptionsBuilder<Context>().UseAdoNetPostgreSql("Host=localhost").Options);

    [Fact]
    public void Range_and_network_types_are_postgresql_columns()
    {
        using var context = Create();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("\"Seats\" int4range NOT NULL", script);
        Assert.Contains("\"Days\" daterange NOT NULL", script);
        Assert.Contains("\"Address\" inet NOT NULL", script);
        Assert.Contains("\"Network\" cidr NOT NULL", script);
        Assert.Contains("\"Mac\" macaddr NOT NULL", script);
    }

    [Fact]
    public void Range_operators_and_bounds()
    {
        using var context = Create();
        var other = new NpgsqlRange<int>(1, 5);

        var sql = context.Bookings
            .Where(b => b.Seats.Contains(3) && b.Seats.Overlaps(other) && b.Seats.IsAdjacentTo(other) && !b.Seats.IsEmpty)
            .Select(b => new { b.Seats.LowerBound, Upper = b.Seats.UpperBound, Merged = b.Seats.Merge(other), Both = b.Seats.Intersect(other) })
            .ToQueryString();

        Assert.Contains("b.\"Seats\" @> 3", sql);
        Assert.Contains("b.\"Seats\" && @other", sql);
        Assert.Contains("b.\"Seats\" -|- @other", sql);
        Assert.Contains("NOT (isempty(b.\"Seats\"))", sql);
        Assert.Contains("lower(b.\"Seats\")", sql);
        Assert.Contains("upper(b.\"Seats\")", sql);
        Assert.Contains("range_merge(b.\"Seats\", @other)", sql);
        Assert.Contains("b.\"Seats\" * @other", sql);
    }

    [Fact]
    public void A_range_binds_as_its_range_type_and_writes_a_literal()
    {
        using var context = Create();
        var source = context.GetService<IRelationalTypeMappingSource>();
        var ints = source.FindMapping(typeof(NpgsqlRange<int>))!;
        var dates = source.FindMapping(typeof(NpgsqlRange<DateOnly>))!;
        using var command = new NpgsqlCommand();

        var parameter = (NpgsqlParameter)ints.CreateParameter(command, "p", new NpgsqlRange<int>(1, 5));

        Assert.Equal(NpgsqlDbType.Range | NpgsqlDbType.Integer, parameter.NpgsqlDbType);
        Assert.Equal("'[\"1\",\"5\"]'::int4range", ints.GenerateSqlLiteral(new NpgsqlRange<int>(1, 5)));
        Assert.Equal("'empty'::int4range", ints.GenerateSqlLiteral(NpgsqlRange<int>.Empty));
        Assert.Equal(
            "'[\"2026-01-02\",)'::daterange",
            dates.GenerateSqlLiteral(new NpgsqlRange<DateOnly>(new DateOnly(2026, 1, 2), true, false, default, false, true)));
    }

    [Fact]
    public void Network_operators_and_functions()
    {
        using var context = Create();
        var network = new NpgsqlInet("10.0.0.0/8");

        var sql = context.Bookings
            .Where(b => EF.Functions.ContainedBy(b.Address, network) && EF.Functions.ContainsOrEqual(b.Network, b.Address))
            .Select(b => new
            {
                Family = EF.Functions.Family(b.Address),
                Mask = EF.Functions.MaskLength(b.Address),
                Host = EF.Functions.Host(b.Address),
                Abbrev = EF.Functions.Abbreviate(b.Network),
                Not = EF.Functions.BitwiseNot(b.Mac),
                Truncated = EF.Functions.Truncate(b.Mac)
            })
            .ToQueryString();

        Assert.Contains("b.\"Address\" << @network", sql);
        Assert.Contains("b.\"Network\" >>= b.\"Address\"", sql);
        Assert.Contains("family(b.\"Address\")", sql);
        Assert.Contains("masklen(b.\"Address\")", sql);
        Assert.Contains("host(b.\"Address\")", sql);
        Assert.Contains("abbrev(b.\"Network\")", sql);
        Assert.Contains("~b.\"Mac\"", sql);
        Assert.Contains("trunc(b.\"Mac\")", sql);
    }

    [Fact]
    public void An_address_binds_as_inet()
    {
        using var context = Create();
        var mapping = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(IPAddress))!;
        using var command = new NpgsqlCommand();

        var parameter = (NpgsqlParameter)mapping.CreateParameter(command, "p", IPAddress.Parse("::1"));

        Assert.Equal(NpgsqlDbType.Inet, parameter.NpgsqlDbType);
        Assert.Equal("'::1'::inet", mapping.GenerateSqlLiteral(IPAddress.IPv6Loopback));
    }

    [Fact]
    public void Trigram_functions_and_operators()
    {
        using var context = Create();

        var sql = context.Bookings
            .Where(b => EF.Functions.TrigramsAreSimilar(b.Name, "jon"))
            .OrderBy(b => EF.Functions.TrigramsSimilarityDistance(b.Name, "jon"))
            .Select(b => new { Similarity = EF.Functions.TrigramsSimilarity(b.Name, "jon"), Trigrams = EF.Functions.TrigramsShow(b.Name) })
            .ToQueryString();

        Assert.Contains("b.\"Name\" % 'jon'", sql);
        Assert.Contains("ORDER BY b.\"Name\" <-> 'jon'", sql);
        Assert.Contains("similarity(b.\"Name\", 'jon')", sql);
        Assert.Contains("show_trgm(b.\"Name\")", sql);
    }

    [Fact]
    public void Fuzzy_string_match_functions()
    {
        using var context = Create();

        var sql = context.Bookings
            .Select(b => new
            {
                Soundex = EF.Functions.FuzzyStringMatchSoundex(b.Name),
                Distance = EF.Functions.FuzzyStringMatchLevenshtein(b.Name, "john", 1, 2, 3),
                Near = EF.Functions.FuzzyStringMatchLevenshteinLessEqual(b.Name, "john", 2),
                Metaphone = EF.Functions.FuzzyStringMatchDoubleMetaphone(b.Name)
            })
            .ToQueryString();

        Assert.Contains("soundex(b.\"Name\")", sql);
        Assert.Contains("levenshtein(b.\"Name\", 'john', 1, 2, 3)", sql);
        Assert.Contains("levenshtein_less_equal(b.\"Name\", 'john', 2)", sql);
        Assert.Contains("dmetaphone(b.\"Name\")", sql);
    }
}
