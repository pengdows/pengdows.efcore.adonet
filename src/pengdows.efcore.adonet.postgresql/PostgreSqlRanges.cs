using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL's range operators and functions over <see cref="NpgsqlRange{T}" /> (int4range, int8range, numrange, tsrange,
///     daterange), as Npgsql's <c>NpgsqlRangeDbFunctionsExtensions</c> has them; each throws when called outside a query. A range's
///     bounds and flags (<c>LowerBound</c>, <c>IsEmpty</c>, ...) are translated too. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlRangeDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlRangeDbFunctionsExtensions
{
    /// <summary>Whether the range holds <paramref name="value" /> (<c>@&gt;</c>).</summary>
    public static bool Contains<T>(this NpgsqlRange<T> range, T value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>Whether <paramref name="a" /> holds <paramref name="b" /> (<c>@&gt;</c>).</summary>
    public static bool Contains<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>Whether <paramref name="a" /> is within <paramref name="b" /> (<c>&lt;@</c>).</summary>
    public static bool ContainedBy<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ContainedBy)));

    /// <summary>Whether the ranges share a point (<c>&amp;&amp;</c>).</summary>
    public static bool Overlaps<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Overlaps)));

    /// <summary>Whether <paramref name="a" /> ends before <paramref name="b" /> starts (<c>&lt;&lt;</c>).</summary>
    public static bool IsStrictlyLeftOf<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsStrictlyLeftOf)));

    /// <summary>Whether <paramref name="a" /> starts after <paramref name="b" /> ends (<c>&gt;&gt;</c>).</summary>
    public static bool IsStrictlyRightOf<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsStrictlyRightOf)));

    /// <summary>Whether <paramref name="a" /> starts no earlier than <paramref name="b" /> (<c>&amp;&gt;</c>).</summary>
    public static bool DoesNotExtendLeftOf<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DoesNotExtendLeftOf)));

    /// <summary>Whether <paramref name="a" /> ends no later than <paramref name="b" /> (<c>&amp;&lt;</c>).</summary>
    public static bool DoesNotExtendRightOf<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(DoesNotExtendRightOf)));

    /// <summary>Whether the ranges meet without overlapping (<c>-|-</c>).</summary>
    public static bool IsAdjacentTo<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsAdjacentTo)));

    /// <summary>The union of two overlapping or adjacent ranges (<c>+</c>).</summary>
    public static NpgsqlRange<T> Union<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Union)));

    /// <summary>The intersection (<c>*</c>).</summary>
    public static NpgsqlRange<T> Intersect<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Intersect)));

    /// <summary>The difference, which must be one range (<c>-</c>).</summary>
    public static NpgsqlRange<T> Except<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Except)));

    /// <summary>The smallest range holding both (<c>range_merge</c>).</summary>
    public static NpgsqlRange<T> Merge<T>(this NpgsqlRange<T> a, NpgsqlRange<T> b)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Merge)));
}

/// <summary>The range types Npgsql reads and binds, with literals written the way PostgreSQL reads them.</summary>
internal static class PostgreSqlRangeTypes
{
    public static IEnumerable<PostgreSqlProviderTypeMapping> Mappings
        =>
        [
            Range<int>("int4range", NpgsqlDbType.Integer),
            Range<long>("int8range", NpgsqlDbType.Bigint),
            Range<decimal>("numrange", NpgsqlDbType.Numeric),
            Range<DateTime>("tsrange", NpgsqlDbType.Timestamp),
            Range<DateOnly>("daterange", NpgsqlDbType.Date)
        ];

    private static PostgreSqlProviderTypeMapping Range<T>(string storeType, NpgsqlDbType element)
        => new(typeof(NpgsqlRange<T>), storeType, NpgsqlDbType.Range | element, value => Text((NpgsqlRange<T>)value));

