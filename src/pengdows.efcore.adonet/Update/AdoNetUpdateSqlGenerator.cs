// Behaviour reference (not copied): Pomelo's MySqlUpdateSqlGenerator (MIT, Pomelo Foundation):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Update/Internal/MySqlUpdateSqlGenerator.cs

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using pengdows.efcore.adonet.Metadata;

namespace pengdows.efcore.adonet.Update;

/// <summary>
///     Reads back generated values and checks concurrency with <c>RETURNING</c>, with a follow-up <c>SELECT</c> in the
///     same command, or with plain statements checked via RecordsAffected, as chosen by <see cref="ISqlDialect.Keys" />.
/// </summary>
internal partial class AdoNetUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : UpdateAndSelectSqlGenerator(dependencies)
{
    private const string IncludeCorrelationColumn = "__pengdows_correlation";

    // A property inside a JSON column is patched in place with the dialect's set function. A sub-document, or a bool sent as
    // its text (AdoNetModificationCommand), is made a JSON value first, so it isn't stored as a string. Behaviour reference
    // (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Update/Internal/SqliteUpdateSqlGenerator.cs
    protected override void AppendUpdateColumnValue(
        ISqlGenerationHelper updateSqlGeneratorHelper,
        IColumnModification columnModification,
        StringBuilder stringBuilder,
        string name,
        string? schema)
    {
        if (columnModification.JsonPath is null or "$")
        {
            base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, stringBuilder, name, schema);
            return;
        }

        var json = dialect.Sql.Json
            ?? throw new InvalidOperationException("This update patches a JSON column, which the dialect has no functions for.");
        if (json.Set is null)
        {
            throw new NotSupportedException("This provider does not support JSON partial updates because the required mutation syntax is not ISO SQL.");
        }
        var property = columnModification.Property;
        var isText = property is null or { IsPrimitiveCollection: true }
            || ((Nullable.GetUnderlyingType(property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType)
                ?? property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType) == typeof(bool));
        var providerType = property?.GetTypeMapping().Converter?.ProviderClrType ?? property?.ClrType;
        var storeType = property?.GetRelationalTypeMapping().StoreType ?? "";
        var scalarNeedsText = (json.ScalarValue != null)
            && !isText
            && (providerType == typeof(DateTime) || providerType == typeof(DateTimeOffset) || providerType == typeof(DateOnly)
                || providerType == typeof(TimeOnly) || providerType == typeof(TimeSpan) || providerType == typeof(Guid)
                || providerType == typeof(byte[]) || providerType == typeof(char)
                || storeType.Contains("DATE", StringComparison.OrdinalIgnoreCase)
                || storeType.Contains("TIME", StringComparison.OrdinalIgnoreCase)
                || storeType.Contains("INTERVAL", StringComparison.OrdinalIgnoreCase)
                || storeType.Contains("UUID", StringComparison.OrdinalIgnoreCase)
                || storeType.Contains("BYTEA", StringComparison.OrdinalIgnoreCase));

