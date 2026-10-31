using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>The compression of an index's data: <c>DATA_COMPRESSION = NONE | ROW | PAGE</c>.</summary>
public enum DataCompressionType
{
    /// <summary>Not compressed.</summary>
    None,

    /// <summary>Row compression.</summary>
    Row,

    /// <summary>Page compression.</summary>
    Page
}

/// <summary>How SQL Server numbers a key: an IDENTITY column, a hi-lo block drawn from a sequence, or a sequence's next value.</summary>
public enum SqlServerValueGenerationStrategy
{
    /// <summary>Not numbered by the database.</summary>
    None,

    /// <summary>Blocks of values drawn from a sequence, numbered on the client.</summary>
    SequenceHiLo,

    /// <summary>An IDENTITY column.</summary>
    IdentityColumn,

    /// <summary>A sequence's next value for each row.</summary>
    Sequence
}

/// <summary>
///     Microsoft's annotation names, so that a model or a migration written for its provider means the same here. Behaviour
///     reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Metadata/Internal/SqlServerAnnotationNames.cs
/// </summary>
internal static class SqlServerAnnotationNames
{
    public const string Prefix = "SqlServer:";
    public const string Clustered = Prefix + "Clustered";
    public const string CreatedOnline = Prefix + "Online";
    public const string FillFactor = Prefix + "FillFactor";
    public const string SortInTempDb = Prefix + "SortInTempDb";
    public const string DataCompression = Prefix + "DataCompression";
    public const string Include = Prefix + "Include";
    public const string Sparse = Prefix + "Sparse";
    public const string MemoryOptimized = Prefix + "MemoryOptimized";
    public const string Identity = Prefix + "Identity";
    public const string IdentitySeed = Prefix + "IdentitySeed";
    public const string IdentityIncrement = Prefix + "IdentityIncrement";
    public const string ValueGenerationStrategy = Prefix + "ValueGenerationStrategy";
    public const string HiLoSequenceName = Prefix + "HiLoSequenceName";
    public const string HiLoSequenceSchema = Prefix + "HiLoSequenceSchema";
    public const string SequenceName = Prefix + "SequenceName";
    public const string SequenceSchema = Prefix + "SequenceSchema";
    public const string SequenceNameSuffix = Prefix + "SequenceNameSuffix";
    public const string IsTemporal = Prefix + "IsTemporal";
    public const string TemporalHistoryTableName = Prefix + "TemporalHistoryTableName";
    public const string TemporalHistoryTableSchema = Prefix + "TemporalHistoryTableSchema";
    public const string TemporalPeriodStartPropertyName = Prefix + "TemporalPeriodStartPropertyName";
    public const string TemporalPeriodStartColumnName = Prefix + "TemporalPeriodStartColumnName";
    public const string TemporalPeriodEndPropertyName = Prefix + "TemporalPeriodEndPropertyName";
    public const string TemporalPeriodEndColumnName = Prefix + "TemporalPeriodEndColumnName";
    public const string TemporalIsPeriodStartColumn = Prefix + "TemporalIsPeriodStartColumn";
    public const string TemporalIsPeriodEndColumn = Prefix + "TemporalIsPeriodEndColumn";

    /// <summary>The sequence <see cref="SqlServerModelBuilderExtensions.UseHiLo" /> draws from when none is named.</summary>
    public const string DefaultHiLoSequenceName = "EntityFrameworkHiLoSequence";

    /// <summary>A hi-lo sequence's increment: the block of values one draw numbers.</summary>
    public const int DefaultHiLoBlockSize = 10;

    /// <summary>Creates the hi-lo sequence <paramref name="name" /> in <paramref name="model" /> unless it has one.</summary>
    public static void EnsureHiLoSequence(IMutableModel model, string name, string? schema)
    {
        if (model.FindSequence(name, schema) != null)
        {
            return;
        }

        model.AddSequence(name, schema).IncrementBy = DefaultHiLoBlockSize;
    }
}

