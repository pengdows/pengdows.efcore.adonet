using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace pengdows.efcore.adonet.Infrastructure;

/// <summary>
///     EF's relational model validation, and a model mapping an entity type's inserts, updates or deletes to stored procedures
///     is refused where the dialect says the database has none (<see cref="ISqlDialect.SupportsStoredProcedures" />).
///     Behaviour reference (not copied): EF Core's SQLite provider (MIT, .NET Foundation):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Infrastructure/Internal/SqliteModelValidator.cs
/// </summary>
internal sealed class AdoNetModelValidator(
    ModelValidatorDependencies dependencies,
    RelationalModelValidatorDependencies relationalDependencies,
    IAdoNetDialect dialect)
    : RelationalModelValidator(dependencies, relationalDependencies)
{
    public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
    {
        if (!dialect.Sql.SupportsStoredProcedures)
        {
            ValidateNoStoredProcedures(model);
        }

        if (!dialect.Sql.SupportsForeignKeyCycles)
        {
            ValidateNoForeignKeyCycles(model);
        }

        base.Validate(model, logger);
    }

    // Tables that refer to one another in a cycle: a cycle's keys can't be declared where the database declares a key only in
    // its table's CREATE. Read from the entity types, since the relational model is not final during validation. A table's key
    // to itself is no cycle.
    private void ValidateNoForeignKeyCycles(IModel model)
    {
        var principals = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entityType in model.GetEntityTypes().Where(e => e.GetTableName() is not null))
        {
            var table = TableName(entityType);
            principals.TryAdd(table, []);
            foreach (var key in entityType.GetForeignKeys())
            {
                if (key.PrincipalEntityType.GetTableName() is not null && TableName(key.PrincipalEntityType) is { } principal && principal != table)
                {
                    principals[table].Add(principal);
                }
            }
        }

        var state = new Dictionary<string, bool>(StringComparer.Ordinal); // false while on the path, true once fully explored
        foreach (var table in principals.Keys)
        {
            if (FindCycle(table, principals, state, []) is { } cycle)
            {
                throw new ForeignKeyCyclesNotSupportedException(
                    $"The tables {string.Join(", ", cycle.Select(t => $"'{t}'"))} refer to one another in a cycle of foreign keys, "
                    + $"which {dialect.Name} can't declare: its keys are declared only with their tables, after the tables they refer to.");
            }
        }
    }

    private static string TableName(IEntityType entityType)
        => entityType.GetSchema() is { } schema ? $"{schema}.{entityType.GetTableName()}" : entityType.GetTableName()!;

    private static List<string>? FindCycle(
        string table,
        Dictionary<string, HashSet<string>> principals,
        Dictionary<string, bool> state,
        List<string> path)
    {
        if (state.TryGetValue(table, out var done))
        {
            return done ? null : path.SkipWhile(t => t != table).ToList();
        }

        state[table] = false;
        path.Add(table);
        foreach (var principal in principals.GetValueOrDefault(table) ?? [])
        {
            if (FindCycle(principal, principals, state, path) is { } cycle)
            {
                return cycle;
            }
        }

        path.RemoveAt(path.Count - 1);
        state[table] = true;
        return null;
    }

    private void ValidateNoStoredProcedures(IModel model)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            if ((entityType.GetInsertStoredProcedure() != null)
                || (entityType.GetUpdateStoredProcedure() != null)
                || (entityType.GetDeleteStoredProcedure() != null))
            {
                throw new StoredProceduresNotSupportedException(
                    $"The entity type '{entityType.DisplayName()}' is mapped to stored procedures, which {dialect.Name} doesn't have.");
            }
        }
    }
}

/// <summary>A model mapped to stored procedures, on a database that has none: the dialect's capability, not a model error.</summary>
internal sealed class StoredProceduresNotSupportedException(string message) : InvalidOperationException(message);

/// <summary>A model whose tables refer to one another in a cycle, on a database that can't declare such a key: the dialect's capability.</summary>
internal sealed class ForeignKeyCyclesNotSupportedException(string message) : InvalidOperationException(message);
