using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Bulk;

/// <summary>
///     Loads rows into one table. The core registers <see cref="AdoNetInsertBulkLoader" /> (multi-row INSERT); a dialect may
///     register a loader built on its driver's native bulk API in <see cref="IAdoNetDialect.ConfigureServices" />.
/// </summary>
public interface IAdoNetBulkLoader
{
    void Load(BulkLoad load);

    Task LoadAsync(BulkLoad load, CancellationToken cancellationToken = default);
}

/// <summary>
///     One load: provider values (already converted), in column order. The connection is open and the transaction, which
///     the load must join, is either the caller's or one the core began for the load.
/// </summary>
public sealed class BulkLoad
{
    public required DbContext Context { get; init; }
    public required string Table { get; init; }
    public string? Schema { get; init; }
    public required IReadOnlyList<BulkColumn> Columns { get; init; }
    public required IReadOnlyList<object?[]> Rows { get; init; }
    public required DbConnection Connection { get; init; }
    public required DbTransaction Transaction { get; init; }
}

public sealed record BulkColumn(string Name, RelationalTypeMapping TypeMapping);
