using Microsoft.EntityFrameworkCore.TestUtilities;

namespace pengdows.efcore.adonet.Specification.Tests;

/// <summary>Shared EF precompiled-query fixture wired to the provider-neutral store and compiler helpers.</summary>
public sealed class AdoNetPrecompiledQueryRelationalFixture<THost>
    : global::Microsoft.EntityFrameworkCore.Query.PrecompiledQueryRelationalFixture
    where THost : IDatabaseHost, new()
{
    public override PrecompiledQueryTestHelpers PrecompiledQueryTestHelpers
        => AdoNetPrecompiledQueryTestHelpers<THost>.Instance;

    protected override ITestStoreFactory TestStoreFactory
        => AdoNetTestStoreFactory<THost>.Instance;
}

/// <summary>Shared EF SQL-pregeneration fixture wired to the provider-neutral store and compiler helpers.</summary>
public sealed class AdoNetPrecompiledSqlPregenerationQueryRelationalFixture<THost>
    : global::Microsoft.EntityFrameworkCore.Query.PrecompiledSqlPregenerationQueryRelationalFixture
    where THost : IDatabaseHost, new()
{
    public override PrecompiledQueryTestHelpers PrecompiledQueryTestHelpers
        => AdoNetPrecompiledQueryTestHelpers<THost>.Instance;

    protected override ITestStoreFactory TestStoreFactory
        => AdoNetTestStoreFactory<THost>.Instance;
}
