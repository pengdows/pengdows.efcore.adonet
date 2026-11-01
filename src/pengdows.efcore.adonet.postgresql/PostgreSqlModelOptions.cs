using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;
using Npgsql.NameTranslation;
using NpgsqlTypes;

namespace pengdows.efcore.adonet.postgresql;

/// <summary>
///     PostgreSQL's database objects, as Npgsql's model builder extensions declare them for migrations: extensions, enum types and
///     collations, and how integer keys are generated. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/BuilderExtensions/NpgsqlModelBuilderExtensions.cs
/// </summary>
public static class PostgreSqlModelBuilderExtensions
{
    /// <summary>Makes migrations create the extension (<c>CREATE EXTENSION IF NOT EXISTS</c>) before anything else.</summary>
    public static ModelBuilder HasPostgresExtension(this ModelBuilder modelBuilder, string name)
        => modelBuilder.HasPostgresExtension(null, name);

    /// <summary>Makes migrations create the extension in <paramref name="schema" />, at <paramref name="version" /> when given.</summary>
    public static ModelBuilder HasPostgresExtension(this ModelBuilder modelBuilder, string? schema, string name, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return modelBuilder.HasAnnotation(PostgreSqlAnnotations.Name(PostgreSqlAnnotations.ExtensionPrefix, schema, name), version ?? "");
    }

    /// <summary>Makes migrations create the enum type <paramref name="name" /> with <paramref name="labels" />, in order.</summary>
    public static ModelBuilder HasPostgresEnum(this ModelBuilder modelBuilder, string name, string[] labels)
        => modelBuilder.HasPostgresEnum(null, name, labels);

    /// <summary>Makes migrations create the enum type <paramref name="name" /> in <paramref name="schema" />.</summary>
    public static ModelBuilder HasPostgresEnum(this ModelBuilder modelBuilder, string? schema, string name, string[] labels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if ((labels.Length == 0) || labels.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("An enum type needs at least one label, and no empty one.", nameof(labels));
        }

        return modelBuilder.HasAnnotation(PostgreSqlAnnotations.Name(PostgreSqlAnnotations.EnumPrefix, schema, name), labels);
    }

    /// <summary>
    ///     Makes migrations create a PostgreSQL enum type for <typeparamref name="TEnum" />, and maps every property of that type
    ///     to it: a value is stored as its label. The type and labels are named by <paramref name="nameTranslator" /> (snake_case by
    ///     default, as Npgsql's), or by a member's <see cref="PgNameAttribute" />.
    /// </summary>
    public static ModelBuilder HasPostgresEnum<TEnum>(
        this ModelBuilder modelBuilder,
        string? schema = null,
        string? name = null,
        INpgsqlNameTranslator? nameTranslator = null)
        where TEnum : struct, Enum
    {
        nameTranslator ??= new NpgsqlSnakeCaseNameTranslator();
        var typeName = name ?? typeof(TEnum).GetCustomAttribute<PgNameAttribute>()?.PgName ?? nameTranslator.TranslateTypeName(typeof(TEnum).Name);
        var labels = Enum.GetNames<TEnum>()
            .Select(member => typeof(TEnum).GetField(member)!.GetCustomAttribute<PgNameAttribute>()?.PgName ?? nameTranslator.TranslateMemberName(member))
            .ToArray();
        modelBuilder.HasPostgresEnum(schema, typeName, labels);
        return modelBuilder.HasAnnotation(PostgreSqlAnnotations.EnumClrPrefix + typeof(TEnum).FullName, PostgreSqlAnnotations.Name("", schema, typeName));
    }

    /// <summary>
    ///     Makes migrations create a collation (<c>CREATE COLLATION</c>), such as an ICU one (<paramref name="provider" />
    ///     <c>icu</c>), and a non-deterministic one when <paramref name="deterministic" /> is false.
    /// </summary>
    public static ModelBuilder HasCollation(this ModelBuilder modelBuilder, string name, string locale, string? provider = null, bool? deterministic = null)
        => modelBuilder.HasCollation(null, name, locale, provider, deterministic);

    /// <summary>Makes migrations create a collation in <paramref name="schema" />.</summary>
    public static ModelBuilder HasCollation(
        this ModelBuilder modelBuilder,
        string? schema,
        string name,
        string locale,
        string? provider = null,
        bool? deterministic = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        return modelBuilder.HasAnnotation(
            PostgreSqlAnnotations.Name(PostgreSqlAnnotations.CollationPrefix, schema, name),
            new PostgreSqlCollation(locale, provider, deterministic).ToString());
    }

    /// <summary>Makes every generated integer key <c>GENERATED ALWAYS AS IDENTITY</c>: the database refuses an explicit value.</summary>
    public static ModelBuilder UseIdentityAlwaysColumns(this ModelBuilder modelBuilder)
        => modelBuilder.HasAnnotation(PostgreSqlAnnotations.IdentityStrategy, PostgreSqlAnnotations.Always);

