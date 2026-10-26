// Portions adapted from Pomelo's MySqlUpdateSqlGenerator.
// Copyright (c) Pomelo Foundation. Licensed under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace pengdows.efcore.adonet.Update;

/// <summary>
///     Reads back generated values and checks concurrency with <c>RETURNING</c>, with a follow-up <c>SELECT</c> in the
///     same command, or with plain statements checked via RecordsAffected, as chosen by <see cref="ISqlDialect.Keys" />.
/// </summary>
internal class AdoNetUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies, IAdoNetDialect dialect)
    : UpdateAndSelectSqlGenerator(dependencies)
{
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
        var property = columnModification.Property;
        var isText = property is null or { IsPrimitiveCollection: true }
            || ((Nullable.GetUnderlyingType(property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType)
                ?? property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType) == typeof(bool));

        var value = new StringBuilder();
        base.AppendUpdateColumnValue(updateSqlGeneratorHelper, columnModification, value, name, schema);
        var column = new StringBuilder();
        updateSqlGeneratorHelper.DelimitIdentifier(column, columnModification.ColumnName);

        stringBuilder.Append(
            json.Set
                .Replace("{0}", column.ToString(), StringComparison.Ordinal)
                .Replace("{1}", dialect.Sql.StringLiteral(columnModification.JsonPath), StringComparison.Ordinal)
                .Replace("{2}", isText ? json.FromText.Replace("{0}", value.ToString(), StringComparison.Ordinal) : value.ToString(), StringComparison.Ordinal));
    }

    // Where the driver returns no output parameters from a text CALL, each output goes through a session variable: set before
    // the call (an input-output one to its input), passed in the parameter's place, and selected back after it. With other
    // outputs, a rows-affected one filters that row, so a procedure that changed no row returns none: a concurrency failure.
    // Adapted from Pomelo's MySqlUpdateSqlGenerator (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Update/Internal/MySqlUpdateSqlGenerator.cs
    public override ResultSetMapping AppendStoredProcedureCall(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
    {
        var outputs = command.ColumnModifications.Where(AdoNetModificationCommand.IsOutput).ToList();
        if ((dialect.Sql.ProcedureOutputVariable is not { } template) || (outputs.Count == 0))
        {
            return base.AppendStoredProcedureCall(commandStringBuilder, command, commandPosition, out requiresTransaction);
        }

        var procedure = command.StoreStoredProcedure!;
        if (procedure.ResultColumns.Any() || (procedure.ReturnValue != null))
        {
            throw new InvalidOperationException(
                $"The stored procedure '{procedure.SchemaQualifiedName}' has output parameters, which this dialect reads back as a "
                + "result set, so it can have neither result columns nor a return value.");
        }

        var terminator = SqlGenerationHelper.StatementTerminator;
        foreach (var output in outputs)
        {
            commandStringBuilder.Append("SET ").Append(Variable(template, output)).Append(" = ");
            if (((IStoreStoredProcedureParameter)output.Column!).Direction.HasFlag(System.Data.ParameterDirection.Input))
            {
                SqlGenerationHelper.GenerateParameterNamePlaceholder(commandStringBuilder, ParameterName(output));
            }
            else
            {
                commandStringBuilder.Append("NULL");
            }

            commandStringBuilder.AppendLine(terminator);
        }

        commandStringBuilder.Append("CALL ");
        SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, procedure.Name, procedure.Schema);
        commandStringBuilder.Append('(');
        var first = true;
        foreach (var modification in command.ColumnModifications.Where(m => m.Column is IStoreStoredProcedureParameter))
        {
            if (!first)
            {
                commandStringBuilder.Append(", ");
            }

            first = false;
            if (AdoNetModificationCommand.IsOutput(modification))
            {
                commandStringBuilder.Append(Variable(template, modification));
            }
            else
            {
                SqlGenerationHelper.GenerateParameterNamePlaceholder(commandStringBuilder, ParameterName(modification));
            }
        }

        commandStringBuilder.Append(')').AppendLine(terminator);
        commandStringBuilder.Append("SELECT ").AppendJoin(", ", outputs.Select(o => Variable(template, o)));
        var rowsAffected = outputs.FirstOrDefault(o => o.Column == command.RowsAffectedColumn);
        if ((rowsAffected != null) && (outputs.Count > 1))
        {
            commandStringBuilder.AppendLine().Append("WHERE ").Append(Variable(template, rowsAffected)).Append(" = 1");
        }

        commandStringBuilder.AppendLine(terminator);
        requiresTransaction = true;
        return (rowsAffected != null) && (outputs.Count == 1)
            ? ResultSetMapping.LastInResultSet | ResultSetMapping.ResultSetWithRowsAffectedOnly
            : ResultSetMapping.LastInResultSet;
    }

    private static string ParameterName(IColumnModification modification)
        => modification.UseOriginalValueParameter ? modification.OriginalParameterName! : modification.ParameterName!;

    private static string Variable(string template, IColumnModification modification)
        => template.Replace("{0}", "_out_" + ParameterName(modification), StringComparison.Ordinal);

    public override ResultSetMapping AppendInsertOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
        => dialect.Sql.Keys switch
        {
            ReturningKeyRetrieval
                => AppendInsertReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            SelectAfterKeyRetrieval
                => AppendInsertAndSelectOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            _ => AppendPlainOperation(commandStringBuilder, command, out requiresTransaction)
        };

    public override ResultSetMapping AppendUpdateOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
        => dialect.Sql.Keys switch
        {
            ReturningKeyRetrieval
                => AppendUpdateReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            SelectAfterKeyRetrieval
                => AppendUpdateAndSelectOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
            _ => AppendPlainOperation(commandStringBuilder, command, out requiresTransaction)
        };

    public override ResultSetMapping AppendDeleteOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        int commandPosition,
        out bool requiresTransaction)
        => dialect.Sql.Keys switch
        {
            ReturningKeyRetrieval
                => AppendDeleteReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction),
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

    private SelectAfterKeyRetrieval SelectAfter
        => (SelectAfterKeyRetrieval)dialect.Sql.Keys;
}
