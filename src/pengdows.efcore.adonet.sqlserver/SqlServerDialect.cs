using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SQL Server. Identifiers use ANSI double quotes, which needs QUOTED_IDENTIFIER ON (the SqlClient default). BIT is not a
///     predicate, paging uses TOP or OFFSET/FETCH, and keys come from <c>SCOPE_IDENTITY()</c> so tables with triggers work.
/// </summary>
public sealed class SqlServerDialect : AdoNetDialect
{
    public static readonly SqlServerDialect Instance = new();

    public override string Name => "SQL Server";
    public override ISqlDialect Sql { get; } = new SqlServerSql();
    public override ITypeMappingDialect TypeMappings { get; } = new SqlServerTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 42, MaxParametersPerCommand: 2000);

    public override void ConfigureServices(IServiceCollection services)
    {
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, SqlServerMigrationsSqlGenerator>();
        services.TryAddScoped<IAdoNetBulkLoader, SqlServerBulkLoader>();
    }
}

public sealed class SqlServerSql : AnsiSqlDialect
{
    public override string TrueLiteral => "CAST(1 AS BIT)";
    public override string FalseLiteral => "CAST(0 AS BIT)";
    public override string LongCountFunction => "COUNT_BIG";
    public override PagingSyntax Paging => PagingSyntax.TopOrOffsetFetch;
    public override BooleanSyntax Booleans => BooleanSyntax.BitValues;
    public override KeyRetrieval Keys { get; } = KeyRetrieval.SelectAfter("SCOPE_IDENTITY()", "@@ROWCOUNT");
    public override DmlSyntax Dml => DmlSyntax.TargetAliasFrom;
    public override FunctionSyntax Functions { get; } = new SqlServerFunctions();

    public override string StringLiteral(string value)
        => "N" + base.StringLiteral(value);
}

/// <summary>
///     LEN ignores trailing spaces and returns bigint for MAX types, so lengths are taken of the value plus one character.
///     CHARINDEX finds an empty string nowhere; .NET finds it at 0.
/// </summary>
public sealed class SqlServerFunctions : FunctionSyntax
{
    public override string Concat => "{0} + {1}";
    public override string? TrimStart => "LTRIM({0})";
    public override string? TrimEnd => "RTRIM({0})";
    public override string? Length => "CAST(LEN({0} + N'x') - 1 AS int)";
    public override string? Substring => "SUBSTRING({0}, {1}, {2})";
    public override string? SubstringFrom => "SUBSTRING({0}, {1}, LEN({0} + N'x'))";
    public override string? Position => "CAST(CASE WHEN DATALENGTH({0}) = 0 THEN 1 ELSE CHARINDEX({0}, {1}) END AS int)";
    public override string? Round => "ROUND({0}, 0)";
    public override string? Year => "DATEPART(year, {0})";
    public override string? Month => "DATEPART(month, {0})";
    public override string? Day => "DATEPART(day, {0})";
    public override string? Hour => "DATEPART(hour, {0})";
    public override string? Minute => "DATEPART(minute, {0})";
    public override string? Second => "DATEPART(second, {0})";
    public override string LikeSpecialCharacters => "%_[";
}

public sealed class SqlServerTypeMappings : AnsiTypeMappings
{
    private static readonly Dictionary<Type, string> StoreTypes = new()
    {
        [typeof(bool)] = "BIT",
        [typeof(byte)] = "TINYINT",
        [typeof(double)] = "FLOAT",
        [typeof(string)] = "NVARCHAR(MAX)",
        [typeof(DateTime)] = "DATETIME2",
        [typeof(DateTimeOffset)] = "DATETIMEOFFSET",
        [typeof(TimeSpan)] = "TIME",
        [typeof(Guid)] = "UNIQUEIDENTIFIER",
        [typeof(byte[])] = "VARBINARY(MAX)"
    };

    public override string? GetStoreType(Type clrType)
        => StoreTypes.GetValueOrDefault(clrType) ?? base.GetStoreType(clrType);

    public override string? GetStoreType(Type clrType, int? size, int? precision, int? scale)
        => clrType == typeof(string) && size != null ? (size <= 4000 ? $"NVARCHAR({size})" : "NVARCHAR(MAX)")
            : clrType == typeof(byte[]) && size != null ? (size <= 8000 ? $"VARBINARY({size})" : "VARBINARY(MAX)")
            : base.GetStoreType(clrType, size, precision, scale);
}