/// <summary>
///     SQL Server's index options, as Microsoft's provider has them. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerIndexBuilderExtensions.cs
/// </summary>
public static class SqlServerIndexBuilderExtensions
{
    /// <summary>Makes the index CLUSTERED, or NONCLUSTERED when <paramref name="clustered" /> is false.</summary>
    public static IndexBuilder IsClustered(this IndexBuilder indexBuilder, bool clustered = true)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.Clustered, clustered);

    /// <summary>Makes the index CLUSTERED, or NONCLUSTERED when <paramref name="clustered" /> is false.</summary>
    public static IndexBuilder<TEntity> IsClustered<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool clustered = true)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.Clustered, clustered);

    /// <summary>Stores the columns of <paramref name="propertyNames" /> in the index's leaf level: INCLUDE (...).</summary>
    public static IndexBuilder IncludeProperties(this IndexBuilder indexBuilder, params string[] propertyNames)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.Include, RequireNames(propertyNames));

    /// <summary>Stores the columns of <paramref name="propertyNames" /> in the index's leaf level: INCLUDE (...).</summary>
    public static IndexBuilder<TEntity> IncludeProperties<TEntity>(this IndexBuilder<TEntity> indexBuilder, params string[] propertyNames)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.Include, RequireNames(propertyNames));

    /// <summary>
    ///     Stores the columns of the properties <paramref name="includeExpression" /> reads (<c>e =&gt; e.A</c> or
    ///     <c>e =&gt; new { e.A, e.B }</c>) in the index's leaf level: INCLUDE (...).
    /// </summary>
    public static IndexBuilder<TEntity> IncludeProperties<TEntity>(this IndexBuilder<TEntity> indexBuilder, Expression<Func<TEntity, object?>> includeExpression)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.Include, PropertyNames(includeExpression));

    /// <summary>Builds the index ONLINE, leaving the table usable meanwhile.</summary>
    public static IndexBuilder IsCreatedOnline(this IndexBuilder indexBuilder, bool createdOnline = true)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.CreatedOnline, createdOnline);

    /// <summary>Builds the index ONLINE, leaving the table usable meanwhile.</summary>
    public static IndexBuilder<TEntity> IsCreatedOnline<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool createdOnline = true)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.CreatedOnline, createdOnline);

    /// <summary>How full, from 1 to 100 percent, each leaf page is built: FILLFACTOR.</summary>
    public static IndexBuilder HasFillFactor(this IndexBuilder indexBuilder, int fillFactor)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.FillFactor, RequireFillFactor(fillFactor));

    /// <summary>How full, from 1 to 100 percent, each leaf page is built: FILLFACTOR.</summary>
    public static IndexBuilder<TEntity> HasFillFactor<TEntity>(this IndexBuilder<TEntity> indexBuilder, int fillFactor)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.FillFactor, RequireFillFactor(fillFactor));

    /// <summary>Sorts the index's build in tempdb: SORT_IN_TEMPDB.</summary>
    public static IndexBuilder SortInTempDb(this IndexBuilder indexBuilder, bool sortInTempDb = true)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.SortInTempDb, sortInTempDb);

    /// <summary>Sorts the index's build in tempdb: SORT_IN_TEMPDB.</summary>
    public static IndexBuilder<TEntity> SortInTempDb<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool sortInTempDb = true)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.SortInTempDb, sortInTempDb);

    /// <summary>Compresses the index's data: DATA_COMPRESSION.</summary>
    public static IndexBuilder UseDataCompression(this IndexBuilder indexBuilder, DataCompressionType dataCompressionType)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.DataCompression, dataCompressionType);

    /// <summary>Compresses the index's data: DATA_COMPRESSION.</summary>
    public static IndexBuilder<TEntity> UseDataCompression<TEntity>(this IndexBuilder<TEntity> indexBuilder, DataCompressionType dataCompressionType)
        => indexBuilder.HasAnnotation(SqlServerAnnotationNames.DataCompression, dataCompressionType);

    internal static int RequireFillFactor(int fillFactor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(fillFactor, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fillFactor, 100);
        return fillFactor;
    }

    private static string[] RequireNames(string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);
        foreach (var name in propertyNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(propertyNames));
        }

        return propertyNames;
    }

    private static string[] PropertyNames<TEntity>(Expression<Func<TEntity, object?>> includeExpression)
    {
        var body = includeExpression.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : includeExpression.Body;
        var members = body switch
        {
            NewExpression created => created.Arguments,
            MemberExpression => [body],
            _ => throw new ArgumentException($"'{includeExpression}' must read properties: e => e.A or e => new {{ e.A, e.B }}.", nameof(includeExpression))
        };
        return members
            .Select(m => m is MemberExpression { Member: PropertyInfo or FieldInfo } member && member.Expression == includeExpression.Parameters[0]
                ? member.Member.Name
                : throw new ArgumentException($"'{m}' in '{includeExpression}' is not a property of the entity.", nameof(includeExpression)))
            .ToArray();
    }
}

