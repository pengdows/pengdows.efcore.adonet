using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.Tests;

/// <summary>A dialect assembled from grammar parts, as a dialect package outside the core would assemble one.</summary>
public sealed class PartsDialect(
    string name,
    ISqlDialect? sql = null,
    IDatabaseCapabilities? capabilities = null,
    ITypeMappingDialect? typeMappings = null,
    Action<IServiceCollection>? services = null) : AdoNetDialect
{
    public override string Name => name;
    public override ISqlDialect Sql { get; } = sql ?? AnsiSqlDialect.Instance;
    public override IDatabaseCapabilities Capabilities { get; } = capabilities ?? DatabaseCapabilities.Default;
    public override ITypeMappingDialect TypeMappings { get; } = typeMappings ?? AnsiTypeMappings.Instance;

    public override void ConfigureServices(IServiceCollection serviceCollection)
        => services?.Invoke(serviceCollection);
}

public sealed class PartsSql(
    PagingSyntax? paging = null,
    BooleanSyntax? booleans = null,
    KeyRetrieval? keys = null,
    DmlSyntax? dml = null,
    string trueLiteral = "TRUE",
    string falseLiteral = "FALSE",
    string longCountFunction = "COUNT",
    string parameterMarker = "@",
    bool parameterNameIncludesMarker = true,
    bool supportsSchemas = true,
    FunctionSyntax? functions = null,
    bool preferExistsToIn = false,
    Func<string, string>? stringLiteral = null,
    string defaultValues = "DEFAULT VALUES",
    string? currentSchema = null,
    SavepointSyntax? savepoints = null,
    bool noSavepoints = false,
    bool rowValues = true,
    bool limitInPredicateSubqueries = true,
    Func<string, bool>? requiresQuoting = null) : AnsiSqlDialect
{
    public override PagingSyntax Paging { get; } = paging ?? PagingSyntax.OffsetFetch;
    public override BooleanSyntax Booleans { get; } = booleans ?? BooleanSyntax.Native;
    public override KeyRetrieval Keys { get; } = keys ?? KeyRetrieval.Returning;
    public override DmlSyntax Dml { get; } = dml ?? DmlSyntax.Standard;
    public override string TrueLiteral => trueLiteral;
    public override string FalseLiteral => falseLiteral;
    public override string LongCountFunction => longCountFunction;
    public override string ParameterMarker => parameterMarker;
    public override bool ParameterNameIncludesMarker => parameterNameIncludesMarker;
    public override bool SupportsSchemas => supportsSchemas;
    public override FunctionSyntax Functions { get; } = functions ?? FunctionSyntax.Iso;
    public override bool PreferExistsToIn => preferExistsToIn;
    public override string DefaultValues => defaultValues;
    public override string? CurrentSchema => currentSchema;
    public override SavepointSyntax? Savepoints => noSavepoints ? null : savepoints ?? SavepointSyntax.Iso;
    public override bool RowValues => rowValues;
    public override bool LimitInPredicateSubqueries => limitInPredicateSubqueries;

    public override bool RequiresQuoting(string identifier)
        => requiresQuoting?.Invoke(identifier) ?? base.RequiresQuoting(identifier);

    public override string StringLiteral(string value)
        => stringLiteral?.Invoke(value) ?? base.StringLiteral(value);
}

/// <summary>Shared instances: each distinct dialect gets its own EF service provider, so tests reuse these.</summary>
public static class TestDialects
{
    public static readonly IAdoNetDialect Ansi = AdoNetDialect.Ansi;
    public static readonly IAdoNetDialect Top = new PartsDialect("Top", new PartsSql(paging: PagingSyntax.TopOrOffsetFetch));
    public static readonly IAdoNetDialect Limit = new PartsDialect("Limit", new PartsSql(paging: PagingSyntax.LimitOffset()));
    public static readonly IAdoNetDialect LimitWithNoLimitValue = new PartsDialect("Limit -1", new PartsSql(paging: PagingSyntax.LimitOffset("-1")));
    public static readonly IAdoNetDialect LiteralPaging = new PartsDialect("Literal paging", new PartsSql(paging: PagingSyntax.OffsetFetch.WithLiteralValues()));

    public static readonly IAdoNetDialect Bit = new PartsDialect(
        "Bit", new PartsSql(booleans: BooleanSyntax.BitValues, trueLiteral: "1", falseLiteral: "0", longCountFunction: "COUNT_BIG"));

