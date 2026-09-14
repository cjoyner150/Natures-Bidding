using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Tag any STATIC method with this to expose it as a console command.
/// Parameters are parsed automatically from the method signature.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ConsoleCommandAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    public ConsoleCommandAttribute(string name, string description = "")
    {
        Name = name;
        Description = description;
    }
}

public class ConsoleCommand
{
    public string Name;
    public string Description;
    public MethodInfo Method;
    public ParameterInfo[] Parameters;

    public string Usage => Parameters.Length == 0
        ? Name
        : $"{Name} " + string.Join(" ", Parameters.Select(p =>
            p.HasDefaultValue ? $"[{p.Name}:{p.ParameterType.Name}]" : $"<{p.Name}:{p.ParameterType.Name}>"));
}

public static class CommandRegistry
{
    private static readonly Dictionary<string, List<ConsoleCommand>> _commands =
        new Dictionary<string, List<ConsoleCommand>>(StringComparer.OrdinalIgnoreCase);

    // Add parsers here for any parameter type you want to support.
    private static readonly Dictionary<Type, Func<string, object>> _parsers = new Dictionary<Type, Func<string, object>>
    {
        { typeof(string), s => s },
        { typeof(int),    s => int.Parse(s) },
        { typeof(float),  s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture) },
        { typeof(bool),   s => s.ToLower() is "true" or "1" or "on" or "yes" },
        { typeof(ulong),  s => ulong.Parse(s) },
        { typeof(Vector3), s => { var p = s.Split(','); return new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])); } },
    };

    private static bool _initialized;

    public static IEnumerable<ConsoleCommand> All => _commands.Values.SelectMany(list => list);

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

            foreach (var type in types)
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var attr = method.GetCustomAttribute<ConsoleCommandAttribute>();
                    if (attr == null) continue;

                    if (!_commands.TryGetValue(attr.Name, out var list))
                        _commands[attr.Name] = list = new List<ConsoleCommand>();

                    list.Add(new ConsoleCommand
                    {
                        Name = attr.Name,
                        Description = attr.Description,
                        Method = method,
                        Parameters = method.GetParameters()
                    });
                }
        }

        // Sort overloads by specificity: fewer string parameters = more specific.
        foreach (var list in _commands.Values)
            list.Sort((a, b) => CountStringParams(a).CompareTo(CountStringParams(b)));
    }

    private static int CountStringParams(ConsoleCommand c) =>
        c.Parameters.Count(p => p.ParameterType == typeof(string));

    public static bool TryExecute(string line, out string output)
    {
        string[] tokens = Tokenize(line);
        if (tokens.Length == 0) { output = ""; return false; }

        if (!_commands.TryGetValue(tokens[0], out var candidates))
        {
            output = $"Unknown command: {tokens[0]}";
            return false;
        }

        string[] args = tokens.Skip(1).ToArray();

        // Try each overload in specificity order; first one whose parameters
        // all parse successfully wins.
        foreach (var cmd in candidates)
        {
            if (TryBindArguments(cmd, args, out object[] parsed))
                return Invoke(cmd, parsed, line, out output);
        }

        output = "No overload matched. Usage:\n" + string.Join("\n", candidates.Select(c => "  " + c.Usage));
        return false;
    }

    private static bool TryBindArguments(ConsoleCommand cmd, string[] args, out object[] parsed)
    {
        var parameters = cmd.Parameters;
        parsed = null;

        int required = parameters.Count(p => !p.HasDefaultValue);
        if (args.Length < required || args.Length > parameters.Length) return false;

        parsed = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            if (i >= args.Length) { parsed[i] = param.DefaultValue; continue; }

            Type t = param.ParameterType;
            try
            {
                if (t.IsEnum) parsed[i] = Enum.Parse(t, args[i], ignoreCase: true);
                else if (_parsers.TryGetValue(t, out var parser)) parsed[i] = parser(args[i]);
                else return false;
            }
            catch { return false; }   // this overload can't take these args — try the next one
        }
        return true;
    }

    private static bool Invoke(ConsoleCommand cmd, object[] parsed, string line, out string output)
    {
        try
        {
            object result = cmd.Method.Invoke(null, parsed);
            output = result != null ? result.ToString() : $"> {line}";
            return true;
        }
        catch (TargetInvocationException e)
        {
            output = $"Command threw: {e.InnerException?.Message ?? e.Message}";
            GameLogger.LogException(LogSeverity.Error, $"{cmd.Name} threw an exception.", e.InnerException ?? e);
            return false;
        }
    }

    /// <summary>Splits on whitespace but keeps quoted strings together: give "Milk Man" 50</summary>
    private static string[] Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens.ToArray();
    }
}
