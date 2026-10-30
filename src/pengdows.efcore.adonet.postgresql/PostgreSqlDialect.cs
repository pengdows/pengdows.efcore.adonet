using System.Data;
using System.Globalization;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using pengdows.efcore.adonet.Bulk;
using pengdows.efcore.adonet.Migrations;

namespace pengdows.efcore.adonet.postgresql;

public sealed class PostgreSqlDialect : AdoNetDialect
{
    public static readonly PostgreSqlDialect Instance = new();

    static PostgreSqlDialect()
        // PostgreSQL's infinity sentinels are not SQL values and would make EF's legal year-one
        // DateTime/DateTimeOffset values non-arithmetic (for example, MIN + 1 second remains -infinity).
        // Npgsql exposes this as an AppContext switch rather than a connection-string option in current releases.
        => AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

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
    {
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<IMigrationsSqlGenerator, PostgreSqlMigrationsSqlGenerator>();
        services.TryAddScoped<IAdoNetBulkLoader, PostgreSqlBulkLoader>();
        new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor, PostgreSqlIntervalReaderInterceptor>()
            .TryAdd<Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure.IProviderConventionSetBuilder, PostgreSqlConventionSetBuilder>()
            .TryAdd<Microsoft.EntityFrameworkCore.Storage.IRelationalTypeMappingSourcePlugin, PostgreSqlXidTypeMappingPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Storage.IRelationalTypeMappingSourcePlugin, PostgreSqlProviderTypeMappingPlugin>()
            .TryAdd<Microsoft.EntityFrameworkCore.Query.IMethodCallTranslatorPlugin, PostgreSqlDbFunctionsTranslatorPlugin>();
    }
}

/// <summary>PostgreSQL requires generated columns to use the SQL:2003 STORED form.</summary>
internal sealed class PostgreSqlMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : AdoNetMigrationsSqlGenerator(dependencies, dialect)
{
    // Every table has PostgreSQL's system columns (xmin, a row version); a model may map them, but no migration declares,
    // alters or drops one. Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Migrations/NpgsqlMigrationsSqlGenerator.cs
    private static readonly HashSet<string> SystemColumns = new(StringComparer.Ordinal) { "tableoid", "xmin", "cmin", "xmax", "cmax", "ctid" };

    protected override void Generate(CreateTableOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        operation.Columns.RemoveAll(c => SystemColumns.Contains(c.Name));
        base.Generate(operation, model, builder, terminate);
        if (operation.Comment is not null)
        {
            TableComment(operation.Name, operation.Schema, operation.Comment, builder);
        }

        foreach (var column in operation.Columns.Where(c => c.Comment is not null))
        {
            ColumnComment(operation.Name, operation.Schema, column.Name, column.Comment, builder);
        }
    }

