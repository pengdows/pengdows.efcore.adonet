using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>An ISO row value, <c>(a, b)</c>: one side of a tuple comparison.</summary>
/// <remarks>
///     Behavior reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/Expressions/Internal/PgRowValueExpression.cs
/// </remarks>
internal sealed class RowValueSqlExpression : SqlExpression, IEquatable<RowValueSqlExpression>
{
    public RowValueSqlExpression(IReadOnlyList<SqlExpression> values, Type type, RelationalTypeMapping? typeMapping = null)
        : base(type, typeMapping)
    {
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    private static ConstructorInfo? _quotingConstructor;

    public IReadOnlyList<SqlExpression> Values { get; }

    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var visited = Values.Select(v => (SqlExpression)visitor.Visit(v)).ToList();
        return Update(visited);
    }

    public RowValueSqlExpression Update(IReadOnlyList<SqlExpression> values)
    {
        return values.SequenceEqual(Values, ReferenceEqualityComparer.Instance)
            ? this
            : new RowValueSqlExpression(values, Type, TypeMapping);
    }

    // EF9100: EF's quoting helpers, used only for precompiled queries, are marked experimental.
#pragma warning disable EF9100
    public override Expression Quote()
        => New(
            _quotingConstructor ??= typeof(RowValueSqlExpression).GetConstructor(
                [typeof(IReadOnlyList<SqlExpression>), typeof(Type), typeof(RelationalTypeMapping)])!,
            NewArrayInit(typeof(SqlExpression), Values.Select(v => v.Quote())),
            Constant(Type),
            RelationalExpressionQuotingUtilities.QuoteTypeMapping(TypeMapping));
#pragma warning restore EF9100

    protected override void Print(ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.Append("(");
        for (var i = 0; i < Values.Count; i++)
        {
            if (i > 0)
            {
                expressionPrinter.Append(", ");
            }

            expressionPrinter.Visit(Values[i]);
        }

        expressionPrinter.Append(")");
    }

    public override bool Equals(object? obj)
        => obj is RowValueSqlExpression other && Equals(other);

    public bool Equals(RowValueSqlExpression? other)
        => (other != null) && base.Equals(other) && Values.SequenceEqual(other.Values);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        foreach (var value in Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <summary>The values of a tuple built in the query: <c>new Tuple(...)</c>, <c>Tuple.Create(...)</c>, or their value-tuple forms.</summary>
    public static IReadOnlyList<Expression>? Elements(Expression expression)
        => expression switch
        {
            UnaryExpression { NodeType: ExpressionType.Convert } convert => Elements(convert.Operand),
            NewExpression created when created.Type.IsAssignableTo(typeof(ITuple)) => created.Arguments,
            MethodCallExpression { Method: { IsStatic: true, Name: "Create" } method } call
                when (method.DeclaringType == typeof(Tuple)) || (method.DeclaringType == typeof(ValueTuple)) => call.Arguments,
            ConstantExpression { Value: ITuple tuple } constant
                => Enumerable.Range(0, tuple.Length)
                    .Select(i => (Expression)Constant(tuple[i], constant.Type.GetGenericArguments()[i]))
                    .ToList(),
            _ => null
        };
}
