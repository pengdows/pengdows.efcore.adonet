using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.sqlserver;

internal sealed class SqlServerFunctionsTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    IAdoNetDialect dialect) : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; }
        = [new SqlServerFunctionsTranslator(sqlExpressionFactory, typeMappingSource, SqlServerDialect.CompatibilityLevelOf(dialect))];
}

/// <summary>
///     <see cref="SqlServerDbFunctionsExtensions" /> in T-SQL. A function the database's compatibility level lacks is left
///     untranslated: the *FROMPARTS functions need 110 (SQL Server 2012), AT TIME ZONE 130 (SQL Server 2016). Behaviour
///     reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerFullTextSearchFunctionsTranslator.cs
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerFromPartsFunctionTranslator.cs
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerDataLengthFunctionTranslator.cs
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerDateTimeMethodTranslator.cs
/// </summary>
internal sealed class SqlServerFunctionsTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    int compatibilityLevel) : IMethodCallTranslator
{
    private static readonly Dictionary<string, (string Function, string StoreType)> FromParts = new(StringComparer.Ordinal)
    {
        [nameof(SqlServerDbFunctionsExtensions.DateFromParts)] = ("DATEFROMPARTS", "date"),
        [nameof(SqlServerDbFunctionsExtensions.DateTimeFromParts)] = ("DATETIMEFROMPARTS", "datetime"),
        [nameof(SqlServerDbFunctionsExtensions.DateTime2FromParts)] = ("DATETIME2FROMPARTS", "datetime2"),
        [nameof(SqlServerDbFunctionsExtensions.DateTimeOffsetFromParts)] = ("DATETIMEOFFSETFROMPARTS", "datetimeoffset"),
        [nameof(SqlServerDbFunctionsExtensions.SmallDateTimeFromParts)] = ("SMALLDATETIMEFROMPARTS", "smalldatetime"),
        [nameof(SqlServerDbFunctionsExtensions.TimeFromParts)] = ("TIMEFROMPARTS", "time")
    };

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(SqlServerDbFunctionsExtensions))
        {
            return null;
        }

        if (method.Name.StartsWith("DateDiff", StringComparison.Ordinal))
        {
            return DateDiff(method, arguments);
        }

        if (FromParts.TryGetValue(method.Name, out var fromParts))
        {
            return compatibilityLevel >= 110 ? FromPartsCall(method, arguments, fromParts.Function, fromParts.StoreType) : null;
        }

        return method.Name switch
        {
            nameof(SqlServerDbFunctionsExtensions.FreeText) => FullText("FREETEXT", arguments),
            nameof(SqlServerDbFunctionsExtensions.Contains) => FullText("CONTAINS", arguments),
            nameof(SqlServerDbFunctionsExtensions.IsDate) => sqlExpressionFactory.Convert(
                sqlExpressionFactory.Function("ISDATE", [arguments[1]], nullable: true, [true], typeof(int)),
                typeof(bool),
                typeMappingSource.FindMapping(typeof(bool))),
            nameof(SqlServerDbFunctionsExtensions.IsNumeric) => sqlExpressionFactory.Equal(
                sqlExpressionFactory.Function("ISNUMERIC", [arguments[1]], nullable: false, [false], typeof(int)),
                sqlExpressionFactory.Constant(1)),
            nameof(SqlServerDbFunctionsExtensions.DataLength) => DataLength(arguments[1]),
            nameof(SqlServerDbFunctionsExtensions.AtTimeZone) => compatibilityLevel >= 130 ? AtTimeZone(arguments[1], arguments[2]) : null,
            nameof(SqlServerDbFunctionsExtensions.PatIndex) => sqlExpressionFactory.Function(
                "PATINDEX", [arguments[1], arguments[2]], nullable: true, [true, true], typeof(long)),
            _ => null
        };
    }

    // FREETEXT and CONTAINS are search conditions over a full-text indexed column; the language is a constant LCID.
    private SqlExpression FullText(string function, IReadOnlyList<SqlExpression> arguments)
    {
        if (arguments[1] is not ColumnExpression column)
        {
            throw new InvalidOperationException(
                $"{function} searches a full-text indexed column; its first argument must be a property mapped to a column.");
        }

        var search = sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[2]);
        var language = arguments.Count == 4 ? ", LANGUAGE {2}" : "";
        SqlExpression[] operands = arguments.Count == 4 ? [column, search, arguments[3]] : [column, search];
        return sqlExpressionFactory.Condition(function + "({0}, {1}" + language + ")", operands, typeMappingSource.FindMapping(typeof(bool)))!;
    }

    // The unit is the method's suffix; the two values take one type mapping, as the core's DateDiff* do.
    private SqlExpression DateDiff(MethodInfo method, IReadOnlyList<SqlExpression> arguments)
    {
        var unit = method.Name["DateDiff".Length..].ToLowerInvariant();
        var mapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2]);
        return sqlExpressionFactory.Function(
            "DATEDIFF",
            [
                sqlExpressionFactory.Fragment(unit),
                sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping),
                sqlExpressionFactory.ApplyTypeMapping(arguments[2], mapping)
            ],
            nullable: true,
            [false, true, true],
            Nullable.GetUnderlyingType(method.ReturnType) ?? method.ReturnType,
            typeMappingSource.FindMapping(typeof(int)));
    }

    private SqlExpression FromPartsCall(MethodInfo method, IReadOnlyList<SqlExpression> arguments, string function, string storeType)
    {
        var parts = arguments.Skip(1).Select(a => sqlExpressionFactory.ApplyDefaultTypeMapping(a)).ToList();
        return sqlExpressionFactory.Function(
            function,
            parts,
            nullable: true,
            parts.Select(_ => true),
            method.ReturnType,
            typeMappingSource.FindMapping(method.ReturnType, storeType));
    }

    // DATALENGTH is bigint for a (max) type; the method returns int.
    private SqlExpression DataLength(SqlExpression argument)
    {
        argument = sqlExpressionFactory.ApplyDefaultTypeMapping(argument);
        var storeType = argument.TypeMapping!.StoreType;
        if (!storeType.EndsWith("(max)", StringComparison.OrdinalIgnoreCase))
        {
            return sqlExpressionFactory.Function("DATALENGTH", [argument], nullable: true, [true], typeof(int), typeMappingSource.FindMapping(typeof(int)));
        }

        return sqlExpressionFactory.Convert(
            sqlExpressionFactory.Function("DATALENGTH", [argument], nullable: true, [true], typeof(long)),
            typeof(int),
            typeMappingSource.FindMapping(typeof(int)));
    }

    // AT TIME ZONE keeps its operand's precision. A constant is typed with CAST, which AT TIME ZONE needs; any operand but a
    // column, parameter, CAST or function call is parenthesized, as AT TIME ZONE binds tighter than any operator.
    private SqlExpression AtTimeZone(SqlExpression operand, SqlExpression timeZone)
    {
        if (operand is SqlConstantExpression)
        {
            operand = sqlExpressionFactory.Convert(operand, operand.Type, operand.TypeMapping);
        }

        var simple = operand is ColumnExpression or SqlParameterExpression or SqlUnaryExpression { OperatorType: System.Linq.Expressions.ExpressionType.Convert }
            || ((operand is SqlFunctionExpression function) && !SqlTemplate.TryGetTemplate(function, out _));
        var mapping = operand.Type == typeof(DateTimeOffset)
            ? operand.TypeMapping
            : typeMappingSource.FindMapping(typeof(DateTimeOffset), "datetimeoffset", precision: operand.TypeMapping?.Precision);
        return sqlExpressionFactory.Call(
            simple ? "{0} AT TIME ZONE {1}" : "({0}) AT TIME ZONE {1}",
            [operand, sqlExpressionFactory.ApplyDefaultTypeMapping(timeZone)],
            typeof(DateTimeOffset),
            mapping)!;
    }
}

