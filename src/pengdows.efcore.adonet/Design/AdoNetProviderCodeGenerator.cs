using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using pengdows.efcore.adonet.Infrastructure;

namespace pengdows.efcore.adonet.Design;

/// <summary>
///     The call scaffolding writes into <c>OnConfiguring</c>: the plugin's own <c>UseAdoNet...</c> entry point with the
///     connection string, and the provider options, if any, in a nested closure. Behaviour reference (not copied):
///     https://github.com/dotnet/efcore/blob/v10.0.11/src/EFCore.SqlServer/Scaffolding/Internal/SqlServerCodeGenerator.cs
/// </summary>
public class AdoNetProviderCodeGenerator(ProviderCodeGeneratorDependencies dependencies, MethodInfo useProvider)
    : ProviderCodeGenerator(dependencies)
{
    /// <summary>
    ///     A plugin's entry point as scaffolding calls it:
    ///     <c>static DbContextOptionsBuilder Name(this DbContextOptionsBuilder, string, Action&lt;AdoNetDbContextOptionsBuilder&gt;?)</c>
    ///     on <paramref name="extensions" />.
    /// </summary>
    public static MethodInfo UseMethod(Type extensions, string name)
        => extensions.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                [typeof(DbContextOptionsBuilder), typeof(string), typeof(Action<AdoNetDbContextOptionsBuilder>)])
            ?? throw new ArgumentException($"{extensions.Name} has no {name}(DbContextOptionsBuilder, string, Action<AdoNetDbContextOptionsBuilder>).", nameof(name));

    public override MethodCallCodeFragment GenerateUseProvider(string connectionString, MethodCallCodeFragment? providerOptions)
        => new(
            useProvider,
            providerOptions == null
                ? [connectionString]
                : [connectionString, new NestedClosureCodeFragment("x", providerOptions)]);
}
