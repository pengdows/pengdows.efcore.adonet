using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using pengdows.efcore.adonet.Metadata;
namespace pengdows.efcore.adonet.Update;

internal partial class AdoNetUpdateSqlGenerator
{
    // SELECT cols FROM FINAL TABLE (INSERT ...): the generated columns come back as the delta table's rows.
    private ResultSetMapping AppendInsertDeltaTableOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        out bool requiresTransaction)
    {
        var writeOperations = command.ColumnModifications.Where(o => o.IsWrite).ToList();
        var readOperations = command.ColumnModifications.Where(o => o.IsRead).ToList();
        requiresTransaction = false;
        if (readOperations.Count == 0)
        {
            AppendInsertCommand(commandStringBuilder, command.TableName, command.Schema, writeOperations, []);
            return ResultSetMapping.NoResults;
        }

        AppendDeltaTable(
            commandStringBuilder, readOperations.Select(o => o.ColumnName), "FINAL",
            inner => AppendDeltaTableInsert(inner, command, writeOperations));
        return ResultSetMapping.LastInResultSet;
    }

    // An update's row comes back from the new table: its generated columns, or a 1 where nothing is generated, which is how a
    // concurrency conflict (no row) is told from success.

    // An update's row comes back from the new table: its generated columns, or a 1 where nothing is generated, which is how a
    // concurrency conflict (no row) is told from success.
    private ResultSetMapping AppendUpdateDeltaTableOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        out bool requiresTransaction)
    {
        var writeOperations = command.ColumnModifications.Where(o => o.IsWrite).ToList();
        var readOperations = command.ColumnModifications.Where(o => o.IsRead).ToList();
        var conditionOperations = command.ColumnModifications.Where(o => o.IsCondition).ToList();
        requiresTransaction = false;
        AppendDeltaTable(
            commandStringBuilder,
            readOperations.Count == 0 ? null : readOperations.Select(o => o.ColumnName),
            readOperations.Count == 0 ? "NEW" : "FINAL",
            inner => AppendDeltaTableUpdate(inner, command, writeOperations, conditionOperations));
        return ResultSetMapping.LastInResultSet;
    }

    private void AppendDeltaTableInsert(
        StringBuilder builder,
        IReadOnlyModificationCommand command,
        IReadOnlyList<IColumnModification> writeOperations)
    {
        var statement = new StringBuilder();
        AppendInsertCommand(statement, command.TableName, command.Schema, writeOperations, []);
        if (UseIncludeColumns())
        {
            AddInsertInclude(statement);
        }

        builder.Append(statement);
    }

    private void AppendDeltaTableUpdate(
        StringBuilder builder,
        IReadOnlyModificationCommand command,
        IReadOnlyList<IColumnModification> writeOperations,
        IReadOnlyList<IColumnModification> conditionOperations)
    {
        var statement = new StringBuilder();
        AppendUpdateCommand(
            statement, command.TableName, command.Schema, writeOperations, [], conditionOperations, appendReturningOneClause: false);
        if (UseIncludeColumns())
        {
            AddUpdateInclude(statement);
        }

        builder.Append(statement);
    }

    private bool UseIncludeColumns()
        => dialect.Sql.Keys is DeltaTableKeyRetrieval { UseIncludeColumns: true };

    private string DelimitedIncludeColumn
        => SqlGenerationHelper.DelimitIdentifier(IncludeCorrelationColumn);

    private void AddInsertInclude(StringBuilder statement)
    {
        var text = statement.ToString();
        var values = text.IndexOf("VALUES (", StringComparison.Ordinal);
        if (values < 0)
        {
            // The Pengdows specification defines an omitted INCLUDE value for DEFAULT VALUES as NULL.
            // EF's generated-key path does not project an INSERT source row, though, and the flatfile
            // binder consequently cannot bind an INCLUDE-bearing DEFAULT VALUES statement. The include
            // column is not part of EF's result projection in this path, so retain the ordinary delta
            // table shape; explicit-value inserts use INCLUDE below.
            return;
        }

        var lineStart = text.LastIndexOf('\n', values);
        var withInclude = text.Insert(lineStart < 0 ? values : lineStart, $" INCLUDE ({DelimitedIncludeColumn} INTEGER)");
        var valuesKeyword = withInclude.IndexOf("VALUES", StringComparison.Ordinal);
        var openParen = withInclude.IndexOf('(', valuesKeyword);
        if (openParen < 0)
        {
            throw new InvalidOperationException("The INSERT delta-table statement has no VALUES row for INCLUDE columns.");
        }

        var closeParen = withInclude.IndexOf(')', openParen);
        if (closeParen < 0)
        {
            throw new InvalidOperationException("The INSERT delta-table statement has no complete VALUES row for INCLUDE columns.");
        }

        statement.Clear().Append(withInclude.Insert(closeParen, ", 1"));
    }

    private void AddUpdateInclude(StringBuilder statement)
    {
        var text = statement.ToString();
        var set = text.IndexOf("SET", StringComparison.Ordinal);
        if (set < 0)
        {
            throw new InvalidOperationException("The UPDATE delta-table statement has no SET clause for INCLUDE columns.");
        }

        var lineStart = text.LastIndexOf('\n', set);
        var withInclude = text.Insert(lineStart < 0 ? set : lineStart, $" INCLUDE ({DelimitedIncludeColumn} INTEGER)");
        var setKeyword = withInclude.IndexOf("SET", set + $" INCLUDE ({DelimitedIncludeColumn} INTEGER)".Length, StringComparison.Ordinal);
        statement.Clear().Append(withInclude.Insert(setKeyword + 3, $" {DelimitedIncludeColumn} = 1,"));
    }

    private ResultSetMapping AppendDeleteDeltaTableOperation(
        StringBuilder commandStringBuilder,
        IReadOnlyModificationCommand command,
        out bool requiresTransaction)
    {
        var conditionOperations = command.ColumnModifications.Where(o => o.IsCondition).ToList();
        requiresTransaction = false;
        AppendDeltaTable(
            commandStringBuilder,
            null,
            "OLD",
            inner => AppendDeleteCommand(inner, command.TableName, command.Schema, [], conditionOperations, appendReturningOneClause: false));
        return ResultSetMapping.LastInResultSet;
    }

    // SELECT {columns, or 1} FROM {FINAL|NEW|OLD} TABLE ({statement});

    // SELECT {columns, or 1} FROM {FINAL|NEW|OLD} TABLE ({statement});
    private void AppendDeltaTable(
        StringBuilder commandStringBuilder,
        IEnumerable<string>? columns,
        string resultOption,
        Action<StringBuilder> appendStatement)
    {
        var statement = new StringBuilder();
        appendStatement(statement);
        var terminator = SqlGenerationHelper.StatementTerminator;
        var text = statement.ToString().TrimEnd();
        if (text.EndsWith(terminator, StringComparison.Ordinal))
        {
            text = text[..^terminator.Length];
        }

        commandStringBuilder.Append("SELECT ");
        if (columns == null)
        {
            commandStringBuilder.Append('1');
        }
        else
        {
            var first = true;
            foreach (var column in columns)
            {
                if (!first)
                {
                    commandStringBuilder.Append(", ");
                }

                first = false;
                SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, column);
            }
        }

        commandStringBuilder
            .AppendLine()
            .Append("FROM ").Append(resultOption).Append(" TABLE (").Append(text).Append(')')
            .AppendLine(terminator);
    }

}
