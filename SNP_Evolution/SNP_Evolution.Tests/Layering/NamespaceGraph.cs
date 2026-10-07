using System.Reflection;
using System.Text.RegularExpressions;

namespace SnpEvolution.Tests.Layering
{
    // Which of the program's namespaces each one depends on: those its files name in using lines, and those whose types
    // its compiled code refers to, which catches a reference to a parent namespace that needs no using.
    internal static partial class NamespaceGraph
    {
        private const string Root = "SnpEvolution";

        // Why each edge is there: the using lines and type references that make it, a few of each.
        public static string Reasons(string from, string to) =>
            string.Join(", ", UsingLines().Where(edge => edge.From == from && edge.To == to).Select(_ => "a using line").Distinct()
                .Concat(TypeReferencesWithTypes().Where(edge => edge.From == from && edge.To == to).Select(edge => $"{edge.FromType} uses {edge.ToType}").Distinct().Take(3)));

        public static Dictionary<string, SortedSet<string>> Build()
        {
            var graph = new Dictionary<string, SortedSet<string>>();
            foreach ((string from, string to) in UsingLines().Concat(TypeReferences()))
            {
                if (from != to)
                {
                    Edges(graph, from).Add(to);
                    Edges(graph, to);
                }
            }
            return graph;
        }

        // Each cycle once, as the namespaces around it, found from the strongly connected parts of the graph.
        public static List<List<string>> Cycles(Dictionary<string, SortedSet<string>> graph)
        {
            var index = new Dictionary<string, int>();
            var low = new Dictionary<string, int>();
            var stack = new Stack<string>();
            var cycles = new List<List<string>>();
            foreach (string node in graph.Keys.Order(StringComparer.Ordinal))
            {
                if (!index.ContainsKey(node))
                {
                    Visit(node);
                }
            }
            return cycles;

            void Visit(string node)
            {
                index[node] = low[node] = index.Count;
                stack.Push(node);
                foreach (string next in graph[node])
                {
                    if (!index.ContainsKey(next))
                    {
                        Visit(next);
                        low[node] = Math.Min(low[node], low[next]);
                    }
                    else if (stack.Contains(next))
                    {
                        low[node] = Math.Min(low[node], index[next]);
                    }
                }
                if (low[node] == index[node])
                {
                    var component = new List<string>();
                    string member;
                    do
                    {
                        member = stack.Pop();
                        component.Add(member);
                    }
                    while (member != node);
                    if (component.Count > 1)
                    {
                        cycles.Add(component.Order(StringComparer.Ordinal).ToList());
                    }
                }
            }
        }

        private static SortedSet<string> Edges(Dictionary<string, SortedSet<string>> graph, string node) =>
            graph.TryGetValue(node, out SortedSet<string>? edges) ? edges : graph[node] = new SortedSet<string>(StringComparer.Ordinal);

        private static IEnumerable<(string From, string To)> UsingLines()
        {
            foreach (string path in Layers.Projects.SelectMany(project => Directory.GetFiles(Layers.Folder(project), "*.cs", SearchOption.AllDirectories)).Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            {
                string text = File.ReadAllText(path);
                if (NamespaceLine().Match(text) is not { Success: true } declared)
                {
                    continue;
                }
                foreach (Match used in UsingLine().Matches(text))
                {
                    // A using static names a type, which lives in the namespace before its last part.
                    string name = used.Groups[2].Value;
                    yield return (declared.Groups[1].Value, used.Groups[1].Success ? name[..name.LastIndexOf('.')] : name);
                }
            }
        }

        private static IEnumerable<(string From, string To)> TypeReferences() =>
            TypeReferencesWithTypes().Select(edge => (edge.From, edge.To));

        // Each type reference across namespaces, with the types at each end, to say why an edge is there.
        private static IEnumerable<(string From, string To, string FromType, string ToType)> TypeReferencesWithTypes()
        {
            Assembly[] assemblies = Layers.Projects.Select(Layers.Assembly).ToArray();
            foreach (Type type in assemblies.SelectMany(assembly => assembly.GetTypes()))
            {
                string? from = OwnNamespace(type);
                if (from == null)
                {
                    continue;
                }
                foreach (Type referenced in Referenced(type))
                {
                    foreach (Type part in Parts(referenced))
                    {
                        if (assemblies.Contains(part.Assembly) && OwnNamespace(part) is string to && to != from)
                        {
                            yield return (from, to, Outermost(type).Name, Outermost(part).Name);
                        }
                    }
                }
            }
        }

        private static Type Outermost(Type type)
        {
            while (type.DeclaringType != null)
            {
                type = type.DeclaringType;
            }
            return type;
        }

        // A nested or compiler-made type belongs to the namespace of the type it is declared in.
        private static string? OwnNamespace(Type type)
        {
            type = Outermost(type);
            return type.Namespace is string name && (name == Root || name.StartsWith(Root + ".", StringComparison.Ordinal)) ? name : null;
        }

        private static IEnumerable<Type> Parts(Type type)
        {
            if (type.HasElementType)
            {
                return Parts(type.GetElementType()!);
            }
            return type.IsGenericType ? new[] { type.GetGenericTypeDefinition() }.Concat(type.GetGenericArguments().SelectMany(Parts)) : new[] { type };
        }

        private static IEnumerable<Type> Referenced(Type type)
        {
            const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var types = new List<Type>();
            if (type.BaseType != null)
            {
                types.Add(type.BaseType);
            }
            types.AddRange(type.GetInterfaces());
            types.AddRange(type.GetCustomAttributesData().Select(attribute => attribute.AttributeType));
            types.AddRange(type.GetFields(Declared).Select(field => field.FieldType));
            types.AddRange(type.GetProperties(Declared).Select(property => property.PropertyType));
            foreach (MethodBase method in type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
            {
                if (method is MethodInfo info)
                {
                    types.Add(info.ReturnType);
                }
                types.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
                types.AddRange(MethodBodyTypes.Of(method));
            }
            return types.Where(each => !each.IsGenericParameter);
        }

        [GeneratedRegex(@"^namespace\s+([\w.]+)", RegexOptions.Multiline)]
        private static partial Regex NamespaceLine();

        [GeneratedRegex(@"^using\s+(static\s+)?(SnpEvolution[\w.]*)\s*;", RegexOptions.Multiline)]
        private static partial Regex UsingLine();
    }
}
