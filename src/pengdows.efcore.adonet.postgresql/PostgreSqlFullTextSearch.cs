using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using NpgsqlTypes;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>How <c>ts_rank</c> and <c>ts_rank_cd</c> normalize a rank by the document's length; flags combine.</summary>
[Flags]
public enum PostgreSqlTsRankingNormalization
{
    /// <summary>Ignores the document's length.</summary>
    Default = 0,

    /// <summary>Divides the rank by 1 + the logarithm of the document's length.</summary>
    DivideBy1PlusLogLength = 1,

    /// <summary>Divides the rank by the document's length.</summary>
    DivideByLength = 2,

    /// <summary>Divides the rank by the mean harmonic distance between extents (ts_rank_cd only).</summary>
    DivideByMeanHarmonicDistanceBetweenExtents = 4,

    /// <summary>Divides the rank by the number of unique words in the document.</summary>
    DivideByUniqueWordCount = 8,

    /// <summary>Divides the rank by 1 + the logarithm of the number of unique words in the document.</summary>
    DividesBy1PlusLogUniqueWordCount = 16,

    /// <summary>Divides the rank by itself + 1.</summary>
    DivideByItselfPlusOne = 32
}

/// <summary>
///     PostgreSQL's full-text search functions for LINQ queries, as Npgsql's <c>NpgsqlFullTextSearchDbFunctionsExtensions</c> has
///     them. A configuration is a constant (<c>"english"</c>) or a parameter, cast to regconfig. Each throws when called outside a
///     query. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlFullTextSearchDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlFullTextSearchDbFunctionsExtensions
{
    /// <summary><c>array_to_tsvector</c>: the lexemes as a tsvector.</summary>
    public static NpgsqlTsVector ArrayToTsVector(this DbFunctions _, string[] lexemes)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ArrayToTsVector)));

    /// <summary><c>to_tsvector</c>: the document reduced to a tsvector with the default configuration.</summary>
    public static NpgsqlTsVector ToTsVector(this DbFunctions _, string document)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToTsVector)));

    /// <summary><c>to_tsvector</c>: the document reduced to a tsvector with <paramref name="config" />.</summary>
    public static NpgsqlTsVector ToTsVector(this DbFunctions _, string config, string document)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToTsVector)));

    /// <summary><c>plainto_tsquery</c>: a query of the words, punctuation ignored.</summary>
    public static NpgsqlTsQuery PlainToTsQuery(this DbFunctions _, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(PlainToTsQuery)));

    /// <summary><c>plainto_tsquery</c> with <paramref name="config" />.</summary>
    public static NpgsqlTsQuery PlainToTsQuery(this DbFunctions _, string config, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(PlainToTsQuery)));

    /// <summary><c>phraseto_tsquery</c>: a query of the words as a phrase, punctuation ignored.</summary>
    public static NpgsqlTsQuery PhraseToTsQuery(this DbFunctions _, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(PhraseToTsQuery)));

    /// <summary><c>phraseto_tsquery</c> with <paramref name="config" />.</summary>
    public static NpgsqlTsQuery PhraseToTsQuery(this DbFunctions _, string config, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(PhraseToTsQuery)));

    /// <summary><c>to_tsquery</c>: the query in tsquery syntax, its words normalized.</summary>
    public static NpgsqlTsQuery ToTsQuery(this DbFunctions _, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToTsQuery)));

    /// <summary><c>to_tsquery</c> with <paramref name="config" />.</summary>
    public static NpgsqlTsQuery ToTsQuery(this DbFunctions _, string config, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToTsQuery)));

    /// <summary><c>websearch_to_tsquery</c>: the query in web search syntax (quotes, or, -).</summary>
    public static NpgsqlTsQuery WebSearchToTsQuery(this DbFunctions _, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(WebSearchToTsQuery)));

    /// <summary><c>websearch_to_tsquery</c> with <paramref name="config" />.</summary>
    public static NpgsqlTsQuery WebSearchToTsQuery(this DbFunctions _, string config, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(WebSearchToTsQuery)));

    /// <summary><c>unaccent</c> (the unaccent extension): the text without diacritics.</summary>
    public static string Unaccent(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Unaccent)));

    /// <summary><c>unaccent</c> with the dictionary <paramref name="regDictionary" />.</summary>
    public static string Unaccent(this DbFunctions _, string regDictionary, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Unaccent)));
}

