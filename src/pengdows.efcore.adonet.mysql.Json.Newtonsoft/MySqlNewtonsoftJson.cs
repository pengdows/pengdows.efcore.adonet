using System.Reflection;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.mysql.Json.Newtonsoft;

/// <summary>
///     Newtonsoft.Json on MySQL's json columns, as Pomelo's <c>UseNewtonsoftJson</c>: POCOs on a column the model names json,
///     and JToken, JObject, JArray and JValue on json columns, serialized, read inside in queries, and tracked as
///     <see cref="MySqlCommonJsonChangeTrackingOptions" /> says. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Newtonsoft/Extensions/MySqlJsonNewtonsoftDbContextOptionsBuilderExtensions.cs
/// </summary>
public static class MySqlNewtonsoftJsonExtensions
{
    /// <summary>
    ///     Maps POCOs and JToken (JObject, JArray, JValue) to json columns with Newtonsoft.Json; <paramref name="options" /> says
    ///     how a change inside one is tracked (only a new value, by default).
    /// </summary>
    public static AdoNetDbContextOptionsBuilder UseNewtonsoftJson(
        this AdoNetDbContextOptionsBuilder optionsBuilder,
        MySqlCommonJsonChangeTrackingOptions options = MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly)
    {
        var builder = ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder;
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new MySqlNewtonsoftJsonOptionsExtension(options));
        return optionsBuilder;
    }

    /// <summary>How a change inside this json column's value is tracked, whatever <c>UseNewtonsoftJson</c> says; null for that.</summary>
    public static PropertyBuilder UseJsonChangeTrackingOptions(this PropertyBuilder propertyBuilder, MySqlCommonJsonChangeTrackingOptions? options)
    {
        propertyBuilder.Metadata.SetValueComparer(options is { } o ? NewtonsoftJson.Comparer(propertyBuilder.Metadata.ClrType, o) : null);
        return propertyBuilder;
    }

    /// <summary>How a change inside this json column's value is tracked, whatever <c>UseNewtonsoftJson</c> says; null for that.</summary>
    public static PropertyBuilder<TProperty> UseJsonChangeTrackingOptions<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        MySqlCommonJsonChangeTrackingOptions? options)
        => (PropertyBuilder<TProperty>)UseJsonChangeTrackingOptions((PropertyBuilder)propertyBuilder, options);
}

internal sealed class MySqlNewtonsoftJsonOptionsExtension(MySqlCommonJsonChangeTrackingOptions changeTracking) : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public MySqlCommonJsonChangeTrackingOptions ChangeTracking
        => changeTracking;

    public DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => new EntityFrameworkRelationalServicesBuilder(services)
            .TryAdd<ISingletonOptions, NewtonsoftJsonOptions>(p => p.GetRequiredService<NewtonsoftJsonOptions>())
            .TryAddProviderSpecificServices(b => b.TryAddSingleton<NewtonsoftJsonOptions, NewtonsoftJsonOptions>())
            .TryAdd<IRelationalTypeMappingSourcePlugin, NewtonsoftJsonMappingSourcePlugin>()
            .TryAdd<IMemberTranslatorPlugin, NewtonsoftJsonTranslatorPlugin>()
            .TryAdd<IMethodCallTranslatorPlugin, NewtonsoftJsonTranslatorPlugin>();

    public void Validate(IDbContextOptions options)
    {
        if (options.FindExtension<AdoNetOptionsExtension>()?.Dialect is not MySqlDialect)
        {
            throw new InvalidOperationException("UseNewtonsoftJson needs a MySQL dialect (UseAdoNetMySql).");
        }
    }

    private sealed class ExtensionInfo(MySqlNewtonsoftJsonOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider
            => false;

        public override string LogFragment
            => "using Newtonsoft.Json on json columns ";

        public override int GetServiceProviderHashCode()
            => (int)extension.ChangeTracking;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo info && (info.Extension is MySqlNewtonsoftJsonOptionsExtension o) && (o.ChangeTracking == extension.ChangeTracking);

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["MySqlNewtonsoftJson"] = ((int)extension.ChangeTracking).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>The change tracking the context's options give, for the singletons that make the mappings (EF's singleton options).</summary>
internal sealed class NewtonsoftJsonOptions : ISingletonOptions
{
    public MySqlCommonJsonChangeTrackingOptions ChangeTracking { get; private set; }

    public void Initialize(IDbContextOptions options)
        => ChangeTracking = options.FindExtension<MySqlNewtonsoftJsonOptionsExtension>()?.ChangeTracking ?? MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly;

    public void Validate(IDbContextOptions options)
    {
        var changeTracking = options.FindExtension<MySqlNewtonsoftJsonOptionsExtension>()?.ChangeTracking ?? MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly;
        if (changeTracking != ChangeTracking)
        {
            throw new InvalidOperationException("The json change tracking options must be the same for every context on one service provider.");
        }
    }
}

internal sealed class NewtonsoftJsonMappingSourcePlugin(ISqlDialect sql, NewtonsoftJsonOptions options) : MySqlJsonMappingSourcePlugin(sql)
{

    protected override bool IsDocument(Type type)
        => typeof(JToken).IsAssignableFrom(type);

    protected override ValueConverter Converter(Type type)
        => (ValueConverter)Activator.CreateInstance(typeof(NewtonsoftJsonConverter<>).MakeGenericType(type))!;

