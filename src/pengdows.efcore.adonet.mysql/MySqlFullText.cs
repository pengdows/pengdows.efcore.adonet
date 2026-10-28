using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Metadata;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.mysql;

/// <summary>How MATCH ... AGAINST reads its pattern.</summary>
public enum MySqlMatchSearchMode
{
    /// <summary>IN NATURAL LANGUAGE MODE, the default.</summary>
    NaturalLanguage,

    /// <summary>IN NATURAL LANGUAGE MODE WITH QUERY EXPANSION.</summary>
    NaturalLanguageWithQueryExpansion,

    /// <summary>IN BOOLEAN MODE: + and - require or exclude a word.</summary>
    Boolean
}

/// <summary>
///     MySQL's full-text search, as Pomelo has it: <see cref="IsFullText(IndexBuilder, bool)" /> makes an index FULLTEXT, which
///     <c>EF.Functions.Match</c> and <c>IsMatch</c> search. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlIndexBuilderExtensions.cs
/// </summary>
public static class MySqlFullTextExtensions
{
    internal const string FullTextAnnotation = "MySql:FullTextIndex";

    /// <summary>Makes the index a FULLTEXT index, which MATCH ... AGAINST searches.</summary>
    public static IndexBuilder IsFullText(this IndexBuilder indexBuilder, bool fullText = true)
        => indexBuilder.HasAnnotation(FullTextAnnotation, fullText);

    /// <summary>Makes the index a FULLTEXT index, which MATCH ... AGAINST searches.</summary>
    public static IndexBuilder<TEntity> IsFullText<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool fullText = true)
        => indexBuilder.HasAnnotation(FullTextAnnotation, fullText);

    /// <summary>The relevance of <paramref name="property" /> to <paramref name="pattern" />: MATCH (property) AGAINST (pattern).</summary>
    public static double Match(this DbFunctions _, string property, string pattern, MySqlMatchSearchMode searchMode)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Match)));

    /// <summary>Whether <paramref name="property" /> matches <paramref name="pattern" />: its MATCH ... AGAINST relevance is above 0.</summary>
    public static bool IsMatch(this DbFunctions _, string property, string pattern, MySqlMatchSearchMode searchMode)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsMatch)));

    /// <summary>
    ///     The relevance of <paramref name="properties" />, together, to <paramref name="pattern" />: MATCH (a, b, ...) AGAINST
    ///     (pattern). The properties are written in the query as an array (<c>new[] { a.Title, a.Body }</c>), and a FULLTEXT
    ///     index must cover exactly them.
    /// </summary>
    public static double Match(this DbFunctions _, string[] properties, string pattern, MySqlMatchSearchMode searchMode)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Match)));

    /// <summary>
    ///     Whether <paramref name="properties" />, together, match <paramref name="pattern" />: their MATCH ... AGAINST relevance
    ///     is above 0. The properties are written in the query as an array (<c>new[] { a.Title, a.Body }</c>).
    /// </summary>
    public static bool IsMatch(this DbFunctions _, string[] properties, string pattern, MySqlMatchSearchMode searchMode)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsMatch)));

    // Two columns of a MATCH over several, "a, b": a query's array of them is rewritten to these before it is translated.
    internal static string Columns(string first, string rest)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Columns)));
}

/// <summary>
///     Rewrites MATCH over an array of columns (<c>new[] { a.Title, a.Body }</c>), which EF can't translate as an argument,
///     to MATCH over their list, before the query is translated. Behaviour reference (not copied): Pomelo's translation of the
///     array in MySqlSqlTranslatingExpressionVisitor:
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionVisitors/Internal/MySqlSqlTranslatingExpressionVisitor.cs
/// </summary>
internal sealed class MySqlFullTextColumnsRewriter : ExpressionVisitor
{
    private static readonly MethodInfo ColumnsMethod
        = typeof(MySqlFullTextExtensions).GetMethod(nameof(MySqlFullTextExtensions.Columns), BindingFlags.NonPublic | BindingFlags.Static)!;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var result = base.VisitMethodCall(node);
        if ((result is not MethodCallExpression visited)
            || (visited.Method.DeclaringType != typeof(MySqlFullTextExtensions))
            || (visited.Arguments.Count != 4)
            || (visited.Arguments[1] is not NewArrayExpression { NodeType: ExpressionType.NewArrayInit, Expressions.Count: > 0 } columns))
        {
            return result;
        }

        var single = typeof(MySqlFullTextExtensions).GetMethod(
            visited.Method.Name,
            [typeof(DbFunctions), typeof(string), typeof(string), typeof(MySqlMatchSearchMode)])!;
        var list = columns.Expressions.Reverse().Aggregate((rest, first) => Expression.Call(ColumnsMethod, first, rest));
        return Expression.Call(single, visited.Arguments[0], list, visited.Arguments[2], visited.Arguments[3]);
    }
}

internal sealed class MySqlQueryTranslationPreprocessorFactory(
    QueryTranslationPreprocessorDependencies dependencies,
    RelationalQueryTranslationPreprocessorDependencies relationalDependencies) : IQueryTranslationPreprocessorFactory
{
    public QueryTranslationPreprocessor Create(QueryCompilationContext queryCompilationContext)
        => new MySqlQueryTranslationPreprocessor(dependencies, relationalDependencies, queryCompilationContext);
}

