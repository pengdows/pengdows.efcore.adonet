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
    internal const string CharSetDelegationAnnotation = "MySql:CharSetDelegation";
    internal const string CollationDelegationAnnotation = "MySql:CollationDelegation";
    internal const string ValueGenerationStrategyAnnotation = "MySql:ValueGenerationStrategy";
    internal const string ComputedColumnStrategy = "ComputedColumn";
    internal const string IdentityColumnStrategy = "IdentityColumn";

    /// <summary>The column's CHARACTER SET.</summary>
    public static PropertyBuilder HasCharSet(this PropertyBuilder propertyBuilder, string charSet)
        => propertyBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>The column's CHARACTER SET.</summary>
    public static PropertyBuilder<TProperty> HasCharSet<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, string charSet)
        => propertyBuilder.HasAnnotation(CharSetAnnotation, charSet);

    /// <summary>
    ///     The entity type's CHARACTER SET: its table's default and, explicitly, its text columns', or only those
    ///     <paramref name="delegationModes" /> names (null: both). A null character set with a delegation keeps the model's
    ///     from the levels it names.
    /// </summary>
    public static EntityTypeBuilder HasCharSet(this EntityTypeBuilder entityTypeBuilder, string? charSet, DelegationModes? delegationModes = null)
        => Delegated(entityTypeBuilder.Metadata, CharSetAnnotation, CharSetDelegationAnnotation, charSet, delegationModes, entityTypeBuilder);

    /// <summary>
    ///     The entity type's CHARACTER SET for its table, and also, explicitly, its text columns when
    ///     <paramref name="explicitlyDelegateToChildren" />.
    /// </summary>
    public static EntityTypeBuilder HasCharSet(this EntityTypeBuilder entityTypeBuilder, string? charSet, bool explicitlyDelegateToChildren)
        => entityTypeBuilder.HasCharSet(charSet, explicitlyDelegateToChildren ? DelegationModes.ApplyToAll : DelegationModes.ApplyToTables);

    /// <summary>
    ///     The entity type's CHARACTER SET: its table's default and, explicitly, its text columns', or only those
    ///     <paramref name="delegationModes" /> names (null: both).
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasCharSet<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        string? charSet,
        DelegationModes? delegationModes = null)
        where TEntity : class
        => (EntityTypeBuilder<TEntity>)HasCharSet((EntityTypeBuilder)entityTypeBuilder, charSet, delegationModes);

    /// <summary>
    ///     The entity type's CHARACTER SET for its table, and also, explicitly, its text columns when
    ///     <paramref name="explicitlyDelegateToChildren" />.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasCharSet<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, string? charSet, bool explicitlyDelegateToChildren)
        where TEntity : class
        => (EntityTypeBuilder<TEntity>)HasCharSet((EntityTypeBuilder)entityTypeBuilder, charSet, explicitlyDelegateToChildren);

    /// <summary>
    ///     The model's CHARACTER SET: the database's (ALTER DATABASE), every table's default and, explicitly, every text
    ///     column's, or only those <paramref name="delegationModes" /> names (null: all three). An entity type's or property's
    ///     own wins below it.
    /// </summary>
    public static ModelBuilder HasCharSet(this ModelBuilder modelBuilder, string? charSet, DelegationModes? delegationModes = null)
        => Delegated(modelBuilder.Model, CharSetAnnotation, CharSetDelegationAnnotation, charSet, delegationModes, modelBuilder);

    /// <summary>
    ///     The database's CHARACTER SET, and also, explicitly, every table's and text column's when
    ///     <paramref name="explicitlyDelegateToChildren" />.
    /// </summary>
    public static ModelBuilder HasCharSet(this ModelBuilder modelBuilder, string? charSet, bool explicitlyDelegateToChildren)
        => modelBuilder.HasCharSet(charSet, explicitlyDelegateToChildren ? DelegationModes.ApplyToAll : DelegationModes.ApplyToDatabases);

    /// <summary>
    ///     The model's collation for the levels <paramref name="delegationModes" /> names (null: the database, every table and
    ///     every text column), where EF's own <c>UseCollation(collation)</c> is the database's only.
    /// </summary>
    public static ModelBuilder UseCollation(this ModelBuilder modelBuilder, string? collation, DelegationModes? delegationModes)
        => Delegated(modelBuilder.Model, CollationAnnotation, CollationDelegationAnnotation, collation, delegationModes ?? DelegationModes.Default, modelBuilder);

    /// <summary>
    ///     The model's collation for the database, and also, explicitly, every table and text column unless
    ///     <paramref name="explicitlyDelegateToChildren" /> is false.
    /// </summary>
    public static ModelBuilder UseCollation(this ModelBuilder modelBuilder, string? collation, bool? explicitlyDelegateToChildren)
        => modelBuilder.UseCollation(collation, explicitlyDelegateToChildren == false ? DelegationModes.ApplyToDatabases : DelegationModes.ApplyToAll);

    private static TBuilder Delegated<TBuilder>(
        Microsoft.EntityFrameworkCore.Metadata.IMutableAnnotatable target,
        string valueAnnotation,
        string delegationAnnotation,
        string? value,
        DelegationModes? delegationModes,
        TBuilder builder)
    {
        target.SetOrRemoveAnnotation(valueAnnotation, value == null ? null : Name(value, nameof(value)));
        target.SetOrRemoveAnnotation(delegationAnnotation, delegationModes);
        return builder;
    }

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

    /// <summary>
    ///     The server generates the column on insert: an integer is AUTO_INCREMENT (the core's <c>UseIdentityColumn</c>), a
    ///     DateTime or DateTimeOffset <c>DEFAULT CURRENT_TIMESTAMP(n)</c>, read back after the insert, as Pomelo's.
    /// </summary>
    public static PropertyBuilder UseMySqlIdentityColumn(this PropertyBuilder propertyBuilder)
    {
        if (!IsDateTime(propertyBuilder.Metadata.ClrType))
        {
            return propertyBuilder.UseIdentityColumn();
        }

        return propertyBuilder.ValueGeneratedOnAdd().HasAnnotation(ValueGenerationStrategyAnnotation, IdentityColumnStrategy);
    }

    /// <summary>
    ///     The server generates the column on insert: an integer is AUTO_INCREMENT (the core's <c>UseIdentityColumn</c>), a
    ///     DateTime or DateTimeOffset <c>DEFAULT CURRENT_TIMESTAMP(n)</c>, read back after the insert, as Pomelo's.
    /// </summary>
    public static PropertyBuilder<TProperty> UseMySqlIdentityColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => (PropertyBuilder<TProperty>)UseMySqlIdentityColumn((PropertyBuilder)propertyBuilder);

    /// <summary>
    ///     Every integer key the server can number is AUTO_INCREMENT, as Pomelo's <c>AutoIncrementColumns</c>: this provider's
    ///     convention already, said explicitly on the model.
    /// </summary>
    public static ModelBuilder AutoIncrementColumns(this ModelBuilder modelBuilder)
        => modelBuilder.HasAnnotation(ValueGenerationStrategyAnnotation, IdentityColumnStrategy);

    /// <summary>The column's collation: EF's <c>UseCollation</c>, under Pomelo's earlier name.</summary>
    [Obsolete("Call 'UseCollation()' instead.")]
    public static PropertyBuilder HasCollation(this PropertyBuilder propertyBuilder, string collation)
        => propertyBuilder.UseCollation(collation);

    /// <summary>The column's collation: EF's <c>UseCollation</c>, under Pomelo's earlier name.</summary>
    [Obsolete("Call 'UseCollation()' instead.")]
    public static PropertyBuilder<TProperty> HasCollation<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, string collation)
        => propertyBuilder.UseCollation(collation);

    /// <summary><see cref="IsSpatial(IndexBuilder, bool)" /> under Pomelo's earlier name.</summary>
    [Obsolete("This extension method is obsolete. Use IsSpatial instead.")]
    public static IndexBuilder ForMySqlIsSpatial(this IndexBuilder indexBuilder, bool spatial = true)
        => indexBuilder.IsSpatial(spatial);

    /// <summary><see cref="IsSpatial(IndexBuilder, bool)" /> under Pomelo's earlier name.</summary>
    [Obsolete("This extension method is obsolete. Use IsSpatial instead.")]
    public static IndexBuilder<TEntity> ForMySqlIsSpatial<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool spatial = true)
        => indexBuilder.IsSpatial(spatial);

    /// <summary><c>IsFullText</c> under Pomelo's earlier name.</summary>
    [Obsolete("This extension method is obsolete. Use IsFullText instead.")]
    public static IndexBuilder ForMySqlIsFullText(this IndexBuilder indexBuilder, bool fullText = true)
        => indexBuilder.IsFullText(fullText);

    /// <summary><c>IsFullText</c> under Pomelo's earlier name.</summary>
    [Obsolete("This extension method is obsolete. Use IsFullText instead.")]
    public static IndexBuilder<TEntity> ForMySqlIsFullText<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool fullText = true)
        => indexBuilder.IsFullText(fullText);

    internal static bool IsDateTime(Type type)
        => (Nullable.GetUnderlyingType(type) ?? type) is var t && ((t == typeof(DateTime)) || (t == typeof(DateTimeOffset)));

    /// <summary>
    ///     The entity type's COLLATE: its table's default and, explicitly, its text columns', or only those
    ///     <paramref name="delegationModes" /> names (null: both).
    /// </summary>
    public static EntityTypeBuilder UseCollation(this EntityTypeBuilder entityTypeBuilder, string? collation, DelegationModes? delegationModes = null)
        => Delegated(entityTypeBuilder.Metadata, CollationAnnotation, CollationDelegationAnnotation, collation, delegationModes, entityTypeBuilder);

    /// <summary>
    ///     The entity type's COLLATE for its table, and also, explicitly, its text columns when
    ///     <paramref name="explicitlyDelegateToChildren" />.
    /// </summary>
    public static EntityTypeBuilder UseCollation(this EntityTypeBuilder entityTypeBuilder, string? collation, bool explicitlyDelegateToChildren)
        => entityTypeBuilder.UseCollation(collation, explicitlyDelegateToChildren ? DelegationModes.ApplyToAll : DelegationModes.ApplyToTables);

    /// <summary>
    ///     The entity type's COLLATE: its table's default and, explicitly, its text columns', or only those
    ///     <paramref name="delegationModes" /> names (null: both).
    /// </summary>
    public static EntityTypeBuilder<TEntity> UseCollation<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        string? collation,
        DelegationModes? delegationModes = null)
        where TEntity : class
        => (EntityTypeBuilder<TEntity>)UseCollation((EntityTypeBuilder)entityTypeBuilder, collation, delegationModes);

    /// <summary>
    ///     The entity type's COLLATE for its table, and also, explicitly, its text columns when
    ///     <paramref name="explicitlyDelegateToChildren" />.
    /// </summary>
    public static EntityTypeBuilder<TEntity> UseCollation<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, string? collation, bool explicitlyDelegateToChildren)
        where TEntity : class
        => (EntityTypeBuilder<TEntity>)UseCollation((EntityTypeBuilder)entityTypeBuilder, collation, explicitlyDelegateToChildren);

    /// <summary>
    ///     A date and time the server sets on insert and on every update (<c>DEFAULT CURRENT_TIMESTAMP ON UPDATE
    ///     CURRENT_TIMESTAMP</c>), read back after each.
    /// </summary>
    public static PropertyBuilder UseMySqlComputedColumn(this PropertyBuilder propertyBuilder)
        => propertyBuilder.ValueGeneratedOnAddOrUpdate().HasAnnotation(ValueGenerationStrategyAnnotation, ComputedColumnStrategy);

    /// <summary>
    ///     A date and time the server sets on insert and on every update (<c>DEFAULT CURRENT_TIMESTAMP ON UPDATE
    ///     CURRENT_TIMESTAMP</c>), read back after each.
    /// </summary>
    public static PropertyBuilder<TProperty> UseMySqlComputedColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => (PropertyBuilder<TProperty>)UseMySqlComputedColumn((PropertyBuilder)propertyBuilder);

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
///     Where a model's or entity type's character set or collation is written, as Pomelo's: <see cref="Default" /> is all of
///     them. Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Metadata/DelegationModes.cs
/// </summary>
[Flags]
public enum DelegationModes
{
    /// <summary>All of them: <see cref="ApplyToAll" />.</summary>
    Default = 0,

