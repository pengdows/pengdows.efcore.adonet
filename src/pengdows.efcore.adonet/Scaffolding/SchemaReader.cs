using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;

namespace pengdows.efcore.adonet.Scaffolding;

/// <summary>
///     Reads a database's schema into EF's <see cref="DatabaseModel" /> (reverse engineering, and the migrations suites' check
///     of what a migration made), from whatever catalog the database has. <see cref="InformationSchemaReader" /> reads ISO's.
/// </summary>
public abstract class SchemaReader
{
    /// <summary>Whether tables are read with their schemas; false where the database has none.</summary>
    public virtual bool ScaffoldsSchemas
        => true;

    /// <summary>Whether constraints read back with the names they were created with; false where the database renames them.</summary>
    public virtual bool KeepsConstraintNames
        => true;

    /// <summary>Whether an index's filter (a partial index's predicate) is read; false where the database has no filtered indexes.</summary>
    public virtual bool ReadsIndexFilters
        => true;

    /// <summary>The schema, on a connection the caller opened and put in the dialect's session mode.</summary>
    public abstract DatabaseModel Read(DbConnection connection, DatabaseModelFactoryOptions options, ISqlDialect sql);

    /// <summary>A foreign key's delete rule as EF's referential action; null for one it doesn't know.</summary>
    protected virtual ReferentialAction? DeleteAction(string? rule)
        => rule?.ToUpperInvariant() switch
        {
            "CASCADE" => ReferentialAction.Cascade,
            "SET NULL" => ReferentialAction.SetNull,
            "SET DEFAULT" => ReferentialAction.SetDefault,
            "RESTRICT" => ReferentialAction.Restrict,
            "NO ACTION" => ReferentialAction.NoAction,
            _ => null
        };

    protected static string? Text(IReadOnlyDictionary<string, object?> row, string column)
        => row.TryGetValue(column, out var value) && value is not null and not DBNull
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    protected static long? Long(IReadOnlyDictionary<string, object?> row, string column)
        => row.TryGetValue(column, out var value) && value is not null and not DBNull
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            : null;

    protected static bool Bool(IReadOnlyDictionary<string, object?> row, string column)
        => row.TryGetValue(column, out var value) && value is not null and not DBNull
            && (value is bool b ? b : Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0);
}