internal sealed class MySqlQueryTranslationPreprocessor(
    QueryTranslationPreprocessorDependencies dependencies,
    RelationalQueryTranslationPreprocessorDependencies relationalDependencies,
    QueryCompilationContext queryCompilationContext)
    : RelationalQueryTranslationPreprocessor(dependencies, relationalDependencies, queryCompilationContext)
{
    public override Expression Process(Expression query)
        => base.Process(new MySqlFullTextColumnsRewriter().Visit(query));
}

/// <summary>Carries <see cref="MySqlFullTextExtensions.IsFullText(IndexBuilder, bool)" /> to the indexes migrations see.</summary>
internal sealed class MySqlAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : AdoNetAnnotationProvider(dependencies)
{
    public override IEnumerable<IAnnotation> For(ITableIndex index, bool designTime)
    {
        foreach (var annotation in base.For(index, designTime))
        {
            yield return annotation;
        }

        if (index.MappedIndexes.Any(i => i.FindAnnotation(MySqlFullTextExtensions.FullTextAnnotation)?.Value is true))
        {
            yield return new Annotation(MySqlFullTextExtensions.FullTextAnnotation, true);
        }

        if (index.MappedIndexes.Any(i => i.FindAnnotation(MySqlModelOptionsExtensions.SpatialAnnotation)?.Value is true))
        {
            yield return new Annotation(MySqlModelOptionsExtensions.SpatialAnnotation, true);
        }

        if (index.MappedIndexes.Select(i => i.FindAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation)?.Value)
                .OfType<int[]>().FirstOrDefault() is { } prefixLengths)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation, prefixLengths);
        }
    }

    // A property's character set, on its column.
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (column.PropertyMappings.Select(m => m.Property.FindAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation)?.Value)
                .OfType<string>().FirstOrDefault() is { } charSet)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet);
        }
    }

    // An entity type's character set, or the model's, on its table.
    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        foreach (var annotation in base.For(table, designTime))
        {
            yield return annotation;
        }

        if ((table.EntityTypeMappings.Select(m => m.TypeBase.FindAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation)?.Value)
                    .OfType<string>().FirstOrDefault()
                ?? table.Model.Model.FindAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation)?.Value as string) is { } charSet)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet);
        }
    }
}

internal sealed class MySqlFullTextTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } = [new MySqlFullTextTranslator(sqlExpressionFactory, typeMappingSource)];
}

/// <summary>
///     MATCH (property) AGAINST (pattern [IN ... MODE]); IsMatch is its relevance above 0. Several columns arrive as a list
///     (<see cref="MySqlFullTextExtensions.Columns" />). The search mode is a constant, or for IsMatch a parameter: each mode's
///     MATCH, chosen by OR, since MariaDB takes no MATCH inside CASE and OR keeps the FULLTEXT index usable. Behaviour reference
///     (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Query/ExpressionTranslators/Internal/MySqlDbFunctionsExtensionsMethodTranslator.cs
/// </summary>
internal sealed class MySqlFullTextTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(MySqlFullTextExtensions))
        {
            return null;
        }

        if (method.Name == nameof(MySqlFullTextExtensions.Columns))
        {
            var columnMapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(arguments[0], arguments[1])
                ?? typeMappingSource.FindMapping(typeof(string));
            return sqlExpressionFactory.Call(
                "{0}, {1}",
                [sqlExpressionFactory.ApplyTypeMapping(arguments[0], columnMapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[1], columnMapping)!],
                typeof(string),
                columnMapping);
        }

        if (method.Name is not (nameof(MySqlFullTextExtensions.Match) or nameof(MySqlFullTextExtensions.IsMatch)))
        {
            return null;
        }

        var mapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2])
            ?? typeMappingSource.FindMapping(typeof(string));
        var operands = new[] { sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[2], mapping)! };
        var isMatch = method.Name == nameof(MySqlFullTextExtensions.IsMatch);
        if (arguments[3] is SqlConstantExpression { Value: MySqlMatchSearchMode mode })
        {
            return isMatch ? Matches(mode, operands) : Relevance(mode, operands);
        }

        if (!isMatch || (arguments[3] is not SqlParameterExpression parameter))
        {
            return null;
        }

        return Enum.GetValues<MySqlMatchSearchMode>()
            .OrderByDescending(m => m)
            .Select(m => sqlExpressionFactory.AndAlso(sqlExpressionFactory.Equal(parameter, sqlExpressionFactory.Constant(m)), Matches(m, operands)))
            .Aggregate((either, or) => sqlExpressionFactory.OrElse(either, or));
    }

    private static string Match(MySqlMatchSearchMode mode)
        => "MATCH ({0}) AGAINST ({1}" + mode switch
        {
            MySqlMatchSearchMode.Boolean => " IN BOOLEAN MODE",
            MySqlMatchSearchMode.NaturalLanguageWithQueryExpansion => " WITH QUERY EXPANSION",
            _ => ""
        } + ")";

    private SqlExpression Relevance(MySqlMatchSearchMode mode, SqlExpression[] operands)
        => sqlExpressionFactory.Call(Match(mode), operands, typeof(double), typeMappingSource.FindMapping(typeof(double)))!;

    private SqlExpression Matches(MySqlMatchSearchMode mode, SqlExpression[] operands)
        => sqlExpressionFactory.Call("(" + Match(mode) + " > 0)", operands, typeof(bool), typeMappingSource.FindMapping(typeof(bool)))!;
}
