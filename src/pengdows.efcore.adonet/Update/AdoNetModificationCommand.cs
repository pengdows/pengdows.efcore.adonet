using System.Diagnostics;
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
    public AdoNetModificationCommand(in ModificationCommandParameters parameters)
        : base(parameters)
    {
    }

    public AdoNetModificationCommand(in NonTrackedModificationCommandParameters parameters)
        : base(parameters)
    {
    }

#pragma warning disable EF1001 // EF's provider-facing hook for JSON property updates, marked internal like the rest.
    protected override void ProcessSinglePropertyJsonUpdate(ref ColumnModificationParameters parameters)
    {
        var mapping = parameters.Property!.GetTypeMapping();
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

internal sealed class AdoNetModificationCommandFactory : IModificationCommandFactory
{
    public IModificationCommand CreateModificationCommand(in ModificationCommandParameters modificationCommandParameters)
        => new AdoNetModificationCommand(modificationCommandParameters);

    public INonTrackedModificationCommand CreateNonTrackedModificationCommand(in NonTrackedModificationCommandParameters modificationCommandParameters)
        => new AdoNetModificationCommand(modificationCommandParameters);
}
