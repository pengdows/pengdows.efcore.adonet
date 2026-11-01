using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     The pg_trgm extension's similarity functions and operators (<c>HasPostgresExtension("pg_trgm")</c>), as Npgsql's
///     <c>NpgsqlTrigramsDbFunctionsExtensions</c> has them; each throws when called outside a query. Behaviour reference (not
///     copied): https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlTrigramsDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlTrigramsDbFunctionsExtensions
{
    /// <summary>The text's trigrams (<c>show_trgm</c>).</summary>
    public static string[] TrigramsShow(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsShow)));

    /// <summary>How similar the texts are, 0 to 1 (<c>similarity</c>).</summary>
    public static double TrigramsSimilarity(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsSimilarity)));

    /// <summary>The greatest similarity of <paramref name="source" /> to a run of words of <paramref name="target" /> (<c>word_similarity</c>).</summary>
    public static double TrigramsWordSimilarity(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsWordSimilarity)));

    /// <summary><c>strict_word_similarity</c>: as word similarity, matching whole words.</summary>
    public static double TrigramsStrictWordSimilarity(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsStrictWordSimilarity)));

    /// <summary>Whether the similarity exceeds <c>pg_trgm.similarity_threshold</c> (<c>%</c>).</summary>
    public static bool TrigramsAreSimilar(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsAreSimilar)));

    /// <summary>Whether the word similarity exceeds <c>pg_trgm.word_similarity_threshold</c> (<c>&lt;%</c>).</summary>
    public static bool TrigramsAreWordSimilar(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsAreWordSimilar)));

    /// <summary>The commutator of <see cref="TrigramsAreWordSimilar" /> (<c>%&gt;</c>).</summary>
    public static bool TrigramsAreNotWordSimilar(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsAreNotWordSimilar)));

    /// <summary>Whether the strict word similarity exceeds its threshold (<c>&lt;&lt;%</c>).</summary>
    public static bool TrigramsAreStrictWordSimilar(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsAreStrictWordSimilar)));

    /// <summary>The commutator of <see cref="TrigramsAreStrictWordSimilar" /> (<c>%&gt;&gt;</c>).</summary>
    public static bool TrigramsAreNotStrictWordSimilar(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsAreNotStrictWordSimilar)));

    /// <summary>One minus the similarity (<c>&lt;-&gt;</c>), which a GiST trigram index orders by.</summary>
    public static double TrigramsSimilarityDistance(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsSimilarityDistance)));

    /// <summary>One minus the word similarity (<c>&lt;&lt;-&gt;</c>).</summary>
    public static double TrigramsWordSimilarityDistance(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsWordSimilarityDistance)));

    /// <summary>The commutator of <see cref="TrigramsWordSimilarityDistance" /> (<c>&lt;-&gt;&gt;</c>).</summary>
    public static double TrigramsWordSimilarityDistanceInverted(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsWordSimilarityDistanceInverted)));

    /// <summary>One minus the strict word similarity (<c>&lt;&lt;&lt;-&gt;</c>).</summary>
    public static double TrigramsStrictWordSimilarityDistance(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsStrictWordSimilarityDistance)));

    /// <summary>The commutator of <see cref="TrigramsStrictWordSimilarityDistance" /> (<c>&lt;-&gt;&gt;&gt;</c>).</summary>
    public static double TrigramsStrictWordSimilarityDistanceInverted(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(TrigramsStrictWordSimilarityDistanceInverted)));
}