    /// <summary>Makes every generated integer key <c>GENERATED BY DEFAULT AS IDENTITY</c>, as it is by default.</summary>
    public static ModelBuilder UseIdentityByDefaultColumns(this ModelBuilder modelBuilder)
        => modelBuilder.HasAnnotation(PostgreSqlAnnotations.IdentityStrategy, PostgreSqlAnnotations.ByDefault);
}

/// <summary>
///     How PostgreSQL generates a column's values, as Npgsql's property builder extensions configure it. Behaviour reference
///     (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/BuilderExtensions/NpgsqlPropertyBuilderExtensions.cs
/// </summary>
public static class PostgreSqlPropertyBuilderExtensions
{
    /// <summary>Makes the column <c>GENERATED ALWAYS AS IDENTITY</c>: the database numbers it and refuses an explicit value.</summary>
    public static PropertyBuilder UseIdentityAlwaysColumn(this PropertyBuilder propertyBuilder)
        => propertyBuilder.UseIdentityColumn().HasAnnotation(PostgreSqlAnnotations.IdentityStrategy, PostgreSqlAnnotations.Always);

    /// <inheritdoc cref="UseIdentityAlwaysColumn(PropertyBuilder)" />
    public static PropertyBuilder<TProperty> UseIdentityAlwaysColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => (PropertyBuilder<TProperty>)UseIdentityAlwaysColumn((PropertyBuilder)propertyBuilder);

    /// <summary>Makes the column <c>GENERATED BY DEFAULT AS IDENTITY</c>: numbered unless a value is given.</summary>
    public static PropertyBuilder UseIdentityByDefaultColumn(this PropertyBuilder propertyBuilder)
        => propertyBuilder.UseIdentityColumn().HasAnnotation(PostgreSqlAnnotations.IdentityStrategy, PostgreSqlAnnotations.ByDefault);

    /// <inheritdoc cref="UseIdentityByDefaultColumn(PropertyBuilder)" />
    public static PropertyBuilder<TProperty> UseIdentityByDefaultColumn<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => (PropertyBuilder<TProperty>)UseIdentityByDefaultColumn((PropertyBuilder)propertyBuilder);

    /// <summary>The options of an identity column's sequence; a null option is PostgreSQL's default.</summary>
    public static PropertyBuilder HasIdentityOptions(
        this PropertyBuilder propertyBuilder,
        long? startValue = null,
        long? incrementBy = null,
        long? minValue = null,
        long? maxValue = null,
        bool? cyclic = null,
        long? numbersToCache = null)
    {
        if (incrementBy == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incrementBy), "An identity column's sequence can't increment by zero.");
        }

        var options = new PostgreSqlIdentityOptions(startValue, incrementBy, minValue, maxValue, cyclic, numbersToCache);
        return propertyBuilder.HasAnnotation(PostgreSqlAnnotations.IdentityOptions, options.ToString());
    }

    /// <inheritdoc cref="HasIdentityOptions(PropertyBuilder, long?, long?, long?, long?, bool?, long?)" />
    public static PropertyBuilder<TProperty> HasIdentityOptions<TProperty>(
        this PropertyBuilder<TProperty> propertyBuilder,
        long? startValue = null,
        long? incrementBy = null,
        long? minValue = null,
        long? maxValue = null,
        bool? cyclic = null,
        long? numbersToCache = null)
        => (PropertyBuilder<TProperty>)HasIdentityOptions((PropertyBuilder)propertyBuilder, startValue, incrementBy, minValue, maxValue, cyclic, numbersToCache);

    /// <summary>
    ///     Numbers the column from a sequence the model declares (<paramref name="name" />, by default the table's name with
    ///     <c>Sequence</c>): its default is the sequence's <c>nextval</c>.
    /// </summary>
    public static PropertyBuilder UseSequence(this PropertyBuilder propertyBuilder, string? name = null, string? schema = null)
    {
        var property = (IMutableProperty)propertyBuilder.Metadata;
        var entityType = (IMutableEntityType)property.DeclaringType.ContainingEntityType;
        var sequenceName = name ?? (entityType.GetTableName() ?? entityType.ShortName()) + "Sequence";
        var sequenceSchema = schema ?? entityType.Model.GetDefaultSchema();
        if (entityType.Model.FindSequence(sequenceName, sequenceSchema) == null)
        {
            entityType.Model.AddSequence(sequenceName, sequenceSchema);
        }

        // nextval takes the sequence's name as text, which it parses as an identifier.
        var identifier = PostgreSqlAnnotations.Delimit(sequenceName, sequenceSchema);
        return propertyBuilder.HasDefaultValueSql("nextval(" + PostgreSqlDialect.Instance.Sql.StringLiteral(identifier) + ")");
    }

    /// <inheritdoc cref="UseSequence(PropertyBuilder, string?, string?)" />
    public static PropertyBuilder<TProperty> UseSequence<TProperty>(this PropertyBuilder<TProperty> propertyBuilder, string? name = null, string? schema = null)
        => (PropertyBuilder<TProperty>)UseSequence((PropertyBuilder)propertyBuilder, name, schema);
}

