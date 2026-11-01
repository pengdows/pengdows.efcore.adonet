using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     A CLR enum stored in a PostgreSQL enum type as its label. Npgsql's provider binds the enum itself, which needs the type
///     mapped on the data source (<c>MapEnum</c>); here the label is bound as text of no declared type, which the server takes as
///     the column's enum, so the plain driver needs nothing. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/Mapping/NpgsqlEnumTypeMapping.cs
/// </summary>
internal sealed class PostgreSqlEnumTypeMapping : RelationalTypeMapping
{
    private PostgreSqlEnumTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    public static PostgreSqlEnumTypeMapping Create(Type enumType, string storeType, string[] labels)
    {
        var underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        var converter = (ValueConverter)Activator.CreateInstance(typeof(LabelConverter<>).MakeGenericType(underlying), [labels])!;
        return new PostgreSqlEnumTypeMapping(
            new RelationalTypeMappingParameters(new CoreTypeMappingParameters(underlying, converter), storeType));
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlEnumTypeMapping(parameters);

    protected override void ConfigureParameter(DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is NpgsqlParameter npgsql)
        {
            npgsql.NpgsqlDbType = NpgsqlDbType.Unknown;
        }
    }

    protected override string GenerateNonNullSqlLiteral(object value)
        => "'" + ((string)value).Replace("'", "''", StringComparison.Ordinal) + "'::" + StoreType;

    // Each member to its label, in declaration order, as the enum type was created.
    private sealed class LabelConverter<TEnum>(string[] labels) : ValueConverter<TEnum, string>(
        value => labels[Array.IndexOf(Enum.GetValues<TEnum>(), value)],
        label => Enum.GetValues<TEnum>()[Array.IndexOf(labels, label)])
        where TEnum : struct, Enum;
}

/// <summary>
///     Maps each property of an enum type declared with <c>HasPostgresEnum&lt;TEnum&gt;</c> to that PostgreSQL enum type. Runs after
///     EF assigns type mappings, replacing the integer one.
/// </summary>
internal sealed class PostgreSqlEnumConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        var model = modelBuilder.Metadata;
        foreach (var property in model.GetEntityTypes().SelectMany(e => e.GetDeclaredProperties()))
        {
            var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (!enumType.IsEnum
                || (property.GetValueConverter() != null)
                || (property.GetColumnType() != null)
                || (model.FindAnnotation(PostgreSqlAnnotations.EnumClrPrefix + enumType.FullName)?.Value is not string typeName)
                || (model.FindAnnotation(PostgreSqlAnnotations.EnumPrefix + typeName)?.Value is not string[] labels))
            {
                continue;
            }

            var (schema, name) = PostgreSqlAnnotations.Parse("", typeName);
            var storeType = PostgreSqlAnnotations.Delimit(name, schema);
            property.Builder.HasColumnType(storeType);
            property.SetTypeMapping(PostgreSqlEnumTypeMapping.Create(enumType, storeType, labels));
        }
    }
}
