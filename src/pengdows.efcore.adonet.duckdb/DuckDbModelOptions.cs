using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet.duckdb;

/// <summary>
///     DuckDB's model options, as the DuckDB EF provider names them. Behaviour reference (not copied):
///     https://github.com/skuirrels/DuckDB.EFCoreProvider/blob/f49a8c3e25f28055ad139383d25e09cede39c6f8/src/DuckDB.EFCoreProvider/Extensions/DuckDBPropertyBuilderExtensions.cs
/// </summary>
public static class DuckDbModelOptionsExtensions
{
    /// <summary>
    ///     Makes the integer column default to <c>nextval</c> of a sequence created with its table: the core's
    ///     <see cref="AdoNetPropertyBuilderExtensions.UseIdentityColumn(PropertyBuilder)" /> by the DuckDB provider's name. A
    ///     single-column integer key is one by convention.
    /// </summary>
    public static PropertyBuilder UseAutoIncrement(this PropertyBuilder propertyBuilder)
        => propertyBuilder.UseIdentityColumn();

    /// <inheritdoc cref="UseAutoIncrement(PropertyBuilder)" />
    public static PropertyBuilder<TProperty> UseAutoIncrement<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => propertyBuilder.UseIdentityColumn();
}
