// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace Snoop {
    public class StandardValueEditor : ValueEditor {
        public static readonly DependencyProperty StringValueProperty =
            DependencyProperty.Register
                (
                    "StringValue",
                    typeof(string),
                    typeof(StandardValueEditor),
                    new PropertyMetadata(HandleStringPropertyChanged)
                );


        bool isUpdatingValue;


        public string StringValue {
            get { return (string) GetValue(StringValueProperty); }
            set { SetValue(StringValueProperty, value); }
        }

        static void HandleStringPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
            ((StandardValueEditor) sender).OnStringPropertyChanged((string) e.NewValue);
        }

        protected virtual void OnStringPropertyChanged(string newValue) {
            if (isUpdatingValue)
                return;

            if (PropertyInfo != null) {
                PropertyInfo.IsValueChangedByUser = true;
            }

            var targetType = PropertyType;

            if (targetType.IsAssignableFrom(typeof(string))) {
                Value = newValue;
            }
            else {
                var converter = TypeDescriptor.GetConverter(targetType);
                if (converter != null) {
                    try {
                        SetValueFromConverter(newValue, targetType, converter);
                    }
                    catch (Exception) {}
                }
            }
        }

        void SetValueFromConverter(string newValue, Type targetType, TypeConverter converter) {
            if (!converter.CanConvertFrom(targetType) && string.IsNullOrEmpty(newValue)) {
                Value = null;
            }
            else {
                Value = converter.ConvertFrom(newValue);
            }
        }


        protected override void OnValueChanged(object newValue) {
            isUpdatingValue = true;

            var value = Value;
            if (value != null)
                StringValue = value.ToString();
            else
                StringValue = string.Empty;

            isUpdatingValue = false;

            var binding = BindingOperations.GetBindingExpression(this, StringValueProperty);
            if (binding != null)
                binding.UpdateSource();
        }
    }
}