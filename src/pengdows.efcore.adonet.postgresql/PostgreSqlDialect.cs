using System.Data;
using System.Globalization;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using pengdows.efcore.adonet.Bulk;

namespace pengdows.efcore.adonet.postgresql;

public sealed class PostgreSqlDialect : AdoNetDialect
{
    public static readonly PostgreSqlDialect Instance = new();

    private PostgreSqlDialect()
    {
    }

    public override string Name => "PostgreSQL";
    public override ISqlDialect Sql { get; } = new PostgreSqlSql();
    public override ITypeMappingDialect TypeMappings { get; } = new PostgreSqlTypeMappings();
    public override IDatabaseCapabilities Capabilities { get; } = new DatabaseCapabilities
    {
        SupportsMultipleStatementsPerCommand = true,
        DefaultMaxBatchSize = 128,
        MaxParametersPerCommand = 65535,
        // NAMEDATALEN - 1; longer names are truncated by the server, which can make two names one.
        MaxIdentifierLength = 63,
        // The driver enlists in a TransactionScope. Behaviour reference (not copied):
        // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/NpgsqlRelationalConnection.cs
        SupportsAmbientTransactions = true
    };

    public override void ConfigureServices(IServiceCollection services)
        => services.TryAddScoped<IAdoNetBulkLoader, PostgreSqlBulkLoader>();
}

internal sealed class PostgreSqlSql : AnsiSqlDialect
{
    // Through the postgres database; WITH (FORCE) ends other sessions (PostgreSQL 13+). Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/NpgsqlDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Server(
        "Database",
        "postgres",
        dropSql: ["DROP DATABASE {0} WITH (FORCE)"],
        clearPool: c => NpgsqlConnection.ClearPool((NpgsqlConnection)c));

    // The reserved words as Npgsql lists them, which is where the official provider takes them from too.
    private static readonly Lazy<HashSet<string>> ReservedWords = new(() =>
    {
        using var connection = new NpgsqlConnection();
        return connection.GetSchema("ReservedWords").Rows.Cast<DataRow>()
            .Select(r => (string)r["ReservedWord"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    });

    // Unquoted names fold to lower case, so a name needs quotes unless it is lower-case letters, digits, _ and $ (not
    // starting with a digit or $) and no reserved word. Follows npgsql/efcore.pg's NpgsqlSqlGenerationHelper (PostgreSQL License).
    public override bool RequiresQuoting(string identifier)
        => (identifier.Length == 0)
            || !(IsLowerAscii(identifier[0]) || (identifier[0] == '_'))
            || identifier.Skip(1).Any(c => !(IsLowerAscii(c) || char.IsAsciiDigit(c) || (c == '_') || (c == '$')))
            || ReservedWords.Value.Contains(identifier);

    private static bool IsLowerAscii(char c)
        => c is >= 'a' and <= 'z';

    public override PagingSyntax Paging { get; } = PagingSyntax.LimitOffset();
    public override FunctionSyntax Functions { get; } = new PostgreSqlFunctions();
    public override string CurrentSchema => "CURRENT_SCHEMA";

    // INFORMATION_SCHEMA.TABLES also lists the system catalogs.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' "
            + "AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')";
}

internal sealed class PostgreSqlTypeMappings : AnsiTypeMappings
{
    public override string? GetStoreType(Type clrType)
        => clrType == typeof(byte[]) ? "BYTEA" : base.GetStoreType(clrType);

    // X'..' is a bit string here; TIMESTAMP '...' would drop the offset; an interval in microseconds keeps sign and days.
    public override string FormatLiteral(object value, string storeType, string literal)
        => value switch
        {
            byte[] bytes => @"'\x" + Convert.ToHexString(bytes) + "'::bytea",
            DateTimeOffset offset => "TIMESTAMPTZ '" + offset.ToString(@"yyyy\-MM\-dd HH\:mm\:ss.FFFFFFzzz", CultureInfo.InvariantCulture) + "'",
            TimeSpan span => "INTERVAL '" + (span.Ticks / (TimeSpan.TicksPerMillisecond / 1000)).ToString(CultureInfo.InvariantCulture) + " microseconds'",
            _ => literal
        };

    // BYTEA takes no length.
    public override string? GetStoreType(Type clrType, StoreTypeFacets facets)
        => clrType == typeof(byte[]) ? null : base.GetStoreType(clrType, facets);
}

/// <summary>
///     ROUND with a number of places is only defined for numeric. A timestamptz holds an instant, whose parts EXTRACT takes in
///     the session's time zone; Npgsql reads it back at UTC, so its parts are taken at UTC.
/// </summary>
internal sealed class PostgreSqlFunctions : FunctionSyntax
{
    // ^ is exponentiation in PostgreSQL; # is exclusive or. Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Internal/NpgsqlQuerySqlGenerator.cs
    public override string? BitwiseAnd => "{0} & {1}";
    public override string? BitwiseOr => "{0} | {1}";
    public override string? BitwiseNot => "~{0}";
    public override string? ExclusiveOr => "{0} # {1}";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? OffsetDateTime => "({0} AT TIME ZONE 'UTC')";
    public override string? RoundDigits => "ROUND(CAST({0} AS numeric), {1})";
}
