using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AssetRegistryModMigrator.Classes
{
    public class UnrealClassSplitConverter :IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string raw = value?.ToString()?.Trim().Trim('\'', '"') ?? string.Empty;
            string mode = parameter?.ToString() ?? "Class";

            int dotIndex = raw.IndexOf('.');
            if (dotIndex <= 0 || dotIndex >= raw.Length - 1)
            {
                // Fallback for strings without a module prefix (e.g., "No Native Parent")
                return mode switch
                {
                    "Module" => string.Empty,
                    "HasModule" => Visibility.Collapsed,
                    _ => raw
                };
            }

            string fullModule = raw.Substring(0, dotIndex);   // e.g. "/Script/Remnant"
            string className = raw.Substring(dotIndex + 1);   // e.g. "AccountAward"
            string shortModule = fullModule.StartsWith("/Script/", StringComparison.OrdinalIgnoreCase)
                ? fullModule.Substring("/Script/".Length)
                : fullModule;

            return mode switch
            {
                "Module" => shortModule,
                "FullModule" => fullModule,
                "HasModule" => Visibility.Visible,
                _ => className
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
