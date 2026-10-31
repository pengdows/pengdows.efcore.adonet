using pengdows.efcore.adonet;

namespace OutsidePlugin;

// A dialect in another assembly, written against the packed core's public surface only.
public sealed class ExampleDialect : AdoNetDialect
{
    public static readonly ExampleDialect Instance = new();

    public override string Name => "Example";
}
