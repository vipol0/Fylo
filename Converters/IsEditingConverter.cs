using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Fylo.Converters
{
    public sealed class IsEditingConverter : IMultiValueConverter
    {
        public static readonly IsEditingConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var entryPath = values[0] as string;
            var editingPath = values[1] as string;

            var isEditing = !string.IsNullOrEmpty(editingPath) &&
                            string.Equals(entryPath, editingPath, StringComparison.OrdinalIgnoreCase);

            var isInverted = parameter is string mode && mode == "Invert";
            var show = isInverted ? !isEditing : isEditing;

            return show ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
