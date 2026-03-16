using System.Text;

namespace APKognito.Base.MVVM;

public sealed class LogBuilder
{
    private readonly StringBuilder _sb = new();

    public LogBuilder()
    {
    }

    public LogBuilder(string initial)
    {
        _ = _sb.Append(initial);
    }

    private LogBuilder(StringBuilder sb)
    {
        _sb = sb;
    }

    public LogBuilder Append(string text)
    {
        _ = _sb.Append(text);
        return this;
    }

    public LogBuilder AppendIf(bool condition, string text)
    {
        if (condition)
        {
            _ = _sb.Append(text);
        }

        return this;
    }

    public LogBuilder AppendConcatIf(bool condition, params IEnumerable<string?> strings)
    {
        if (condition)
        {
            string text = string.Concat(strings);
            _sb.Append(text);
        }

        return this;
    }

    public LogBuilder AppendJoin<T>(string separator, params IEnumerable<T> values)
    {
        string text = string.Join(separator, values);
        _ = _sb.Append(text);

        return this;
    }

    public LogBuilder AppendJoin<T>(char separator, params IEnumerable<T> strings)
    {
        string text = string.Join(separator, strings);
        _ = _sb.Append(text);

        return this;
    }

    public string Build()
    {
        return _sb.ToString();
    }

    public override string ToString()
    {
        return Build();
    }

    public static implicit operator StringBuilder(LogBuilder builder)
    {
        return builder._sb;
    }

    public static implicit operator LogBuilder(StringBuilder builder)
    {
        return new(builder);
    }
}
