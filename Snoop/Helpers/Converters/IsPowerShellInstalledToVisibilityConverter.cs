// (c) Copyright Bailey Ling.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
#if !NETCORE
using Snoop.Shell;
#endif

namespace Snoop.Converters {
    public class IsPowerShellInstalledToVisibilityConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
#if !NETCORE
            return ShellConstants.IsPowerShellInstalled ? Visibility.Visible : Visibility.Collapsed;
#else
            return Visibility.Collapsed;
#endif
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}