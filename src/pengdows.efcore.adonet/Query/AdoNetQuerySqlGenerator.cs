using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using pengdows.efcore.adonet.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Query;

/// <summary>EF's relational SQL, with paging, functions, <c>LongCount</c> and bulk DML as the dialect writes them.</summary>
internal class AdoNetQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect, ITypeMappingDialect typeMappings, bool reverseNullOrdering = false)
    : QuerySqlGenerator(dependencies), IQuerySqlWriter
{
    private TableExpression? _qualifyWithTableName;

    protected override Expression VisitDelete(DeleteExpression deleteExpression)
        => dialect.Dml.TryGenerateDelete(this, deleteExpression) ? deleteExpression : base.VisitDelete(deleteExpression);

    protected override Expression VisitUpdate(UpdateExpression updateExpression)
        => dialect.Dml.TryGenerateUpdate(this, updateExpression) ? updateExpression : base.VisitUpdate(updateExpression);

    // EF's SELECT 1 AS "Value" UNION ALL VALUES (2), (3), with the dialect's prefix before each row after the first.
    protected override void GenerateValues(ValuesExpression valuesExpression)
    {
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

    // A value inside a JSON column, with the dialect's JSON functions: a sub-document (structural type or primitive
    // collection) as JSON, a string as text, any other type returned as its store type. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Query/Internal/SqlServerQuerySqlGenerator.cs
    protected override Expression VisitJsonScalar(JsonScalarExpression jsonScalarExpression)
    {
        if (jsonScalarExpression.Path.Count == 0)
        {
            Visit(jsonScalarExpression.Json);
            return jsonScalarExpression;
        }

        var json = dialect.Json
            ?? throw new InvalidOperationException("This query reads a value inside a JSON column, which the dialect has no functions for.");
        var typeMapping = jsonScalarExpression.TypeMapping;
        var template = (Nullable.GetUnderlyingType(jsonScalarExpression.Type) ?? jsonScalarExpression.Type) == typeof(byte[]) && json.BinaryValue != null ? json.BinaryValue
            : typeMapping is Storage.AdoNetJsonTypeMapping || typeMapping?.ElementTypeMapping != null ? json.Query
            : typeMapping is not null
                && (Nullable.GetUnderlyingType(typeMapping.ClrType) ?? typeMapping.ClrType) == typeof(bool)
                && typeMapping.Converter?.ProviderClrType != typeof(string)
                && (json.BooleanValue != null) ? json.BooleanValue
            : typeMapping is null or StringTypeMapping ? json.Value
            : json.TypedValue;

        WriteTemplate(
            template,
            () => Visit(jsonScalarExpression.Json),
            () => WriteJsonPath(json, jsonScalarExpression.Path),
            () => Sql.Append(typeMappings.CastTarget(typeMapping!.StoreType)));
        return jsonScalarExpression;
    }

    // A JSON scalar is written as a function call (the dialect's templates), so it never needs parentheses.
    protected override bool RequiresParentheses(SqlExpression outerExpression, SqlExpression innerExpression)
        => innerExpression is not JsonScalarExpression && base.RequiresParentheses(outerExpression, innerExpression);

    // '$.a[0].b' when every array index is a constant; otherwise the constant runs joined to the computed indexes, each cast
    // to text, with the dialect's concatenation.
    private void WriteJsonPath(JsonSyntax json, IReadOnlyList<PathSegment> path)
    {
        var text = JsonPathText.Of(path, Dependencies.SqlGenerationHelper);
        var parts = new List<Action>();
        for (var i = 0; i < text.Indexes.Count; i++)
        {
            var literal = text.Literals[i];
            var computed = text.Indexes[i];
            parts.Add(() => Sql.Append(dialect.StringLiteral(literal)));
            parts.Add(() => WriteCast(computed, typeMappings.GetStoreType(typeof(string))!));
        }

        var rest = text.Literals[^1];
        parts.Add(() => Sql.Append(dialect.StringLiteral(rest)));
        WriteConcatenation(json.Concatenate, parts, 0);
    }

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
        if (extensionExpression is JsonArrayTableExpression table)
        {
            var helper = Dependencies.SqlGenerationHelper;
            WriteTemplate(
                dialect.Json!.ArrayElements!,
                () => Visit(table.Json),
                () => Sql.Append(helper.DelimitIdentifier(JsonArrayTableExpression.OrdinalColumn)),
                () => Sql.Append(helper.DelimitIdentifier(JsonArrayTableExpression.ValueColumn)),
                () => Sql.Append(ArrayElementType(table.ElementTypeMapping!)));
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

        // A native JSON column compared with JSON text: a bare string is a JSON string scalar to some databases, which
        // equals no document, so the text is turned into a JSON value first.
        if (sqlBinaryExpression is { OperatorType: ExpressionType.Equal or ExpressionType.NotEqual }
            && dialect.Json is { } json
            && FindJsonTextOperand(sqlBinaryExpression, json) is { } text)
        {
            var jsonOnLeft = IsJsonDocument(sqlBinaryExpression.Left, json);
            void Operand(Expression e)
            {
                if (ReferenceEquals(e, text))
                {
                    SqlTemplate.Write(json.FromText, [e], s => Sql.Append(s), x => Visit(x));
                }
                else
                {
                    Visit(e);
                }
            }

            Operand(sqlBinaryExpression.Left);
            Sql.Append(sqlBinaryExpression.OperatorType == ExpressionType.Equal ? " = " : " <> ");
            Operand(sqlBinaryExpression.Right);
            return sqlBinaryExpression;
        }

        return base.VisitSqlBinary(sqlBinaryExpression);
    }

    private static bool IsJsonDocument(SqlExpression operand, JsonSyntax json)
        => operand is ColumnExpression && string.Equals(operand.TypeMapping?.StoreType, json.StoreType, StringComparison.OrdinalIgnoreCase);

    private static SqlExpression? FindJsonTextOperand(SqlBinaryExpression expression, JsonSyntax json)
    {
        if (IsJsonDocument(expression.Left, json)
            && expression.Right is SqlConstantExpression or SqlParameterExpression)
        {
            return expression.Right;
        }

        if (IsJsonDocument(expression.Right, json)
            && expression.Left is SqlConstantExpression or SqlParameterExpression)
        {
            return expression.Left;
        }

        return null;
    }

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

    // Text elements in the dialect's text form; binary ones are read as their base64 text, then decoded.
    private string ArrayElementType(RelationalTypeMapping element)
    {
        var json = dialect.Json!;
        var storeType = (element.ClrType == typeof(byte[])) && (json.ArrayElementsBinaryValue != null)
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

internal class AdoNetQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect, ITypeMappingDialect typeMappings, IAdoNetSingletonOptions options)
    : IQuerySqlGeneratorFactory
{
    public virtual QuerySqlGenerator Create()
        => new AdoNetQuerySqlGenerator(dependencies, dialect, typeMappings, options.ReverseNullOrdering);
}

/// <summary>
///     The settings from the context's options that the singleton services read. EF initializes this from the options of the
///     first context that uses the service provider, however the provider was built, and checks the others against it.
/// </summary>
internal interface IAdoNetSingletonOptions : ISingletonOptions
{
    /// <summary>Whether every ORDER BY says where nulls sort; see <see cref="AdoNetOptionsExtension.ReverseNullOrdering" />.</summary>
    bool ReverseNullOrdering { get; }
}

internal sealed class AdoNetSingletonOptions : IAdoNetSingletonOptions
{
    public bool ReverseNullOrdering { get; private set; }

    public void Initialize(IDbContextOptions options)
        => ReverseNullOrdering = options.FindExtension<AdoNetOptionsExtension>()?.ReverseNullOrdering ?? false;

    public void Validate(IDbContextOptions options)
    {
        var requested = options.FindExtension<AdoNetOptionsExtension>()?.ReverseNullOrdering ?? false;
        if (requested != ReverseNullOrdering)
        {
            throw new InvalidOperationException(
                "ReverseNullOrdering changed between contexts that share a service provider. Set it the same on every one, "
                + "or give each its own service provider.");
        }
    }
}
