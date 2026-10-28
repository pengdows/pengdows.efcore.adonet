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
    internal const string FullTextParserAnnotation = "MySql:FullTextParser";

    /// <summary>
    ///     Makes the index a FULLTEXT index, which MATCH ... AGAINST searches; <paramref name="parser" /> names the full-text
    ///     parser plugin it uses (<c>WITH PARSER ngram</c>), null for the server's own.
    /// </summary>
    public static IndexBuilder IsFullText(this IndexBuilder indexBuilder, bool fullText = true, string? parser = null)
        => indexBuilder
            .HasAnnotation(FullTextAnnotation, fullText)
            .HasAnnotation(FullTextParserAnnotation, parser == null ? null : MySqlModelOptionsExtensions.Name(parser, nameof(parser)));

    /// <summary>
    ///     Makes the index a FULLTEXT index, which MATCH ... AGAINST searches; <paramref name="parser" /> names the full-text
    ///     parser plugin it uses (<c>WITH PARSER ngram</c>), null for the server's own.
    /// </summary>
    public static IndexBuilder<TEntity> IsFullText<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool fullText = true, string? parser = null)
        => (IndexBuilder<TEntity>)IsFullText((IndexBuilder)indexBuilder, fullText, parser);

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
internal sealed partial class MySqlAnnotationProvider(RelationalAnnotationProviderDependencies dependencies) : AdoNetAnnotationProvider(dependencies)
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

        if (index.MappedIndexes.Select(i => i.FindAnnotation(MySqlFullTextExtensions.FullTextParserAnnotation)?.Value)
                .OfType<string>().FirstOrDefault() is { } parser)
        {
            yield return new Annotation(MySqlFullTextExtensions.FullTextParserAnnotation, parser);
        }
    }

    // A key's prefix lengths, on its primary key or unique constraint.
    public override IEnumerable<IAnnotation> For(IUniqueConstraint constraint, bool designTime)
    {
        foreach (var annotation in base.For(constraint, designTime))
        {
            yield return annotation;
        }

        if (constraint.MappedKeys.Select(k => k.FindAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation)?.Value)
                .OfType<int[]>().FirstOrDefault() is { } prefixLengths)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation, prefixLengths);
        }
    }

    // A property's character set and spatial reference system, on its column; the model's GUID collation on a GUID column
    // without its own.
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if ((column.PropertyMappings.Select(m => m.Property.FindAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation)?.Value)
                    .OfType<string>().FirstOrDefault()
                ?? (designTime ? InheritedCharSet(column) : null)) is { } charSet)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet);
        }

        // Design time only: the read-optimized model keeps no collation.
        if (designTime && (column.Collation == null) && IsText(column) && (Entity(column.Table) is { } entityType)
            && (MySqlDelegation.EntityCollation(entityType, DelegationModes.ApplyToColumns) is { } inheritedCollation))
        {
            yield return new Annotation(InheritedCollationAnnotation, inheritedCollation);
        }

        if (column.PropertyMappings.Any(m => m.Property.FindAnnotation(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation)?.Value is MySqlModelOptionsExtensions.ComputedColumnStrategy))
        {
            yield return new Annotation(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation, MySqlModelOptionsExtensions.ComputedColumnStrategy);
        }

        if (column.PropertyMappings.Select(m => m.Property.FindAnnotation(MySqlModelOptionsExtensions.SpatialReferenceSystemAnnotation)?.Value)
                .OfType<int>().Cast<int?>().FirstOrDefault() is { } srid)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.SpatialReferenceSystemAnnotation, srid);
        }

        // Design time only: the read-optimized model keeps no collation.
        if (designTime
            && (column.Table.Model.Model.FindAnnotation(MySqlModelOptionsExtensions.GuidCollationAnnotation)?.Value is string guidCollation)
            && column.PropertyMappings.Any(m => (Nullable.GetUnderlyingType(m.Property.ClrType) ?? m.Property.ClrType) == typeof(Guid))
            && (column.Collation == null))
        {
            yield return new Annotation(MySqlModelOptionsExtensions.GuidCollationAnnotation, guidCollation);
        }
    }

    // The table's character set and collation, its entity type's or the model's as their delegation reaches tables, and its
    // options.
    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        foreach (var annotation in base.For(table, designTime))
        {
            yield return annotation;
        }

        if (!designTime || (Entity(table) is not { } entityType))
        {
            yield break;
        }

        if (MySqlDelegation.EntityCharSet(entityType, DelegationModes.ApplyToTables) is { } charSet)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet);
        }

        if (MySqlDelegation.EntityCollation(entityType, DelegationModes.ApplyToTables) is { } collation)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CollationAnnotation, collation);
        }

        if (entityType[MySqlModelOptionsExtensions.StoreOptionsAnnotation] is string options)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.StoreOptionsAnnotation, options);
        }
    }

    // The database's character set and collation, where the model's delegation reaches it (ALTER DATABASE). EF's differ
    // reads a database operation's collation from this annotation, and gives a new database no other.
    public override IEnumerable<IAnnotation> For(IRelationalModel model, bool designTime)
    {
        foreach (var annotation in base.For(model, designTime))
        {
            yield return annotation;
        }

        if (!designTime)
        {
            yield break;
        }

        if (MySqlDelegation.ModelCharSet(model.Model, DelegationModes.ApplyToDatabases) is { } charSet)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet);
        }

        if (MySqlDelegation.ModelCollation(model.Model, DelegationModes.ApplyToDatabases) is { } collation)
        {
            yield return new Annotation(MySqlModelOptionsExtensions.CollationAnnotation, collation);
        }
    }

    // A column's collation from its entity type's or the model's delegation, where it has none of its own.
    internal const string InheritedCollationAnnotation = "MySql:InheritedCollation";

    private static IEntityType? Entity(ITable table)
        => table.EntityTypeMappings.Select(m => m.TypeBase).OfType<IEntityType>().FirstOrDefault();

    // A text column inherits a character set unless its own collation names another.
    private static string? InheritedCharSet(IColumn column)
    {
        if (!IsText(column) || (Entity(column.Table) is not { } entityType)
            || (MySqlDelegation.EntityCharSet(entityType, DelegationModes.ApplyToColumns) is not { } charSet))
        {
            return null;
        }

        return (column.Collation is { } collation) && !MySqlDelegation.Compatible(collation, charSet) ? null : charSet;
    }

    // A string stored as text; not JSON, which takes no character set.
    private static bool IsText(IColumn column)
        => column.PropertyMappings.All(m => m.Property.ClrType == typeof(string))
           && TextStoreType().IsMatch(column.StoreType);

    [System.Text.RegularExpressions.GeneratedRegex(@"^(?:N?(?:VAR)?CHAR|(?:TINY|MEDIUM|LONG)?TEXT|ENUM|SET)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex TextStoreType();
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
