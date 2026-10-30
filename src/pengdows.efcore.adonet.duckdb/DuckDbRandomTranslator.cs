using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.duckdb;

internal sealed class DuckDbMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslatorPlugin
{
    public IEnumerable<IMethodCallTranslator> Translators { get; } = [new DuckDbRandomTranslator(sqlExpressionFactory, typeMappingSource)];
}

/// <summary>
///     <c>EF.Functions.Random()</c> is DuckDB's <c>random()</c>, a double from zero up to but not including one. EF's default
///     translation is <c>RAND()</c>, which DuckDB doesn't have.
/// </summary>
internal sealed class DuckDbRandomTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        => (method.DeclaringType == typeof(DbFunctionsExtensions)) && (method.Name == nameof(DbFunctionsExtensions.Random)) && (arguments.Count == 1)
            ? sqlExpressionFactory.Call("random()", [], typeof(double), typeMappingSource.FindMapping(typeof(double)))
            : null;
}
