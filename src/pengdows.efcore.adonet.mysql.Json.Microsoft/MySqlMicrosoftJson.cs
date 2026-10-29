using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.mysql.Json.Microsoft;

/// <summary>
///     System.Text.Json on MySQL's json columns, as Pomelo's <c>UseMicrosoftJson</c>: POCOs on a column the model names json,
///     and JsonDocument and JsonElement on json columns, serialized, read inside in queries, and tracked as
///     <see cref="MySqlCommonJsonChangeTrackingOptions" /> says. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Microsoft/Extensions/MySqlJsonMicrosoftDbContextOptionsBuilderExtensions.cs
/// </summary>
public static class MySqlMicrosoftJsonExtensions
{
    /// <summary>
    ///     Maps POCOs, JsonDocument and JsonElement to json columns with System.Text.Json; <paramref name="options" /> says how
    ///     a change inside one is tracked (only a new value, by default).
    /// </summary>
    public static AdoNetDbContextOptionsBuilder UseMicrosoftJson(
        this AdoNetDbContextOptionsBuilder optionsBuilder,
        MySqlCommonJsonChangeTrackingOptions options = MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly)
    {
        var builder = ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder;
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new MySqlMicrosoftJsonOptionsExtension(options));
        return optionsBuilder;
    }

    /// <summary>How a change inside this json column's value is tracked, whatever <c>UseMicrosoftJson</c> says; null for that.</summary>
    public static PropertyBuilder UseJsonChangeTrackingOptions(this PropertyBuilder propertyBuilder, MySqlCommonJsonChangeTrackingOptions? options)
    {
        propertyBuilder.Metadata.SetValueComparer(options is { } o ? MicrosoftJson.Comparer(propertyBuilder.Metadata.ClrType, o) : null);
        return propertyBuilder;
    }

    /// <summary>How a change inside this json column's value is tracked, whatever <c>UseMicrosoftJson</c> says; null for that.</summary>
    public static PropertyBuilder<TProperty> UseJsonChangeTrackingOptions<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        MySqlCommonJsonChangeTrackingOptions? options)
        => (PropertyBuilder<TProperty>)UseJsonChangeTrackingOptions((PropertyBuilder)propertyBuilder, options);
}

internal sealed class MySqlMicrosoftJsonOptionsExtension(MySqlCommonJsonChangeTrackingOptions changeTracking) : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public MySqlCommonJsonChangeTrackingOptions ChangeTracking
        => changeTracking;

    public DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<ISingletonOptions, MicrosoftJsonOptions>(p => p.GetRequiredService<MicrosoftJsonOptions>())
            .TryAddProviderSpecificServices(b => b.TryAddSingleton<MicrosoftJsonOptions, MicrosoftJsonOptions>())
            .TryAdd<IRelationalTypeMappingSourcePlugin, MicrosoftJsonMappingSourcePlugin>()
            .TryAdd<IMemberTranslatorPlugin, MicrosoftJsonTranslatorPlugin>()
            .TryAdd<IMethodCallTranslatorPlugin, MicrosoftJsonTranslatorPlugin>();

    public void Validate(IDbContextOptions options)
    {
        if (options.FindExtension<AdoNetOptionsExtension>()?.Dialect is not MySqlDialect)
        {
            throw new InvalidOperationException("UseMicrosoftJson needs a MySQL dialect (UseAdoNetMySql).");
        }
    }

    private sealed class ExtensionInfo(MySqlMicrosoftJsonOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider
            => false;

        public override string LogFragment
            => "using System.Text.Json on json columns ";

        public override int GetServiceProviderHashCode()
            => (int)extension.ChangeTracking;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo info && (info.Extension is MySqlMicrosoftJsonOptionsExtension o) && (o.ChangeTracking == extension.ChangeTracking);

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["MySqlMicrosoftJson"] = ((int)extension.ChangeTracking).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>The change tracking the context's options give, for the singletons that make the mappings (EF's singleton options).</summary>
internal sealed class MicrosoftJsonOptions : ISingletonOptions
{
    public MySqlCommonJsonChangeTrackingOptions ChangeTracking { get; private set; }

    public void Initialize(IDbContextOptions options)
        => ChangeTracking = options.FindExtension<MySqlMicrosoftJsonOptionsExtension>()?.ChangeTracking ?? MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly;

    public void Validate(IDbContextOptions options)
    {
        var changeTracking = options.FindExtension<MySqlMicrosoftJsonOptionsExtension>()?.ChangeTracking ?? MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly;
        if (changeTracking != ChangeTracking)
        {
            throw new InvalidOperationException("The json change tracking options must be the same for every context on one service provider.");
        }
    }
}

internal sealed class MicrosoftJsonMappingSourcePlugin(ISqlDialect sql, MicrosoftJsonOptions options) : MySqlJsonMappingSourcePlugin(sql)
{

    protected override bool IsDocument(Type type)
        => (type == typeof(JsonDocument)) || (type == typeof(JsonElement));

    protected override ValueConverter Converter(Type type)
        => (ValueConverter)Activator.CreateInstance(typeof(MicrosoftJsonConverter<>).MakeGenericType(type))!;

    protected override ValueComparer Comparer(Type type)
        => MicrosoftJson.Comparer(type, options.ChangeTracking);
}

internal sealed class MicrosoftJsonTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMemberTranslatorPlugin, IMethodCallTranslatorPlugin
{
    private readonly MicrosoftJsonTranslator _translator = new(sqlExpressionFactory, typeMappingSource);

    IEnumerable<IMemberTranslator> IMemberTranslatorPlugin.Translators
        => [_translator];