/// <summary>
///     SQL Server's primary key and alternate key options, as Microsoft's provider has them. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerKeyBuilderExtensions.cs
/// </summary>
public static class SqlServerKeyBuilderExtensions
{
    /// <summary>Makes the key's index CLUSTERED, or NONCLUSTERED when <paramref name="clustered" /> is false.</summary>
    public static KeyBuilder IsClustered(this KeyBuilder keyBuilder, bool clustered = true)
        => keyBuilder.HasAnnotation(SqlServerAnnotationNames.Clustered, clustered);

    /// <summary>Makes the key's index CLUSTERED, or NONCLUSTERED when <paramref name="clustered" /> is false.</summary>
    public static KeyBuilder<TEntity> IsClustered<TEntity>(this KeyBuilder<TEntity> keyBuilder, bool clustered = true)
        => (KeyBuilder<TEntity>)keyBuilder.HasAnnotation(SqlServerAnnotationNames.Clustered, clustered);

    /// <summary>How full, from 1 to 100 percent, each leaf page of the key's index is built: FILLFACTOR.</summary>
    public static KeyBuilder HasFillFactor(this KeyBuilder keyBuilder, int fillFactor)
        => keyBuilder.HasAnnotation(SqlServerAnnotationNames.FillFactor, SqlServerIndexBuilderExtensions.RequireFillFactor(fillFactor));

    /// <summary>How full, from 1 to 100 percent, each leaf page of the key's index is built: FILLFACTOR.</summary>
    public static KeyBuilder<TEntity> HasFillFactor<TEntity>(this KeyBuilder<TEntity> keyBuilder, int fillFactor)
        => (KeyBuilder<TEntity>)keyBuilder.HasAnnotation(SqlServerAnnotationNames.FillFactor, SqlServerIndexBuilderExtensions.RequireFillFactor(fillFactor));
}

