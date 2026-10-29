using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     MySQL's model options read and set on the metadata itself, under the names Oracle's MySql.EntityFrameworkCore (and Pomelo)
///     gives them: what <see cref="MySqlModelOptionsExtensions" /> and <see cref="MySqlFullTextExtensions" /> set, for conventions
///     and code that work on the model. The names and signatures come from that package's public API, not its code.
/// </summary>
public static class MySqlMetadataExtensions
{
    /// <summary>The entity type's own character set.</summary>
    public static string? GetCharSet(this IReadOnlyEntityType entityType)
        => entityType[MySqlModelOptionsExtensions.CharSetAnnotation] as string;

    /// <summary>Sets the entity type's character set; null removes it.</summary>
    public static void SetCharSet(this IMutableEntityType entityType, string? charSet)
        => entityType.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, Checked(charSet));

    /// <summary>Sets the entity type's character set from a convention or data annotation; null removes it.</summary>
    public static string? SetCharSet(this IConventionEntityType entityType, string? charSet, bool fromDataAnnotation = false)
        => (string?)entityType.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, Checked(charSet), fromDataAnnotation)?.Value;

    /// <summary>Where the entity type's character set came from.</summary>
    public static ConfigurationSource? GetCharSetConfigurationSource(this IConventionEntityType entityType)
        => entityType.FindAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation)?.GetConfigurationSource();

    /// <summary>The entity type's own collation.</summary>
    public static string? GetCollation(this IReadOnlyEntityType entityType)
        => entityType[MySqlModelOptionsExtensions.CollationAnnotation] as string;

    /// <summary>Sets the entity type's collation; null removes it.</summary>
    public static void SetCollation(this IMutableEntityType entityType, string? collation)
        => entityType.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.CollationAnnotation, Checked(collation));

    /// <summary>Sets the entity type's collation from a convention or data annotation; null removes it.</summary>
    public static string? SetCollation(this IConventionEntityType entityType, string? collation, bool fromDataAnnotation = false)
        => (string?)entityType.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.CollationAnnotation, Checked(collation), fromDataAnnotation)?.Value;

    /// <summary>Where the entity type's collation came from.</summary>
    public static ConfigurationSource? GetCollationConfigurationSource(this IConventionEntityType entityType)
        => entityType.FindAnnotation(MySqlModelOptionsExtensions.CollationAnnotation)?.GetConfigurationSource();

    /// <summary>The table's options (ENGINE, ROW_FORMAT ...), by name.</summary>
    public static Dictionary<string, string> GetTableOptions(this IReadOnlyEntityType entityType)
        => MySqlTableOptions.Deserialize(entityType[MySqlModelOptionsExtensions.StoreOptionsAnnotation] as string);

    /// <summary>Sets the table's options, each validated as <c>HasTableOption</c> validates it; null or empty removes them.</summary>
    /// <exception cref="ArgumentException">A name isn't an option name, or a value could end the statement.</exception>
    public static void SetTableOptions(this IMutableEntityType entityType, Dictionary<string, string>? options)
        => entityType.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.StoreOptionsAnnotation, Serialized(options));

    /// <summary>Sets the table's options from a convention or data annotation.</summary>
    /// <exception cref="ArgumentException">A name isn't an option name, or a value could end the statement.</exception>
    public static Dictionary<string, string>? SetTableOptions(this IConventionEntityType entityType, Dictionary<string, string>? options, bool fromDataAnnotation = false)
    {
        entityType.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.StoreOptionsAnnotation, Serialized(options), fromDataAnnotation);
        return options;
    }

    /// <summary>Where the table's options came from.</summary>
    public static ConfigurationSource? GetTableOptionsConfigurationSource(this IConventionEntityType entityType)
        => entityType.FindAnnotation(MySqlModelOptionsExtensions.StoreOptionsAnnotation)?.GetConfigurationSource();

    /// <summary>The character set of the entity type or complex type.</summary>
    public static string? GetCharSet(this IReadOnlyTypeBase typeBase)
        => typeBase[MySqlModelOptionsExtensions.CharSetAnnotation] as string;

    /// <summary>The collation of the entity type or complex type.</summary>
    public static string? GetCollation(this IReadOnlyTypeBase typeBase)
        => typeBase[MySqlModelOptionsExtensions.CollationAnnotation] as string;

    /// <summary>Whether the index is FULLTEXT; null where not said.</summary>
    public static bool? IsFullText(this IReadOnlyIndex index)
        => index[MySqlFullTextExtensions.FullTextAnnotation] as bool?;

    /// <summary>Makes the index FULLTEXT, or not; null removes the setting.</summary>
    public static void SetIsFullText(this IMutableIndex index, bool? value)
        => index.SetOrRemoveAnnotation(MySqlFullTextExtensions.FullTextAnnotation, value);

    /// <summary>Makes the index FULLTEXT, or not, from a convention or data annotation.</summary>
    public static void SetIsFullText(this IConventionIndex index, bool? value, bool fromDataAnnotation = false)
        => index.SetOrRemoveAnnotation(MySqlFullTextExtensions.FullTextAnnotation, value, fromDataAnnotation);

    /// <summary>Where the index's FULLTEXT setting came from.</summary>
    public static ConfigurationSource? GetIsFullTextConfigurationSource(this IConventionIndex index)
        => index.FindAnnotation(MySqlFullTextExtensions.FullTextAnnotation)?.GetConfigurationSource();

    /// <summary>The FULLTEXT index's parser.</summary>
    public static string? FullTextParser(this IReadOnlyIndex index)
        => index[MySqlFullTextExtensions.FullTextParserAnnotation] as string;

    /// <summary>Sets the FULLTEXT index's parser; null removes it.</summary>
    public static void SetFullTextParser(this IMutableIndex index, string? value)
        => index.SetOrRemoveAnnotation(MySqlFullTextExtensions.FullTextParserAnnotation, Checked(value));

    /// <summary>Sets the FULLTEXT index's parser from a convention or data annotation.</summary>
    public static string? SetFullTextParser(this IConventionIndex index, string? value, bool fromDataAnnotation = false)
        => (string?)index.SetOrRemoveAnnotation(MySqlFullTextExtensions.FullTextParserAnnotation, Checked(value), fromDataAnnotation)?.Value;

    /// <summary>Where the FULLTEXT index's parser came from.</summary>
    public static ConfigurationSource? GetFullTextParserConfigurationSource(this IConventionIndex index)
        => index.FindAnnotation(MySqlFullTextExtensions.FullTextParserAnnotation)?.GetConfigurationSource();

    /// <summary>Whether the index is SPATIAL; null where not said.</summary>
    public static bool? IsSpatial(this IReadOnlyIndex index)
        => index[MySqlModelOptionsExtensions.SpatialAnnotation] as bool?;

    /// <summary>Makes the index SPATIAL, or not; null removes the setting.</summary>
    public static void SetIsSpatial(this IMutableIndex index, bool? value)
        => index.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.SpatialAnnotation, value);

    /// <summary>Makes the index SPATIAL, or not, from a convention or data annotation.</summary>
    public static void SetIsSpatial(this IConventionIndex index, bool? value, bool fromDataAnnotation = false)
        => index.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.SpatialAnnotation, value, fromDataAnnotation);

    /// <summary>Where the index's SPATIAL setting came from.</summary>
    public static ConfigurationSource? GetIsSpatialConfigurationSource(this IConventionIndex index)
        => index.FindAnnotation(MySqlModelOptionsExtensions.SpatialAnnotation)?.GetConfigurationSource();

    /// <summary>The index's prefix lengths, one for each column (0 for a whole column).</summary>
    public static int[]? PrefixLength(this IReadOnlyIndex index)
        => index[MySqlModelOptionsExtensions.PrefixLengthAnnotation] as int[];

    /// <summary>Sets the index's prefix lengths; null removes them.</summary>
    public static void SetPrefixLength(this IMutableIndex index, int[]? values)
        => index.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation, values);

    /// <summary>Sets the index's prefix lengths from a convention or data annotation.</summary>
    public static int[]? SetPrefixLength(this IConventionIndex index, int[]? values, bool fromDataAnnotation = false)
        => (int[]?)index.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation, values, fromDataAnnotation)?.Value;

    /// <summary>Where the index's prefix lengths came from.</summary>
    public static ConfigurationSource? GetPrefixLengthConfigurationSource(this IConventionIndex index)
        => index.FindAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation)?.GetConfigurationSource();

    /// <summary>The key's prefix lengths, one for each column (0 for a whole column).</summary>
    public static int[]? PrefixLength(this IReadOnlyKey key)
        => key[MySqlModelOptionsExtensions.PrefixLengthAnnotation] as int[];

    /// <summary>Sets the key's prefix lengths; null removes them.</summary>
    public static void SetPrefixLength(this IMutableKey key, int[]? values)
        => key.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation, values);

    /// <summary>Sets the key's prefix lengths from a convention or data annotation.</summary>
    public static void SetPrefixLength(this IConventionKey key, int[]? values, bool fromDataAnnotation = false)
        => key.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation, values, fromDataAnnotation);

    /// <summary>Where the key's prefix lengths came from.</summary>
    public static ConfigurationSource? GetPrefixLengthConfigurationSource(this IConventionKey key)
        => key.FindAnnotation(MySqlModelOptionsExtensions.PrefixLengthAnnotation)?.GetConfigurationSource();

    /// <summary>The column's own character set.</summary>
    public static string? GetCharSet(this IReadOnlyProperty property)
        => property[MySqlModelOptionsExtensions.CharSetAnnotation] as string;

    /// <summary>Sets the column's character set; null removes it.</summary>
    public static string? SetCharSet(this IMutableProperty property, string? charSet)
    {
        property.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, Checked(charSet));
        return charSet;
    }

    /// <summary>How the server generates the column's value: <c>UseMySqlIdentityColumn</c> or <c>UseMySqlComputedColumn</c>; null where it doesn't.</summary>
    public static MySQLValueGenerationStrategy? GetValueGenerationStrategy(this IReadOnlyProperty property, StoreObjectIdentifier storeObject = default)
    {
        if (property[MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation] is MySqlModelOptionsExtensions.ComputedColumnStrategy)
        {
            return MySQLValueGenerationStrategy.ComputedColumn;
        }

        return property[AdoNetAnnotationNames.Identity] is true ? MySQLValueGenerationStrategy.IdentityColumn : null;
    }

    /// <summary>Sets how the server generates the column's value, as <c>UseMySqlIdentityColumn</c> or <c>UseMySqlComputedColumn</c> do; None or null removes it.</summary>
    public static void SetValueGenerationStrategy(this IMutableProperty property, MySQLValueGenerationStrategy? value)
    {
        property.SetOrRemoveAnnotation(AdoNetAnnotationNames.Identity, value == MySQLValueGenerationStrategy.IdentityColumn ? true : null);
        property.SetOrRemoveAnnotation(
            MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation,
            value == MySQLValueGenerationStrategy.ComputedColumn ? MySqlModelOptionsExtensions.ComputedColumnStrategy : null);
        property.ValueGenerated = value switch
        {
            MySQLValueGenerationStrategy.IdentityColumn => ValueGenerated.OnAdd,
            MySQLValueGenerationStrategy.ComputedColumn => ValueGenerated.OnAddOrUpdate,
            _ => ValueGenerated.Never
        };
    }

    /// <summary>Sets how the server generates the column's value from a convention or data annotation.</summary>
    public static MySQLValueGenerationStrategy? SetValueGenerationStrategy(
        this IConventionProperty property,
        MySQLValueGenerationStrategy? value,
        bool fromDataAnnotation = false)
    {
        property.SetOrRemoveAnnotation(AdoNetAnnotationNames.Identity, value == MySQLValueGenerationStrategy.IdentityColumn ? true : null, fromDataAnnotation);
        property.SetOrRemoveAnnotation(
            MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation,
            value == MySQLValueGenerationStrategy.ComputedColumn ? MySqlModelOptionsExtensions.ComputedColumnStrategy : null,
            fromDataAnnotation);
        property.SetValueGenerated(
            value switch
            {
                MySQLValueGenerationStrategy.IdentityColumn => ValueGenerated.OnAdd,
                MySQLValueGenerationStrategy.ComputedColumn => ValueGenerated.OnAddOrUpdate,
                _ => null
            },
            fromDataAnnotation);
        return value;
    }

    /// <summary>Where the column's value generation strategy came from.</summary>
    public static ConfigurationSource? GetValueGenerationStrategyConfigurationSource(this IConventionProperty property)
        => (property.FindAnnotation(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation)
                ?? property.FindAnnotation(AdoNetAnnotationNames.Identity))
            ?.GetConfigurationSource();

    /// <summary>Whether the server can generate the column's value on insert: an integer (AUTO_INCREMENT) or a date and time (CURRENT_TIMESTAMP).</summary>
    public static bool IsCompatibleIdentityColumn(this IReadOnlyProperty property)
        => property.IsCompatibleAutoIncrementColumn() || property.IsCompatibleCurrentTimestampColumn();

    /// <summary>Whether the column can be AUTO_INCREMENT: an integer.</summary>
    public static bool IsCompatibleAutoIncrementColumn(this IReadOnlyProperty property)
        => ClrType(property) is { } type
           && ((type == typeof(byte)) || (type == typeof(sbyte)) || (type == typeof(short)) || (type == typeof(ushort))
               || (type == typeof(int)) || (type == typeof(uint)) || (type == typeof(long)) || (type == typeof(ulong)));

    /// <summary>Whether the column can default to CURRENT_TIMESTAMP: a DateTime or DateTimeOffset.</summary>
    public static bool IsCompatibleCurrentTimestampColumn(this IReadOnlyProperty property)
        => ClrType(property) is { } type && ((type == typeof(DateTime)) || (type == typeof(DateTimeOffset)));

    /// <summary>Whether the server can set the column on insert and update (<c>UseMySqlComputedColumn</c>): a date and time.</summary>
    public static bool IsCompatibleComputedColumn(this IReadOnlyProperty property)
        => property.IsCompatibleCurrentTimestampColumn();

    /// <summary>Sets the model's character set from a convention or data annotation, as Oracle's convention builder does.</summary>
    public static IConventionModelBuilder? HasCharSet(this IConventionModelBuilder modelBuilder, string? charSet, bool fromDataAnnotation = false)
    {
        if (!modelBuilder.CanSetCharSet(charSet, fromDataAnnotation))
        {
            return null;
        }

        modelBuilder.Metadata.SetOrRemoveAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, Checked(charSet), fromDataAnnotation);
        return modelBuilder;
    }

    /// <summary>Whether a convention or data annotation may set the model's character set.</summary>
    public static bool CanSetCharSet(this IConventionModelBuilder modelBuilder, string? charSet, bool fromDataAnnotation = false)
        => modelBuilder.CanSetAnnotation(MySqlModelOptionsExtensions.CharSetAnnotation, charSet, fromDataAnnotation);

    private static Type? ClrType(IReadOnlyProperty property)
    {
        var type = property.GetValueConverter()?.ProviderClrType ?? property.ClrType;
        return Nullable.GetUnderlyingType(type) ?? type;
    }

    private static string? Checked(string? name)
        => name == null ? null : MySqlModelOptionsExtensions.Name(name, nameof(name));

    private static string? Serialized(Dictionary<string, string>? options)
    {
        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in options ?? [])
        {
            MySqlTableOptions.Update(validated, name, value);
        }

        return MySqlTableOptions.Serialize(validated);
    }
}
