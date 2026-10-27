using pengdows.efcore.adonet.flatfile;
using pengdows.efcore.adonet.mysql;
using pengdows.efcore.adonet.postgresql;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     Shared architecture contracts for the three primary provider packages. These tests intentionally inspect the
///     composition boundary, not provider-specific SQL text: SQL differences belong to <see cref="ISqlDialect" /> and
///     driver facts belong to <see cref="IDatabaseCapabilities" />.
/// </summary>
public sealed class ProviderDialectMatrixTests
{
    public static IEnumerable<object[]> PrimaryDialects()
    {
        yield return ["mysql", MySqlDialect.Instance];
        yield return ["postgresql", PostgreSqlDialect.Instance];
        yield return ["flatfile", FlatFileDialect.Instance];
    }

    [Theory]
    [MemberData(nameof(PrimaryDialects))]
    public void Primary_provider_is_composed_from_shared_contract(string name, IAdoNetDialect dialect)
    {
        Assert.Equal(name, name.ToLowerInvariant());
        Assert.False(string.IsNullOrWhiteSpace(dialect.Name));
        Assert.NotNull(dialect.Sql);
        Assert.NotNull(dialect.TypeMappings);
        Assert.NotNull(dialect.Capabilities);
        Assert.True(dialect.Capabilities.DefaultMaxBatchSize > 0);
        Assert.True(dialect.Capabilities.MaxParametersPerCommand > 0);
        Assert.True(dialect.Capabilities.MaxIdentifierLength > 0);
    }

    [Theory]
    [MemberData(nameof(PrimaryDialects))]
    public void Key_strategy_and_batching_are_coherent(string _, IAdoNetDialect dialect)
    {
        if (dialect.Sql.Keys is SelectAfterKeyRetrieval or DeltaTableKeyRetrieval)
        {
            Assert.True(dialect.Capabilities.SupportsMultipleStatementsPerCommand);
        }

        if (dialect.Sql.Keys is SequenceKeyRetrieval)
        {
            Assert.NotNull(dialect.Sql.Keys);
        }
    }

    [Fact]
    public void Primary_providers_keep_database_specific_features_in_the_sql_boundary()
    {
        Assert.Equal(":", FlatFileDialect.Instance.Sql.ParameterMarker);
        Assert.Equal("@", PostgreSqlDialect.Instance.Sql.ParameterMarker);
        Assert.Equal("@", MySqlDialect.Instance.Sql.ParameterMarker);

        Assert.NotNull(FlatFileDialect.Instance.Sql.Json);
        Assert.NotNull(PostgreSqlDialect.Instance.Sql.Json);
        Assert.NotNull(MySqlDialect.Instance.Sql.Json);
    }
}
