using System.Globalization;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>Provider-neutral raw JSON fixtures for relational JSON tests.</summary>
internal static class JsonFixtureSeeds
{
    public static Task Seed30028<THost>(DbContext context) where THost : IDatabaseHost, new()
        => InsertAsync<THost>(context, "Entities", ["Id", "Json"],
        [
            ["1", "{\"RootName\":\"e1\",\"Collection\":[{\"BranchName\":\"e1 c1\",\"Nested\":{\"LeafName\":\"e1 c1 l\"}},{\"BranchName\":\"e1 c2\",\"Nested\":{\"LeafName\":\"e1 c2 l\"}}],\"OptionalReference\":{\"BranchName\":\"e1 or\",\"Nested\":{\"LeafName\":\"e1 or l\"}},\"RequiredReference\":{\"BranchName\":\"e1 rr\",\"Nested\":{\"LeafName\":\"e1 rr l\"}}}"],
            ["2", "{\"RootName\":\"e2\",\"OptionalReference\":{\"BranchName\":\"e2 or\",\"Nested\":{\"LeafName\":\"e2 or l\"}},\"RequiredReference\":{\"BranchName\":\"e2 rr\",\"Nested\":{\"LeafName\":\"e2 rr l\"}}}"],
            ["3", "{\"RootName\":\"e3\",\"Collection\":[{\"BranchName\":\"e3 c1\",\"Nested\":{\"LeafName\":\"e3 c1 l\"}},{\"BranchName\":\"e3 c2\",\"Nested\":{\"LeafName\":\"e3 c2 l\"}}],\"RequiredReference\":{\"BranchName\":\"e3 rr\",\"Nested\":{\"LeafName\":\"e3 rr l\"}}}"],
            ["4", "{\"RootName\":\"e4\",\"Collection\":[{\"BranchName\":\"e4 c1\",\"Nested\":{\"LeafName\":\"e4 c1 l\"}},{\"BranchName\":\"e4 c2\",\"Nested\":{\"LeafName\":\"e4 c2 l\"}}],\"OptionalReference\":{\"BranchName\":\"e4 or\",\"Nested\":{\"LeafName\":\"e4 or l\"}}}"]
        ]);

    public static Task Seed33046<THost>(DbContext context) where THost : IDatabaseHost, new()
        => InsertAsync<THost>(context, "Reviews", ["Rounds", "Id"], [[
            "[{\"RoundNumber\":11,\"SubRounds\":[{\"SubRoundNumber\":111},{\"SubRoundNumber\":112}]}]", "1"
        ]]);

    public static Task SeedJunkInJson<THost>(DbContext context) where THost : IDatabaseHost, new()
        => InsertAsync<THost>(context, "Entities", ["Collection", "CollectionWithCtor", "Reference", "ReferenceWithCtor", "Id"], [[
            "[{\"JunkReference\":{\"Something\":\"SomeValue\"},\"Name\":\"c11\",\"JunkProperty1\":50,\"Number\":11.5,\"JunkCollection1\":[],\"JunkCollection2\":[{\"Foo\":\"junk value\"}],\"NestedCollection\":[{\"DoB\":\"2002-04-01T00:00:00\",\"DummyProp\":\"Dummy value\"},{\"DoB\":\"2002-04-02T00:00:00\",\"DummyReference\":{\"Foo\":5}}],\"NestedReference\":{\"DoB\":\"2002-03-01T00:00:00\"}},{\"Name\":\"c12\",\"Number\":12.5,\"NestedCollection\":[{\"DoB\":\"2002-06-01T00:00:00\"},{\"DoB\":\"2002-06-02T00:00:00\"}],\"NestedDummy\":59,\"NestedReference\":{\"DoB\":\"2002-05-01T00:00:00\"}}]",
            "[{\"MyBool\":true,\"Name\":\"c11 ctor\",\"JunkReference\":{\"Something\":\"SomeValue\",\"JunkCollection\":[{\"Foo\":\"junk value\"}]},\"NestedCollection\":[{\"DoB\":\"2002-08-01T00:00:00\"},{\"DoB\":\"2002-08-02T00:00:00\"}],\"NestedReference\":{\"DoB\":\"2002-07-01T00:00:00\"}},{\"MyBool\":false,\"Name\":\"c12 ctor\",\"NestedCollection\":[{\"DoB\":\"2002-10-01T00:00:00\"},{\"DoB\":\"2002-10-02T00:00:00\"}],\"JunkCollection\":[{\"Foo\":\"junk value\"}],\"NestedReference\":{\"DoB\":\"2002-09-01T00:00:00\"}}]",
            "{\"Name\":\"r1\",\"JunkCollection\":[{\"Foo\":\"junk value\"}],\"JunkReference\":{\"Something\":\"SomeValue\"},\"Number\":1.5,\"NestedCollection\":[{\"DoB\":\"2000-02-01T00:00:00\",\"JunkReference\":{\"Something\":\"SomeValue\"}},{\"DoB\":\"2000-02-02T00:00:00\"}],\"NestedReference\":{\"DoB\":\"2000-01-01T00:00:00\"}}",
            "{\"MyBool\":true,\"JunkCollection\":[{\"Foo\":\"junk value\"}],\"Name\":\"r1 ctor\",\"JunkReference\":{\"Something\":\"SomeValue\"},\"NestedCollection\":[{\"DoB\":\"2001-02-01T00:00:00\"},{\"DoB\":\"2001-02-02T00:00:00\"}],\"NestedReference\":{\"JunkCollection\":[{\"Foo\":\"junk value\"}],\"DoB\":\"2001-01-01T00:00:00\"}}",
            "1"
        ]]);