/// <summary>
///     Leaves <see cref="SqlServerDbFunctionsExtensions" /> calls to the translator even when every argument is a constant,
///     which EF would otherwise evaluate on the client, where they throw. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerEvaluatableExpressionFilter.cs
/// </summary>
internal sealed class SqlServerEvaluatableExpressionFilterPlugin : IEvaluatableExpressionFilterPlugin
{
    public bool IsEvaluatableExpression(System.Linq.Expressions.Expression expression)
        => expression is not System.Linq.Expressions.MethodCallExpression call
            || (call.Method.DeclaringType != typeof(SqlServerDbFunctionsExtensions));
}

internal sealed class SqlServerAggregateTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IAggregateMethodCallTranslatorPlugin
{
    public IEnumerable<IAggregateMethodCallTranslator> Translators { get; }
        = [new SqlServerStatisticsAggregateTranslator(sqlExpressionFactory, typeMappingSource)];
}

/// <summary>
///     STDEV, STDEVP, VAR and VARP over a group: a filtered group is CASE WHEN filter THEN value END, a distinct one DISTINCT.
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/Translators/SqlServerStatisticsAggregateMethodTranslator.cs
/// </summary>
internal sealed class SqlServerStatisticsAggregateTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IAggregateMethodCallTranslator
{
    public SqlExpression? Translate(
        MethodInfo method,
        EnumerableExpression source,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType != typeof(SqlServerDbFunctionsExtensions)) || (source.Selector is not SqlExpression value))
        {
            return null;
        }

        var function = method.Name switch
        {
            nameof(SqlServerDbFunctionsExtensions.StandardDeviationSample) => "STDEV",
            nameof(SqlServerDbFunctionsExtensions.StandardDeviationPopulation) => "STDEVP",
            nameof(SqlServerDbFunctionsExtensions.VarianceSample) => "VAR",
            nameof(SqlServerDbFunctionsExtensions.VariancePopulation) => "VARP",
            _ => null
        };
        if (function == null)
        {
            return null;
        }

        if (source.Predicate != null)
        {
            value = sqlExpressionFactory.Case([new CaseWhenClause(source.Predicate, value)], elseResult: null);
        }

        if (source.IsDistinct)
        {
            value = new DistinctExpression(value);
        }

        return sqlExpressionFactory.Function(function, [value], nullable: true, [false], typeof(double), typeMappingSource.FindMapping(typeof(double)));
    }
}
