using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.adonet.Testing;

/// <summary>
///     Each .NET member the core translates, run on the server and compared with what .NET computes. Projections go through
///     <c>Distinct</c>, which EF can't evaluate on the client, so an untranslated member fails instead of quietly passing.
/// </summary>
public abstract class TranslationConformanceTests<TFixture>(TFixture fixture) : IClassFixture<TFixture>, IAsyncLifetime
    where TFixture : ProviderFixture
{
    // Spaces to trim, LIKE wildcards (and SQL Server's [) and the usual escape character to match literally, mixed case.
    // The rating is no rounding midpoint: .NET rounds those to even, SQL away from zero. The time has a fraction of a second.
    private static readonly Blog Seeded = new()
    {
        Name = @"  Ab%_c\d[x]  ",
        Rating = 12.37m,
        IsActive = true,
        CreatedAt = new DateTime(2024, 3, 5, 6, 7, 8, 500, DateTimeKind.Unspecified),
        ExternalId = Guid.NewGuid()
    };

    protected TFixture Fixture { get; } = fixture;

    private static CancellationToken Ct
        => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        Fixture.SkipIfNotRunnable();

        await Fixture.ResetAsync();
        await using var context = Fixture.CreateContext();
        context.Add(new Blog
        {
            Name = Seeded.Name,
            Rating = Seeded.Rating,
            IsActive = Seeded.IsActive,
            CreatedAt = Seeded.CreatedAt,
            ExternalId = Seeded.ExternalId
        });
        await context.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;

    private async Task Server<T>(Expression<Func<Blog, T>> selector)
    {
        await using var context = Fixture.CreateContext();

        var actual = await context.Blogs.Select(selector).Distinct().SingleAsync(Ct);

        Assert.Equal(selector.Compile()(Seeded), actual);
    }

    private async Task Matches(Expression<Func<Blog, bool>> predicate)
    {
        await using var context = Fixture.CreateContext();

        Assert.Equal(predicate.Compile()(Seeded) ? 1 : 0, await context.Blogs.CountAsync(predicate, Ct));
    }

    [Fact]
    public Task Concatenation()
        => Server(b => b.Name + "!" + b.Name);

    [Fact]
    public async Task Case()
    {
        await Server(b => b.Name.ToUpper());
        await Server(b => b.Name.ToLower());
    }

    [Fact]
    public async Task Trimming()
    {
        await Server(b => b.Name.Trim());
        await Server(b => b.Name.TrimStart());
        await Server(b => b.Name.TrimEnd());
    }

    [Fact]
    public Task Length()
        => Server(b => b.Name.Length);

    [Fact]
    public async Task Substring()
    {
        await Server(b => b.Name.Substring(1));
        await Server(b => b.Name.Substring(3));
        await Server(b => b.Name.Substring(2, 3));
    }

    [Fact]
    public async Task IndexOf()
    {
        await Server(b => b.Name.IndexOf("b%"));
        await Server(b => b.Name.IndexOf("zz"));
        await Server(b => b.Name.IndexOf(""));
    }

    [Fact]
    public Task Replace()
        => Server(b => b.Name.Replace("b%", "-"));

    [Fact]
    public async Task Constant_patterns_match_wildcards_literally()
    {
        await Matches(b => b.Name.StartsWith("  Ab%"));
        await Matches(b => b.Name.StartsWith("  A_"));
        await Matches(b => b.Name.EndsWith(@"\d  "));
        await Matches(b => b.Name.Contains(@"%_c\"));
        await Matches(b => b.Name.Contains("b%c"));
        await Matches(b => b.Name.Contains(""));
        await Matches(b => b.Name.Contains("[x]"));
        await Matches(b => b.Name.EndsWith("[x]  "));
    }

    [Fact]
    public async Task Parameter_patterns_match_wildcards_literally()
    {
        foreach (var value in new[] { "  Ab%", "  A_", @"\d  ", @"%_c\", "b%c", "", "longer than the name itself" })
        {
            await Matches(b => b.Name.StartsWith(value));
            await Matches(b => b.Name.EndsWith(value));
            await Matches(b => b.Name.Contains(value));
        }
    }

    [Fact]
    public async Task Null_or_white_space()
    {
        await Matches(b => string.IsNullOrWhiteSpace(b.Name));
        await Matches(b => !string.IsNullOrWhiteSpace(b.Name));
    }

    [Fact]
    public async Task Math_on_decimals()
    {
        await Server(b => Math.Abs(-b.Rating));
        await Server(b => Math.Floor(b.Rating));
        await Server(b => Math.Ceiling(b.Rating));
        await Server(b => Math.Round(b.Rating));
        await Server(b => Math.Round(b.Rating, 1));
    }

    [Fact]
    public async Task Math_on_doubles()
    {
        await Server(b => Math.Floor((double)b.Rating));
        await Server(b => Math.Round((double)b.Rating, 1));
        await Server(b => Math.Pow((double)b.Rating, 2) > 153);
        await Server(b => Math.Sqrt((double)b.Rating) > 3.5);
    }

    [Fact]
    public async Task Date_parts()
    {
        await Server(b => b.CreatedAt.Year);
        await Server(b => b.CreatedAt.Month);
        await Server(b => b.CreatedAt.Day);
        await Server(b => b.CreatedAt.Hour);
        await Server(b => b.CreatedAt.Minute);
        await Server(b => b.CreatedAt.Second);
        await Matches(b => b.CreatedAt.Second == 8);
    }
}