/// <summary>
///     SQL Server's column options and key numbering, as Microsoft's provider has them. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerPropertyBuilderExtensions.cs
/// </summary>
public static class SqlServerPropertyBuilderExtensions
{
    /// <summary>An IDENTITY column starting at <paramref name="seed" />, stepping by <paramref name="increment" />.</summary>
    public static PropertyBuilder UseIdentityColumn(this PropertyBuilder propertyBuilder, long seed = 1, int increment = 1)
    {
        propertyBuilder.ValueGeneratedOnAdd();
        propertyBuilder.HasAnnotation(AdoNetAnnotationNames.Identity, true);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy, SqlServerValueGenerationStrategy.IdentityColumn);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.IdentitySeed, seed);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.IdentityIncrement, increment);
        return propertyBuilder;
    }

    /// <summary>An IDENTITY column starting at <paramref name="seed" />, stepping by <paramref name="increment" />.</summary>
    public static PropertyBuilder UseIdentityColumn(this PropertyBuilder propertyBuilder, int seed, int increment = 1)
        => propertyBuilder.UseIdentityColumn((long)seed, increment);

    /// <summary>An IDENTITY column starting at <paramref name="seed" />, stepping by <paramref name="increment" />.</summary>
    public static PropertyBuilder<TProperty> UseIdentityColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, long seed = 1, int increment = 1)
        => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder).UseIdentityColumn(seed, increment);

    /// <summary>An IDENTITY column starting at <paramref name="seed" />, stepping by <paramref name="increment" />.</summary>
    public static PropertyBuilder<TProperty> UseIdentityColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, int seed, int increment = 1)
        => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder).UseIdentityColumn((long)seed, increment);

    /// <summary>
    ///     Numbers the key on the client from blocks drawn from the sequence <paramref name="name" /> (created with an increment
    ///     of 10 unless the model has it; <c>EntityFrameworkHiLoSequence</c> unless named): one round trip per block of rows.
    /// </summary>
    public static PropertyBuilder UseHiLo(this PropertyBuilder propertyBuilder, string? name = null, string? schema = null)
    {
        name ??= SqlServerAnnotationNames.DefaultHiLoSequenceName;
        SqlServerAnnotationNames.EnsureHiLoSequence(propertyBuilder.Metadata.DeclaringType.Model, name, schema);
        propertyBuilder.ValueGeneratedOnAdd();
        propertyBuilder.HasAnnotation(AdoNetAnnotationNames.Identity, false);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy, SqlServerValueGenerationStrategy.SequenceHiLo);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.HiLoSequenceName, name);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.HiLoSequenceSchema, schema);
        return propertyBuilder;
    }

    /// <inheritdoc cref="UseHiLo(PropertyBuilder, string?, string?)" />
    public static PropertyBuilder<TProperty> UseHiLo<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, string? name = null, string? schema = null)
        => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder).UseHiLo(name, schema);

    /// <summary>
    ///     Takes each row's value from the sequence <paramref name="name" /> (the table's name and <c>Sequence</c> unless
    ///     named), created with the model: <c>DEFAULT (NEXT VALUE FOR ...)</c>. A key's value is drawn before the INSERT.
    /// </summary>
    public static PropertyBuilder UseSequence(this PropertyBuilder propertyBuilder, string? name = null, string? schema = null)
    {
        propertyBuilder.ValueGeneratedOnAdd();
        propertyBuilder.HasAnnotation(AdoNetAnnotationNames.Identity, false);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy, SqlServerValueGenerationStrategy.Sequence);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.SequenceName, name);
        propertyBuilder.HasAnnotation(SqlServerAnnotationNames.SequenceSchema, schema);
        return propertyBuilder;
    }

    /// <inheritdoc cref="UseSequence(PropertyBuilder, string?, string?)" />
    public static PropertyBuilder<TProperty> UseSequence<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, string? name = null, string? schema = null)
        => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder).UseSequence(name, schema);

    /// <summary>Makes the column SPARSE: a null takes no space, a value more.</summary>
    public static PropertyBuilder IsSparse(this PropertyBuilder propertyBuilder, bool sparse = true)
        => propertyBuilder.HasAnnotation(SqlServerAnnotationNames.Sparse, sparse);

    /// <summary>Makes the column SPARSE: a null takes no space, a value more.</summary>
    public static PropertyBuilder<TProperty> IsSparse<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, bool sparse = true)
        => propertyBuilder.HasAnnotation(SqlServerAnnotationNames.Sparse, sparse);
}

