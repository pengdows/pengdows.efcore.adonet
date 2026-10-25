using System.Globalization;
using System.Runtime.CompilerServices;

internal static class ModuleInitializer
{
    // EF's expected data and messages are en-US, as in EF's own test projects.
    [ModuleInitializer]
    internal static void Initialize()
    {
        var culture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
