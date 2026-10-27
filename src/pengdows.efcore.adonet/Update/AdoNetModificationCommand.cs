// Behaviour reference (not copied): Pomelo's MySqlModificationCommand (MIT, Pomelo Foundation):
// https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Update/Internal/MySqlModificationCommand.cs

using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace pengdows.efcore.adonet.Update;

/// <summary>
///     Patches a property inside a JSON column with a value the database stores as the right JSON type: a bool bound as a
///     parameter would land as 1 or 0, so it goes as the text <c>true</c> or <c>false</c>, which the update SQL makes JSON.
/// </summary>
/// <remarks>
///     Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.Sqlite.Core/Update/Internal/SqliteModificationCommand.cs
/// </remarks>
internal sealed class AdoNetModificationCommand : ModificationCommand
{
    private readonly bool _outputVariables;
    private readonly bool _detailedErrorsEnabled;

    public AdoNetModificationCommand(in ModificationCommandParameters parameters, bool outputVariables = false)
        : base(parameters)
    {
        _outputVariables = outputVariables;
        _detailedErrorsEnabled = parameters.DetailedErrorsEnabled;
    }

    public AdoNetModificationCommand(in NonTrackedModificationCommandParameters parameters)
        : base(parameters)
    {
    }

    // A session-variable procedure returns output values in a row, in parameter order; EF's own reader skips the parameters.
    public override void PropagateResults(RelationalDataReader relationalReader)
    {
        if (!UsesResultRowForOutputs())
        {
            base.PropagateResults(relationalReader);
            return;
        }

        CopyOutputValues(relationalReader);
    }

    private bool UsesResultRowForOutputs()
        => _outputVariables && StoreStoredProcedure != null && ColumnModifications.Any(IsOutput);

    private void CopyOutputValues(RelationalDataReader relationalReader)
    {
        var ordinal = 0;
        foreach (var columnModification in ColumnModifications)
        {
            if (!IsOutput(columnModification))
            {
                continue;
            }

            if ((columnModification.Property is { } property) && columnModification.IsRead)
            {
#pragma warning disable EF1001 // EF's reader of a property's value, which its own PropagateResults uses.
                columnModification.Value = property.GetReaderFieldValue(relationalReader, ordinal, _detailedErrorsEnabled);
#pragma warning restore EF1001
            }

            ordinal++;
        }
    }

    internal static bool IsOutput(IColumnModification columnModification)
        => columnModification.Column is IStoreStoredProcedureParameter parameter && parameter.Direction.HasFlag(ParameterDirection.Output);

#pragma warning disable EF1001 // EF's provider-facing hook for JSON property updates, marked internal like the rest.
    protected override void ProcessSinglePropertyJsonUpdate(ref ColumnModificationParameters parameters)
    {
        var mapping = parameters.Property!.GetTypeMapping();
        // The CLR lets an sbyte[] satisfy `is byte[]`, which would write a signed-byte collection as base64 text.
        if (parameters.Property.IsPrimitiveCollection && parameters.Value is byte[] bytes && bytes.GetType() == typeof(byte[]))
        {
            parameters = parameters with { Value = JsonSerializer.Serialize(bytes) };
            return;
        }

        if ((Nullable.GetUnderlyingType(mapping.Converter?.ProviderClrType ?? parameters.Property.ClrType)
                ?? mapping.Converter?.ProviderClrType ?? parameters.Property.ClrType) != typeof(bool))
        {
            base.ProcessSinglePropertyJsonUpdate(ref parameters);
            return;
        }

        var value = mapping.Converter is { } converter ? converter.ConvertToProvider(parameters.Value) : parameters.Value;
        parameters = parameters with
        {
            Value = value switch
            {
                true => "true",
                false => "false",
                _ => throw new UnreachableException()
            }
        };
    }
#pragma warning restore EF1001
}

internal sealed class AdoNetModificationCommandFactory(ISqlDialect sql) : IModificationCommandFactory
{
    public IModificationCommand CreateModificationCommand(in ModificationCommandParameters modificationCommandParameters)
        => new AdoNetModificationCommand(modificationCommandParameters, sql.ProcedureOutputVariable != null);

    public INonTrackedModificationCommand CreateNonTrackedModificationCommand(in NonTrackedModificationCommandParameters modificationCommandParameters)
        => new AdoNetModificationCommand(modificationCommandParameters);
}