/// <summary>
///     PostgreSQL's tsvector and tsquery operators and functions for LINQ queries, as Npgsql's
///     <c>NpgsqlFullTextSearchLinqExtensions</c> has them; each throws when called outside a query. Behaviour reference (not
///     copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlFullTextSearchLinqExtensions.cs
/// </summary>
public static class PostgreSqlFullTextSearchLinqExtensions
{
    /// <summary>Both queries (<c>&amp;&amp;</c>).</summary>
    public static NpgsqlTsQuery And(this NpgsqlTsQuery query1, NpgsqlTsQuery query2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(And)));

    /// <summary>Either query (<c>||</c>).</summary>
    public static NpgsqlTsQuery Or(this NpgsqlTsQuery query1, NpgsqlTsQuery query2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Or)));

    /// <summary>The query negated (<c>!!</c>).</summary>
    public static NpgsqlTsQuery ToNegative(this NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToNegative)));

    /// <summary>Whether <paramref name="query1" /> contains <paramref name="query2" /> (<c>@&gt;</c>).</summary>
    public static bool Contains(this NpgsqlTsQuery query1, NpgsqlTsQuery query2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>Whether <paramref name="query1" /> is contained in <paramref name="query2" /> (<c>&lt;@</c>).</summary>
    public static bool IsContainedIn(this NpgsqlTsQuery query1, NpgsqlTsQuery query2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(IsContainedIn)));

    /// <summary>The number of lexemes and operators in the query (<c>numnode</c>).</summary>
    public static int GetNodeCount(this NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GetNodeCount)));

    /// <summary>The indexable part of the query (<c>querytree</c>).</summary>
    public static string GetQueryTree(this NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GetQueryTree)));

    /// <summary>The document with the query's matches marked (<c>ts_headline</c>).</summary>
    public static string GetResultHeadline(this NpgsqlTsQuery query, string document)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GetResultHeadline)));

    /// <summary><c>ts_headline</c> with its <paramref name="options" /> (<c>MaxWords=..., StartSel=...</c>).</summary>
    public static string GetResultHeadline(this NpgsqlTsQuery query, string document, string options)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GetResultHeadline)));

    /// <summary><c>ts_headline</c> with a configuration and options.</summary>
    public static string GetResultHeadline(this NpgsqlTsQuery query, string config, string document, string options)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GetResultHeadline)));

    /// <summary>The query with <paramref name="target" /> replaced by <paramref name="substitute" /> (<c>ts_rewrite</c>).</summary>
    public static NpgsqlTsQuery Rewrite(this NpgsqlTsQuery query, NpgsqlTsQuery target, NpgsqlTsQuery substitute)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Rewrite)));

    /// <summary>The query rewritten by each row of <paramref name="select" /> (target, substitute) (<c>ts_rewrite</c>).</summary>
    public static NpgsqlTsQuery Rewrite(this NpgsqlTsQuery query, string select)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Rewrite)));

    /// <summary>A match of <paramref name="query1" /> followed by one of <paramref name="query2" /> (<c>tsquery_phrase</c>).</summary>
    public static NpgsqlTsQuery ToPhrase(this NpgsqlTsQuery query1, NpgsqlTsQuery query2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToPhrase)));

    /// <summary><c>tsquery_phrase</c>, the second match <paramref name="distance" /> lexemes after the first.</summary>
    public static NpgsqlTsQuery ToPhrase(this NpgsqlTsQuery query1, NpgsqlTsQuery query2, int distance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToPhrase)));

    /// <summary>Whether the vector matches the words of <paramref name="query" /> (<c>@@ plainto_tsquery</c>).</summary>
    public static bool Matches(this NpgsqlTsVector vector, string query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Matches)));

    /// <summary>Whether the vector matches the query (<c>@@</c>).</summary>
    public static bool Matches(this NpgsqlTsVector vector, NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Matches)));

    /// <summary>Both vectors' lexemes and positions (<c>||</c>).</summary>
    public static NpgsqlTsVector Concat(this NpgsqlTsVector vector1, NpgsqlTsVector vector2)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Concat)));

    /// <summary>Every lexeme given <paramref name="weight" /> (<c>setweight</c>).</summary>
    public static NpgsqlTsVector SetWeight(this NpgsqlTsVector vector, NpgsqlTsVector.Lexeme.Weight weight)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetWeight)));

    /// <summary>The <paramref name="lexemes" /> given <paramref name="weight" /> (<c>setweight</c>).</summary>
    public static NpgsqlTsVector SetWeight(this NpgsqlTsVector vector, NpgsqlTsVector.Lexeme.Weight weight, string[] lexemes)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetWeight)));

    /// <summary>Every lexeme given <paramref name="weight" />, A to D (<c>setweight</c>).</summary>
    public static NpgsqlTsVector SetWeight(this NpgsqlTsVector vector, char weight)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetWeight)));

    /// <summary>The <paramref name="lexemes" /> given <paramref name="weight" />, A to D (<c>setweight</c>).</summary>
    public static NpgsqlTsVector SetWeight(this NpgsqlTsVector vector, char weight, string[] lexemes)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetWeight)));

    /// <summary>The vector without <paramref name="lexeme" /> (<c>ts_delete</c>).</summary>
    public static NpgsqlTsVector Delete(this NpgsqlTsVector vector, string lexeme)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Delete)));

    /// <summary>The vector without <paramref name="lexemes" /> (<c>ts_delete</c>).</summary>
    public static NpgsqlTsVector Delete(this NpgsqlTsVector vector, string[] lexemes)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Delete)));

    /// <summary>Only the lexemes of the constant <paramref name="weights" /> (<c>ts_filter</c>).</summary>
    public static NpgsqlTsVector Filter(this NpgsqlTsVector vector, char[] weights)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Filter)));

    /// <summary>The number of lexemes (<c>length</c>).</summary>
    public static int GetLength(this NpgsqlTsVector vector)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GetLength)));

    /// <summary>The vector without weights and positions (<c>strip</c>).</summary>
    public static NpgsqlTsVector ToStripped(this NpgsqlTsVector vector)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ToStripped)));

    /// <summary>How well the vector matches the query (<c>ts_rank</c>).</summary>
    public static float Rank(this NpgsqlTsVector vector, NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Rank)));

    /// <summary><c>ts_rank</c>, normalized by the document's length.</summary>
    public static float Rank(this NpgsqlTsVector vector, NpgsqlTsQuery query, PostgreSqlTsRankingNormalization normalization)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Rank)));

    /// <summary><c>ts_rank</c> with the weights of D, C, B and A.</summary>
    public static float Rank(this NpgsqlTsVector vector, float[] weights, NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Rank)));

    /// <summary><c>ts_rank</c> with the weights of D, C, B and A, normalized by the document's length.</summary>
    public static float Rank(this NpgsqlTsVector vector, float[] weights, NpgsqlTsQuery query, PostgreSqlTsRankingNormalization normalization)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Rank)));

    /// <summary>The cover density rank of the vector for the query (<c>ts_rank_cd</c>).</summary>
    public static float RankCoverDensity(this NpgsqlTsVector vector, NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RankCoverDensity)));

    /// <summary><c>ts_rank_cd</c>, normalized by the document's length.</summary>
    public static float RankCoverDensity(this NpgsqlTsVector vector, NpgsqlTsQuery query, PostgreSqlTsRankingNormalization normalization)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RankCoverDensity)));

    /// <summary><c>ts_rank_cd</c> with the weights of D, C, B and A.</summary>
    public static float RankCoverDensity(this NpgsqlTsVector vector, float[] weights, NpgsqlTsQuery query)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RankCoverDensity)));

    /// <summary><c>ts_rank_cd</c> with the weights of D, C, B and A, normalized by the document's length.</summary>
    public static float RankCoverDensity(
        this NpgsqlTsVector vector,
        float[] weights,
        NpgsqlTsQuery query,
        PostgreSqlTsRankingNormalization normalization)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(RankCoverDensity)));
}

