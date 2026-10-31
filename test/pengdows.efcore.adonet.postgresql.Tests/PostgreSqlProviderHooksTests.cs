using System.Data;
using System.Data.Common;
using pengdows.crud.fakeDb;

namespace pengdows.efcore.adonet.postgresql.Tests;

/// <summary>
///     The PostgreSQL behaviour the shared code asks for through dialect hooks, which the core guard keeps out of the core.
/// </summary>
public sealed class PostgreSqlProviderHooksTests
{
    [Fact]
    public void An_inet_parameter_is_cast_to_inet()
    {
        Assert.Equal("::inet", PostgreSqlDialect.Instance.Sql.ParameterCast("inet"));
        Assert.Equal("::inet", PostgreSqlDialect.Instance.Sql.ParameterCast("INET"));
    }

    [Fact]
    public void Other_parameters_get_no_cast()
    {
        Assert.Null(PostgreSqlDialect.Instance.Sql.ParameterCast("text"));
        Assert.Null(PostgreSqlDialect.Instance.Sql.ParameterCast("jsonb"));
    }

    [Fact]
    public void A_json_parameter_is_sent_as_jsonb()
    {
        var parameter = new NpgsqlLikeParameter();

        PostgreSqlDialect.Instance.Sql.Json!.ConfigureParameter!.Invoke(parameter);

        Assert.Equal(NpgsqlLikeType.Jsonb, parameter.NpgsqlDbType);
    }

    [Fact]
    public void A_parameter_without_an_NpgsqlDbType_is_left_alone()
    {
        var parameter = new fakeDbParameter();
        var before = parameter.DbType;

        PostgreSqlDialect.Instance.Sql.Json!.ConfigureParameter!.Invoke(parameter);

        Assert.Equal(before, parameter.DbType);
    }

    // Stands in for Npgsql's parameter: the same property name and an enum type, which is all the hook reads.
    private sealed class NpgsqlLikeParameter : fakeDbParameter
    {
        public NpgsqlLikeType NpgsqlDbType { get; set; }
    }

    private enum NpgsqlLikeType
    {
        Text,
        Jsonb
    }
}
