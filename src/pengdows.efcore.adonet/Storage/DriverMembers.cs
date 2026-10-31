using System.Collections.Concurrent;
using System.Reflection;

namespace pengdows.efcore.adonet.Storage;

/// <summary>
///     Driver members the provider reads by name, because it has no reference to the driver that owns them (a driver's parameter type,
///     a geometry UDT name, an exception's error number). Each lookup is cached per type, so the reflection runs once; an absent member is
///     cached too, so a missing one costs nothing after the first lookup.
/// </summary>
public static class DriverMembers
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> Properties = new();
    private static readonly ConcurrentDictionary<(Type Type, string Name, Type[] Parameters), MethodInfo?> StaticMethods = new(new MethodKeyComparer());

    public static PropertyInfo? Property(Type type, string name)
        => Properties.GetOrAdd((type, name), key => key.Type.GetProperty(key.Name));

    public static MethodInfo? StaticMethod(Type type, string name, params Type[] parameters)
        => StaticMethods.GetOrAdd((type, name, parameters), key => key.Type.GetMethod(
            key.Name, BindingFlags.Public | BindingFlags.Static, key.Parameters));

    private sealed class MethodKeyComparer : IEqualityComparer<(Type Type, string Name, Type[] Parameters)>
    {
        public bool Equals((Type Type, string Name, Type[] Parameters) x, (Type Type, string Name, Type[] Parameters) y)
            => x.Type == y.Type && x.Name == y.Name && x.Parameters.AsSpan().SequenceEqual(y.Parameters);

        public int GetHashCode((Type Type, string Name, Type[] Parameters) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Type);
            hash.Add(obj.Name);
            foreach (var parameter in obj.Parameters)
            {
                hash.Add(parameter);
            }

            return hash.ToHashCode();
        }
    }
}