    IEnumerable<IMethodCallTranslator> IMethodCallTranslatorPlugin.Translators
        => [_translator];
}

/// <summary>
///     A POCO's members by their JsonPropertyName, and JsonDocument's and JsonElement's: RootElement, GetProperty, the index,
///     Get* as the type they read, GetArrayLength as JSON_LENGTH. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Microsoft/Query/Internal/MySqlJsonMicrosoftDomTranslator.cs
/// </summary>
internal sealed class MicrosoftJsonTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : MySqlJsonPathTranslator(sqlExpressionFactory, typeMappingSource, m => m.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
{
    private static readonly string[] Getters =
    [
        nameof(JsonElement.GetBoolean), nameof(JsonElement.GetDateTime), nameof(JsonElement.GetDateTimeOffset),
        nameof(JsonElement.GetDecimal), nameof(JsonElement.GetDouble), nameof(JsonElement.GetGuid), nameof(JsonElement.GetInt16),
        nameof(JsonElement.GetInt32), nameof(JsonElement.GetInt64), nameof(JsonElement.GetSingle), nameof(JsonElement.GetString)
    ];

    public override SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((member.DeclaringType == typeof(JsonDocument)) || (member.DeclaringType == typeof(JsonElement)))
        {
            // The document's root is the column itself.
            return (member.Name == nameof(JsonDocument.RootElement)) && (instance != null) && IsJson(instance) ? instance : null;
        }

        return base.Translate(instance, member, returnType, logger);
    }

    public override SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if (method.DeclaringType != typeof(JsonElement))
        {
            return base.Translate(instance, method, arguments, logger);
        }

        if ((instance == null) || !IsJson(instance))
        {
            return null;
        }

        return method.Name switch
        {
            nameof(JsonElement.GetProperty) when arguments is [SqlConstantExpression { Value: string name }]
                => Append(instance, new PathSegment(name), typeof(JsonElement)),
            "get_Item" when arguments is [var index]
                => Append(instance, new PathSegment(SqlExpressionFactory.ApplyDefaultTypeMapping(index)), typeof(JsonElement)),
            nameof(JsonElement.GetArrayLength) => Length(instance),
            _ when Getters.Contains(method.Name) && (arguments.Count == 0) => As(instance, method.ReturnType),
            _ => null
        };
    }
}

/// <summary>A value as its System.Text.Json text.</summary>
internal sealed class MicrosoftJsonConverter<T>() : ValueConverter<T, string>(
    value => MicrosoftJson.Serialize(value),
    text => MicrosoftJson.Deserialize<T>(text));

/// <summary>System.Text.Json's reading and writing of a json column's value, and the change tracking of it.</summary>
internal static class MicrosoftJson
{
    public static string Serialize<T>(T value)
        => value switch
        {
            null => "null",
            JsonDocument document => document.RootElement.GetRawText(),
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(value)
        };

    public static T Deserialize<T>(string text)
    {
        if (typeof(T) == typeof(JsonDocument))
        {
            return (T)(object)JsonDocument.Parse(text);
        }

        if (typeof(T) == typeof(JsonElement))
        {
            using var document = JsonDocument.Parse(text);
            return (T)(object)document.RootElement.Clone();
        }

        return JsonSerializer.Deserialize<T>(text)!;
    }

    public static ValueComparer Comparer(Type type, MySqlCommonJsonChangeTrackingOptions options)
        => (ValueComparer)Activator.CreateInstance(typeof(MicrosoftJsonComparer<>).MakeGenericType(type), options)!;

    // Whitespace and key order aside: the documents compared.
    public static bool SameDocument<T>(T left, T right)
        => JsonNode.DeepEquals(JsonNode.Parse(Serialize(left)), JsonNode.Parse(Serialize(right)));
}

/// <summary>
///     RootPropertyOnly: a value is unchanged while it is the same value (snapshot the value itself). The full-hierarchy options
///     snapshot a copy (a round trip through JSON) and compare the JSON text, or the documents. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Microsoft/Storage/ValueComparison/Internal/MySqlJsonMicrosoftValueComparer.cs
/// </summary>
internal sealed class MicrosoftJsonComparer<T>(MySqlCommonJsonChangeTrackingOptions options)
    : ValueComparer<T>(Same(options), Hash(options), Snapshot(options))
{
    private static System.Linq.Expressions.Expression<Func<T?, T?, bool>> Same(MySqlCommonJsonChangeTrackingOptions options)
    {
        if (options == MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly)
        {
            return (left, right) => object.Equals(left, right);
        }

        if (options == MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedFast)
        {
            return (left, right) => MicrosoftJson.Serialize(left) == MicrosoftJson.Serialize(right);
        }

        return (left, right) => MicrosoftJson.SameDocument(left, right);
    }

    // A semantic comparison has no cheap hash of the document that agrees with it: equal values hash alike, at 0.
    private static System.Linq.Expressions.Expression<Func<T, int>> Hash(MySqlCommonJsonChangeTrackingOptions options)
    {
        if (options == MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly)
        {
            return value => value == null ? 0 : value.GetHashCode();
        }

        if (options == MySqlCommonJsonChangeTrackingOptions.FullHierarchyOptimizedFast)
        {
            return value => MicrosoftJson.Serialize(value).GetHashCode();
        }

        return value => 0;
    }

    private static System.Linq.Expressions.Expression<Func<T, T>> Snapshot(MySqlCommonJsonChangeTrackingOptions options)
    {
        if (options == MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly)
        {
            return value => value;
        }

        return value => MicrosoftJson.Deserialize<T>(MicrosoftJson.Serialize(value));
    }
}
