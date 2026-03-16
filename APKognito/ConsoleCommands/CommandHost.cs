using System.Collections.ObjectModel;
using System.Reflection;
using APKognito.Base.MVVM;
using static APKognito.ViewModels.Pages.AdbConsoleViewModel;

namespace APKognito.ConsoleCommands;

public class CommandHost
{
    private readonly CommandParameterProvider _commandParameterService = new();

    internal ReadOnlyCollection<CommandInfo> Commands { get; init; }

    public CommandParameterProvider ParameterProvider => _commandParameterService;

    internal CommandHost(ReadOnlyCollection<CommandInfo> commands)
    {
        Commands = commands;
    }

    public CommandHost()
    {
        Commands = CommandRegistrar.GetCommands();
    }

    public async ValueTask<bool> RunCommandAsync(string command, IViewLogger logger, CancellationToken token = default)
    {
        command = command.TrimStart(':');

        ParsedCommand parsedCommand = new(command);

        CommandInfo? commandInfo = Commands.FirstOrDefault(c => c.CommandName == parsedCommand.Command);
        if (commandInfo is null)
        {
            return false;
        }

        await InvokeCommandAsync(commandInfo, parsedCommand, logger, token);

        return true;
    }

    internal async Task InvokeCommandDirectAsync(CommandInfo commandInfo, ParsedCommand parsedCommand, IViewLogger logger, CancellationToken token = default)
    {
        await InvokeCommandAsync(commandInfo, parsedCommand, logger, token);
    }

    private async Task InvokeCommandAsync(CommandInfo commandInfo, ParsedCommand parsedCommand, IViewLogger logger, CancellationToken token)
    {
        ParameterInfo[] parameters = commandInfo.CommandMethod.GetParameters();
        var arguments = new List<object?>();

        var callLocals = new Dictionary<Type, object>
        {
            [typeof(ParsedCommand)] = parsedCommand,
            [typeof(IViewLogger)] = logger,
            [typeof(CancellationToken)] = token,
            [typeof(List<CommandInfo>)] = Commands.ToList(),
        };

        foreach (ParameterInfo param in parameters)
        {
            Type paramType = param.ParameterType;

            object? service = paramType != typeof(ParsedCommand)
                ? callLocals.GetValueOrDefault(paramType) ?? _commandParameterService.GetService(paramType)
                : parsedCommand;

            if (service is null)
            {
                if (!param.HasDefaultValue)
                {
                    throw new InvalidOperationException($"No service registered for type {paramType.Name}");
                }

                service = param.DefaultValue;
            }

            arguments.Add(service);
        }

        try
        {
            object? target = null;

            /// I don't think any of the new commands would be in instance classes..
            // if (!commandInfo.CommandMethod.IsStatic)
            // {
            //     if (targetObject is null)
            //     {
            //         throw new InvalidOperationException("Unable to invoke command with null target object.");
            //     }
            // 
            //     target = targetObject;
            // }

            object? result = commandInfo.CommandMethod.Invoke(target, [.. arguments]);

            if (commandInfo.IsAsync && result is Task task)
            {
                await task;
            }
        }
        catch (TargetInvocationException ex)
        {
            logger.Log($"Command execution error: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            logger.Log($"An unexpected error occurred during command invocation: {ex.Message}");
        }
    }
}
