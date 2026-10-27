// Portions adapted from Pomelo's MySqlModificationCommand.
// Copyright (c) Pomelo Foundation. Licensed under the MIT license.

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

    // Where a procedure's outputs come back through session variables (ISqlDialect.ProcedureOutputVariable), the row selected
    // after the call holds each output, in parameter order; EF's own reading skips parameters, which elsewhere arrive as
    // output parameters once the reader is closed. Adapted from Pomelo's MySqlModificationCommand (MIT, Pomelo Foundation):
    // https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/blob/14c2cd558e37c71e7a2fca75e339ef902effdf60/src/EFCore.MySql/Update/Internal/MySqlModificationCommand.cs
    public override void PropagateResults(RelationalDataReader relationalReader)
    {
        if (!_outputVariables || (StoreStoredProcedure is null) || !ColumnModifications.Any(IsOutput))
        {
            base.PropagateResults(relationalReader);
            return;
        }

        var ordinal = -1;
        foreach (var columnModification in ColumnModifications)
        {
            if (!IsOutput(columnModification))
            {
                continue;
            }

            ordinal++;
            if ((columnModification.Property is { } property) && columnModification.IsRead)
            {
#pragma warning disable EF1001 // EF's reader of a property's value, which its own PropagateResults uses.
                columnModification.Value = property.GetReaderFieldValue(relationalReader, ordinal, _detailedErrorsEnabled);
#pragma warning restore EF1001
            }
        }
    }

    internal static bool IsOutput(IColumnModification columnModification)
        => columnModification.Column is IStoreStoredProcedureParameter parameter && parameter.Direction.HasFlag(ParameterDirection.Output);

#pragma warning disable EF1001 // EF's provider-facing hook for JSON property updates, marked internal like the rest.
    protected override void ProcessSinglePropertyJsonUpdate(ref ColumnModificationParameters parameters)
    {
        var mapping = parameters.Property!.GetTypeMapping();
        if (parameters.Property.IsPrimitiveCollection && parameters.Value is byte[] bytes)
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