    // [lower,upper) with each bound quoted; an infinite bound is empty.
    private static string Text<T>(NpgsqlRange<T> range)
    {
        if (range.IsEmpty)
        {
            return "empty";
        }

        return (range.LowerBoundIsInclusive ? "[" : "(")
            + (range.LowerBoundInfinite ? "" : Bound(range.LowerBound))
            + ","
            + (range.UpperBoundInfinite ? "" : Bound(range.UpperBound))
            + (range.UpperBoundIsInclusive ? "]" : ")");
    }

    private static string Bound<T>(T value)
        => "\""
            + value switch
            {
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
                DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value!.ToString()
            }
            + "\"";
}

/// <summary>
///     Translates <see cref="PostgreSqlRangeDbFunctionsExtensions" />. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlRangeTranslator.cs
/// </summary>
internal sealed class PostgreSqlRangeTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator, IMemberTranslator
{
    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlRangeDbFunctionsExtensions.Contains)] = "@>",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.ContainedBy)] = "<@",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.Overlaps)] = "&&",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.IsStrictlyLeftOf)] = "<<",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.IsStrictlyRightOf)] = ">>",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.DoesNotExtendLeftOf)] = "&>",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.DoesNotExtendRightOf)] = "&<",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.IsAdjacentTo)] = "-|-",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.Union)] = "+",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.Intersect)] = "*",
        [nameof(PostgreSqlRangeDbFunctionsExtensions.Except)] = "-"
    };

    private static readonly Dictionary<string, string> Members = new(StringComparer.Ordinal)
    {
        [nameof(NpgsqlRange<int>.LowerBound)] = "lower",
        [nameof(NpgsqlRange<int>.UpperBound)] = "upper",
        [nameof(NpgsqlRange<int>.IsEmpty)] = "isempty",
        [nameof(NpgsqlRange<int>.LowerBoundIsInclusive)] = "lower_inc",
        [nameof(NpgsqlRange<int>.UpperBoundIsInclusive)] = "upper_inc",
        [nameof(NpgsqlRange<int>.LowerBoundInfinite)] = "lower_inf",
        [nameof(NpgsqlRange<int>.UpperBoundInfinite)] = "upper_inf"
    };

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(PostgreSqlRangeDbFunctionsExtensions))
        {
            return null;
        }

        var range = arguments[0];
        var other = arguments[1];
        var rangeMapping = ExpressionExtensions.InferTypeMapping(range, other) ?? typeMappingSource.FindMapping(range.Type);
        range = sqlExpressionFactory.ApplyTypeMapping(range, rangeMapping);
        other = other.Type == range.Type
            ? sqlExpressionFactory.ApplyTypeMapping(other, rangeMapping)
            : sqlExpressionFactory.ApplyDefaultTypeMapping(other);
        var resultMapping = method.ReturnType == typeof(bool) ? typeMappingSource.FindMapping(typeof(bool)) : rangeMapping;
        if (method.Name == nameof(PostgreSqlRangeDbFunctionsExtensions.Merge))
        {
            return sqlExpressionFactory.Function("range_merge", [range, other], nullable: true, [true, true], method.ReturnType, resultMapping);
        }

        return PostgreSqlArraySql.Binary(sqlExpressionFactory, Operators[method.Name], range, other, method.ReturnType, resultMapping);
    }

    public SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((instance == null)
            || !(member.DeclaringType is { IsGenericType: true } declaring && (declaring.GetGenericTypeDefinition() == typeof(NpgsqlRange<>)))
            || !Members.TryGetValue(member.Name, out var function))
        {
            return null;
        }

        return sqlExpressionFactory.Function(
            function,
            [sqlExpressionFactory.ApplyDefaultTypeMapping(instance)],
            nullable: true,
            [true],
            returnType,
            typeMappingSource.FindMapping(returnType));
    }
}

internal sealed class PostgreSqlMemberTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMemberTranslatorPlugin
{
    public IEnumerable<IMemberTranslator> Translators { get; } = [new PostgreSqlRangeTranslator(sqlExpressionFactory, typeMappingSource)];
}
