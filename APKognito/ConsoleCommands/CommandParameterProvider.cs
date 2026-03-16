using System.Collections.ObjectModel;

namespace APKognito.ConsoleCommands;

public class CommandParameterProvider
{
    private readonly Dictionary<Type, object> _parameters = [];

    public int ParamCount => _parameters.Count;

    public ReadOnlyDictionary<Type, object> GetParams => _parameters.AsReadOnly();

    public void Register<T>(T instance) where T : class
    {
        _parameters[typeof(T)] = instance;
    }

    public object? GetService(Type type)
    {
        return _parameters.TryGetValue(type, out object? service)
            ? service
            : null;
    }
}
