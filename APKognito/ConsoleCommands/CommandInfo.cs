using System.Reflection;

namespace APKognito.ConsoleCommands;

internal sealed record CommandInfo
{
    public string CommandName { get; }
    public string HelpInfo { get; }
    public string CommandUsage { get; }
    public bool IsVisible { get; }

    internal MethodInfo CommandMethod { get; }

    public bool IsAsync => CommandMethod.ReturnType == typeof(Task) || CommandMethod.ReturnType.IsSubclassOf(typeof(Task));

    internal CommandInfo(CommandAttribute commandAttribute, MethodInfo commandMethod)
    {
        CommandName = commandAttribute.CommandName;
        HelpInfo = commandAttribute.HelpInfo;
        CommandUsage = commandAttribute.CommandUsage;
        IsVisible = commandAttribute.IsVisible;
        CommandMethod = commandMethod;
    }
}