/// <summary>
///     The fuzzystrmatch extension's functions (<c>HasPostgresExtension("fuzzystrmatch")</c>), as Npgsql's
///     <c>NpgsqlFuzzyStringMatchDbFunctionsExtensions</c> has them; each throws when called outside a query. Behaviour reference
///     (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlFuzzyStringMatchDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlFuzzyStringMatchDbFunctionsExtensions
{
    /// <summary>The text's Soundex code (<c>soundex</c>).</summary>
    public static string FuzzyStringMatchSoundex(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchSoundex)));

    /// <summary>How many of the texts' Soundex positions match, 0 to 4 (<c>difference</c>).</summary>
    public static int FuzzyStringMatchDifference(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchDifference)));

    /// <summary>The Levenshtein distance between the texts (<c>levenshtein</c>).</summary>
    public static int FuzzyStringMatchLevenshtein(this DbFunctions _, string source, string target)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchLevenshtein)));

    /// <summary>The Levenshtein distance with the costs of an insertion, a deletion and a substitution.</summary>
    public static int FuzzyStringMatchLevenshtein(
        this DbFunctions _,
        string source,
        string target,
        int insertionCost,
        int deletionCost,
        int substitutionCost)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchLevenshtein)));

    /// <summary>The Levenshtein distance when at most <paramref name="maximumDistance" />, else something greater (<c>levenshtein_less_equal</c>).</summary>
    public static int FuzzyStringMatchLevenshteinLessEqual(this DbFunctions _, string source, string target, int maximumDistance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchLevenshteinLessEqual)));

    /// <summary><c>levenshtein_less_equal</c> with the costs of an insertion, a deletion and a substitution.</summary>
    public static int FuzzyStringMatchLevenshteinLessEqual(
        this DbFunctions _,
        string source,
        string target,
        int insertionCost,
        int deletionCost,
        int substitutionCost,
        int maximumDistance)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchLevenshteinLessEqual)));

    /// <summary>The text's Metaphone code, at most <paramref name="maximumOutputLength" /> long (<c>metaphone</c>).</summary>
    public static string FuzzyStringMatchMetaphone(this DbFunctions _, string text, int maximumOutputLength)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchMetaphone)));

    /// <summary>The text's primary Double Metaphone code (<c>dmetaphone</c>).</summary>
    public static string FuzzyStringMatchDoubleMetaphone(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchDoubleMetaphone)));

    /// <summary>The text's alternate Double Metaphone code (<c>dmetaphone_alt</c>).</summary>
    public static string FuzzyStringMatchDoubleMetaphoneAlt(this DbFunctions _, string text)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(FuzzyStringMatchDoubleMetaphoneAlt)));
}

/// <summary>
///     Translates <see cref="PostgreSqlTrigramsDbFunctionsExtensions" /> and <see cref="PostgreSqlFuzzyStringMatchDbFunctionsExtensions" />.
///     Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlTrigramsMethodTranslator.cs
///     and https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlFuzzyStringMatchMethodTranslator.cs
/// </summary>
internal sealed class PostgreSqlTextSimilarityTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    private static readonly Dictionary<string, string> Functions = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsShow)] = "show_trgm",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsSimilarity)] = "similarity",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsWordSimilarity)] = "word_similarity",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsStrictWordSimilarity)] = "strict_word_similarity",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchSoundex)] = "soundex",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchDifference)] = "difference",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchLevenshtein)] = "levenshtein",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchLevenshteinLessEqual)] = "levenshtein_less_equal",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchMetaphone)] = "metaphone",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchDoubleMetaphone)] = "dmetaphone",
        [nameof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions.FuzzyStringMatchDoubleMetaphoneAlt)] = "dmetaphone_alt"
    };

    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsAreSimilar)] = "%",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsAreWordSimilar)] = "<%",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsAreNotWordSimilar)] = "%>",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsAreStrictWordSimilar)] = "<<%",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsAreNotStrictWordSimilar)] = "%>>",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsSimilarityDistance)] = "<->",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsWordSimilarityDistance)] = "<<->",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsWordSimilarityDistanceInverted)] = "<->>",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsStrictWordSimilarityDistance)] = "<<<->",
        [nameof(PostgreSqlTrigramsDbFunctionsExtensions.TrigramsStrictWordSimilarityDistanceInverted)] = "<->>>"
    };

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((method.DeclaringType != typeof(PostgreSqlTrigramsDbFunctionsExtensions))
            && (method.DeclaringType != typeof(PostgreSqlFuzzyStringMatchDbFunctionsExtensions)))
        {
            return null;
        }

        var operands = arguments.Skip(1).Select(a => sqlExpressionFactory.ApplyDefaultTypeMapping(a)!).ToArray();
        var resultMapping = method.ReturnType == typeof(string[])
            ? typeMappingSource.FindMapping(typeof(string[]), "text[]")
            : typeMappingSource.FindMapping(method.ReturnType);
        if (Operators.TryGetValue(method.Name, out var @operator))
        {
            return PostgreSqlArraySql.Binary(sqlExpressionFactory, @operator, operands[0], operands[1], method.ReturnType, resultMapping);
        }

        return Functions.TryGetValue(method.Name, out var name)
            ? sqlExpressionFactory.Function(name, operands, nullable: true, operands.Select(_ => true), method.ReturnType, resultMapping)
            : null;
    }
}
