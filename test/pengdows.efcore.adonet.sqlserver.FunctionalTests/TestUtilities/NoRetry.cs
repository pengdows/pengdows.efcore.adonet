namespace Microsoft.EntityFrameworkCore.TestUtilities;

/// <summary>pengdows: stands in for TestSqlServerRetryingExecutionStrategy until the plugin's retrying strategy is ported.</summary>
public sealed class NoRetry
{
    public static readonly NoRetry Instance = new();

    public void Execute<TState>(TState state, Action<TState> operation)
        => operation(state);

    public TResult Execute<TState, TResult>(TState state, Func<TState, TResult> operation)
        => operation(state);

    public Task ExecuteAsync<TState>(TState state, Func<TState, Task> operation)
        => operation(state);

    public Task<TResult> ExecuteAsync<TState, TResult>(TState state, Func<TState, Task<TResult>> operation)
        => operation(state);
}
