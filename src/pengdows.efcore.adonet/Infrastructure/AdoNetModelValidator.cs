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

        base.Validate(model, logger);
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