/// <summary>
///     PostgreSQL's index options, as Npgsql's index builder extensions have them: the access method (GIN, GiST, BRIN, ...),
///     operator classes, covering columns, whether nulls are distinct, and concurrent creation. Behaviour reference (not copied):
///     https://github.com/npgsql/efcore.pg/blob/v10.0.3/src/EFCore.PG/Extensions/BuilderExtensions/NpgsqlIndexBuilderExtensions.cs
/// </summary>
public static class PostgreSqlIndexBuilderExtensions
{
    /// <summary>The index's access method: <c>btree</c> (the default), <c>hash</c>, <c>gist</c>, <c>spgist</c>, <c>gin</c>, <c>brin</c>.</summary>
    public static IndexBuilder HasMethod(this IndexBuilder indexBuilder, string? method)
        => indexBuilder.HasAnnotation(PostgreSqlAnnotations.IndexMethod, method);

    /// <inheritdoc cref="HasMethod(IndexBuilder, string?)" />
    public static IndexBuilder<TEntity> HasMethod<TEntity>(this IndexBuilder<TEntity> indexBuilder, string? method)
        => (IndexBuilder<TEntity>)HasMethod((IndexBuilder)indexBuilder, method);

    /// <summary>Each column's operator class, in order (<c>gin_trgm_ops</c>, <c>jsonb_path_ops</c>, ...).</summary>
    public static IndexBuilder HasOperators(this IndexBuilder indexBuilder, params string[]? operators)
        => indexBuilder.HasAnnotation(PostgreSqlAnnotations.IndexOperators, operators);

    /// <inheritdoc cref="HasOperators(IndexBuilder, string[])" />
    public static IndexBuilder<TEntity> HasOperators<TEntity>(this IndexBuilder<TEntity> indexBuilder, params string[]? operators)
        => (IndexBuilder<TEntity>)HasOperators((IndexBuilder)indexBuilder, operators);

    /// <summary>Columns the index carries without indexing them (<c>INCLUDE</c>), so a query reading them needs no table access.</summary>
    public static IndexBuilder IncludeProperties(this IndexBuilder indexBuilder, params string[] propertyNames)
        => indexBuilder.HasAnnotation(PostgreSqlAnnotations.IndexInclude, propertyNames);

    /// <inheritdoc cref="IncludeProperties(IndexBuilder, string[])" />
    public static IndexBuilder<TEntity> IncludeProperties<TEntity>(this IndexBuilder<TEntity> indexBuilder, params string[] propertyNames)
        => (IndexBuilder<TEntity>)IncludeProperties((IndexBuilder)indexBuilder, propertyNames);

    /// <summary>The properties <paramref name="includeExpression" /> names (<c>e =&gt; new { e.A, e.B }</c>), carried by the index.</summary>
    public static IndexBuilder<TEntity> IncludeProperties<TEntity>(this IndexBuilder<TEntity> indexBuilder, Expression<Func<TEntity, object?>> includeExpression)
    {
        var body = includeExpression.Body is UnaryExpression { NodeType: ExpressionType.Convert } convert ? convert.Operand : includeExpression.Body;
        string[] names = body switch
        {
            NewExpression created => created.Arguments.Select(a => ((MemberExpression)a).Member.Name).ToArray(),
            MemberExpression member => [member.Member.Name],
            _ => throw new ArgumentException("The included properties must be a property or an anonymous type of properties.", nameof(includeExpression))
        };
        return IncludeProperties(indexBuilder, names);
    }

    /// <summary>Whether a unique index takes rows whose columns are null as distinct (the default) or as equal (<c>NULLS NOT DISTINCT</c>, PostgreSQL 15).</summary>
    public static IndexBuilder AreNullsDistinct(this IndexBuilder indexBuilder, bool nullsDistinct = true)
        => indexBuilder.HasAnnotation(PostgreSqlAnnotations.NullsDistinct, nullsDistinct);