/// <summary>
///     A generated tsvector column, kept up to date by the database from other columns of its table, as Npgsql's
///     <c>HasGeneratedTsVectorColumn</c> and <c>IsGeneratedTsVectorColumn</c> configure it:
///     <c>GENERATED ALWAYS AS (to_tsvector(config, coalesce(a, '') || ' ' || coalesce(b, ''))) STORED</c>. Behaviour reference (not
///     copied): https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/BuilderExtensions/NpgsqlEntityTypeBuilderExtensions.cs
/// </summary>
public static class PostgreSqlFullTextSearchModelExtensions
{
    internal const string TsVectorConfigAnnotation = "PostgreSql:TsVectorConfig";
    internal const string TsVectorPropertiesAnnotation = "PostgreSql:TsVectorProperties";

    /// <summary>
    ///     Makes the tsvector property a column the database generates from the properties <paramref name="includeExpression" />
    ///     names (<c>e =&gt; new { e.Title, e.Body }</c>), with the text search configuration <paramref name="config" />.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasGeneratedTsVectorColumn<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        Expression<Func<TEntity, NpgsqlTsVector>> tsVectorPropertyExpression,
        string config,
        Expression<Func<TEntity, object>> includeExpression)
        where TEntity : class
    {
        entityTypeBuilder.Property(tsVectorPropertyExpression).IsGeneratedTsVectorColumn(config, PropertyNames(includeExpression));
        return entityTypeBuilder;
    }

    /// <summary>
    ///     Makes the tsvector property a column the database generates from <paramref name="includedPropertyNames" /> of its entity
    ///     type, with the text search configuration <paramref name="config" />.
    /// </summary>
    public static PropertyBuilder<NpgsqlTsVector> IsGeneratedTsVectorColumn(
        this PropertyBuilder<NpgsqlTsVector> propertyBuilder,
        string config,
        params string[] includedPropertyNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(config);
        if (includedPropertyNames.Length == 0)
        {
            throw new ArgumentException("A generated tsvector column needs at least one property to index.", nameof(includedPropertyNames));
        }

        propertyBuilder.HasAnnotation(TsVectorConfigAnnotation, config);
        propertyBuilder.HasAnnotation(TsVectorPropertiesAnnotation, includedPropertyNames);
        propertyBuilder.ValueGeneratedOnAddOrUpdate();
        return propertyBuilder;
    }

    private static string[] PropertyNames<TEntity>(Expression<Func<TEntity, object>> includeExpression)
    {
        var body = includeExpression.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : includeExpression.Body;
        return body switch
        {
            NewExpression created => created.Arguments.Select(MemberName).ToArray(),
            MemberExpression member => [member.Member.Name],
            _ => throw new ArgumentException("The included properties must be a property or an anonymous type of properties.", nameof(includeExpression))
        };
    }

    private static string MemberName(Expression expression)
        => expression is MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException($"'{expression}' is not a property access.", nameof(expression));
}