    /// <summary>The database's (ALTER DATABASE).</summary>
    ApplyToDatabases = 1 << 0,

    /// <summary>Each table's default.</summary>
    ApplyToTables = 1 << 1,

    /// <summary>Each text column's, explicitly.</summary>
    ApplyToColumns = 1 << 2,

    /// <summary>The database, its tables and their text columns.</summary>
    ApplyToAll = ApplyToDatabases | ApplyToTables | ApplyToColumns
}

/// <summary>
///     Which character set and collation each level takes, as Pomelo resolves them: a level's own where its delegation reaches
///     it, otherwise the level above's where that one's does, unless the two name different character sets. No character set
///     delegation is all levels, as in Pomelo; EF's own model collation (no delegation) is the database's only. Behaviour
///     reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Metadata/Internal/MySqlAnnotationProvider.cs
/// </summary>
internal static class MySqlDelegation
{
    public static string? ModelCharSet(Microsoft.EntityFrameworkCore.Infrastructure.IReadOnlyAnnotatable model, DelegationModes level)
        => (model[MySqlModelOptionsExtensions.CharSetAnnotation] is string charSet)
           && Applies(model[MySqlModelOptionsExtensions.CharSetDelegationAnnotation] as DelegationModes?, level)
            ? charSet
            : null;