    /// <inheritdoc cref="AreNullsDistinct(IndexBuilder, bool)" />
    public static IndexBuilder<TEntity> AreNullsDistinct<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool nullsDistinct = true)
        => (IndexBuilder<TEntity>)AreNullsDistinct((IndexBuilder)indexBuilder, nullsDistinct);

    /// <summary>Creates the index <c>CONCURRENTLY</c>, without locking out writes, and so outside the migration's transaction.</summary>
    public static IndexBuilder IsCreatedConcurrently(this IndexBuilder indexBuilder, bool createdConcurrently = true)
        => indexBuilder.HasAnnotation(PostgreSqlAnnotations.CreatedConcurrently, createdConcurrently);

    /// <inheritdoc cref="IsCreatedConcurrently(IndexBuilder, bool)" />
    public static IndexBuilder<TEntity> IsCreatedConcurrently<TEntity>(this IndexBuilder<TEntity> indexBuilder, bool createdConcurrently = true)
        => (IndexBuilder<TEntity>)IsCreatedConcurrently((IndexBuilder)indexBuilder, createdConcurrently);
}

internal static class PostgreSqlAnnotations
{
    public const string Prefix = "PostgreSql:";
    public const string ExtensionPrefix = Prefix + "Extension:";
    public const string EnumPrefix = Prefix + "Enum:";
    public const string EnumClrPrefix = Prefix + "EnumClrType:";
    public const string CollationPrefix = Prefix + "Collation:";
    public const string IdentityStrategy = Prefix + "IdentityStrategy";
    public const string IdentityOptions = Prefix + "IdentityOptions";
    public const string IndexMethod = Prefix + "IndexMethod";
    public const string IndexOperators = Prefix + "IndexOperators";
    public const string IndexInclude = Prefix + "IndexInclude";
    public const string NullsDistinct = Prefix + "NullsDistinct";
    public const string CreatedConcurrently = Prefix + "CreatedConcurrently";
    public const string Always = "always";
    public const string ByDefault = "by default";

    /// <summary><c>prefix</c> then <c>schema.name</c>, or <c>.name</c> without a schema.</summary>
    public static string Name(string prefix, string? schema, string name)
        => prefix + schema + "." + name;

    /// <summary>A name as PostgreSQL's SQL writes it: quoted where it must be, and after its schema.</summary>
    public static string Delimit(string name, string? schema)
        => (schema == null ? "" : Delimit(schema) + ".") + Delimit(name);

    private static string Delimit(string name)
        => PostgreSqlDialect.Instance.Sql.RequiresQuoting(name) ? "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : name;

    /// <summary>The schema and name of an annotation <see cref="Name" /> wrote.</summary>
    public static (string? Schema, string Name) Parse(string prefix, string annotation)
    {
        var rest = annotation[prefix.Length..];
        var dot = rest.IndexOf('.');
        return (dot == 0 ? null : rest[..dot], rest[(dot + 1)..]);
    }
}

/// <summary>A collation's definition, carried in its annotation as text.</summary>
internal sealed record PostgreSqlCollation(string Locale, string? Provider, bool? Deterministic)
{
    public override string ToString()
        => string.Join('\u001f', Locale, Provider ?? "", Deterministic?.ToString() ?? "");

    public static PostgreSqlCollation Parse(string text)
    {
        var parts = text.Split('\u001f');
        return new PostgreSqlCollation(parts[0], parts[1].Length == 0 ? null : parts[1], parts[2].Length == 0 ? null : bool.Parse(parts[2]));
    }
}

/// <summary>An identity column's sequence options, carried in its annotation as text.</summary>
internal sealed record PostgreSqlIdentityOptions(long? StartValue, long? IncrementBy, long? MinValue, long? MaxValue, bool? Cyclic, long? NumbersToCache)
{
    public override string ToString()
        => string.Join(';', Text(StartValue), Text(IncrementBy), Text(MinValue), Text(MaxValue), Cyclic?.ToString() ?? "", Text(NumbersToCache));

    public static PostgreSqlIdentityOptions Parse(string text)
    {
        var parts = text.Split(';');
        return new PostgreSqlIdentityOptions(
            Number(parts[0]), Number(parts[1]), Number(parts[2]), Number(parts[3]), parts[4].Length == 0 ? null : bool.Parse(parts[4]), Number(parts[5]));
    }

    /// <summary>The options as PostgreSQL's sequence options, empty when every one is the default.</summary>
    public string ToSql()
    {
        var options = new List<string>();
        if (StartValue is { } start)
        {
            options.Add("START WITH " + Text(start));
        }

        if (IncrementBy is { } increment)
        {
            options.Add("INCREMENT BY " + Text(increment));
        }

        if (MinValue is { } min)
        {
            options.Add("MINVALUE " + Text(min));
        }

        if (MaxValue is { } max)
        {
            options.Add("MAXVALUE " + Text(max));
        }

        if (Cyclic is { } cyclic)
        {
            options.Add(cyclic ? "CYCLE" : "NO CYCLE");
        }

        if (NumbersToCache is { } cache)
        {
            options.Add("CACHE " + Text(cache));
        }

        return string.Join(' ', options);
    }

    private static string Text(long? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static long? Number(string text)
        => text.Length == 0 ? null : long.Parse(text, CultureInfo.InvariantCulture);
}
