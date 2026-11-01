using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using NpgsqlTypes;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL's network address operators and functions over <c>inet</c> (<see cref="IPAddress" />, <see cref="NpgsqlInet" />),
///     <c>cidr</c> (<see cref="IPNetwork" />, <see cref="NpgsqlCidr" />) and <c>macaddr</c> (<see cref="PhysicalAddress" />), as
///     Npgsql's <c>NpgsqlNetworkDbFunctionsExtensions</c> has them (its obsolete tuple overloads aside); each throws when called
///     outside a query. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/DbFunctionsExtensions/NpgsqlNetworkDbFunctionsExtensions.cs
/// </summary>
public static class PostgreSqlNetworkDbFunctionsExtensions
{
    /// <summary><c>&lt;</c></summary>
    public static bool LessThan(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(LessThan)));

    /// <summary><c>&lt;</c></summary>
    public static bool LessThan(this DbFunctions _, PhysicalAddress macaddr, PhysicalAddress other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(LessThan)));

    /// <summary><c>&lt;=</c></summary>
    public static bool LessThanOrEqual(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(LessThanOrEqual)));

    /// <summary><c>&lt;=</c></summary>
    public static bool LessThanOrEqual(this DbFunctions _, PhysicalAddress macaddr, PhysicalAddress other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(LessThanOrEqual)));

    /// <summary><c>&gt;=</c></summary>
    public static bool GreaterThanOrEqual(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GreaterThanOrEqual)));

    /// <summary><c>&gt;=</c></summary>
    public static bool GreaterThanOrEqual(this DbFunctions _, PhysicalAddress macaddr, PhysicalAddress other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GreaterThanOrEqual)));

    /// <summary><c>&gt;</c></summary>
    public static bool GreaterThan(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GreaterThan)));

    /// <summary><c>&gt;</c></summary>
    public static bool GreaterThan(this DbFunctions _, PhysicalAddress macaddr, PhysicalAddress other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(GreaterThan)));

    /// <summary>Whether <paramref name="inet" /> is strictly within <paramref name="other" /> (<c>&lt;&lt;</c>).</summary>
    public static bool ContainedBy(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ContainedBy)));

    /// <summary>Whether <paramref name="inet" /> is within or equals <paramref name="other" /> (<c>&lt;&lt;=</c>).</summary>
    public static bool ContainedByOrEqual(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ContainedByOrEqual)));

    /// <summary>Whether <paramref name="inet" /> strictly contains <paramref name="other" /> (<c>&gt;&gt;</c>).</summary>
    public static bool Contains(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Contains)));

    /// <summary>Whether <paramref name="inet" /> contains or equals <paramref name="other" /> (<c>&gt;&gt;=</c>).</summary>
    public static bool ContainsOrEqual(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ContainsOrEqual)));

    /// <summary>Whether either contains or equals the other (<c>&amp;&amp;</c>).</summary>
    public static bool ContainsOrContainedBy(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(ContainsOrContainedBy)));

    /// <summary><c>~</c></summary>
    public static NpgsqlInet BitwiseNot(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(BitwiseNot)));

    /// <summary><c>~</c></summary>
    public static PhysicalAddress BitwiseNot(this DbFunctions _, PhysicalAddress macaddr)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(BitwiseNot)));

    /// <summary><c>&amp;</c></summary>
    public static NpgsqlInet BitwiseAnd(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(BitwiseAnd)));

    /// <summary><c>&amp;</c></summary>
    public static PhysicalAddress BitwiseAnd(this DbFunctions _, PhysicalAddress macaddr, PhysicalAddress other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(BitwiseAnd)));

    /// <summary><c>|</c></summary>
    public static NpgsqlInet BitwiseOr(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(BitwiseOr)));

    /// <summary><c>|</c></summary>
    public static PhysicalAddress BitwiseOr(this DbFunctions _, PhysicalAddress macaddr, PhysicalAddress other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(BitwiseOr)));

    /// <summary>The address <paramref name="value" /> on (<c>+</c>).</summary>
    public static NpgsqlInet Add(this DbFunctions _, NpgsqlInet inet, int value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Add)));

    /// <summary>The address <paramref name="value" /> back (<c>-</c>).</summary>
    public static NpgsqlInet Subtract(this DbFunctions _, NpgsqlInet inet, long value)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Subtract)));

    /// <summary>The difference of two addresses (<c>-</c>).</summary>
    public static long Subtract(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Subtract)));

    /// <summary>The abbreviated display form (<c>abbrev</c>).</summary>
    public static string Abbreviate(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Abbreviate)));

    /// <summary>The abbreviated display form (<c>abbrev</c>).</summary>
    public static string Abbreviate(this DbFunctions _, NpgsqlCidr cidr)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Abbreviate)));

    /// <summary>The abbreviated display form (<c>abbrev</c>).</summary>
    public static string Abbreviate(this DbFunctions _, IPNetwork cidr)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Abbreviate)));

    /// <summary>The network's broadcast address (<c>broadcast</c>).</summary>
    public static NpgsqlInet Broadcast(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Broadcast)));

    /// <summary>4 for IPv4, 6 for IPv6 (<c>family</c>).</summary>
    public static int Family(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Family)));

    /// <summary>The address as text, without its mask (<c>host</c>).</summary>
    public static string Host(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Host)));

    /// <summary>The host mask (<c>hostmask</c>).</summary>
    public static NpgsqlInet HostMask(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(HostMask)));

    /// <summary>The mask length (<c>masklen</c>).</summary>
    public static int MaskLength(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(MaskLength)));

    /// <summary>The netmask (<c>netmask</c>).</summary>
    public static NpgsqlInet Netmask(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Netmask)));

    /// <summary>The network part (<c>network</c>).</summary>
    public static IPNetwork Network(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Network)));

    /// <summary>The address with mask length <paramref name="length" /> (<c>set_masklen</c>).</summary>
    public static NpgsqlInet SetMaskLength(this DbFunctions _, NpgsqlInet inet, int length)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetMaskLength)));

    /// <summary>The network with mask length <paramref name="length" /> (<c>set_masklen</c>).</summary>
    public static NpgsqlCidr SetMaskLength(this DbFunctions _, NpgsqlCidr cidr, int length)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetMaskLength)));

    /// <summary>The network with mask length <paramref name="length" /> (<c>set_masklen</c>).</summary>
    public static IPNetwork SetMaskLength(this DbFunctions _, IPNetwork cidr, int length)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SetMaskLength)));

    /// <summary>The address and mask length as text (<c>text</c>).</summary>
    public static string Text(this DbFunctions _, NpgsqlInet inet)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Text)));

    /// <summary>Whether both addresses are of one family (<c>inet_same_family</c>).</summary>
    public static bool SameFamily(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(SameFamily)));

    /// <summary>The smallest network holding both (<c>inet_merge</c>).</summary>
    public static IPNetwork Merge(this DbFunctions _, NpgsqlInet inet, NpgsqlInet other)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Merge)));

    /// <summary>The address with its last three bytes zero (<c>trunc</c>).</summary>
    public static PhysicalAddress Truncate(this DbFunctions _, PhysicalAddress macAddress)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Truncate)));

    /// <summary>The macaddr8 with its 7th bit set, for IPv6 link-local addresses (<c>macaddr8_set7bit</c>).</summary>
    public static PhysicalAddress Set7BitMac8(this DbFunctions _, PhysicalAddress macAddress)
        => throw new InvalidOperationException(CoreStrings.FunctionOnClient(nameof(Set7BitMac8)));
}

