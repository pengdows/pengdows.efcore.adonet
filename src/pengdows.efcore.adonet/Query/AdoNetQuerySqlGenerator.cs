using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using pengdows.efcore.adonet.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational SQL, with paging, functions, <c>LongCount</c> and bulk DML as the dialect writes them.</summary>
public partial class AdoNetQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect, ITypeMappingDialect typeMappings, bool reverseNullOrdering = false)
    : QuerySqlGenerator(dependencies), IQuerySqlWriter
{
    private TableExpression? _qualifyWithTableName;
    private int _selectDepth;
    private int _derivedProjectionAlias;

    protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
    {
        base.VisitSqlParameter(sqlParameterExpression);
        if (sqlParameterExpression.TypeMapping?.StoreType is { } storeType && dialect.ParameterCast(storeType) is { } cast)
        {
            Sql.Append(cast);
        }

        return sqlParameterExpression;
    }

    protected override Expression VisitSelect(SelectExpression selectExpression)
    {
        _selectDepth++;
        try
        {
            if ((_selectDepth > 1)
                && dialect.RequiresDerivedTableColumnAliases
                && selectExpression.Projection.Any(projection => projection.Alias.Length == 0))
            {
                selectExpression = selectExpression.Update(
                    tables: selectExpression.Tables,
                    predicate: selectExpression.Predicate,
                    groupBy: selectExpression.GroupBy,
                    having: selectExpression.Having,
                    projections: selectExpression.Projection.Select(projection =>
                        projection.Alias.Length == 0
                            ? new ProjectionExpression(projection.Expression, $"c{_derivedProjectionAlias++}")
                            : projection).ToList(),
                    orderings: selectExpression.Orderings,
                    offset: selectExpression.Offset,
                    limit: selectExpression.Limit);
            }

            return base.VisitSelect(selectExpression);
        }
        finally
        {
            _selectDepth--;
        }
    }

    protected override void GenerateProjection(SelectExpression selectExpression)
    {
        base.GenerateProjection(selectExpression);
        if ((_selectDepth > 1)
            && dialect.RequiresDerivedTableColumnAliases
            && (selectExpression.Projection.Count == 0))
        {
            Sql.Append(AliasSeparator)
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier($"c{_derivedProjectionAlias++}"));
        }
    }

    protected override Expression VisitValues(ValuesExpression valuesExpression)
    {
        if (!dialect.UsesNativeValuesTable)
        {
            return base.VisitValues(valuesExpression);
        }

        Sql.Append("(");
        GenerateValues(valuesExpression);
        Sql.Append(") AS ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(valuesExpression.Alias)).Append("(");
        for (var index = 0; index < valuesExpression.ColumnNames.Count; index++)
        {
            if (index > 0)
            {
                Sql.Append(", ");
            }

            Sql.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(valuesExpression.ColumnNames[index]));
        }

        Sql.Append(")");
        return valuesExpression;
    }

    protected override Expression VisitDelete(DeleteExpression deleteExpression)
        => dialect.Dml.TryGenerateDelete(this, deleteExpression) ? deleteExpression : base.VisitDelete(deleteExpression);

    protected override Expression VisitUpdate(UpdateExpression updateExpression)
        => dialect.Dml.TryGenerateUpdate(this, updateExpression) ? updateExpression : base.VisitUpdate(updateExpression);

    // EF's SELECT 1 AS "Value" UNION ALL VALUES (2), (3), with the dialect's prefix before each row after the first.
    protected override void GenerateValues(ValuesExpression valuesExpression)
    {
        if (dialect.UsesNativeValuesTable)
        {
            if (valuesExpression.RowValues is not { Count: > 0 } nativeRows)
            {
                throw new InvalidOperationException("A native VALUES table requires at least one row.");
            }

            Sql.Append("VALUES ");
            for (var index = 0; index < nativeRows.Count; index++)
            {
                if (index > 0)
                {
                    Sql.Append(", ");
                }

                Visit(nativeRows[index]);
            }

            return;
        }

        if ((dialect.ValuesRowPrefix.Length == 0) || (valuesExpression.RowValues is not { Count: > 1 } rows))
        {
            base.GenerateValues(valuesExpression);
            return;
        }

        Sql.Append("SELECT ");
        for (var i = 0; i < rows[0].Values.Count; i++)
        {
            if (i > 0)
            {
                Sql.Append(", ");
            }

            Visit(rows[0].Values[i]);
            Sql.Append(AliasSeparator).Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(valuesExpression.ColumnNames[i]));
        }

        Sql.Append(" UNION ALL VALUES ");
        for (var j = 1; j < rows.Count; j++)
        {
            if (j > 1)
            {
                Sql.Append(", ");
            }

            Sql.Append(dialect.ValuesRowPrefix);
            Visit(rows[j]);
        }
    }

    // VALUES of several rows is written SELECT ... UNION ALL VALUES ...; as an operand of another set operation it is
    // parenthesized, or its UNION ALL would apply to the whole operation (a UNION would keep duplicates).
    protected override void GenerateSetOperationOperand(SetOperationBase setOperation, SelectExpression operand)
    {
        if (operand.Tables is not [ValuesExpression { RowValues.Count: > 1 }])
        {
            base.GenerateSetOperationOperand(setOperation, operand);
            return;
        }

        Sql.Append("(");
        base.GenerateSetOperationOperand(setOperation, operand);
        Sql.Append(")");
    }

    protected override Expression VisitColumn(ColumnExpression columnExpression)
    {
        if (_qualifyWithTableName is { } table && columnExpression.TableAlias == table.Alias)
        {
            Sql.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(table.Name, table.Schema))
                .Append(".")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(columnExpression.Name));
            return columnExpression;
        }

        return base.VisitColumn(columnExpression);
    }

    protected override bool RequiresParentheses(SqlExpression outerExpression, SqlExpression innerExpression)
        => innerExpression is not JsonScalarExpression && base.RequiresParentheses(outerExpression, innerExpression);


    private void WriteConcatenation(string template, List<Action> parts, int first)
    {
        if (first == parts.Count - 1)
        {
            parts[first]();
            return;
        }

        WriteTemplate(template, parts[first], () => WriteConcatenation(template, parts, first + 1));
    }

    // Writes a template, running the writer for each {n} where it appears.
    private void WriteTemplate(string template, params Action[] arguments)
    {
        var at = 0;
        while (at < template.Length)
        {
            var open = template.IndexOf('{', at);
            if ((open < 0) || (open + 2 >= template.Length) || (template[open + 2] != '}') || !char.IsDigit(template[open + 1]))
            {
                Sql.Append(open < 0 ? template[at..] : template[at..(open + 1)]);
                at = open < 0 ? template.Length : open + 1;
                continue;
            }

            Sql.Append(template[at..open]);
            arguments[template[open + 1] - '0']();
            at = open + 3;
        }
    }

    protected override Expression VisitExtension(Expression extensionExpression)
    {
        if (extensionExpression is CaseExpression constantCase && ConstantCaseFolder.TryFold(constantCase, out var folded) && folded is not null)
        {
            return Visit(folded);
        }

        if (extensionExpression is JsonArrayTableExpression table)
        {
            var helper = Dependencies.SqlGenerationHelper;
            // A collection in the database's own array type has its own elements table, of the element's own store type.
            var array = table.Json.TypeMapping as Storage.IArrayTypeMapping;
            WriteTemplate(
                array?.Elements ?? dialect.Json!.ArrayElements!,
                () => Visit(table.Json),
                () => Sql.Append(helper.DelimitIdentifier(JsonArrayTableExpression.OrdinalColumn)),
                () => Sql.Append(helper.DelimitIdentifier(JsonArrayTableExpression.ValueColumn)),
                () => Sql.Append(array != null ? table.ElementTypeMapping!.StoreType : ArrayElementType(table.ElementTypeMapping!)));
            Sql.Append(AliasSeparator).Append(helper.DelimitIdentifier(table.Alias));
            return table;
        }

        if (extensionExpression is not RowValueSqlExpression rowValue)
        {
            return base.VisitExtension(extensionExpression);
        }

        Sql.Append("(");
        for (var i = 0; i < rowValue.Values.Count; i++)
        {
            if (i > 0)
            {
                Sql.Append(", ");
            }

            Visit(rowValue.Values[i]);
        }

        Sql.Append(")");
        return rowValue;
    }

    protected override Expression VisitCrossApply(CrossApplyExpression crossApplyExpression)
    {
        Sql.Append(LateralJoins.CrossJoin);
        VisitLateral(crossApplyExpression.Table);
        return crossApplyExpression;
    }

    protected override Expression VisitOuterApply(OuterApplyExpression outerApplyExpression)
    {
        Sql.Append(LateralJoins.OuterJoin);
        VisitLateral(outerApplyExpression.Table);
        Sql.Append(LateralJoins.OuterJoinCondition ?? "");
        return outerApplyExpression;
    }

    // A table function sees the tables before it without LATERAL (ISO's rule, and some databases refuse the keyword there).
    private void VisitLateral(TableExpressionBase table)
    {
        if (table is not (TableExpression or TableValuedFunctionExpression))
        {
            Sql.Append(dialect.LateralJoins?.Lateral ?? throw new InvalidOperationException("A lateral join was not refused at translation."));
        }

        Visit(table);
    }

    // Without lateral joins, only a plain table is applied (a subquery has failed to translate): an ordinary ISO join.
    private LateralJoinSyntax LateralJoins
        => dialect.LateralJoins ?? LateralJoinSyntax.Iso;

    // A paged subquery under IN, where the database can't page there, is read through a derived table, which can.
    protected override void GenerateIn(InExpression inExpression, bool negated)
    {
        if (dialect.LimitInPredicateSubqueries
            || (inExpression.Subquery is not { } subquery)
            || ((subquery.Limit == null) && (subquery.Offset == null)))
        {
            base.GenerateIn(inExpression, negated);
            return;
        }

        const string Alias = "limited";
        Visit(inExpression.Item);
        Sql.Append(negated ? " NOT IN (SELECT " : " IN (SELECT ");
        if ((dialect.Json?.ArrayElementsHint is { } hint) && ReadsArrayElements(subquery))
        {
            Sql.Append(hint).Append(" ");
        }

        Sql.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(subquery.Projection[0].Alias, Alias))
            .AppendLine(" FROM (");
        using (Sql.Indent())
        {
            Visit(subquery);
        }

        Sql.AppendLine().Append(") AS ").Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(Alias)).Append(")");
    }

    // CAST names the dialect's target for the store type, which in ISO SQL is the store type itself.
    protected override Expression VisitSqlUnary(SqlUnaryExpression sqlUnaryExpression)
    {
        if ((sqlUnaryExpression.OperatorType is ExpressionType.OnesComplement or ExpressionType.Not)
            && (sqlUnaryExpression.Type != typeof(bool))
            && (dialect.Functions.BitwiseNot is { } bitwiseNot))
        {
            SqlTemplate.Write(bitwiseNot, [sqlUnaryExpression.Operand], s => Sql.Append(s), VisitOperand);
            return sqlUnaryExpression;
        }

        if ((sqlUnaryExpression.OperatorType != ExpressionType.Convert) || (sqlUnaryExpression.TypeMapping is not { } typeMapping))
        {
            return base.VisitSqlUnary(sqlUnaryExpression);
        }

        WriteCast(sqlUnaryExpression.Operand, typeMapping.StoreType, RequiresParentheses(sqlUnaryExpression, sqlUnaryExpression.Operand));
        return sqlUnaryExpression;
    }

    // The dialect's conversion of the value, parenthesized when EF would parenthesize it.
    private void WriteCast(SqlExpression operand, string storeType, bool parenthesize = false)
        => SqlTemplate.Write(
            typeMappings.Cast(storeType),
            [operand],
            text => Sql.Append(text),
            value =>
            {
                if (parenthesize)
                {
                    Sql.Append("(");
                }

                Visit(value);
                if (parenthesize)
                {
                    Sql.Append(")");
                }
            });

    protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
    {
        if (sqlBinaryExpression is { OperatorType: ExpressionType.Add } && sqlBinaryExpression.Type == typeof(string))
        {
            SqlTemplate.Write(
                dialect.Functions.Concat, [sqlBinaryExpression.Left, sqlBinaryExpression.Right], s => Sql.Append(s), e => Visit(e));
            return sqlBinaryExpression;
        }

        if ((sqlBinaryExpression is { OperatorType: ExpressionType.Modulo })
            && IsFractional(sqlBinaryExpression)
            && (dialect.Functions.FractionalModulo is { } fractionalModulo))
        {
            SqlTemplate.Write(fractionalModulo, [sqlBinaryExpression.Left, sqlBinaryExpression.Right], s => Sql.Append(s), e => Visit(e));
            return sqlBinaryExpression;
        }

        if ((sqlBinaryExpression is { OperatorType: ExpressionType.Divide })
            && (IsOfType(sqlBinaryExpression.Left, typeof(decimal)) || IsOfType(sqlBinaryExpression.Right, typeof(decimal)))
            && (dialect.Functions.DecimalDivide is { } decimalDivide))
        {
            SqlTemplate.Write(decimalDivide, [sqlBinaryExpression.Left, sqlBinaryExpression.Right], s => Sql.Append(s), VisitOperand);
            return sqlBinaryExpression;
        }

        if (sqlBinaryExpression is { OperatorType: ExpressionType.Modulo } && dialect.Functions.Modulo is { } modulo)
        {
            SqlTemplate.Write(modulo, [sqlBinaryExpression.Left, sqlBinaryExpression.Right], s => Sql.Append(s), e => Visit(e));
            return sqlBinaryExpression;
        }

        var bitwise = sqlBinaryExpression.Type == typeof(bool)
            ? null
            : sqlBinaryExpression.OperatorType switch
            {
                ExpressionType.And => dialect.Functions.BitwiseAnd,
                ExpressionType.Or => dialect.Functions.BitwiseOr,
                ExpressionType.ExclusiveOr => dialect.Functions.ExclusiveOr,
                _ => null
            };
        if (bitwise != null)
        {
            SqlTemplate.Write(bitwise, [sqlBinaryExpression.Left, sqlBinaryExpression.Right], s => Sql.Append(s), VisitOperand);
            return sqlBinaryExpression;
        }

        if (TryWriteJsonEquality(sqlBinaryExpression))
        {
            return sqlBinaryExpression;
        }

        return base.VisitSqlBinary(sqlBinaryExpression);
    }





    private static bool IsFractional(SqlBinaryExpression expression)
        => IsOfType(expression.Left, typeof(decimal)) || IsOfType(expression.Right, typeof(decimal))
            || IsOfType(expression.Left, typeof(double)) || IsOfType(expression.Right, typeof(double))
            || IsOfType(expression.Left, typeof(float)) || IsOfType(expression.Right, typeof(float));

    // The operand's provider type: a decimal converted to something else is stored, and computes, as that.
    private static bool IsOfType(SqlExpression operand, Type type)
        => (operand.TypeMapping?.Converter?.ProviderClrType ?? Nullable.GetUnderlyingType(operand.Type) ?? operand.Type) == type;

    private static bool IsConstantOrParameter(SqlExpression expression)
        => (expression is SqlConstantExpression) || (expression is SqlParameterExpression);

    // An operand written into an operator template is parenthesized when it is an operation itself: databases order bitwise
    // operators differently (in some & binds tighter than |, in others they are equal).
    private void VisitOperand(Expression operand)
    {
        if (operand is SqlBinaryExpression or SqlUnaryExpression { OperatorType: not ExpressionType.Convert })
        {
            Sql.Append("(");
            Visit(operand);
            Sql.Append(")");
            return;
        }

        Visit(operand);
    }

    // SQL's standard precedence, highest first (vendors document the same order), so only what needs them gets parentheses.
    protected override bool TryGetOperatorInfo(SqlExpression expression, out int precedence, out bool isAssociative)
    {
        (precedence, isAssociative) = expression switch
        {
            SqlBinaryExpression binary => binary.OperatorType switch
            {
                ExpressionType.Multiply => (900, true),
                ExpressionType.Divide or ExpressionType.Modulo => (900, false),
                ExpressionType.Add => (700, true),
                ExpressionType.Subtract => (700, false),
                ExpressionType.And or ExpressionType.Or or ExpressionType.ExclusiveOr => (700, true),
                ExpressionType.LessThan or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan
                    or ExpressionType.GreaterThanOrEqual or ExpressionType.Equal or ExpressionType.NotEqual => (500, false),
                ExpressionType.AndAlso => (200, true),
                ExpressionType.OrElse => (100, true),
                _ => default
            },
            SqlUnaryExpression unary => unary.OperatorType switch
            {
                ExpressionType.Convert => (1300, false),
                ExpressionType.OnesComplement => (1200, false),
                ExpressionType.Not when unary.Type != typeof(bool) => (1200, false),
                ExpressionType.Negate => (1100, false),
                ExpressionType.Equal or ExpressionType.NotEqual => (500, false),
                ExpressionType.Not => (300, false),
                _ => default
            },
            CollateExpression => (900, false),
            LikeExpression => (350, false),
            AtTimeZoneExpression => (1200, false),
            _ => default
        };

        return precedence != default;
    }

    protected override void GenerateTop(SelectExpression selectExpression)
    {
        if ((dialect.Json?.ArrayElementsHint is { } hint) && ReadsArrayElements(selectExpression))
        {
            Sql.Append(hint).Append(" ");
        }

        dialect.Paging.GenerateTop(this, selectExpression);
    }

    // Text elements in the dialect's text form; binary ones are read as their base64 text, then decoded, and a DateTimeOffset
    // as its text where the dialect converts it (JsonSyntax.DateTimeOffsetFromText).
    private string ArrayElementType(RelationalTypeMapping element)
    {
        var json = dialect.Json!;
        var readAsText = ((element.ClrType == typeof(byte[])) && (json.ArrayElementsBinaryValue != null))
            || (((Nullable.GetUnderlyingType(element.ClrType) ?? element.ClrType) == typeof(DateTimeOffset))
                && (json.DateTimeOffsetFromText != null));
        var storeType = readAsText
            ? typeMappings.GetStoreType(typeof(string))!
            : element.StoreType;
        return ((element.ClrType == typeof(string)) || (storeType != element.StoreType)) && (json.ArrayElementsTextType is { } textType)
            ? textType.Replace("{0}", storeType, StringComparison.Ordinal)
            : storeType;
    }

    // A JSON array table in the query's own FROM, or in a derived table there.
    private static bool ReadsArrayElements(SelectExpression select)
        => select.Tables.Any(t => t switch
        {
            JsonArrayTableExpression => true,
            SelectExpression derived => ReadsArrayElements(derived),
            JoinExpressionBase { Table: var joined } => joined is JsonArrayTableExpression
                || ((joined is SelectExpression joinedSelect) && ReadsArrayElements(joinedSelect)),
            _ => false
        });

    protected override Expression VisitOrdering(OrderingExpression orderingExpression)
    {
        base.VisitOrdering(orderingExpression);
        if (reverseNullOrdering)
        {
            Sql.Append(orderingExpression.IsAscending ? " NULLS FIRST" : " NULLS LAST");
        }

        return orderingExpression;
    }

    protected override void GenerateOrderings(SelectExpression selectExpression)
    {
        base.GenerateOrderings(selectExpression);
        dialect.Paging.GenerateAfterOrderings(this, selectExpression);
    }

    protected override void GenerateLimitOffset(SelectExpression selectExpression)
        => dialect.Paging.GenerateLimitOffset(this, selectExpression);

    protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
    {
        if (SqlTemplate.TryGetTemplate(sqlFunctionExpression, out var template))
        {
            SqlTemplate.Write(template, sqlFunctionExpression.Arguments!, s => Sql.Append(s), e => Visit(e));
            return sqlFunctionExpression;
        }

        // EF's COUNT always has exactly one argument: * or an expression.
        if (sqlFunctionExpression is { IsBuiltIn: true, Name: "COUNT", Instance: null, Arguments: [var argument] }
            && sqlFunctionExpression.Type == typeof(long)
            && dialect.LongCountFunction != "COUNT")
        {
            Sql.Append(dialect.LongCountFunction).Append("(");
            Visit(argument);
            Sql.Append(")");
            return sqlFunctionExpression;
        }

        return base.VisitSqlFunction(sqlFunctionExpression);
    }

    IQuerySqlWriter IQuerySqlWriter.Append(string sql)
    {
        Sql.Append(sql);
        return this;
    }

    IQuerySqlWriter IQuerySqlWriter.AppendLine()
    {
        Sql.AppendLine();
        return this;
    }

    IQuerySqlWriter IQuerySqlWriter.AppendIdentifier(string name, string? schema)
    {
        Sql.Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(name, schema));
        return this;
    }

    IQuerySqlWriter IQuerySqlWriter.Visit(Expression expression)
    {
        Visit(expression);
        return this;
    }

    IDisposable IQuerySqlWriter.QualifyWithTableName(TableExpression table)
    {
        var previous = _qualifyWithTableName;
        _qualifyWithTableName = table;
        return new Restore(() => _qualifyWithTableName = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose()
            => restore();
    }
}
