using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework(
    "pengdows.efcore.adonet.Specification.Tests.CapabilitySkipTestFramework", "pengdows.efcore.adonet.Specification.Tests")]

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>
///     A suite test whose query the provider refuses for want of something the dialect says the database hasn't got is skipped,
///     naming the database, rather than failed: a lateral join, where <see cref="ISqlDialect.LateralJoins" /> is null, and a
///     model mapped to stored procedures, where <see cref="ISqlDialect.SupportsStoredProcedures" /> is false. The refusals
///     themselves are the core's and conformance tests' to check (QueryGrammarTests, StoredProcedureCapabilityTests).
/// </summary>
public sealed class CapabilitySkips : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
        => Add(services);

    public void Validate(IDbContextOptions options)
    {
    }

    // Replaces the provider's factory whether or not the provider has registered it yet: its own registration only adds one
    // where none is.
    public static void Add(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IQueryTranslationPostprocessorFactory, SkippingPostprocessorFactory>());
        services.Replace(ServiceDescriptor.Singleton<IModelValidator, SkippingModelValidator>());
    }

    /// <summary>
    ///     A suite test that needs the host's procedures, where it has none: skipped where the database has none, failed where
    ///     the host hasn't written them yet.
    /// </summary>
    public static Exception NoProcedures<THost>(string what)
        where THost : IDatabaseHost, new()
    {
        var dialect = new THost().Dialect;
        return dialect.Sql.SupportsStoredProcedures
            ? new NotSupportedException($"{typeof(THost).Name} has no {what} written yet.")
            : new CapabilitySkipException($"[SKIP:{dialect.Name}] {dialect.Name} has no stored procedures.");
    }

    private sealed class SkippingModelValidator(
        ModelValidatorDependencies dependencies,
        RelationalModelValidatorDependencies relationalDependencies,
        IAdoNetDialect dialect) : IModelValidator
    {
        private readonly pengdows.efcore.adonet.Infrastructure.AdoNetModelValidator _provider
            = new(dependencies, relationalDependencies, dialect);

        public void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            try
            {
                _provider.Validate(model, logger);
            }
            catch (pengdows.efcore.adonet.Infrastructure.StoredProceduresNotSupportedException e)
            {
                throw new CapabilitySkipException($"[SKIP:{dialect.Name}] {e.Message}");
            }

        }
    }

    private sealed class SkippingPostprocessorFactory(
        QueryTranslationPostprocessorDependencies dependencies,
        RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
        IAdoNetDialect dialect) : IQueryTranslationPostprocessorFactory
    {
        private readonly pengdows.efcore.adonet.Query.AdoNetQueryTranslationPostprocessorFactory _provider
            = new(dependencies, relationalDependencies, dialect);

        public QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
            => new SkippingPostprocessor(dependencies, queryCompilationContext, _provider.Create(queryCompilationContext), dialect);
    }

    private sealed class SkippingPostprocessor(
        QueryTranslationPostprocessorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        QueryTranslationPostprocessor provider,
        IAdoNetDialect dialect)
        : QueryTranslationPostprocessor(dependencies, queryCompilationContext)
    {
        public override Expression Process(Expression query)
        {
            try
            {
                return provider.Process(query);
            }
            catch (pengdows.efcore.adonet.Query.LateralJoinNotSupportedException e)
            {
                throw new CapabilitySkipException($"[SKIP:{dialect.Name}] {e.Message}");
            }
        }
    }

    private sealed class ExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider
            => false;

        public override string LogFragment
            => "skipping what the dialect lacks ";

        public override int GetServiceProviderHashCode()
            => 0;

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["CapabilitySkips"] = "1";
    }
}

/// <summary>Thrown from inside a test for something the database's dialect says it hasn't got: the test is reported skipped.</summary>
public sealed class CapabilitySkipException(string reason) : Exception(reason);

