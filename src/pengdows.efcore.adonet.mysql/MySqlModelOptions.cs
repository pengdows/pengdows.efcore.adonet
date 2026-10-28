using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's model options, as Pomelo has them: a character set for a column, a table or every table; an index of a column's
///     first characters; a SPATIAL index. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlPropertyBuilderExtensions.cs
/// </summary>
public static partial class MySqlModelOptionsExtensions
{
    internal const string CharSetAnnotation = "MySql:CharSet";
    internal const string PrefixLengthAnnotation = "MySql:IndexPrefixLength";
    internal const string SpatialAnnotation = "MySql:SpatialIndex";
    internal const string StoreOptionsAnnotation = "MySql:StoreOptions";
    internal const string SpatialReferenceSystemAnnotation = "MySql:SpatialReferenceSystemId";
    internal const string GuidCollationAnnotation = "MySql:GuidCollation";
    internal const string CollationAnnotation = RelationalAnnotationNames.Collation;

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

    /// <summary>
    ///     Indexes only the first <paramref name="prefixLengths" /> characters (or bytes) of each of the key's columns, in order;
    ///     0 indexes a column whole.
    /// </summary>
    public static KeyBuilder HasPrefixLength(this KeyBuilder keyBuilder, params int[] prefixLengths)
        => keyBuilder.HasAnnotation(PrefixLengthAnnotation, prefixLengths);

    /// <summary>The column's CHARACTER SET, for a property of a complex type.</summary>
    public static ComplexTypePropertyBuilder HasCharSet(this ComplexTypePropertyBuilder propertyBuilder, string charSet)
        => propertyBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>The column's CHARACTER SET, for a property of a complex type.</summary>
    public static ComplexTypePropertyBuilder<TProperty> HasCharSet<TProperty>(this ComplexTypePropertyBuilder<TProperty> propertyBuilder, string charSet)
        => propertyBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>
    ///     The spatial reference system of a geometry column, which then holds only values in it: <c>SRID n</c> on MySQL,
    ///     <c>REF_SYSTEM_ID=n</c> on MariaDB. Null removes it.
    /// </summary>
    public static PropertyBuilder HasSpatialReferenceSystem(this PropertyBuilder propertyBuilder, int? srid)
        => propertyBuilder.HasAnnotation(SpatialReferenceSystemAnnotation, srid);

    /// <summary>
    ///     The spatial reference system of a geometry column, which then holds only values in it: <c>SRID n</c> on MySQL,
    ///     <c>REF_SYSTEM_ID=n</c> on MariaDB. Null removes it.
    /// </summary>
    public static PropertyBuilder<TProperty> HasSpatialReferenceSystem<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, int? srid)
        => propertyBuilder.HasAnnotation(SpatialReferenceSystemAnnotation, srid);

    /// <summary>The key is AUTO_INCREMENT: the core's <c>UseIdentityColumn</c> under Pomelo's name.</summary>
    public static PropertyBuilder UseMySqlIdentityColumn(this PropertyBuilder propertyBuilder)
        => propertyBuilder.UseIdentityColumn();

    /// <summary>The key is AUTO_INCREMENT: the core's <c>UseIdentityColumn</c> under Pomelo's name.</summary>
    public static PropertyBuilder<TProperty> UseMySqlIdentityColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => propertyBuilder.UseIdentityColumn();

    /// <summary>The table's default COLLATE, which its text columns without one of their own take.</summary>
    public static EntityTypeBuilder UseCollation(this EntityTypeBuilder entityTypeBuilder, string collation)
        => entityTypeBuilder.HasAnnotation(CollationAnnotation, Name(collation, nameof(collation)));

    /// <summary>The table's default COLLATE, which its text columns without one of their own take.</summary>
    public static EntityTypeBuilder<TEntity> UseCollation<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, string collation)
        where TEntity : class
        => entityTypeBuilder.HasAnnotation(CollationAnnotation, Name(collation, nameof(collation)));

    /// <summary>The COLLATE of every GUID column (CHAR(36)) without a collation of its own.</summary>
    public static ModelBuilder UseGuidCollation(this ModelBuilder modelBuilder, string collation)
        => modelBuilder.HasAnnotation(GuidCollationAnnotation, Name(collation, nameof(collation)));

    /// <summary>
    ///     A table option written after the table's definition, <c>name=value</c> (<c>ENGINE</c>, <c>ROW_FORMAT</c>,
    ///     <c>AUTO_INCREMENT</c> ...). A null value removes the option; a null name and value remove them all. The value is
    ///     SQL: a string value is written with its quotes (<c>"'a comment'"</c>).
    /// </summary>
    /// <exception cref="ArgumentException">The name isn't an option name, or the value could end the statement.</exception>
    public static EntityTypeBuilder HasTableOption(this EntityTypeBuilder entityTypeBuilder, string? name, string? value)
    {
        var options = MySqlTableOptions.Deserialize(entityTypeBuilder.Metadata[StoreOptionsAnnotation] as string);
        MySqlTableOptions.Update(options, name, value);
        entityTypeBuilder.Metadata.SetOrRemoveAnnotation(StoreOptionsAnnotation, MySqlTableOptions.Serialize(options));
        return entityTypeBuilder;
    }

    /// <summary>
    ///     A table option written after the table's definition, <c>name=value</c> (<c>ENGINE</c>, <c>ROW_FORMAT</c>,
    ///     <c>AUTO_INCREMENT</c> ...). A null value removes the option; a null name and value remove them all. The value is
    ///     SQL: a string value is written with its quotes (<c>"'a comment'"</c>).
    /// </summary>
    /// <exception cref="ArgumentException">The name isn't an option name, or the value could end the statement.</exception>
    public static EntityTypeBuilder<TEntity> HasTableOption<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, string? name, string? value)
        where TEntity : class
        => (EntityTypeBuilder<TEntity>)HasTableOption((EntityTypeBuilder)entityTypeBuilder, name, value);

    /// <summary>Whether the context's database is MySQL or a server that speaks it (MariaDB, SingleStore), as Pomelo's.</summary>
    public static bool IsMySql(this DatabaseFacade database)
        => database.GetInfrastructure().GetService<IAdoNetDialect>() is MySqlDialect;

    // A collation, character set or parser is written into DDL as it is: a name, nothing else.
    internal static string Name(string name, string parameter)
        => NamePattern().IsMatch(name) ? name : throw new ArgumentException($"'{name}' is not a name.", parameter);

    [GeneratedRegex(@"^[A-Za-z0-9_]+$")]
    private static partial Regex NamePattern();
}

