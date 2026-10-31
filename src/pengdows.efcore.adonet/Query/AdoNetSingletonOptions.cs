using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using pengdows.efcore.adonet.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
namespace pengdows.efcore.adonet.Query;

internal class AdoNetQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies, ISqlDialect dialect, ITypeMappingDialect typeMappings, IAdoNetSingletonOptions options)
    : IQuerySqlGeneratorFactory
{
    public virtual QuerySqlGenerator Create()
        => new AdoNetQuerySqlGenerator(dependencies, dialect, typeMappings, options.ReverseNullOrdering);
}

/// <summary>
///     The settings from the context's options that the singleton services read. EF initializes this from the options of the
///     first context that uses the service provider, however the provider was built, and checks the others against it.
/// </summary>
public interface IAdoNetSingletonOptions : ISingletonOptions
{
    /// <summary>Whether every ORDER BY says where nulls sort; see <see cref="AdoNetOptionsExtension.ReverseNullOrdering" />.</summary>
    bool ReverseNullOrdering { get; }
}

internal sealed class AdoNetSingletonOptions : IAdoNetSingletonOptions
{
    public bool ReverseNullOrdering { get; private set; }

    public void Initialize(IDbContextOptions options)
        => ReverseNullOrdering = options.FindExtension<AdoNetOptionsExtension>()?.ReverseNullOrdering ?? false;

    public void Validate(IDbContextOptions options)
    {
        var requested = options.FindExtension<AdoNetOptionsExtension>()?.ReverseNullOrdering ?? false;
        if (requested != ReverseNullOrdering)
        {
            throw new InvalidOperationException(
                "ReverseNullOrdering changed between contexts that share a service provider. Set it the same on every one, "
                + "or give each its own service provider.");
        }
    }
}