    public static readonly IAdoNetDialect BitXor = new PartsDialect(
        "Bit with bitwise operators",
        new PartsSql(booleans: BooleanSyntax.BitValuesWithBitwiseOperators, trueLiteral: "1", falseLiteral: "0"));

    public static readonly IAdoNetDialect StringParameters4000 = new PartsDialect(
        "String parameters 4000", typeMappings: new StringParameterSizes());

    public static readonly IAdoNetDialect NationalStrings = new PartsDialect("National strings", typeMappings: new NationalStringTypeMappings());

    public static readonly IAdoNetDialect PreferExists = new PartsDialect("Prefer EXISTS", new PartsSql(preferExistsToIn: true));

    public static readonly IAdoNetDialect CastTargets = new PartsDialect("Cast targets", typeMappings: new CastTargetTypeMappings());

    public static readonly IAdoNetDialect CastLiterals = new PartsDialect("Cast literals", typeMappings: new CastLiteralTypeMappings());

    public static readonly IAdoNetDialect SelectAfter = new PartsDialect(
        "SelectAfter",
        new PartsSql(keys: KeyRetrieval.SelectAfter("last_id()", "row_count()")),
        new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 10));

    public static readonly IAdoNetDialect Sequence = new PartsDialect("Sequence", new PartsSql(keys: KeyRetrieval.Sequence()));

    public static readonly IAdoNetDialect BatchesOfTwo = new PartsDialect(
        "Batches of two", capabilities: new DatabaseCapabilities(SupportsMultipleStatementsPerCommand: true, DefaultMaxBatchSize: 2));

    public static readonly IAdoNetDialect ColonParameters = new PartsDialect(
        "Colon", new PartsSql(parameterMarker: ":", parameterNameIncludesMarker: false, supportsSchemas: false));

    public static readonly IAdoNetDialect UnaliasedTarget = new PartsDialect("Unaliased", new PartsSql(dml: DmlSyntax.UnaliasedTarget));
    public static readonly IAdoNetDialect TargetAliasFrom = new PartsDialect("Alias FROM", new PartsSql(dml: DmlSyntax.TargetAliasFrom));
    public static readonly IAdoNetDialect MultiTable = new PartsDialect("Multi-table", new PartsSql(dml: DmlSyntax.MultiTable));
    public static readonly IAdoNetDialect Functions = new PartsDialect(
        "Functions",
        new PartsSql(functions: new TestFunctions(), stringLiteral: v => "N'" + v.Replace("'", "''").Replace(@"\", @"\\") + "'"));
    public static readonly IAdoNetDialect NoSavepoints = new PartsDialect("No savepoints", new PartsSql(noSavepoints: true));
    public static readonly IAdoNetDialect TransactionSavepoints = new PartsDialect(
        "Transaction savepoints", new PartsSql(savepoints: new SavepointSyntax("SAVE TRANSACTION {0}", "ROLLBACK TRANSACTION {0}", release: null)));
    public static readonly IAdoNetDialect NoSubstrings = new PartsDialect("No substrings", new PartsSql(functions: new NoSubstringFunctions()));
    public static readonly IAdoNetDialect LowerCaseUnquoted = new PartsDialect(
        "Lower case unquoted", new PartsSql(requiresQuoting: i => i.Any(c => !char.IsLower(c))));
    public static readonly IAdoNetDialect NoLimitInSubqueryPredicates = new PartsDialect(
        "No LIMIT in IN subqueries", new PartsSql(limitInPredicateSubqueries: false));
    public static readonly IAdoNetDialect NoRowValues = new PartsDialect("No row values", new PartsSql(rowValues: false));
    public static readonly IAdoNetDialect NoEscape = new PartsDialect("No escape", new PartsSql(functions: new NoEscapeFunctions()));
    public static readonly IAdoNetDialect NoFunctions = new PartsDialect("No functions", new PartsSql(functions: new NoFunctions()));

    public static readonly IAdoNetDialect ReverseTranslator = new PartsDialect(
        "Reverse translator",
        services: s => new EntityFrameworkRelationalServicesBuilder(s).TryAdd<IMethodCallTranslatorPlugin, ReverseTranslatorPlugin>());
}

/// <summary>Sizes unbounded string parameters as SQL Server's provider does.</summary>
public sealed class StringParameterSizes : AnsiTypeMappings
{
    public override int? StringParameterSize(bool unicode)
        => unicode ? 4000 : 8000;
}
