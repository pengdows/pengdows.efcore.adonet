using System.Reflection;
using System.Text.Json;

var names = JsonSerializer.Deserialize<List<List<JsonElement>>>(File.ReadAllText(args[0]))!.Select(e => e[0].GetString()!).ToList();
var asms = new[] { typeof(Microsoft.EntityFrameworkCore.Query.QueryTestBase<>).Assembly, typeof(Microsoft.EntityFrameworkCore.TestUtilities.RelationalTestStore).Assembly };
var types = asms.SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; } }).Cast<Type>().ToList();

// C# name; generic parameters print as their own names, so a constraint shows where the fixture refers back to TFixture.
string Cs(Type t)
{
    if (t.IsGenericParameter) return t.Name;
    if (t.IsArray) return Cs(t.GetElementType()!) + "[]";
    if (Nullable.GetUnderlyingType(t) is { } u) return Cs(u) + "?";
    var simple = t.FullName switch { "System.String" => "string", "System.Boolean" => "bool", "System.Int32" => "int", "System.Object" => "object", "System.Void" => "void", _ => null };
    if (simple != null) return simple;
    var name = t.Name.Split('`')[0];
    var allArgs = t.IsGenericType ? t.GetGenericArguments() : [];
    string prefix;
    int skip = 0;
    if (t.IsNested)
    {
        var decl = t.DeclaringType!;
        var declArity = decl.IsGenericTypeDefinition ? decl.GetGenericArguments().Length : 0;
        var declType = declArity > 0 ? decl.MakeGenericType(allArgs.Take(declArity).ToArray()) : decl;
        prefix = Cs(declType) + ".";
        skip = declArity;
    }
    else prefix = "global::" + (t.Namespace is { } ns ? ns + "." : "");
    var own = allArgs.Skip(skip).ToArray();
    return prefix + name + (own.Length > 0 ? "<" + string.Join(", ", own.Select(Cs)) + ">" : "");
}

object Member(MemberInfo m) => m switch
{
    MethodInfo mi => new { kind = "method", name = mi.Name, access = mi.IsPublic ? "public" : "protected", returns = Cs(mi.ReturnType), parameters = mi.GetParameters().Select(p => Cs(p.ParameterType) + " " + p.Name).ToArray(), isAsync = typeof(Task).IsAssignableFrom(mi.ReturnType), generic = mi.IsGenericMethodDefinition, declaring = mi.DeclaringType!.Name },
    PropertyInfo pi => new { kind = "property", name = pi.Name, access = pi.GetMethod!.IsPublic ? "public" : "protected", returns = Cs(pi.PropertyType), parameters = Array.Empty<string>(), isAsync = false, generic = false, declaring = pi.DeclaringType!.Name },
    _ => new { kind = "?", name = m.Name, access = "", returns = "", parameters = Array.Empty<string>(), isAsync = false, generic = false, declaring = "" }
};

IEnumerable<MemberInfo> Abstracts(Type x) => x.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
    .Where(m => (m is MethodInfo mi && mi.IsAbstract && !mi.IsSpecialName && (mi.IsPublic || mi.IsFamily || mi.IsFamilyOrAssembly)) || (m is PropertyInfo pi && (pi.GetMethod?.IsAbstract ?? false)));

var result = new List<object>();
foreach (var name in names)
{
    var candidates = types.Where(x => x.Name.Split('`')[0] == name && (x.IsPublic || x.IsNestedPublic)).OrderBy(x => x.Assembly.GetName().Name!.Contains("Relational") ? 0 : 1).ToList();
    var t = candidates.FirstOrDefault();
    if (t == null) { result.Add(new { name, missing = true }); continue; }
    var generics = t.IsGenericTypeDefinition ? t.GetGenericArguments() : [];
    result.Add(new
    {
        name,
        cs = Cs(t),
        nested = t.IsNested,
        @namespace = t.Namespace,
        generic = generics.Select(g => new { g.Name, constraints = g.GetGenericParameterConstraints().Select(c => new { cs = Cs(c), isClass = c.IsClass, isAbstract = c.IsAbstract, abstracts = c.IsClass ? Abstracts(c).Select(Member).ToArray() : [], listLogger = c.IsClass && (c.GetProperty("ListLoggerFactory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null) }).ToArray() }).ToArray(),
        ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(c => !c.IsPrivate).Select(c => c.GetParameters().Select(p => Cs(p.ParameterType) + " " + p.Name).ToArray()).ToArray(),
        ctorFixtures = t.IsGenericTypeDefinition ? [] : t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(c => !c.IsPrivate)
            .SelectMany(c => c.GetParameters()).Where(p => p.ParameterType.IsClass && p.ParameterType.Name.Contains("Fixture"))
            .Select(p => new { cs = Cs(p.ParameterType), isAbstract = p.ParameterType.IsAbstract, abstracts = Abstracts(p.ParameterType).Select(Member).ToArray() }).ToArray(),
        abstracts = Abstracts(t).Select(Member).ToArray(),
        // Overridable members a provider is expected to fill in, though they have a default (UseTransaction does nothing).
        virtuals = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.IsVirtual && !m.IsAbstract && !m.IsFinal && (m.IsFamily || m.IsPublic) && m.DeclaringType != typeof(object))
            .Select(m => m.Name).Distinct().OrderBy(n => n).ToArray()
    });
}
File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{result.Count} written");
