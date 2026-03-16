using System.Globalization;
using System.Windows.Data;

namespace APKognito.Helpers;

public sealed class NotNullToTrueConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v is not null;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}