    protected override ValueComparer Comparer(Type type)
        => NewtonsoftJson.Comparer(type, options.ChangeTracking);
}

internal sealed class NewtonsoftJsonTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : IMemberTranslatorPlugin, IMethodCallTranslatorPlugin
{
    private readonly NewtonsoftJsonTranslator _translator = new(sqlExpressionFactory, typeMappingSource);

    IEnumerable<IMemberTranslator> IMemberTranslatorPlugin.Translators
        => [_translator];

    IEnumerable<IMethodCallTranslator> IMethodCallTranslatorPlugin.Translators
        => [_translator];
}

/// <summary>
///     A POCO's members by their JsonProperty name, and JToken's: Root, the index by name or position, Value&lt;T&gt;() as the
///     type it reads, Count as JSON_LENGTH. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Newtonsoft/Query/Internal/MySqlJsonNewtonsoftDomTranslator.cs
/// </summary>
internal sealed class NewtonsoftJsonTranslator(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    : MySqlJsonPathTranslator(sqlExpressionFactory, typeMappingSource, m => m.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName)
{
    public override SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((member.DeclaringType == null) || !typeof(JToken).IsAssignableFrom(member.DeclaringType))
        {
            return base.Translate(instance, member, returnType, logger);
        }

        if ((instance == null) || !IsJson(instance))
        {
            return null;
        }

        return member.Name switch
        {
            // The token's root is the column itself.
            nameof(JToken.Root) when instance is ColumnExpression => instance,
            nameof(JContainer.Count) when typeof(JContainer).IsAssignableFrom(member.DeclaringType) => Length(instance),
            _ => null
        };
    }

    public override SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        if ((instance != null) && IsJson(instance) && typeof(JToken).IsAssignableFrom(method.DeclaringType)
            && (method.Name == "get_Item") && (arguments is [var key]))
        {
            var unconverted = key is SqlUnaryExpression { OperatorType: System.Linq.Expressions.ExpressionType.Convert } convert ? convert.Operand : key;
            return unconverted switch
            {
                SqlConstantExpression { Value: string name } => Append(instance, new PathSegment(name), method.ReturnType),
                _ when unconverted.Type == typeof(int)
                    => Append(instance, new PathSegment(SqlExpressionFactory.ApplyDefaultTypeMapping(unconverted)), method.ReturnType),
                _ => null
            };
        }

        // Value<T>() on a token, an extension method.
        if ((instance == null) && (method.DeclaringType == typeof(Extensions)) && (method.Name == nameof(Extensions.Value))
            && method.IsGenericMethod && (arguments is [var token]) && IsJson(token))
        {
            return As(token, method.ReturnType);
        }

        return base.Translate(instance, method, arguments, logger);
    }
}

/// <summary>A value as its Newtonsoft.Json text.</summary>
internal sealed class NewtonsoftJsonConverter<T>() : ValueConverter<T, string>(
    value => NewtonsoftJson.Serialize(value),
    text => NewtonsoftJson.Deserialize<T>(text));

/// <summary>Newtonsoft.Json's reading and writing of a json column's value, and the change tracking of it.</summary>
internal static class NewtonsoftJson
{
    public static string Serialize<T>(T value)
        => value switch
        {
            null => "null",
            JToken token => token.ToString(Formatting.None),
            _ => JsonConvert.SerializeObject(value)
        };

    public static T Deserialize<T>(string text)
        => typeof(JToken).IsAssignableFrom(typeof(T))
            ? (T)(object)JToken.Parse(text)
            : JsonConvert.DeserializeObject<T>(text)!;

    public static ValueComparer Comparer(Type type, MySqlCommonJsonChangeTrackingOptions options)
        => (ValueComparer)Activator.CreateInstance(typeof(NewtonsoftJsonComparer<>).MakeGenericType(type), options)!;

    // Whitespace and key order aside: the documents compared.
    public static bool SameDocument<T>(T left, T right)
        => JToken.DeepEquals(JToken.Parse(Serialize(left)), JToken.Parse(Serialize(right)));
}

/// <summary>
///     RootPropertyOnly: a value is unchanged while it is the same value (snapshot the value itself). The full-hierarchy options
///     snapshot a copy (a round trip through JSON) and compare the JSON text, or the documents. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql.Json.Newtonsoft/Storage/ValueComparison/Internal/MySqlJsonNewtonsoftValueComparer.cs
/// </summary>
internal sealed class NewtonsoftJsonComparer<T>(MySqlCommonJsonChangeTrackingOptions options)
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
            return (left, right) => NewtonsoftJson.Serialize(left) == NewtonsoftJson.Serialize(right);
        }

        return (left, right) => NewtonsoftJson.SameDocument(left, right);
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
            return value => NewtonsoftJson.Serialize(value).GetHashCode();
        }

        return value => 0;
    }

    private static System.Linq.Expressions.Expression<Func<T, T>> Snapshot(MySqlCommonJsonChangeTrackingOptions options)
    {
        if (options == MySqlCommonJsonChangeTrackingOptions.RootPropertyOnly)
        {
            return value => value;
        }

        return value => NewtonsoftJson.Deserialize<T>(NewtonsoftJson.Serialize(value));
    }
}