        var value = new StringBuilder();
        base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, value, name, schema);
        var column = new StringBuilder();
        updateSqlGeneratorHelper.DelimitIdentifier(column, columnModification.ColumnName);

        var replacement = value.ToString();
        if ((json.NullValue is { } nullValue) && (columnModification.Value is null))
        {
            // The set function propagates SQL NULL, but an EF nullable JSON property means the JSON
            // literal null and must preserve the surrounding document.
            replacement = nullValue;
        }
        else if (scalarNeedsText)
        {
            // As text, so it's stored as a JSON string. EF's JSON byte[] mapping already supplies the base64 text; wrapping it
            // as a JSON string must not encode that text a second time.
            var text = dialect.TypeMappings.Cast(dialect.TypeMappings.GetStoreType(typeof(string))!)
                .Replace("{0}", replacement, StringComparison.Ordinal);
            replacement = json.ScalarValue!.Replace("{0}", text, StringComparison.Ordinal);
        }
        else if (providerType == typeof(bool) && json.SetBooleanValue is { } setBooleanValue)
        {
            replacement = setBooleanValue.Replace("{0}", replacement, StringComparison.Ordinal);
        }
        else if (isText)
        {
            replacement = json.FromText.Replace("{0}", replacement, StringComparison.Ordinal);
        }
        else if (json.ScalarValue is { } scalarValue)
        {
            replacement = scalarValue.Replace("{0}", replacement, StringComparison.Ordinal);
        }

        stringBuilder.Append(
            json.Set
                .Replace("{0}", column.ToString(), StringComparison.Ordinal)
                .Replace("{1}", dialect.Sql.StringLiteral(columnModification.JsonPath), StringComparison.Ordinal)
                .Replace("{2}", replacement, StringComparison.Ordinal));
    }








    public override ResultSetMapping AppendInsertOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        if ((dialect.Sql.IdentityInsert is not { } identityInsert) || !command.ColumnModifications.Any(WritesIdentity))
        {
            return AppendInsertWithKeys(commandStringBuilder, command, commandPosition, out requiresTransaction);
        }

        var table = SqlGenerationHelper.DelimitIdentifier(command.TableName, command.Schema);
        AppendTableStatement(commandStringBuilder, identityInsert.Enable, table);
        var mapping = AppendInsertWithKeys(commandStringBuilder, command, commandPosition, out requiresTransaction);
        AppendTableStatement(commandStringBuilder, identityInsert.Disable, table);
        return mapping;
    }

    // A key the application set, written into a column the database numbers. A table-per-type derived table copies its key
    // from the root row, so its column is no identity column (AdoNetAnnotationProvider).
    private static bool WritesIdentity(IColumnModification modification)
        => modification.IsWrite && (modification.Column is IColumn column) && AdoNetAnnotationProvider.IsIdentity(column);

    private void AppendTableStatement(StringBuilder commandStringBuilder, string template, string delimitedTable)
        => commandStringBuilder
            .Append(template.Replace("{0}", delimitedTable, StringComparison.Ordinal))
            .AppendLine(SqlGenerationHelper.StatementTerminator);

    private ResultSetMapping AppendInsertWithKeys(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
        => KeysFor(command) switch
        {
            ReturningKeyRetrieval
                => AppendInsertReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            DeltaTableKeyRetrieval
                => AppendInsertDeltaTableOperation(commandStringBuilder, command, out requiresTransaction),
            SelectAfterKeyRetrieval
                => AppendInsertAndSelectOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            _ => AppendPlainOperation(commandStringBuilder, command, out requiresTransaction)
        };

    protected override ResultSetMapping AppendInsertAndSelectOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        var readOperations = operations.Where(o => o.IsRead).ToList();
        var suppliedKeyOperations = operations.Where(o => o is { IsKey: true, IsRead: false }).ToList();
        var generatedDefaultKey = readOperations.Any(o => o is { IsKey: true, Property: { } property } && HasNonClrDefault(property));

        // The identity function identifies an auto-increment key, but cannot identify a row whose generated key came from a
        // column default (for example, a defaulted member of a composite key). The values supplied for the remaining key
        // columns are then the stable predicate for reading the generated value.
        if (generatedDefaultKey && suppliedKeyOperations.Count > 0)
        {
            AppendInsertCommand(commandStringBuilder, command.TableName, command.Schema, operations.Where(o => o.IsWrite).ToList(), []);
            requiresTransaction = true;
            return AppendSelectAffectedCommand(
                commandStringBuilder,
                command.TableName,
                command.Schema,
                readOperations,
                suppliedKeyOperations,
                commandPosition);
        }

        return base.AppendInsertAndSelectOperation(commandStringBuilder, command, commandPosition, out requiresTransaction);
    }

    private static bool HasNonClrDefault(IProperty property)
        => (property.GetDefaultValueSql() is not null)
            || ((property.GetDefaultValue() is { } defaultValue)
                && !Equals(defaultValue, property.ClrType.IsValueType ? Activator.CreateInstance(property.ClrType) : null));

    public override ResultSetMapping AppendUpdateOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
        => KeysFor(command) switch
        {
            ReturningKeyRetrieval
                => AppendUpdateReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            DeltaTableKeyRetrieval
                => AppendUpdateDeltaTableOperation(commandStringBuilder, command, out requiresTransaction),
            SelectAfterKeyRetrieval
                => AppendUpdateAndSelectOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            _ => AppendPlainOperation(commandStringBuilder, command, out requiresTransaction)
        };

    public override ResultSetMapping AppendDeleteOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
        => KeysFor(command) switch
        {
            ReturningKeyRetrieval
                => AppendDeleteReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            DeltaTableKeyRetrieval
                => AppendDeleteDeltaTableOperation(commandStringBuilder, command, out requiresTransaction),
            SelectAfterKeyRetrieval
                => AppendDeleteAndSelectOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            _ => AppendPlainOperation(commandStringBuilder, command, out requiresTransaction)
        };








    /// <summary>
    ///     <see cref="SequenceKeyRetrieval" />: the statement alone, with no result set;
    ///     <see cref="AdoNetRecordsAffectedModificationCommandBatch" /> checks the affected row count.
    /// </summary>
    protected virtual ResultSetMapping AppendPlainOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        out bool requiresTransaction)
    {
        var operations = command.ColumnModifications;
        if (operations.FirstOrDefault(o => o.IsRead) is { } read)
        {
            throw new NotSupportedException(
                $"Column '{command.TableName}.{read.ColumnName}' is generated by the database, but the '{dialect.Name}' dialect "
                + "cannot read generated values back. Generate the value on the client or use a key sequence.");
        }

        var writeOperations = operations.Where(o => o.IsWrite).ToList();
        var conditionOperations = operations.Where(o => o.IsCondition).ToList();

        switch (command.EntityState)
        {
            case EntityState.Added:
                AppendInsertCommand(commandStringBuilder, command.TableName, command.Schema, writeOperations, []);
                break;
            case EntityState.Modified:
                AppendUpdateCommand(
                    commandStringBuilder, command.TableName, command.Schema, writeOperations, [], conditionOperations,
                    appendReturningOneClause: false);
                break;
            case EntityState.Deleted:
                AppendDeleteCommand(
                    commandStringBuilder, command.TableName, command.Schema, [], conditionOperations, appendReturningOneClause: false);
                break;
            default:
                throw new InvalidOperationException($"Unexpected entity state '{command.EntityState}'.");
        }

        requiresTransaction = false;
        return ResultSetMapping.NoResults;
    }

    protected override void AppendValuesHeader(StringBuilder commandStringBuilder, IReadOnlyList<IColumnModification> operations)
    {
        if (operations.Count > 0)
        {
            base.AppendValuesHeader(commandStringBuilder, operations);
            return;
        }

        commandStringBuilder.AppendLine().Append(dialect.Sql.DefaultValues);
    }

    protected override void AppendIdentityWhereCondition(StringBuilder commandStringBuilder, IColumnModification columnModification)
    {
        SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, columnModification.ColumnName);
        commandStringBuilder.Append(" = ").Append(SelectAfter.IdentityFunction);
    }

    protected override void AppendRowsAffectedWhereCondition(StringBuilder commandStringBuilder, int expectedRowsAffected)
        => commandStringBuilder
            .Append(SelectAfter.RowsAffectedFunction)
            .Append(" = ")
            .Append(expectedRowsAffected.ToString(CultureInfo.InvariantCulture));

    protected override ResultSetMapping AppendSelectAffectedCountCommand(
        StringBuilder commandStringBuilder,
        string name,
        string? schema,
        int commandPosition)
    {
        commandStringBuilder
            .Append("SELECT ")
            .Append(SelectAfter.RowsAffectedFunction)
            .AppendLine(SqlGenerationHelper.StatementTerminator)
            .AppendLine();

        return ResultSetMapping.LastInResultSet | ResultSetMapping.ResultSetWithRowsAffectedOnly;
    }

    // Only the SELECT-after strategy reaches these, so it is the dialect's own or its fallback for tables without RETURNING.
    private SelectAfterKeyRetrieval SelectAfter
        => (dialect.Sql.Keys as SelectAfterKeyRetrieval) ?? dialect.Sql.KeysWithoutReturning!;

    // A table that can't take RETURNING (AdoNetAnnotationNames.UseReturningClause false) reads keys back the dialect's other
    // way. Behaviour reference (not copied):
    // https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Extensions/SqliteTableExtensions.cs
    private KeyRetrieval KeysFor(IReadOnlyModificationCommand command)
        => (dialect.Sql.Keys is ReturningKeyRetrieval)
            && (dialect.Sql.KeysWithoutReturning is { } withoutReturning)
            && (command.Table is { } table)
            && table.EntityTypeMappings.Any(m => UsesReturningClause(m.TypeBase, table) is false)
                ? withoutReturning
                : dialect.Sql.Keys;

    // An entity type split across tables says so for one table on that table's mapping fragment, else for all its tables.
    // The dialect's name first, then the shared one, which models written before a dialect named its own still use.
    private bool? UsesReturningClause(ITypeBase type, ITable table)
        => ReturningClauseSetting(type, table, dialect.Sql.ReturningClauseAnnotation)
            ?? ReturningClauseSetting(type, table, AdoNetAnnotationNames.UseReturningClause);

    private static bool? ReturningClauseSetting(ITypeBase type, ITable table, string annotation)
        => ((type as IEntityType)?.FindMappingFragment(StoreObjectIdentifier.Table(table.Name, table.Schema))
                ?.FindAnnotation(annotation)?.Value as bool?)
            ?? (type.FindAnnotation(annotation)?.Value as bool?);
}