/// <summary>
///     SQL Server's model-wide key numbering, as Microsoft's provider has it. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerModelBuilderExtensions.cs
/// </summary>
public static class SqlServerModelBuilderExtensions
{
    /// <summary>Keys the database numbers are IDENTITY columns starting at <paramref name="seed" />, stepping by <paramref name="increment" />.</summary>
    public static ModelBuilder UseIdentityColumns(this ModelBuilder modelBuilder, long seed = 1, int increment = 1)
        => modelBuilder
            .HasAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy, SqlServerValueGenerationStrategy.IdentityColumn)
            .HasAnnotation(SqlServerAnnotationNames.IdentitySeed, seed)
            .HasAnnotation(SqlServerAnnotationNames.IdentityIncrement, increment);

    /// <summary>Keys the database numbers are IDENTITY columns starting at <paramref name="seed" />, stepping by <paramref name="increment" />.</summary>
    public static ModelBuilder UseIdentityColumns(this ModelBuilder modelBuilder, int seed, int increment = 1)
        => modelBuilder.UseIdentityColumns((long)seed, increment);

    /// <summary>Keys the database would number are numbered on the client from blocks drawn from one hi-lo sequence.</summary>
    public static ModelBuilder UseHiLo(this ModelBuilder modelBuilder, string? name = null, string? schema = null)
    {
        name ??= SqlServerAnnotationNames.DefaultHiLoSequenceName;
        SqlServerAnnotationNames.EnsureHiLoSequence(modelBuilder.Model, name, schema);
        return modelBuilder
            .HasAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy, SqlServerValueGenerationStrategy.SequenceHiLo)
            .HasAnnotation(SqlServerAnnotationNames.HiLoSequenceName, name)
            .HasAnnotation(SqlServerAnnotationNames.HiLoSequenceSchema, schema);
    }

    /// <summary>
    ///     Keys the database would number take their values from a sequence per table, named the table's name and
    ///     <paramref name="nameSuffix" /> (<c>Sequence</c> unless given).
    /// </summary>
    public static ModelBuilder UseKeySequences(this ModelBuilder modelBuilder, string? nameSuffix = null, string? schema = null)
        => modelBuilder
            .HasAnnotation(SqlServerAnnotationNames.ValueGenerationStrategy, SqlServerValueGenerationStrategy.Sequence)
            .HasAnnotation(SqlServerAnnotationNames.SequenceNameSuffix, nameSuffix ?? "Sequence")
            .HasAnnotation(SqlServerAnnotationNames.SequenceSchema, schema);
}

/// <summary>
///     SQL Server's table options, as Microsoft's provider has them. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Extensions/SqlServerTableBuilderExtensions.cs
/// </summary>
public static class SqlServerEntityTypeBuilderExtensions
{
    /// <summary>Makes the table MEMORY_OPTIMIZED; its keys and indexes are nonclustered unless configured otherwise.</summary>
    public static EntityTypeBuilder IsMemoryOptimized(this EntityTypeBuilder entityTypeBuilder, bool memoryOptimized = true)
        => entityTypeBuilder.HasAnnotation(SqlServerAnnotationNames.MemoryOptimized, memoryOptimized);

    /// <summary>Makes the table MEMORY_OPTIMIZED; its keys and indexes are nonclustered unless configured otherwise.</summary>
    public static EntityTypeBuilder<TEntity> IsMemoryOptimized<TEntity>(this EntityTypeBuilder<TEntity> entityTypeBuilder, bool memoryOptimized = true)
        where TEntity : class
        => entityTypeBuilder.HasAnnotation(SqlServerAnnotationNames.MemoryOptimized, memoryOptimized);

    /// <summary>Makes the table MEMORY_OPTIMIZED; its keys and indexes are nonclustered unless configured otherwise.</summary>
    public static TableBuilder IsMemoryOptimized(this TableBuilder tableBuilder, bool memoryOptimized = true)
    {
        tableBuilder.Metadata.SetAnnotation(SqlServerAnnotationNames.MemoryOptimized, memoryOptimized);
        return tableBuilder;
    }

    /// <summary>Makes the table MEMORY_OPTIMIZED; its keys and indexes are nonclustered unless configured otherwise.</summary>
    public static TableBuilder<TEntity> IsMemoryOptimized<TEntity>(this TableBuilder<TEntity> tableBuilder, bool memoryOptimized = true)
        where TEntity : class
    {
        tableBuilder.Metadata.SetAnnotation(SqlServerAnnotationNames.MemoryOptimized, memoryOptimized);
        return tableBuilder;
    }
}
