namespace APKognito.ConsoleCommands;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class CommandAttribute : Attribute
{
    public const string NO_USAGE = "";

    public string CommandName { get; }

    public string HelpInfo { get; }

    public string CommandUsage { get; }

    public bool IsVisible { get; }

    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "It's literally used in this class, how does Sonar not see that?")]
    [SuppressMessage("CodeQuality",
        "IDE0079:Remove unnecessary suppression",
        Justification = "Without the suppression, there's a warning for the suppression that should suppress another warning. Am I having a stroke?")]
    public CommandAttribute(string commandName, string helpInfo, string usage = NO_USAGE, bool visible = true)
    {
        CommandName = commandName;
        HelpInfo = helpInfo;
        CommandUsage = usage;
        IsVisible = visible;
    }

    /// <summary>
    /// Creates an invisible command. It will not be listed when using the ':help' command.
    /// </summary>
    /// <param name="commandName"></param>
    [SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "")]
    [SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "")]
    public CommandAttribute(string commandName)
    {
        CommandName = commandName;
        HelpInfo = CommandUsage = string.Empty;
        IsVisible = false;
    }

    public override string ToString()
    {
        return CommandName;
    }
}
