namespace pengdows.efcore.adonet.Specification.Tests;

// EF's base fixture makes every warning an error, and this suite's model keys entities by collections with converters and no
// comparers, which warn. Every upstream provider's fixture replaces the base options (setting only its batch size), so the
// warning is logged instead. Behaviour reference (not copied): https://github.com/dotnet/efcore/blob/v10.0.11/test/EFCore.Sqlite.FunctionalTests/KeysWithConvertersSqliteTest.cs
public abstract partial class KeysWithConvertersAdoNetTest<THost>
{
    public partial class AdoNetFixture
    {
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => builder;
    }
}
