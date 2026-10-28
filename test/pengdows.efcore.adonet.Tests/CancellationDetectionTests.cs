using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Drivers report a canceled command in their own exceptions (SqlClient a SqlException, Npgsql a PostgresException
///     57014, MySqlConnector a MySqlException): any failure once the caller's token is canceled is the cancellation, so EF
///     logs it as one rather than as an error. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Storage/Internal/SqlServerExceptionDetector.cs
/// </summary>
public class CancellationDetectionTests
{
    private static IExceptionDetector Detector()
        => new FakeDatabase().Context(new PartsDialect("Cancellation")).GetService<IExceptionDetector>();

    [Fact]
    public void A_driver_failure_after_the_token_is_canceled_is_a_cancellation()
        => Assert.True(Detector().IsCancellation(new InvalidOperationException("driver"), new CancellationToken(canceled: true)));

    [Fact]
    public void An_operation_canceled_exception_is_a_cancellation_whatever_the_token()
        => Assert.True(Detector().IsCancellation(new TaskCanceledException(), CancellationToken.None));

    [Fact]
    public void A_failure_with_the_token_still_live_is_not()
        => Assert.False(Detector().IsCancellation(new InvalidOperationException("driver"), new CancellationTokenSource().Token));
}
