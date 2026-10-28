using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's model options, as Pomelo has them: a character set for a column, a table or every table; an index of a column's
///     first characters; a SPATIAL index. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlPropertyBuilderExtensions.cs
/// </summary>
public static class MySqlModelOptionsExtensions
{
    internal const string CharSetAnnotation = "MySql:CharSet";
    internal const string PrefixLengthAnnotation = "MySql:IndexPrefixLength";
    internal const string SpatialAnnotation = "MySql:SpatialIndex";

    /// <summary>The column's CHARACTER SET.</summary>
    public static PropertyBuilder HasCharSet(this PropertyBuilder propertyBuilder, string charSet)
        => propertyBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>The column's CHARACTER SET.</summary>
    public static PropertyBuilder<TProperty> HasCharSet<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, string charSet)
        => propertyBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>The table's DEFAULT CHARACTER SET, which its text columns without one of their own take.</summary>
    public static EntityTypeBuilder HasCharSet(this EntityTypeBuilder entityTypeBuilder, string charSet)
        => entityTypeBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>The table's DEFAULT CHARACTER SET, which its text columns without one of their own take.</summary>
    public static EntityTypeBuilder<TEntity> HasCharSet<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, string charSet)
        where TEntity : class
        => entityTypeBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>Every table's DEFAULT CHARACTER SET, unless its entity type names one.</summary>
    public static ModelBuilder HasCharSet(this ModelBuilder modelBuilder, string charSet)
        => modelBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>
    ///     Indexes only the first <paramref name="prefixLengths" /> characters (or bytes) of each column, in order; 0 indexes a
    ///     column whole.
    /// </summary>
    public static IndexBuilder HasPrefixLength(this IndexBuilder indexBuilder, params int[] prefixLengths)
        => indexBuilder.HasAnnotation(PrefixLengthAnnotation, prefixLengths);

    /// <summary>
    ///     Indexes only the first <paramref name="prefixLengths" /> characters (or bytes) of each column, in order; 0 indexes a
    ///     column whole.
    /// </summary>
    public static IndexBuilder<TEntity> HasPrefixLength<TEntity>(this IndexBuilder<TEntity> indexBuilder, params int[] prefixLengths)
        => indexBuilder.HasAnnotation(PrefixLengthAnnotation, prefixLengths);

    /// <summary>Makes the index a SPATIAL index, over a geometry column.</summary>
    public static IndexBuilder IsSpatial(this IndexBuilder indexBuilder, bool spatial = true)
        => indexBuilder.HasAnnotation(SpatialAnnotation, spatial);

    /// <summary>Makes the index a SPATIAL index, over a geometry column.</summary>
    public static IndexBuilder<TEntity> IsSpatial<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool spatial = true)
        => indexBuilder.HasAnnotation(SpatialAnnotation, spatial);
}
