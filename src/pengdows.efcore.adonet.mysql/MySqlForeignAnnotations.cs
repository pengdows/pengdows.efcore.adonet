using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace pengdows.efcore.adonet.mysql;

/// <summary>
///     The annotations an application's migrations and model snapshots carry from the MySQL provider it used before:
///     Pomelo's "MySql:" keys with its own enums as values, and Oracle's MySql.EntityFrameworkCore "MySQL:" keys. Each is
///     read as this provider's own, a value by its name, so the same migrations write the same tables. Oracle's key names are
///     from its package's public metadata (not its source). Behaviour reference (not copied):
///     https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Metadata/Internal/MySqlAnnotationNames.cs
/// </summary>
internal static class MySqlForeignAnnotations
{
    private const string OraclePrefix = "MySQL:";

    // Oracle's keys and this provider's: the same meaning, another name.
    private static readonly Dictionary<string, string> OracleKeys = new(StringComparer.Ordinal)
    {
        ["MySQL:Charset"] = MySqlModelOptionsExtensions.CharSetAnnotation,
        ["MySQL:CharSet"] = MySqlModelOptionsExtensions.CharSetAnnotation,
        ["MySQL:FullTextIndex"] = MySqlFullTextExtensions.FullTextAnnotation,
        ["MySQL:FullTextParser"] = MySqlFullTextExtensions.FullTextParserAnnotation,
        ["MySQL:IndexPrefixLength"] = MySqlModelOptionsExtensions.PrefixLengthAnnotation,
        ["MySQL:SpatialIndex"] = MySqlModelOptionsExtensions.SpatialAnnotation,
        ["MySQL:StoreOptions"] = MySqlModelOptionsExtensions.StoreOptionsAnnotation
    };

    /// <summary>Reads every operation's foreign annotations, and its columns' and old columns', as this provider's.</summary>
    public static void Normalize(IEnumerable<MigrationOperation> operations)
    {
        foreach (var operation in operations)
        {
            Normalize(operation);
            switch (operation)
            {
                case CreateTableOperation create:
                    foreach (var column in create.Columns)
                    {
                        Normalize(column);
                    }

                    break;
                case AlterColumnOperation alter:
                    Normalize(alter.OldColumn);
                    break;
                case AlterTableOperation alterTable:
                    Normalize(alterTable.OldTable);
                    break;
                case AlterDatabaseOperation alterDatabase:
                    Normalize(alterDatabase.OldDatabase);
                    break;
            }
        }
    }

    private static void Normalize(IMutableAnnotatable target)
    {
        foreach (var annotation in target.GetAnnotations().ToList())
        {
            var name = annotation.Name;
            if (name.EndsWith(":ValueGenerationStrategy", StringComparison.Ordinal)
                && (name.StartsWith(OraclePrefix, StringComparison.Ordinal) || (name == MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation)))
            {
                Strategy(target, annotation.Value?.ToString());
                continue;
            }

            if (name == OraclePrefix + "ValueGeneratedOnAdd")
            {
                Strategy(target, annotation.Value is true ? MySqlModelOptionsExtensions.IdentityColumnStrategy : null);
                continue;
            }

            if (name == OraclePrefix + "ValueGeneratedOnAddOrUpdate")
            {
                Strategy(target, annotation.Value is true ? MySqlModelOptionsExtensions.ComputedColumnStrategy : null);
                continue;
            }

            if ((name == OraclePrefix + "Collation") && (target is ColumnOperation { Collation: null } collated))
            {
                collated.Collation = annotation.Value as string;
                continue;
            }

            if ((name == OraclePrefix + "DefaultValueSql") && (target is ColumnOperation { DefaultValueSql: null } defaulted))
            {
                defaulted.DefaultValueSql = annotation.Value as string;
                continue;
            }

            if (OracleKeys.TryGetValue(name, out var own) && (target.FindAnnotation(own) == null))
            {
                target[own] = annotation.Value;
            }
        }
    }

    private static bool IsInteger(Type clrType)
        => (Nullable.GetUnderlyingType(clrType) ?? clrType) is var type
            && ((type == typeof(int)) || (type == typeof(long)) || (type == typeof(short)) || (type == typeof(byte))
                || (type == typeof(uint)) || (type == typeof(ulong)) || (type == typeof(ushort)) || (type == typeof(sbyte)));

    // A strategy by its name: an identity column is AUTO_INCREMENT (or, for a date and time, set on insert), a computed one
    // set on insert and update; none is neither.
    private static void Strategy(IMutableAnnotatable target, string? strategy)
    {
        if (strategy is MySqlModelOptionsExtensions.IdentityColumnStrategy or MySqlModelOptionsExtensions.ComputedColumnStrategy)
        {
            target[MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation] = strategy;
            if ((strategy == MySqlModelOptionsExtensions.IdentityColumnStrategy) && (target is ColumnOperation column)
                && IsInteger(column.ClrType)
                && (column.FindAnnotation(AdoNetAnnotationNames.Identity) == null))
            {
                column[AdoNetAnnotationNames.Identity] = true;
            }
        }
        else if (target.FindAnnotation(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation)?.Value is not string)
        {
            target.RemoveAnnotation(MySqlModelOptionsExtensions.ValueGenerationStrategyAnnotation);
        }
    }
}
