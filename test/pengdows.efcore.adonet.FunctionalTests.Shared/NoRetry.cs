namespace pengdows.efcore.adonet.FunctionalTests;

/// <summary>Stands in for the official providers' test retrying strategies until a plugin ports its retrying strategy.</summary>
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