    public static Task SeedTrickyBuffering<THost>(DbContext context) where THost : IDatabaseHost, new()
        => InsertAsync<THost>(context, "Entities", ["Reference", "Id"], [[
            "{\"Name\":\"r1\",\"Number\":7,\"JunkReference\":{\"Something\":\"SomeValue\"},\"JunkCollection\":[{\"Foo\":\"junk value\"}],\"NestedReference\":{\"DoB\":\"2000-01-01T00:00:00\"},\"NestedCollection\":[{\"DoB\":\"2000-02-01T00:00:00\",\"JunkReference\":{\"Something\":\"SomeValue\"}},{\"DoB\":\"2000-02-02T00:00:00\"}]}", "1"
        ]]);

    public static Task SeedShadowProperties<THost>(DbContext context) where THost : IDatabaseHost, new()
        => InsertAsync<THost>(context, "Entities", ["Collection", "CollectionWithCtor", "Reference", "ReferenceWithCtor", "Id", "Name"], [[
            "[{\"Name\":\"e1_c1\",\"ShadowDouble\":5.5},{\"ShadowDouble\":20.5,\"Name\":\"e1_c2\"}]",
            "[{\"Name\":\"e1_c1 ctor\",\"ShadowNullableByte\":6},{\"ShadowNullableByte\":null,\"Name\":\"e1_c2 ctor\"}]",
            "{\"Name\":\"e1_r\",\"ShadowString\":\"Foo\"}", "{\"ShadowInt\":143,\"Name\":\"e1_r ctor\"}", "1", "e1"
        ]]);

    public static async Task SeedNotICollection<THost>(DbContext context) where THost : IDatabaseHost, new()
    {
        await InsertAsync<THost>(context, "Entities", ["Json", "Id"], [["{\"Collection\":[{\"Bar\":11,\"Foo\":\"c11\"},{\"Bar\":12,\"Foo\":\"c12\"},{\"Bar\":13,\"Foo\":\"c13\"}]}", "1"]]);
        await InsertAsync<THost>(context, "Entities", ["Json", "Id"], [["{\"Collection\":[{\"Bar\":21,\"Foo\":\"c21\"},{\"Bar\":22,\"Foo\":\"c22\"}]}", "2"]]);
    }

    private static Task InsertAsync<THost>(DbContext context, string table, string[] columns, string[][] rows)
        where THost : IDatabaseHost, new()
    {
        var host = new THost();
        var sql = $"INSERT INTO {Quote(host, table)} ({string.Join(", ", columns.Select(c => Quote(host, c)))}) VALUES "
            + string.Join(", ", rows.Select(row => "(" + string.Join(", ", row.Select(value =>
                decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                    ? value
                    : host.Dialect.Sql.StringLiteral(value))) + ")"));
        // ExecuteSqlRaw uses composite-format processing even with no parameters;
        // JSON object braces therefore need escaping before it receives the SQL.
        return context.Database.ExecuteSqlRawAsync(sql.Replace("{", "{{").Replace("}", "}}"));
    }

    private static string Quote(IDatabaseHost host, string identifier)
        => host.Dialect.Sql.IdentifierQuotePrefix + identifier + host.Dialect.Sql.IdentifierQuoteSuffix;
}
