using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.Infrastructure;
using pengdows.efcore.adonet.duckdb;
using pengdows.efcore.adonet.flatfile;
using pengdows.efcore.adonet.mysql;
using pengdows.efcore.adonet.postgresql;
using pengdows.efcore.adonet.sqlite;
using pengdows.efcore.adonet.sqlserver;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     The null-ordering option with each provider's real dialect: the four whose engines take NULLS FIRST / NULLS LAST write
///     them into every ORDER BY, and SQL Server and MySQL, which don't, refuse the option when the context starts.
///     Runs on: PostgreSQL, DuckDB, SQLite, FlatFile (SQL shape); SQL Server and MySQL (refusal). No database is contacted.
/// </summary>
public sealed class NullOrderingProviderSqlTests
{
    public class Person
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class PeopleContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Person> People => Set<Person>();
    }

    private static string Sql(Func<DbContextOptionsBuilder, Action<AdoNetDbContextOptionsBuilder>, DbContextOptionsBuilder> use)
    {
        var builder = new DbContextOptionsBuilder<PeopleContext>();
        use(builder, o => o.ReverseNullOrdering());
        using var context = new PeopleContext(builder.Options);
        return context.People.OrderBy(p => p.Name).ThenByDescending(p => p.Id).ToQueryString().ReplaceLineEndings("\n");
    }

    [Fact]
    public void PostgreSQL_says_where_nulls_sort()
        => Assert.Contains(@"NULLS FIRST", Sql((b, o) => b.UseAdoNetPostgreSql("Host=localhost", o)));

    [Fact]
    public void DuckDB_says_where_nulls_sort()
        => Assert.Contains(@"NULLS FIRST", Sql((b, o) => b.UseAdoNetDuckDb("Data Source=:memory:", o)));

    [Fact]
    public void SQLite_says_where_nulls_sort()
        => Assert.Contains(@"NULLS FIRST", Sql((b, o) => b.UseAdoNetSqlite("Data Source=:memory:", o)));

    [Fact]
    public void FlatFile_says_where_nulls_sort()
        => Assert.Contains(@"NULLS FIRST", Sql((b, o) => b.UseAdoNetFlatFile("path=" + Path.GetTempPath(), o)));

    [Fact]
    public void Every_provider_that_takes_the_clauses_writes_both_directions()
    {
        foreach (var sql in new[]
                 {
                     Sql((b, o) => b.UseAdoNetPostgreSql("Host=localhost", o)),
                     Sql((b, o) => b.UseAdoNetDuckDb("Data Source=:memory:", o)),
                     Sql((b, o) => b.UseAdoNetSqlite("Data Source=:memory:", o)),
                     Sql((b, o) => b.UseAdoNetFlatFile("path=" + Path.GetTempPath(), o))
                 })
        {
            Assert.Contains("NULLS FIRST", sql);
            Assert.Contains("DESC NULLS LAST", sql);
        }
    }

    [Fact]
    public void SQL_Server_refuses_the_option()
        => Assert.Throws<NotSupportedException>(() => Sql((b, o) => b.UseAdoNetSqlServer("Server=localhost", o)));

    [Fact]
    public void MySQL_refuses_the_option()
        => Assert.Throws<NotSupportedException>(() => Sql((b, o) => b.UseAdoNetMySql("Server=localhost", o)));
}
