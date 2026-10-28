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

/// <summary>MATCH (property) AGAINST (pattern [IN ... MODE]); IsMatch is its relevance above 0. The search mode is a constant.</summary>
internal sealed class MySqlFullTextTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType != typeof(MySqlFullTextExtensions))
            || (method.Name is not (nameof(MySqlFullTextExtensions.Match) or nameof(MySqlFullTextExtensions.IsMatch)))
            || (arguments[3] is not SqlConstantExpression { Value: MySqlMatchSearchMode mode }))
        {
            return null;
        }

        var mapping = Microsoft.EntityFrameworkCore.Query.ExpressionExtensions.InferTypeMapping(arguments[1], arguments[2])
            ?? typeMappingSource.FindMapping(typeof(string));
        var modifier = mode switch
        {
            MySqlMatchSearchMode.Boolean => " IN BOOLEAN MODE",
            MySqlMatchSearchMode.NaturalLanguageWithQueryExpansion => " WITH QUERY EXPANSION",
            _ => ""
        };
        var match = "MATCH ({0}) AGAINST ({1}" + modifier + ")";
        var operands = new[] { sqlExpressionFactory.ApplyTypeMapping(arguments[1], mapping)!, sqlExpressionFactory.ApplyTypeMapping(arguments[2], mapping)! };
        return method.Name == nameof(MySqlFullTextExtensions.Match)
            ? sqlExpressionFactory.Call(match, operands, typeof(double), typeMappingSource.FindMapping(typeof(double)))
            : sqlExpressionFactory.Call("(" + match + " > 0)", operands, typeof(bool), typeMappingSource.FindMapping(typeof(bool)));
    }
}
