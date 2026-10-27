using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace pengdows.efcore.adonet.Query;

/// <summary>
///     <c>StartsWith</c>, <c>EndsWith</c> and <c>Contains</c> with a parameter become <c>x LIKE @p_startswith ESCAPE '\'</c>,
///     the pattern a second parameter computed from the first with its wildcards escaped, so an index can serve the
///     prefix match. Everything else goes to the translators.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied): EF Core SQL Server provider (MIT, .NET Foundation):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerSqlTranslatingExpressionVisitor.cs
/// </remarks>
internal class AdoNetSqlTranslatingExpressionVisitor : RelationalSqlTranslatingExpressionVisitor
{
    private readonly QueryCompilationContext _queryCompilationContext;
    private readonly FunctionSyntax _functions;
    private readonly bool _rowValues;
    private readonly bool _computesLimits;

    public AdoNetSqlTranslatingExpressionVisitor(
        RelationalSqlTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor,
        FunctionSyntax functions,
        bool rowValues,
        bool computesLimits)
        : base(dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor)
    {
        _queryCompilationContext = queryCompilationContext;
        _functions = functions;
        _rowValues = rowValues;
        _computesLimits = computesLimits;
    }

    private static readonly MethodInfo PatternMethod
        = typeof(FunctionSyntax).GetMethod(nameof(FunctionSyntax.FormatLikePattern), BindingFlags.Public | BindingFlags.Static)!;

    private enum Match
    {
        StartsWith,
        EndsWith,
        Contains
    }

    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        if (_rowValues && TryTranslateTupleEquals(methodCallExpression) is { } rowComparison)
        {
            return rowComparison;
        }

