using System.Data.Common;
using pengdows.efcore.adonet.Storage;

namespace pengdows.efcore.adonet.Tests;

/// <summary>
///     Driver members are looked up by name on the hot path (a parameter's DbType, a geometry's UDT name, an exception's Number). The
///     lookup is cached per type, so the reflection runs once, and an absent member is cached as absent.
/// </summary>
public class DriverMembersTests
{
    private sealed class Driver
    {
        public int Number { get; set; }
        public static string Describe(int value) => "n" + value;
    }

    [Fact]
    public void A_property_lookup_returns_the_same_instance_each_time()
    {
        var first = DriverMembers.Property(typeof(Driver), "Number");
        var second = DriverMembers.Property(typeof(Driver), "Number");

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void An_absent_property_is_null_and_stays_null()
    {
        Assert.Null(DriverMembers.Property(typeof(Driver), "Missing"));
        Assert.Null(DriverMembers.Property(typeof(Driver), "Missing"));
    }

    [Fact]
    public void A_static_method_lookup_is_cached_by_name_and_parameter_types()
    {
        var first = DriverMembers.StaticMethod(typeof(Driver), "Describe", typeof(int));

        Assert.NotNull(first);
        Assert.Same(first, DriverMembers.StaticMethod(typeof(Driver), "Describe", typeof(int)));
        Assert.Null(DriverMembers.StaticMethod(typeof(Driver), "Describe", typeof(string)));
    }
}