/// <summary>
///     xUnit's own framework, but a test that fails with a <see cref="CapabilitySkipException" />, or with one inside its failure,
///     is reported skipped, with its message as the reason, and every count above it moved from failed to skipped. xUnit 2 has no skip from inside a test
///     (EF's specification tests are on xUnit 2). Behaviour reference (not copied), the same message-bus conversion:
///     https://github.com/AArnott/Xunit.SkippableFact/blob/v1.5.85/src/Xunit.SkippableFact/Sdk/SkippableTestMessageBus.cs
/// </summary>
public sealed class CapabilitySkipTestFramework(IMessageSink messageSink) : XunitTestFramework(messageSink)
{
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        => new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    private sealed class Executor(AssemblyName assemblyName, ISourceInformationProvider sourceInformationProvider, IMessageSink diagnosticMessageSink)
        : XunitTestFrameworkExecutor(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
        protected override async void RunTestCases(
            IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
        {
            using var runner = new AssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
            await runner.RunAsync();
        }
    }

    private sealed class AssemblyRunner(
        ITestAssembly testAssembly,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
        : XunitTestAssemblyRunner(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
    {
        protected override async Task<RunSummary> RunTestCollectionAsync(
            IMessageBus messageBus, ITestCollection testCollection, IEnumerable<IXunitTestCase> testCases, CancellationTokenSource cancellationTokenSource)
        {
            using var skipping = new SkippingMessageBus(messageBus);
            var summary = await base.RunTestCollectionAsync(skipping, testCollection, testCases, cancellationTokenSource);
            var skipped = skipping.SkippedIn(testCollection);
            summary.Failed -= skipped;
            summary.Skipped += skipped;
            return summary;
        }
    }

    /// <summary>Turns each capability failure into a skip, and corrects the finished counts of its case, method, class and collection.</summary>
    internal sealed class SkippingMessageBus(IMessageBus inner) : IMessageBus
    {
        private static readonly string SkipType = typeof(CapabilitySkipException).FullName!;

        private readonly ConcurrentDictionary<object, int> _skipped = new(ReferenceEqualityComparer.Instance);

        public int SkippedIn(object scope)
            => _skipped.GetValueOrDefault(scope);

        public bool QueueMessage(IMessageSinkMessage message)
            => inner.QueueMessage(Convert(message));

        public void Dispose()
        {
        }

        private IMessageSinkMessage Convert(IMessageSinkMessage message)
        {
            switch (message)
            {
                // Anywhere in the chain: an assertion expecting another failure gets the refusal as its inner exception.
                case ITestFailed failed when Array.IndexOf(failed.ExceptionTypes, SkipType) is var at and >= 0:
                    foreach (var scope in new object[] { failed.TestCase, failed.TestMethod, failed.TestClass, failed.TestCollection })
                    {
                        _skipped.AddOrUpdate(scope, 1, (_, count) => count + 1);
                    }

                    return new TestSkipped(failed.Test, failed.Messages[at]);
                case ITestCaseFinished finished when SkippedIn(finished.TestCase) is var n and > 0:
                    return new TestCaseFinished(
                        finished.TestCase, finished.ExecutionTime, finished.TestsRun, finished.TestsFailed - n, finished.TestsSkipped + n);
                case ITestMethodFinished finished when SkippedIn(finished.TestMethod) is var n and > 0:
                    return new TestMethodFinished(
                        finished.TestCases, finished.TestMethod, finished.ExecutionTime, finished.TestsRun, finished.TestsFailed - n,
                        finished.TestsSkipped + n);
                case ITestClassFinished finished when SkippedIn(finished.TestClass) is var n and > 0:
                    return new TestClassFinished(
                        finished.TestCases, finished.TestClass, finished.ExecutionTime, finished.TestsRun, finished.TestsFailed - n,
                        finished.TestsSkipped + n);
                case ITestCollectionFinished finished when SkippedIn(finished.TestCollection) is var n and > 0:
                    return new TestCollectionFinished(
                        finished.TestCases, finished.TestCollection, finished.ExecutionTime, finished.TestsRun, finished.TestsFailed - n,
                        finished.TestsSkipped + n);
                default:
                    return message;
            }
        }
    }
}
