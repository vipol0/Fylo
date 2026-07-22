using System;
using System.Globalization;
using System.Windows.Data;

namespace Fylo.Converters
{
    public sealed class IsDirectoryToIconConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isDirectory)
                return isDirectory ? "\uD83D\uDCC1" : "\uD83D\uDCC4";

            return "\uD83D\uDCC4";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
