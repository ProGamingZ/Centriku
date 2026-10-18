using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Centriku.Converters
{
    public class SafeDoubleConverter : IValueConverter
    {
        // Source (C# double) -> Target (NumericUpDown decimal?)
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d) return (decimal)d;
            if (value is int i) return (decimal)i;
            if (value is decimal dec) return dec;
            return 0m;
        }

        // Target (NumericUpDown decimal?) -> Source (C# double)
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // When the user backspaces the field to empty, ignore the null update instead of crashing!
            if (value is null) 
                return BindingOperations.DoNothing;

            if (value is decimal dec) return (double)dec;
            if (value is double d) return d;

            if (double.TryParse(value.ToString(), out double parsed))
                return parsed;

            return BindingOperations.DoNothing;
        }
    }
}