/// <summary>
///     Translates <see cref="PostgreSqlNetworkDbFunctionsExtensions" />. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Query/ExpressionTranslators/Internal/NpgsqlNetworkTranslator.cs
/// </summary>
internal sealed class PostgreSqlNetworkTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMethodCallTranslator
{
    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.LessThan)] = "<",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.LessThanOrEqual)] = "<=",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.GreaterThanOrEqual)] = ">=",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.GreaterThan)] = ">",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.ContainedBy)] = "<<",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.ContainedByOrEqual)] = "<<=",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Contains)] = ">>",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.ContainsOrEqual)] = ">>=",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.ContainsOrContainedBy)] = "&&",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.BitwiseAnd)] = "&",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.BitwiseOr)] = "|",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Add)] = "+",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Subtract)] = "-"
    };

    private static readonly Dictionary<string, string> Functions = new(StringComparer.Ordinal)
    {
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Abbreviate)] = "abbrev",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Broadcast)] = "broadcast",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Family)] = "family",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Host)] = "host",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.HostMask)] = "hostmask",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.MaskLength)] = "masklen",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Netmask)] = "netmask",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Network)] = "network",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.SetMaskLength)] = "set_masklen",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Text)] = "text",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.SameFamily)] = "inet_same_family",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Merge)] = "inet_merge",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Truncate)] = "trunc",
        [nameof(PostgreSqlNetworkDbFunctionsExtensions.Set7BitMac8)] = "macaddr8_set7bit"
    };

    public SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(PostgreSqlNetworkDbFunctionsExtensions))
        {
            return null;
        }

        var operands = Operands(arguments.Skip(1).ToArray());
        var resultMapping = typeMappingSource.FindMapping(method.ReturnType);
        if (method.Name == nameof(PostgreSqlNetworkDbFunctionsExtensions.BitwiseNot))
        {
            return sqlExpressionFactory.Call("~{0}", operands, method.ReturnType, operands[0].TypeMapping);
        }

        if (Operators.TryGetValue(method.Name, out var @operator))
        {
            return PostgreSqlArraySql.Binary(sqlExpressionFactory, @operator, operands[0], operands[1], method.ReturnType, resultMapping);
        }

        return Functions.TryGetValue(method.Name, out var name)
            ? sqlExpressionFactory.Function(name, operands, nullable: true, operands.Select(_ => true), method.ReturnType, resultMapping)
            : null;
    }

    // An IPAddress passed as NpgsqlInet arrives converted; the operand is the address, typed as its column is, or the other
    // operand's when it has none of its own.
    private SqlExpression[] Operands(SqlExpression[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            while (arguments[i] is SqlUnaryExpression { OperatorType: ExpressionType.Convert } convert)
            {
                arguments[i] = convert.Operand;
            }
        }

        var inferred = arguments.Length > 1 ? ExpressionExtensions.InferTypeMapping(arguments[0], arguments[1]) : null;
        return arguments
            .Select(a => (a.TypeMapping == null) && (inferred != null) && (a.Type == inferred.ClrType)
                ? sqlExpressionFactory.ApplyTypeMapping(a, inferred)
                : sqlExpressionFactory.ApplyDefaultTypeMapping(a))
            .ToArray()!;
    }
}
