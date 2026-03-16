using System.Collections.ObjectModel;
using System.Reflection;
using APKognito.Exceptions;

namespace APKognito.ConsoleCommands;

public static class CommandRegistrar
{
    private static List<CommandInfo>? s_commands;

    internal static ReadOnlyCollection<CommandInfo> GetCommands()
    {
        if (s_commands is null)
        {
            RegisterCommands();
        }

        return s_commands!.AsReadOnly();
    }

    public static CommandHost CreateHost()
    {
        return new CommandHost(GetCommands());
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields", Justification = "It's okay here.")]
    private static void RegisterCommands()
    {
        string currentNamespace = typeof(CommandRegistrar).Namespace;

#if DEBUG
        if (currentNamespace is null)
        {
            throw new DeveloperErrorException($"{nameof(CommandRegistrar)} isn't inside a namespace.");
        }
#endif

        IEnumerable<Type> types = Assembly.GetExecutingAssembly().GetTypes().Where(t => string.Equals(t.Namespace, currentNamespace, StringComparison.Ordinal));
        IEnumerable<MethodInfo> methods = types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        s_commands = [];

        foreach (MethodInfo method in methods)
        {
            CommandAttribute? commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttribute != null)
            {
                var commandInfo = new CommandInfo(commandAttribute, method);
                s_commands.Add(commandInfo);
            }
        }
    }
}
