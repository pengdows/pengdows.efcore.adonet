using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.adonet.Metadata;

/// <summary>
///     Tells the operations the differ writes for a dropped or renamed table which of its columns are identities
///     (<see cref="AdoNetAnnotationNames.IdentityColumns" />), so a sequence-key dialect drops or renames their sequences.
/// </summary>
// EF1001: EF's providers all derive from MigrationsAnnotationProvider through its dependencies object, which EF marks as
// internal; the public alternative is implementing every member of IMigrationsAnnotationProvider to return nothing.
#pragma warning disable EF1001
/// <summary>EF Core's migrations annotation provider with the dialect's annotations, for a plugin's migration annotations.</summary>
public class AdoNetMigrationsAnnotationProvider(MigrationsAnnotationProviderDependencies dependencies)
    : MigrationsAnnotationProvider(dependencies)
#pragma warning restore EF1001
{
    public override IEnumerable<IAnnotation> ForRemove(ITable table)
        => WithIdentityColumns(base.ForRemove(table), table);

    public override IEnumerable<IAnnotation> ForRename(ITable table)
        => WithIdentityColumns(base.ForRename(table), table);

    private static IEnumerable<IAnnotation> WithIdentityColumns(IEnumerable<IAnnotation> annotations, ITable table)
    {
        var identities = table.Columns
            .Where(AdoNetAnnotationProvider.IsIdentity)
            .Select(c => c.Name)
            .ToArray();
        return identities.Length == 0 ? annotations : annotations.Append(new Annotation(AdoNetAnnotationNames.IdentityColumns, identities));
    }
}