    protected override void Generate(AddColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            base.Generate(operation, model, builder, terminate);
            if (operation.Comment is not null)
            {
                ColumnComment(operation.Table, operation.Schema, operation.Name, operation.Comment, builder);
            }
        }
    }

    protected override void Generate(AlterColumnOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            base.Generate(operation, model, builder);
            if (operation.Comment != operation.OldColumn.Comment)
            {
                ColumnComment(operation.Table, operation.Schema, operation.Name, operation.Comment, builder);
            }
        }
    }

    protected override void Generate(DropColumnOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!SystemColumns.Contains(operation.Name))
        {
            base.Generate(operation, model, builder, terminate);
        }
    }

    // A table comment is COMMENT ON TABLE; the core generator has no table comment, so it was dropped. Clearing one is IS NULL.
    protected override void Generate(AlterTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        TableComment(operation.Name, operation.Schema, operation.Comment, builder);
    }

    private void ColumnComment(string table, string? schema, string column, string? comment, MigrationCommandListBuilder builder)
    {
        builder
            .Append("COMMENT ON COLUMN ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema))
            .Append(".")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(column))
            .Append(" IS ")
            .Append(comment is null ? "NULL" : Dialect.Sql.StringLiteral(comment))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    private void TableComment(string table, string? schema, string? comment, MigrationCommandListBuilder builder)
    {
        builder
            .Append("COMMENT ON TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table, schema))
            .Append(" IS ")
            .Append(comment is null ? "NULL" : Dialect.Sql.StringLiteral(comment))
            .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
        EndStatement(builder);
    }

    protected override void ColumnDefinition(
        string? schema,
        string table,
        string name,
        ColumnOperation operation,
        IModel? model,
        MigrationCommandListBuilder builder)
    {
        base.ColumnDefinition(schema, table, name, operation, model, builder);
        if ((operation.ComputedColumnSql != null) && (operation.IsStored != true))
        {
            builder.Append(" STORED");
        }
    }
}

internal sealed class PostgreSqlSql : AnsiSqlDialect
{
    public override bool SupportsNullOrderingClauses => true;

    // Through the postgres database; WITH (FORCE) ends other sessions (PostgreSQL 13+). Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/NpgsqlDatabaseCreator.cs
    public override DatabaseLifecycle Databases { get; } = DatabaseLifecycle.Server(
        "Database",
        "postgres",
        dropSql: ["DROP DATABASE {0} WITH (FORCE)"]);

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
    public override JsonSyntax Json { get; } = PostgreSqlJson.Syntax;
    public override string CurrentSchema => "CURRENT_SCHEMA";
    // INFORMATION_SCHEMA.TABLES also lists the system catalogs.
    public override string HasTablesSql
        => "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' "
            + "AND TABLE_SCHEMA NOT IN ('pg_catalog', 'information_schema')";
}

internal sealed class PostgreSqlTypeMappings : AnsiTypeMappings
{
    // Npgsql writes timestamptz from offset-zero values only.
    public override bool WritesDateTimeOffsetAsUtc => true;

    // A uint row version is the xid every row's xmin system column holds (PostgreSqlRowVersionConvention names it).
    public override string? RowVersionStoreType(Type clrType)
        => clrType == typeof(uint) ? "xid" : null;

    // A timestamptz holds an instant: a DateTime (Npgsql writes it as UTC) as well as a DateTimeOffset.
    public override IReadOnlyList<Type> GetClrTypes(string storeTypeBase)
        => string.Equals(storeTypeBase, "TIMESTAMP WITH TIME ZONE", StringComparison.OrdinalIgnoreCase)
            ? [typeof(DateTimeOffset), typeof(DateTime)]
            : base.GetClrTypes(storeTypeBase);

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

    // btrim/ltrim/rtrim(string, characters) remove any of the characters. Behaviour reference (not copied):
    // https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlStringMethodTranslator.cs
    // NOW() is an instant shown in the session's time zone; AT TIME ZONE 'UTC' gives its UTC timestamp. Behaviour reference
    // (not copied): https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlDateTimeMemberTranslator.cs
    public override string? UtcNow => "(NOW() AT TIME ZONE 'UTC')";

    public override string? TrimCharacters => "BTRIM({0}, {1})";
    public override string? PadLeft => "CASE WHEN CHAR_LENGTH({0}) >= {1} THEN {0} ELSE LPAD({0}, {1}, {2}) END";
    public override string? PadRight => "CASE WHEN CHAR_LENGTH({0}) >= {1} THEN {0} ELSE RPAD({0}, {1}, {2}) END";
    public override string? TrimStartCharacters => "LTRIM({0}, {1})";
    public override string? TrimEndCharacters => "RTRIM({0}, {1})";

    // The % operator, as the database's EF Core provider writes it.
    public override string? Modulo => null;

    public override string? OffsetDateTime => "({0} AT TIME ZONE 'UTC')";
    public override string? RoundDigits => "ROUND(CAST({0} AS numeric), {1})";
    public override string? UnixSeconds => "FLOOR(EXTRACT(EPOCH FROM {0}))";
    public override string? UnixMilliseconds => "FLOOR(EXTRACT(EPOCH FROM {0}) * 1000)";

    // PostgreSQL's interval arithmetic preserves fractional amounts and is the ISO-shaped
    // equivalent of the date/time arithmetic exposed by the shared EF translator.
    public override string? AddYears => "({0} + ({1} * INTERVAL '1 year'))";
    public override string? AddMonths => "({0} + ({1} * INTERVAL '1 month'))";
    public override string? AddDays => "({0} + ({1} * INTERVAL '1 day'))";
    public override string? AddMicroseconds => "({0} + ({1} * INTERVAL '1 microsecond'))";

    public override string? DateDifference => "({0} - {1})";
    public override string? DateDifferenceTotalDays => "(EXTRACT(EPOCH FROM ({0} - {1})) / 86400.0)";
    public override string? TimeSpanTotalDays => "(EXTRACT(EPOCH FROM {0}) / 86400.0)";
    public override string? TimeOfDay => "CAST({0} AS time)";
    public override string? TimeSpanAdd => "({0} + {1})";
    public override string? TimeSpanSubtract => "({0} - {1})";
    public override string? TimeSpanHours => "EXTRACT(HOUR FROM {0})";
    public override string? TimeSpanMinutes => "EXTRACT(MINUTE FROM {0})";
    public override string? TimeSpanSeconds => "FLOOR(EXTRACT(SECOND FROM {0}))";
    public override string? TimeSpanMilliseconds => "FLOOR(EXTRACT(MILLISECONDS FROM {0}) % 1000)";
    public override string? TimeSpanMicroseconds => "EXTRACT(MICROSECONDS FROM {0}) % 1000";
    public override string? TimeSpanNanoseconds => "0";
}

internal static class PostgreSqlJson
{
    public static JsonSyntax Syntax { get; } = new(
            "jsonb",
            "(jsonb_path_query_first({0}, CAST({1} AS jsonpath)) #>> '{}')",
            "jsonb_path_query_first({0}, CAST({1} AS jsonpath))",
            "CAST((jsonb_path_query_first({0}, CAST({1} AS jsonpath)) #>> '{}') AS {2})",
            "CAST((jsonb_path_query_first({0}, CAST({1} AS jsonpath)) #>> '{}') AS boolean)",
            "decode((jsonb_path_query_first({0}, CAST({1} AS jsonpath)) #>> '{}'), 'base64')",
            "({0} || {1})",
            "jsonb_set({0}, string_to_array(ltrim(regexp_replace(regexp_replace({1}, '^\\$\\.?', ''), '\\[([0-9]+)\\]', '.\\1', 'g'), '.'), '.'), to_jsonb({2}), true)",
            "CAST({0} AS jsonb)")
    {
        UsesBinaryDocument = true,
        ScalarValue = "(jsonb_build_array({0}) -> 0)",
        NullValue = "CAST('null' AS jsonb)",
        // The shared query generator supplies the table alias after this expression. Wrapping the
        // set-returning function gives PostgreSQL stable column names matching the shared JSON AST.
        // jsonb_array_elements returns jsonb. Extract the scalar text before casting it to the
        // element mapping; leaving e.value uncast makes predicates compare jsonb to INTEGER,
        // BOOLEAN, etc., which PostgreSQL correctly rejects.
        ArrayElements = "(SELECT CAST(e.value #>> '{}' AS {3}) AS {2}, e.ordinality AS {1} FROM jsonb_array_elements({0}) WITH ORDINALITY AS e(value))",
        ArrayElementsTextType = "{0}",
        ArrayElementsBinaryValue = "decode({0}, 'base64')"
    };
}
