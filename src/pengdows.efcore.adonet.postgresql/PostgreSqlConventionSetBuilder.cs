using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using pengdows.efcore.adonet.Metadata;

namespace pengdows.efcore.adonet.postgresql;

internal sealed class PostgreSqlConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    RelationalConventionSetBuilderDependencies relationalDependencies,
    IDatabaseCapabilities capabilities)
    : AdoNetConventionSetBuilder(dependencies, relationalDependencies, capabilities)
{
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();
        conventionSet.ModelFinalizingConventions.Add(new PostgreSqlRowVersionConvention());
        return conventionSet;
    }
}

/// <summary>
///     A uint row version (a concurrency token the database sets on every insert and update) is PostgreSQL's xmin system
///     column, which every row has and every change sets. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Metadata/Conventions/NpgsqlPostgresModelFinalizingConvention.cs
/// </summary>
internal sealed class PostgreSqlRowVersionConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var property in modelBuilder.Metadata.GetEntityTypes().SelectMany(e => e.GetDeclaredProperties()))
        {
            if (property is { ValueGenerated: ValueGenerated.OnAddOrUpdate, IsConcurrencyToken: true }
                && (property.ClrType == typeof(uint))
                && (property.GetColumnType() is null or "xid"))
            {
                property.Builder.HasColumnName("xmin");
            }
        }
    }
}

/// <summary>
///     xid, the type of the xmin row version: read as uint, and bound as xid, since Npgsql binds a plain uint as oid and
///     PostgreSQL has no xid = oid operator. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Storage/Internal/Mapping/NpgsqlUIntTypeMapping.cs
/// </summary>
internal sealed class PostgreSqlXidTypeMapping : Microsoft.EntityFrameworkCore.Storage.UIntTypeMapping
{
    public PostgreSqlXidTypeMapping()
        : base("xid")
    {
    }

    private PostgreSqlXidTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new PostgreSqlXidTypeMapping(parameters);

    protected override void ConfigureParameter(System.Data.Common.DbParameter parameter)
    {
        base.ConfigureParameter(parameter);
        if (parameter is Npgsql.NpgsqlParameter npgsql)
        {
            npgsql.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Xid;
        }
    }
}

/// <summary>Maps a uint row version, or a uint over an xid column, to <see cref="PostgreSqlXidTypeMapping" />.</summary>
internal sealed class PostgreSqlXidTypeMappingPlugin : Microsoft.EntityFrameworkCore.Storage.IRelationalTypeMappingSourcePlugin
{
    public Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping? FindMapping(in Microsoft.EntityFrameworkCore.Storage.RelationalTypeMappingInfo mappingInfo)
        => ((mappingInfo.ClrType == typeof(uint)) || (mappingInfo.ClrType == null))
            && ((mappingInfo.IsRowVersion == true && mappingInfo.StoreTypeName == null) || (mappingInfo.StoreTypeNameBase == "xid"))
                ? new PostgreSqlXidTypeMapping()
                : null;
}
