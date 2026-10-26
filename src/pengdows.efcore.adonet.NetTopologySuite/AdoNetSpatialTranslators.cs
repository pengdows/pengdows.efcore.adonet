using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Geometries;
using pengdows.efcore.adonet.Query;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>
///     NetTopologySuite's members and methods, each written through the dialect's <see cref="SpatialSyntax" /> template of the
///     same name. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/tree/v10.0.11/src/EFCore.Sqlite.NTS/Query/Internal
/// </summary>
public class AdoNetSpatialTranslator(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect sql) : IMemberTranslator, IMethodCallTranslator
{
    // Members whose template is another's.
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["ToBinary"] = "AsBinary",
        ["ToText"] = "AsText",
        ["Count"] = "NumGeometries",
        ["LineString.Count"] = "NumPoints",
        ["get_Item"] = "GetGeometryN"
    };

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MemberInfo member,
        Type returnType,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        => instance != null && IsGeometry(member.DeclaringType)
            ? Call(typeof(LineString).IsAssignableFrom(member.DeclaringType) && (member.Name == "Count") ? "LineString.Count" : member.Name, instance, [], returnType)
            : null;

    public virtual SqlExpression? Translate(
        SqlExpression? instance,
        MethodInfo method,
        IReadOnlyList<SqlExpression> arguments,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger)
    {
        // collection[i] reaches here as Enumerable.ElementAt, a geometry collection being enumerable.
        if ((instance == null) && (method.DeclaringType == typeof(Enumerable)) && (method.Name == nameof(Enumerable.ElementAt))
            && (arguments is [var collection, var index]) && IsGeometry(collection.Type))
        {
            return Call("GetGeometryN", collection, [index], method.ReturnType);
        }

        if ((instance == null) || !IsGeometry(method.DeclaringType))
        {
            return null;
        }

        // Union() of a single geometry dissolves it; Union(other) joins two.
        var name = (method.Name == nameof(Geometry.Union)) && (arguments.Count == 0) ? "UnaryUnion" : method.Name;
        return Call(name, instance, arguments, method.ReturnType);
    }

    private SqlExpression? Call(string member, SqlExpression instance, IReadOnlyList<SqlExpression> arguments, Type returnType)
    {
        if ((sql.Spatial?.Function(Aliases.GetValueOrDefault(member, member)) is not { } template))
        {
            return null;
        }

        var geometryMapping = instance.TypeMapping ?? typeMappingSource.FindMapping(instance.Type);
        var values = new List<SqlExpression> { sqlExpressionFactory.ApplyTypeMapping(instance, geometryMapping)! };
        // A geometry argument binds through a mapping of its own type: a polygon isn't converted as the column's point.
        values.AddRange(arguments.Select(a => typeof(Geometry).IsAssignableFrom(a.Type)
            ? sqlExpressionFactory.ApplyTypeMapping(a, a.TypeMapping ?? typeMappingSource.FindMapping(a.Type))!
            : sqlExpressionFactory.ApplyDefaultTypeMapping(a)!));

        var resultMapping = typeof(Geometry).IsAssignableFrom(returnType)
            ? typeMappingSource.FindMapping(returnType, geometryMapping?.StoreType)
            : typeMappingSource.FindMapping(Nullable.GetUnderlyingType(returnType) ?? (returnType.IsEnum ? Enum.GetUnderlyingType(returnType) : returnType));
        return sqlExpressionFactory.Call(template, values, returnType, resultMapping);
    }

    private static bool IsGeometry(Type? type)
        => (type != null) && typeof(Geometry).IsAssignableFrom(type);
}

/// <summary>Registers <see cref="AdoNetSpatialTranslator" /> for members.</summary>
public class AdoNetSpatialMemberTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect sql) : IMemberTranslatorPlugin
{
    public virtual IEnumerable<IMemberTranslator> Translators { get; } = [new AdoNetSpatialTranslator(sqlExpressionFactory, typeMappingSource, sql)];
}

/// <summary>Registers <see cref="AdoNetSpatialTranslator" /> for methods.</summary>
public class AdoNetSpatialMethodCallTranslatorPlugin(
    ISqlExpressionFactory sqlExpressionFactory,
    IRelationalTypeMappingSource typeMappingSource,
    ISqlDialect sql) : IMethodCallTranslatorPlugin
{
    public virtual IEnumerable<IMethodCallTranslator> Translators { get; } = [new AdoNetSpatialTranslator(sqlExpressionFactory, typeMappingSource, sql)];
}
