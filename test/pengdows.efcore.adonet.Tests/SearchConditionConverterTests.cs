using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     The BIT converter on SQL trees directly: EF's own optimizations remove most of these shapes before it runs, but a
///     query can still produce them.
/// </summary>
public class SearchConditionConverterTests
{
    private readonly ISqlExpressionFactory _sql;
    private readonly RelationalTypeMapping _bit;
    private readonly ColumnExpression _a;
    private readonly ColumnExpression _b;

    public SearchConditionConverterTests()
    {
        using var context = new FakeDatabase().Context(TestDialects.Bit);
        _sql = context.GetService<ISqlExpressionFactory>();
        _bit = context.GetService<IRelationalTypeMappingSource>().FindMapping(typeof(bool))!;
        _a = new ColumnExpression("A", "t", typeof(bool), _bit, nullable: false);
        _b = new ColumnExpression("B", "t", typeof(bool), _bit, nullable: false);
    }

    private SqlExpression Value(SqlExpression expression, bool bitwise = false)
        => (SqlExpression)new Converter(_sql, bitwise).Run(expression, inSearchConditionContext: false);

    private SqlExpression Condition(SqlExpression expression, bool bitwise = false)
        => (SqlExpression)new Converter(_sql, bitwise).Run(expression, inSearchConditionContext: true);

    private SqlExpression True
        => _sql.Constant(true, _bit);

    // Built directly: the factory's Not would already simplify NOT (a = b) and NOT NOT a.
    private SqlExpression Not(SqlExpression operand)
        => new SqlUnaryExpression(ExpressionType.Not, operand, typeof(bool), _bit);

    private SqlExpression Equal(SqlExpression left, SqlExpression right)
        => new SqlBinaryExpression(ExpressionType.Equal, left, right, typeof(bool), _bit);

    private static SqlExpression Test(SqlExpression caseExpression)
        => Assert.Single(Assert.IsType<CaseExpression>(caseExpression).WhenClauses).Test;

    [Fact]
    public void A_negated_comparison_becomes_the_opposite_comparison()
    {
        var test = Assert.IsType<SqlBinaryExpression>(Test(Value(Not(Equal(_a, _b)))));

        Assert.Equal(ExpressionType.NotEqual, test.OperatorType);
    }

    [Fact]
    public void A_negated_comparison_with_a_constant_flips_the_constant_on_either_side()
    {
        var right = Assert.IsType<SqlBinaryExpression>(Test(Value(Not(Equal(_a, True)))));
        var left = Assert.IsType<SqlBinaryExpression>(Test(Value(Not(Equal(True, _a)))));

        Assert.Equal(false, Assert.IsType<SqlConstantExpression>(right.Right).Value);
        Assert.Equal(false, Assert.IsType<SqlConstantExpression>(left.Left).Value);
    }

    [Fact]
    public void Constant_conditions_compare_integers()
    {
        var condition = Assert.IsType<SqlBinaryExpression>(Condition(True));

        Assert.Equal(1, Assert.IsType<SqlConstantExpression>(condition.Left).Value);
        Assert.Equal(1, Assert.IsType<SqlConstantExpression>(condition.Right).Value);
    }

    [Fact]
    public void Comparing_two_complements_compares_their_operands()
    {
        var notA = new SqlUnaryExpression(ExpressionType.OnesComplement, _a, typeof(bool), _bit);
        var notB = new SqlUnaryExpression(ExpressionType.OnesComplement, _b, typeof(bool), _bit);

        var condition = Assert.IsType<SqlBinaryExpression>(Condition(Equal(notA, notB), bitwise: true));

        Assert.Same(_a, condition.Left);
        Assert.Same(_b, condition.Right);
    }

    [Fact]
    public void A_double_negation_as_a_value_is_the_value()
        => Assert.Same(_a, Value(Not(Not(_a)), bitwise: true));

    [Fact]
    public void Comparisons_of_other_types_stay_conditions_with_bitwise_operators()
    {
        var name = new ColumnExpression("N", "t", typeof(string), null, nullable: false);

        Assert.IsType<CaseExpression>(Value(_sql.Equal(name, name), bitwise: true));
    }

    [Fact]
    public void Other_unary_operators_are_values()
    {
        var number = new ColumnExpression("I", "t", typeof(int), null, nullable: false);
        var converted = _sql.Convert(number, typeof(bool), _bit);

        var condition = Assert.IsType<SqlBinaryExpression>(Condition(converted));

        Assert.Equal(ExpressionType.Equal, condition.OperatorType);
        Assert.IsType<SqlUnaryExpression>(condition.Left);
    }

    private sealed class Converter(ISqlExpressionFactory sqlExpressionFactory, bool bitwiseOperators)
        : AdoNetSearchConditionConverter(sqlExpressionFactory, bitwiseOperators)
    {
        public Expression Run(Expression expression, bool inSearchConditionContext)
            => Visit(expression, inSearchConditionContext, allowNullFalseEquivalence: false);
    }
}
