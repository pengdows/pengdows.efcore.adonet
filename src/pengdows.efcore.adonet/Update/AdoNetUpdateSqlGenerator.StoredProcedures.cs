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
    // Where the driver returns no output parameters from a text CALL, each output goes through a session variable: set before
    // the call (an input-output one to its input), passed in the parameter's place, and selected back after it. With other
    // outputs, a rows-affected one filters that row, so a procedure that changed no row returns none: a concurrency failure.
    // Behaviour reference (not copied): Pomelo's MySqlUpdateSqlGenerator (MIT, Pomelo Foundation):
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

        AppendOutputVariableInitialization(commandStringBuilder, outputs, template);
        AppendProcedureInvocation(commandStringBuilder, command, procedure, outputs, template);
        var rowsAffected = outputs.FirstOrDefault(o => o.Column == command.RowsAffectedColumn);
        AppendOutputProjection(commandStringBuilder, outputs, rowsAffected, template);
        requiresTransaction = true;
        return OutputResultMapping(rowsAffected, outputs.Count);
    }

    private void AppendOutputVariableInitialization(
        StringBuilder builder,
        IReadOnlyList<IColumnModification> outputs,
        string template)
    {
        var terminator = SqlGenerationHelper.StatementTerminator;
        foreach (var output in outputs)
        {
            builder.Append("SET ").Append(Variable(template, output)).Append(" = ");
            var parameter = (IStoreStoredProcedureParameter)output.Column!;
            if (parameter.Direction.HasFlag(System.Data.ParameterDirection.Input))
            {
                SqlGenerationHelper.GenerateParameterNamePlaceholder(builder, ParameterName(output));
            }
            else
            {
                builder.Append("NULL");
            }

            builder.AppendLine(terminator);
        }
    }

    private void AppendProcedureInvocation(
        StringBuilder builder,
        IReadOnlyModificationCommand command,
        IStoreStoredProcedure procedure,
        IReadOnlyList<IColumnModification> outputs,
        string template)
    {
        builder.Append("CALL ");
        SqlGenerationHelper.DelimitIdentifier(builder, procedure.Name, procedure.Schema);
        builder.Append('(');

        var first = true;
        foreach (var modification in command.ColumnModifications.Where(m => m.Column is IStoreStoredProcedureParameter))
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            if (outputs.Contains(modification))
            {
                builder.Append(Variable(template, modification));
            }
            else
            {
                SqlGenerationHelper.GenerateParameterNamePlaceholder(builder, ParameterName(modification));
            }
        }

        builder.Append(')').AppendLine(SqlGenerationHelper.StatementTerminator);
    }

    private void AppendOutputProjection(
        StringBuilder builder,
        IReadOnlyList<IColumnModification> outputs,
        IColumnModification? rowsAffected,
        string template)
    {
        builder.Append("SELECT ").AppendJoin(", ", outputs.Select(o => Variable(template, o)));
        if ((rowsAffected != null) && (outputs.Count > 1))
        {
            builder.AppendLine().Append("WHERE ").Append(Variable(template, rowsAffected)).Append(" = 1");
        }

        builder.AppendLine(SqlGenerationHelper.StatementTerminator);
    }

    private static ResultSetMapping OutputResultMapping(IColumnModification? rowsAffected, int outputCount)
        => (rowsAffected != null) && (outputCount == 1)
            ? ResultSetMapping.LastInResultSet | ResultSetMapping.ResultSetWithRowsAffectedOnly
            : ResultSetMapping.LastInResultSet;

    private static string ParameterName(IColumnModification modification)
        => modification.UseOriginalValueParameter ? modification.OriginalParameterName! : modification.ParameterName!;

    private static string Variable(string template, IColumnModification modification)
        => template.Replace("{0}", "_out_" + ParameterName(modification), StringComparison.Ordinal);
}