        if ((methodCallExpression is { Object: { } instance, Arguments: [var argument] })
            && (methodCallExpression.Method.DeclaringType == typeof(string))
            && (argument.Type == typeof(string))
            && (_functions.LikeEscapeCharacter is { } escape)
            && Enum.TryParse<Match>(methodCallExpression.Method.Name, out var match)
            && (Visit(argument) is SqlParameterExpression parameter)
            && (Visit(instance) is SqlExpression translatedInstance))
        {
            var mapping = ExpressionExtensions.InferTypeMapping(translatedInstance, parameter);
            var pattern = _queryCompilationContext.RegisterRuntimeParameter(
                $"{parameter.Name}_{match.ToString().ToLowerInvariant()}",
                Expression.Lambda(
                    Expression.Call(
                        PatternMethod,
                        QueryCompilationContext.QueryContextParameter,
                        Expression.Constant(parameter.Name),
                        Expression.Constant(match.ToString()),
                        Expression.Constant(escape),
                        Expression.Constant(_functions.LikeSpecialCharacters)),
                    QueryCompilationContext.QueryContextParameter));

            return Dependencies.SqlExpressionFactory.Like(
                Dependencies.SqlExpressionFactory.ApplyTypeMapping(translatedInstance, mapping),
                new SqlParameterExpression(pattern.Name!, typeof(string), mapping),
                Dependencies.SqlExpressionFactory.Constant(escape.ToString(), mapping));
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    // tuple == other and tuple != other compare the values, as the query's expected results do. A bitwise operator the
    // database doesn't have is left untranslated.
    protected override Expression VisitBinary(BinaryExpression binaryExpression)
    {
        if ((binaryExpression.NodeType is ExpressionType.Add or ExpressionType.Subtract)
            && (Nullable.GetUnderlyingType(binaryExpression.Type) ?? binaryExpression.Type) == typeof(TimeSpan))
        {
            var template = binaryExpression.NodeType == ExpressionType.Add ? _functions.TimeSpanAdd : _functions.TimeSpanSubtract;
            if (template is { }
                && Visit(binaryExpression.Left) is SqlExpression left
                && Visit(binaryExpression.Right) is SqlExpression right)
            {
                return Dependencies.SqlExpressionFactory.Call(
                    template,
                    [left, Dependencies.SqlExpressionFactory.ApplyTypeMapping(right, left.TypeMapping)!],
                    binaryExpression.Type,
                    Dependencies.TypeMappingSource.FindMapping(typeof(TimeSpan)))!;
            }
        }

        if (IsBitwise(binaryExpression.NodeType, binaryExpression.Type) && (BitwiseTemplate(binaryExpression.NodeType) == null))
        {
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (binaryExpression.NodeType is ExpressionType.LeftShift or ExpressionType.RightShift)
        {
            return TranslateShift(binaryExpression) ?? QueryCompilationContext.NotTranslatedExpression;
        }

        if (binaryExpression is { NodeType: ExpressionType.ArrayIndex, Left.Type: var array } && (array == typeof(byte[])))
        {
            return TranslateByteAt(binaryExpression) ?? QueryCompilationContext.NotTranslatedExpression;
        }

        if (IsDateDifference(binaryExpression))
        {
            var difference = IsTimeDifference(binaryExpression) ? _functions.TimeDifference : _functions.DateDifference;
            return difference is { }
                && (Visit(binaryExpression.Left) is SqlExpression left)
                && (Visit(binaryExpression.Right) is SqlExpression right)
                ? Dependencies.SqlExpressionFactory.Call(
                    difference,
                    [left, Dependencies.SqlExpressionFactory.ApplyTypeMapping(right, left.TypeMapping)!],
                    binaryExpression.Type,
                    Dependencies.TypeMappingSource.FindMapping(typeof(TimeSpan)))!
                : QueryCompilationContext.NotTranslatedExpression;
        }

        if (_rowValues
            && (binaryExpression.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            && (TryTranslateTupleComparison(binaryExpression.Left, binaryExpression.Right) is SqlBinaryExpression rowComparison))
        {
            return binaryExpression.NodeType == ExpressionType.Equal
                ? rowComparison
                : Dependencies.SqlExpressionFactory.Not(rowComparison);
        }

        return base.VisitBinary(binaryExpression);
    }

    protected override Expression VisitMember(MemberExpression memberExpression)
    {
        if (memberExpression is
            {
                Member: { DeclaringType: var declaringType, Name: nameof(TimeSpan.TotalDays) },
                Expression: BinaryExpression { } difference
            }
            && declaringType == typeof(TimeSpan)
            && IsDateDifference(difference)
            && !IsTimeDifference(difference)
            && _functions.DateDifferenceTotalDays is { } totalDays
            && Visit(difference.Left) is SqlExpression left
            && Visit(difference.Right) is SqlExpression right)
        {
            return Dependencies.SqlExpressionFactory.Call(
                totalDays,
                [left, Dependencies.SqlExpressionFactory.ApplyTypeMapping(right, left.TypeMapping)!],
                typeof(double),
                Dependencies.TypeMappingSource.FindMapping(typeof(double)))!;
        }

        return base.VisitMember(memberExpression);
    }

    protected override Expression VisitUnary(UnaryExpression unaryExpression)
    {
        if (unaryExpression is { NodeType: ExpressionType.ArrayLength, Operand.Type: var array } && (array == typeof(byte[])))
        {
            return Visit(unaryExpression.Operand) is SqlExpression bytes
                && Dependencies.SqlExpressionFactory.Call(
                    _functions.ByteLength, [bytes], typeof(int), Dependencies.TypeMappingSource.FindMapping(typeof(int))) is { } length
                    ? length
                    : QueryCompilationContext.NotTranslatedExpression;
        }

        return IsBitwise(unaryExpression.NodeType, unaryExpression.Type) && (_functions.BitwiseNot == null)
            ? QueryCompilationContext.NotTranslatedExpression
            : base.VisitUnary(unaryExpression);
    }

    // bytes[i]: the byte at 1-based position i + 1.
    private SqlExpression? TranslateByteAt(BinaryExpression index)
    {
        if ((_functions.ByteAt == null) || (Visit(index.Left) is not SqlExpression bytes) || (Visit(index.Right) is not SqlExpression position))
        {
            return null;
        }

        var factory = Dependencies.SqlExpressionFactory;
        position = factory.ApplyDefaultTypeMapping(position)!;
        var oneBased = position is SqlConstantExpression { Value: int value }
            ? factory.Constant(value + 1, position.TypeMapping)
            : factory.Add(position, factory.Constant(1));
        return factory.Call(_functions.ByteAt, [bytes, oneBased], typeof(byte), Dependencies.TypeMappingSource.FindMapping(typeof(byte)));
    }

    // time - time: a TimeOnly difference, which wraps past midnight.
    private static bool IsTimeDifference(BinaryExpression binary)
        => (Nullable.GetUnderlyingType(binary.Left.Type) ?? binary.Left.Type) == typeof(TimeOnly);

    // a << b and a >> b, through the dialect's operator; untranslated where it has none.
    private SqlExpression? TranslateShift(BinaryExpression shift)
    {
        var template = shift.NodeType == ExpressionType.LeftShift ? _functions.LeftShift : _functions.RightShift;
        if ((template == null) || (Visit(shift.Left) is not SqlExpression left) || (Visit(shift.Right) is not SqlExpression right))
        {
            return null;
        }

        var factory = Dependencies.SqlExpressionFactory;
        left = factory.ApplyDefaultTypeMapping(left)!;
        return factory.Call(template, [left, factory.ApplyDefaultTypeMapping(right)!], shift.Type, left.TypeMapping);
    }

    // EF.Functions.Greatest and Least, Math.Max and Min: the dialect's two-value form, nested for more values.
    public override SqlExpression? GenerateGreatest(IReadOnlyList<SqlExpression> expressions, Type resultType)
        => Nest(_functions.Greatest, expressions, resultType);

    // EF also combines two row limits (Take(a).Take(b), Take(a).Single()) as LEAST(a, b), and pushes the query down into a
    // subquery when there is no LEAST. Only then are all the values constants and parameters (EF evaluates such an expression
    // on the client anywhere else), so where a limit takes only values, that combination is left to the subquery.
    public override SqlExpression? GenerateLeast(IReadOnlyList<SqlExpression> expressions, Type resultType)
        => !_computesLimits && expressions.All(e => e is SqlConstantExpression or SqlParameterExpression)
            ? null
            : Nest(_functions.Least, expressions, resultType);

    private SqlExpression? Nest(string? template, IReadOnlyList<SqlExpression> expressions, Type resultType)
    {
        if ((template == null) || (expressions.Count == 0))
        {
            return null;
        }

        var factory = Dependencies.SqlExpressionFactory;
        var mapping = ExpressionExtensions.InferTypeMapping([.. expressions]);
        var values = expressions.Select(e => factory.ApplyTypeMapping(e, mapping)!).ToList();
        var result = values[^1];
        for (var i = values.Count - 2; i >= 0; i--)
        {
            result = factory.Call(template, [values[i], result], resultType, mapping ?? result.TypeMapping)!;
        }

        return result;
    }

    // date - date is a TimeSpan; the database's own difference of dates (an interval, or a number of days) isn't read back as
    // one, so it is left to the client, or untranslated in a predicate.
    private static bool IsDateDifference(BinaryExpression binary)
        => (binary.NodeType == ExpressionType.Subtract)
            && ((Nullable.GetUnderlyingType(binary.Type) ?? binary.Type) == typeof(TimeSpan))
            && ((Nullable.GetUnderlyingType(binary.Left.Type) ?? binary.Left.Type) is var left)
            && ((left == typeof(DateTime)) || (left == typeof(DateTimeOffset)) || (left == typeof(TimeOnly)));

    private static bool IsBitwise(ExpressionType nodeType, Type type)
        => (nodeType is ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr or ExpressionType.Not or ExpressionType.OnesComplement)
            && ((Nullable.GetUnderlyingType(type) ?? type) != typeof(bool));

    private string? BitwiseTemplate(ExpressionType nodeType)
        => nodeType switch
        {
            ExpressionType.And => _functions.BitwiseAnd,
            ExpressionType.Or => _functions.BitwiseOr,
            _ => _functions.ExclusiveOr
        };

    // tuple.Equals(other), both built in the query: (a, b) = (x, y), each pair typed as its own comparison would be.
    private SqlExpression? TryTranslateTupleEquals(MethodCallExpression call)
        => call is { Method.Name: nameof(Equals), Object: { } instance, Arguments: [var argument] }
            ? TryTranslateTupleComparison(instance, argument)
            : null;

    private SqlExpression? TryTranslateTupleComparison(Expression instance, Expression argument)
    {
        if (!instance.Type.IsAssignableTo(typeof(System.Runtime.CompilerServices.ITuple))
            || (RowValueSqlExpression.Elements(instance) is not { } left)
            || (RowValueSqlExpression.Elements(argument) is not { } right)
            || (left.Count != right.Count))
        {
            return null;
        }

        var leftValues = new SqlExpression[left.Count];
        var rightValues = new SqlExpression[right.Count];
        for (var i = 0; i < left.Count; i++)
        {
            if ((Visit(left[i]) is not SqlExpression l)
                || (Visit(right[i]) is not SqlExpression r)
                || (Dependencies.SqlExpressionFactory.Equal(l, r) is not SqlBinaryExpression pair))
            {
                return null;
            }

            (leftValues[i], rightValues[i]) = (pair.Left, pair.Right);
        }

        return new SqlBinaryExpression(
            ExpressionType.Equal,
            new RowValueSqlExpression(leftValues, instance.Type),
            new RowValueSqlExpression(rightValues, argument.Type),
            typeof(bool),
            Dependencies.TypeMappingSource.FindMapping(typeof(bool)));
    }
}

internal class AdoNetSqlTranslatingExpressionVisitorFactory(RelationalSqlTranslatingExpressionVisitorDependencies dependencies, ISqlDialect dialect)
    : IRelationalSqlTranslatingExpressionVisitorFactory
{
    public virtual RelationalSqlTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext,
        QueryableMethodTranslatingExpressionVisitor queryableMethodTranslatingExpressionVisitor)
        => new AdoNetSqlTranslatingExpressionVisitor(
            dependencies, queryCompilationContext, queryableMethodTranslatingExpressionVisitor, dialect.Functions, dialect.RowValues,
            dialect.Paging.ComputesLimits);
}
