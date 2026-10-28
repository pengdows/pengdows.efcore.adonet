// Behaviour reference (not copied): EF Core's SQL Server transient exception policy (MIT, .NET Foundation):
// https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerTransientExceptionDetector.cs

using System.ComponentModel;
using Microsoft.Data.SqlClient;

namespace pengdows.efcore.adonet.sqlserver;

/// <summary>
///     SqlClient doesn't report <c>DbException.IsTransient</c>, so SQL Server's transient errors are the error numbers EF's
///     own provider retries: deadlocks, throttling, failovers, dropped connections.
/// </summary>
internal sealed record SqlServerCapabilities : DatabaseCapabilities
{
    private static readonly HashSet<int> TransientNumbers =
    [
        49983, 49977, 49920, 49919, 49918, 49802, 49510, 47139, 47137, 47132, 45547, 45319, 45182, 45179, 45161, 45157, 45156,
        45153, 42109, 42108, 42029, 41840, 41839, 41823, 41701, 41700, 41640, 41619, 41614, 41383, 41339, 41325, 41305, 41302,
        41301, 40938, 40918, 40903, 40890, 40675, 40671, 40648, 40642, 40623, 40613, 40540, 40501, 40197, 40189, 40143, 40106,
        39152, 39151, 39110, 39108, 39025, 37327, 37202, 35293, 35256, 35218, 35216, 33123, 30085, 30080, 25740, 25738, 25003,
        22984, 22760, 22759, 22758, 22754, 22498, 22493, 22491, 22430, 22427, 22358, 22355, 22353, 22335, 22226, 22225, 21503,
        20041, 19494, 19416, 19413, 18858, 18401, 17889, 17197, 17067, 17066, 17065, 16555, 16554, 16528, 14906, 14868, 14817,
        14355, 11539, 11001, 10936, 10930, 10929, 10928, 10922, 10060, 10054, 10053, 9985, 9515, 9020, 8651, 8645, 8628, 7951,
        6292, 5529, 5280, 4221, 3980, 4184, 4117, 4060, 3966, 3960, 3957, 3953, 3950, 3948, 3947, 3941, 3935, 3635, 3429, 2816,
        2021, 1807, 1535, 1534, 1533, 1532, 1438, 1421, 1413, 1404, 1232, 1222, 1221, 1216, 1215, 1205, 1204, 1203, 997, 988,
        982, 952, 941, 927, 926, 921, 669, 617, 615, 601, 539, 233, 121, 64, 20
    ];

    // A connection that couldn't be made (a network-level failure, error 203 with a Win32 cause) is worth retrying too.
    private const int ConnectionFailed = 203;

    // SqlClient holds several open readers on a connection only with MultipleActiveResultSets, which each connection string
    // chooses.
    public override bool SupportsMultipleActiveReadersFor(string? connectionString)
        => (connectionString != null) && new SqlConnectionStringBuilder(connectionString).MultipleActiveResultSets;

    public override bool IsTransient(Exception exception)
        => base.IsTransient(exception)
            || ((exception is SqlException sql)
                && HasRetryableError(sql, exception.InnerException));

    private static bool HasRetryableError(SqlException exception, Exception? innerException)
        => exception.Errors.Cast<SqlError>().Any(error => IsRetryableNumber(error.Number, innerException));

    private static bool IsRetryableNumber(int number, Exception? innerException)
        => TransientNumbers.Contains(number)
            || ((number == ConnectionFailed) && (innerException is Win32Exception));
}
