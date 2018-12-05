using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace Snoop.Converters {
    public class BooleanNegationConverterExtension : MarkupExtension, IValueConverter {
        public override object ProvideValue(IServiceProvider serviceProvider) { return this; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return !((bool)value); }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { return !((bool)value); }
    }
}