    public static string? ModelCollation(Microsoft.EntityFrameworkCore.Infrastructure.IReadOnlyAnnotatable model, DelegationModes level)
    {
        if (model[MySqlModelOptionsExtensions.CollationAnnotation] is not string collation)
        {
            return null;
        }

        var reaches = model[MySqlModelOptionsExtensions.CollationDelegationAnnotation] is DelegationModes modes
            ? Applies(modes, level)
            : level == DelegationModes.ApplyToDatabases;
        return reaches ? collation : null;
    }

    public static string? EntityCharSet(Microsoft.EntityFrameworkCore.Metadata.IReadOnlyTypeBase type, DelegationModes level)
    {
        if (Own(type, MySqlModelOptionsExtensions.CharSetAnnotation, MySqlModelOptionsExtensions.CharSetDelegationAnnotation, level, out var own))
        {
            return own;
        }

        var inherited = ModelCharSet(type.Model, level);
        var collation = Own(type, MySqlModelOptionsExtensions.CollationAnnotation, MySqlModelOptionsExtensions.CollationDelegationAnnotation, level, out var ownCollation)
            ? ownCollation
            : ModelCollation(type.Model, level);
        return (inherited != null) && (collation != null) && !Compatible(collation, inherited) ? null : inherited;
    }

    public static string? EntityCollation(Microsoft.EntityFrameworkCore.Metadata.IReadOnlyTypeBase type, DelegationModes level)
    {
        if (Own(type, MySqlModelOptionsExtensions.CollationAnnotation, MySqlModelOptionsExtensions.CollationDelegationAnnotation, level, out var own))
        {
            return own;
        }

        var inherited = ModelCollation(type.Model, level);
        return (inherited != null)
               && Own(type, MySqlModelOptionsExtensions.CharSetAnnotation, MySqlModelOptionsExtensions.CharSetDelegationAnnotation, level, out var charSet)
               && (charSet != null)
               && !Compatible(inherited, charSet)
            ? null
            : inherited;
    }

    public static bool Compatible(string collation, string charSet)
        => string.Equals(collation, charSet, StringComparison.OrdinalIgnoreCase)
           || collation.StartsWith(charSet + "_", StringComparison.OrdinalIgnoreCase);

    // A level's own setting (a value, or a delegation naming none) where its delegation reaches the level.
    private static bool Own(
        Microsoft.EntityFrameworkCore.Infrastructure.IReadOnlyAnnotatable annotatable,
        string valueAnnotation,
        string delegationAnnotation,
        DelegationModes level,
        out string? value)
    {
        value = annotatable[valueAnnotation] as string;
        var modes = annotatable[delegationAnnotation] as DelegationModes?;
        return ((value != null) || (modes != null)) && Applies(modes, level);
    }

    private static bool Applies(DelegationModes? modes, DelegationModes level)
        => ((modes ?? DelegationModes.Default) == DelegationModes.Default) || modes!.Value.HasFlag(level);
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
