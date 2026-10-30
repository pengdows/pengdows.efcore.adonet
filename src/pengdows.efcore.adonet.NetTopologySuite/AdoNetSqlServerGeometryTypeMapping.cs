using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace pengdows.efcore.adonet.NetTopologySuite;

/// <summary>SQL Server geometry mapping using its UDT binary contract.</summary>
internal sealed class AdoNetSqlServerGeometryTypeMapping<TGeometry>
    : RelationalGeometryTypeMapping<TGeometry, SqlBytes>
    where TGeometry : Geometry
{
    private static readonly MethodInfo GetSqlBytes
        = Type.GetType("Microsoft.Data.SqlClient.SqlDataReader, Microsoft.Data.SqlClient")!
            .GetRuntimeMethod("GetSqlBytes", [typeof(int)])!;

    private readonly bool _geography;

    public AdoNetSqlServerGeometryTypeMapping(NtsGeometryServices geometryServices, string storeType)
        : base(
            CreateConverter(geometryServices, IsGeography(storeType)),
            storeType,
            JsonGeometryWktReaderWriter.Instance)
        => _geography = IsGeography(storeType);

    private static ValueConverter<TGeometry, SqlBytes> CreateConverter(NtsGeometryServices services, bool geography)
    {
        var reader = new SqlServerBytesReader(services) { IsGeography = geography };
        var writer = new SqlServerBytesWriter { IsGeography = geography };
        return new ValueConverter<TGeometry, SqlBytes>(
            geometry => new SqlBytes(writer.Write(geometry)),
            bytes => ReadGeometry<TGeometry>(reader, bytes));
    }

    private static T ReadGeometry<T>(SqlServerBytesReader reader, SqlBytes bytes)
        where T : Geometry
    {
        if (bytes == null || bytes.IsNull)
        {
            return null!;
        }

        var value = bytes.Value ?? throw new InvalidOperationException("SQL Server returned a non-null spatial value without binary bytes.");
        try
        {
            return (T)reader.Read(value);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Could not decode SQL Server spatial payload ({value.Length} bytes).", exception);
        }
    }

    private AdoNetSqlServerGeometryTypeMapping(
        RelationalTypeMappingParameters parameters,
        ValueConverter<TGeometry, SqlBytes>? converter)
        : base(parameters, converter)
        => _geography = IsGeography(StoreType);

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AdoNetSqlServerGeometryTypeMapping<TGeometry>(parameters, SpatialConverter);

    protected override string GenerateNonNullSqlLiteral(object value)
    {
        var geometry = (Geometry)value;
        var defaultSrid = geometry.SRID == (_geography ? 4326 : 0) || geometry == Point.Empty;
        var writer = WKTWriter.ForMicrosoftSqlServer().Write(geometry);
        var type = _geography ? "geography" : "geometry";
        return $"{type}::{(defaultSrid ? "Parse" : "STGeomFromText")}('{writer}'{(defaultSrid ? "" : ", " + geometry.SRID.ToString(CultureInfo.InvariantCulture))})";
    }

    public override MethodInfo GetDataReaderMethod() => GetSqlBytes;

    protected override string AsText(object value) => ((Geometry)value).AsText();

    protected override int GetSrid(object value) => ((Geometry)value).SRID;

    protected override Type WktReaderType => typeof(WKTReader);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter.Value == DBNull.Value)
        {
            parameter.Value = SqlBytes.Null;
        }

        var type = parameter.GetType();
        if (DriverMembers.Property(type, "SqlDbType") is { PropertyType.IsEnum: true } sqlDbType)
        {
            sqlDbType.SetValue(parameter, Enum.Parse(sqlDbType.PropertyType, nameof(SqlDbType.Udt)));
        }

        DriverMembers.Property(type, "UdtTypeName")?.SetValue(parameter, _geography ? "geography" : "geometry");
    }

    private static bool IsGeography(string storeType)
        => string.Equals(storeType, "geography", StringComparison.OrdinalIgnoreCase);
}