/// <summary>
///     Translates <see cref="PostgreSqlFullTextSearchDbFunctionsExtensions" /> and <see cref="PostgreSqlFullTextSearchLinqExtensions" />.
///     Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlFullTextSearchMethodTranslator.cs
/// </summary>
internal sealed class PostgreSqlFullTextSearchTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    private static readonly Dictionary<string, string> ConfigFunctions = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.ToTsVector)] = "to_tsvector",
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.PlainToTsQuery)] = "plainto_tsquery",
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.PhraseToTsQuery)] = "phraseto_tsquery",
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.ToTsQuery)] = "to_tsquery",
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.WebSearchToTsQuery)] = "websearch_to_tsquery",
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.ArrayToTsVector)] = "array_to_tsvector",
        [nameof(PostgreSqlFullTextSearchDbFunctionsExtensions.Unaccent)] = "unaccent"
    };

    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlFullTextSearchLinqExtensions.And)] = "&&",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.Or)] = "||",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.Contains)] = "@>",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.IsContainedIn)] = "<@",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.Concat)] = "||"
    };

    private static readonly Dictionary<string, string> Functions = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlFullTextSearchLinqExtensions.GetNodeCount)] = "numnode",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.GetQueryTree)] = "querytree",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.Rewrite)] = "ts_rewrite",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.ToPhrase)] = "tsquery_phrase",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.Delete)] = "ts_delete",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.GetLength)] = "length",
        [nameof(PostgreSqlFullTextSearchLinqExtensions.ToStripped)] = "strip"
    };

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType == typeof(PostgreSqlFullTextSearchDbFunctionsExtensions))
        {
            return TranslateDbFunction(method, arguments.Skip(1).ToArray());
        }

        return method.DeclaringType == typeof(PostgreSqlFullTextSearchLinqExtensions)
            ? TranslateLinq(method, arguments)
            : null;
    }

    // to_tsvector(config, document): a configuration other than a constant is cast to regconfig (regdictionary for unaccent),
    // since a bound parameter isn't coerced from text.
    private SqlExpression TranslateDbFunction(MethodInfo method, SqlExpression[] arguments)
    {
        var name = ConfigFunctions[method.Name];
        if (arguments.Length == 2)
        {
            var regType = name == "unaccent" ? "regdictionary" : "regconfig";
            arguments[0] = arguments[0] is SqlConstantExpression
                ? sqlExpressionFactory.ApplyDefaultTypeMapping(arguments[0])
                : sqlExpressionFactory.Convert(arguments[0], typeof(string), typeMappingSource.FindMapping(typeof(string), regType));
        }

        if (name == "array_to_tsvector")
        {
            arguments[0] = sqlExpressionFactory.ApplyTypeMapping(arguments[0], typeMappingSource.FindMapping(typeof(string[]), "text[]"));
        }

        return Function(name, arguments, method.ReturnType);
    }

    private SqlExpression? TranslateLinq(MethodInfo method, IReadOnlyList<SqlExpression> arguments)
    {
        var boolMapping = typeMappingSource.FindMapping(typeof(bool));
        switch (method.Name)
        {
            case nameof(PostgreSqlFullTextSearchLinqExtensions.Matches):
            {
                var query = arguments[1].Type == typeof(string)
                    ? Function("plainto_tsquery", [arguments[1]], typeof(NpgsqlTsQuery))
                    : arguments[1];
                return PostgreSqlArraySql.Binary(sqlExpressionFactory, "@@", arguments[0], query, typeof(bool), boolMapping);
            }

            case nameof(PostgreSqlFullTextSearchLinqExtensions.ToNegative):
            {
                return sqlExpressionFactory.Call("!!({0})", [arguments[0]], method.ReturnType, arguments[0].TypeMapping);
            }

            case nameof(PostgreSqlFullTextSearchLinqExtensions.Rank):
            case nameof(PostgreSqlFullTextSearchLinqExtensions.RankCoverDensity):
            {
                return Rank(method, arguments);
            }

            case nameof(PostgreSqlFullTextSearchLinqExtensions.SetWeight):
            {
                return Weight(arguments[1]) is { } weight
                    ? Function("setweight", [arguments[0], weight, .. arguments.Skip(2).Select(TextArray)], method.ReturnType)
                    : null;
            }

            case nameof(PostgreSqlFullTextSearchLinqExtensions.Filter):
            {
                return arguments[1] is SqlConstantExpression { Value: char[] weights }
                    ? Function(
                        "ts_filter",
                        [arguments[0], sqlExpressionFactory.Fragment("ARRAY[" + string.Join(",", weights.Select(w => Literal(w.ToString()))) + "]::\"char\"[]")],
                        method.ReturnType)
                    : null;
            }

            case nameof(PostgreSqlFullTextSearchLinqExtensions.GetResultHeadline):
            {
                // ts_headline([config,] document, query[, options])
                SqlExpression[] headline = arguments.Count switch
                {
                    2 => [arguments[1], arguments[0]],
                    3 => [arguments[1], arguments[0], arguments[2]],
                    _ => [Config(arguments[1]), arguments[2], arguments[0], arguments[3]]
                };
                return Function("ts_headline", headline, method.ReturnType);
            }
        }

        if (Operators.TryGetValue(method.Name, out var @operator))
        {
            var mapping = method.ReturnType == typeof(bool) ? boolMapping : arguments[0].TypeMapping;
            return PostgreSqlArraySql.Binary(sqlExpressionFactory, @operator, arguments[0], arguments[1], method.ReturnType, mapping);
        }

        if (Functions.TryGetValue(method.Name, out var name))
        {
            return Function(name, arguments.Select(a => a.Type == typeof(string[]) ? TextArray(a) : a).ToArray(), method.ReturnType);
        }

        return null;
    }

    // ts_rank([weights,] vector, query[, normalization]); the weights come first in SQL.
    private SqlExpression Rank(MethodInfo method, IReadOnlyList<SqlExpression> arguments)
    {
        var name = method.Name == nameof(PostgreSqlFullTextSearchLinqExtensions.Rank) ? "ts_rank" : "ts_rank_cd";
        var ordered = new List<SqlExpression>();
        if (arguments[1].Type == typeof(float[]))
        {
            ordered.Add(sqlExpressionFactory.ApplyTypeMapping(arguments[1], typeMappingSource.FindMapping(typeof(float[]), "real[]")));
            ordered.Add(arguments[0]);
        }
        else
        {
            ordered.Add(arguments[0]);
            ordered.Add(arguments[1]);
        }

        foreach (var rest in arguments.Skip(2))
        {
            ordered.Add(rest.Type == typeof(PostgreSqlTsRankingNormalization) ? Normalization(rest) : rest);
        }

        return Function(name, ordered, typeof(float));
    }

    // ts_rank's normalization is an integer of flags.
    private SqlExpression Normalization(SqlExpression normalization)
    {
        var intMapping = typeMappingSource.FindMapping(typeof(int));
        return normalization is SqlConstantExpression { Value: PostgreSqlTsRankingNormalization flags }
            ? sqlExpressionFactory.Constant((int)flags, intMapping)
            : sqlExpressionFactory.Convert(normalization, typeof(int), intMapping);
    }

    // A weight is a constant A to D: a char, or Npgsql's weight enum.
    private SqlExpression? Weight(SqlExpression weight)
        => weight is SqlConstantExpression { Value: { } value }
            ? sqlExpressionFactory.Constant(value.ToString()![..1], typeMappingSource.FindMapping(typeof(string)))
            : null;

    private SqlExpression Config(SqlExpression config)
        => config is SqlConstantExpression
            ? sqlExpressionFactory.ApplyDefaultTypeMapping(config)
            : sqlExpressionFactory.Convert(config, typeof(string), typeMappingSource.FindMapping(typeof(string), "regconfig"));

    private SqlExpression TextArray(SqlExpression lexemes)
        => sqlExpressionFactory.ApplyTypeMapping(lexemes, typeMappingSource.FindMapping(typeof(string[]), "text[]"));

    private static string Literal(string text)
        => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

    private SqlExpression Function(string name, IReadOnlyList<SqlExpression> arguments, Type returnType)
        => sqlExpressionFactory.Function(
            name,
            arguments.Select(a => sqlExpressionFactory.ApplyDefaultTypeMapping(a)),
            nullable: true,
            arguments.Select(_ => true),
            returnType,
            typeMappingSource.FindMapping(returnType));
}
