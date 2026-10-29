using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore;

/// <summary>Microsoft's SQLite property extensions: the value-generation strategy, stored as one annotation.</summary>
public static class SqlitePropertyExtensions
{
    /// <summary>The annotation holding the SQLite value-generation strategy.</summary>
    public const string ValueGenerationStrategyAnnotation = "Sqlite:ValueGenerationStrategy";

    public static SqliteValueGenerationStrategy GetValueGenerationStrategy(this IReadOnlyProperty property)
        => property.FindAnnotation(ValueGenerationStrategyAnnotation)?.Value is SqliteValueGenerationStrategy strategy
            ? strategy
            : SqliteValueGenerationStrategy.None;

    public static void SetValueGenerationStrategy(this IMutableProperty property, SqliteValueGenerationStrategy? value)
    {
        if (value is null)
        {
            property.RemoveAnnotation(ValueGenerationStrategyAnnotation);
            return;
        }

        property.SetAnnotation(ValueGenerationStrategyAnnotation, value.Value);
        if (value == SqliteValueGenerationStrategy.Autoincrement)
        {
            property.ValueGenerated = ValueGenerated.OnAdd;
        }
    }
}

/// <summary>Microsoft's <c>UseAutoincrement</c> on property builders.</summary>
public static class SqlitePropertyBuilderExtensions
{
    public static PropertyBuilder UseAutoincrement(this PropertyBuilder propertyBuilder)
    {
        propertyBuilder.Metadata.SetValueGenerationStrategy(SqliteValueGenerationStrategy.Autoincrement);
        return propertyBuilder;
    }

    public static PropertyBuilder<TProperty> UseAutoincrement<TProperty>(this PropertyBuilder<TProperty> propertyBuilder)
        => (PropertyBuilder<TProperty>)((PropertyBuilder)propertyBuilder).UseAutoincrement();
}