/// <summary>
///     Table options as Pomelo stores them in one annotation, so its migration snapshots read the same: <c>name=value,</c> for
///     each, a comma in a value doubled. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Extensions/MySqlEntityTypeExtensions.cs
/// </summary>
internal static partial class MySqlTableOptions
{
    public static Dictionary<string, string> Deserialize(string? text)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var part = new StringBuilder();
        for (var i = 0; i < (text?.Length ?? 0); i++)
        {
            var c = text![i];
            if ((c == ',') && (i + 1 < text.Length) && (text[i + 1] == ','))
            {
                part.Append(',');
                i++;
                continue;
            }

            if (c != ',')
            {
                part.Append(c);
                continue;
            }

            Add(options, part.ToString());
            part.Clear();
        }

        Add(options, part.ToString());
        return options;
    }

    public static string? Serialize(Dictionary<string, string> options)
        => options.Count == 0
            ? null
            : string.Concat(options.Select(o => o.Key + "=" + o.Value.Replace(",", ",,", StringComparison.Ordinal) + ","));

    public static void Update(Dictionary<string, string> options, string? name, string? value)
    {
        if (name == null)
        {
            if (value != null)
            {
                throw new ArgumentException("A table option's value needs its name.", nameof(value));
            }

            options.Clear();
            return;
        }

        if (!OptionName().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a table option name.", nameof(name));
        }

        if (value == null)
        {
            options.Remove(name);
            return;
        }

        if (string.IsNullOrWhiteSpace(value) || UnsafeValue().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a table option value: it is empty or could end the statement.", nameof(value));
        }

        options[name.Trim()] = value.Trim();
    }

    // Validated as HasTableOption validates, since a migration's annotation may have been written by hand.
    private static void Add(Dictionary<string, string> options, string part)
    {
        var equals = part.IndexOf('=', StringComparison.Ordinal);
        if ((equals > 0) && (equals < part.Length - 1))
        {
            Update(options, part[..equals], part[(equals + 1)..]);
        }
    }

    // ENGINE, ROW_FORMAT, DEFAULT CHARSET: words of letters, digits and underscores.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*(?: [A-Za-z][A-Za-z0-9_]*)*$")]
    private static partial Regex OptionName();

    // A statement terminator, a comment or a line break.
    [GeneratedRegex(@";|--|/\*|\*/|#|[\r\n]")]
    private static partial Regex UnsafeValue();
}
