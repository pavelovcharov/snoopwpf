﻿// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Globalization;
using System.Windows.Data;

namespace Snoop.Converters {
    internal class CsvStringToArrayConverter : IValueConverter {
        public static readonly CsvStringToArrayConverter Default = new CsvStringToArrayConverter();

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            // value (String[])	
            // return	string		CSV version of the string array

            if (value == null)
                return string.Empty;

            var val = (string[]) value;
            return string.Join(",", val);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            // value (string)		CSV version of the string array
            // return (string[])	array of strings split by ","

            if (value == null)
                return new string[0];

            var val = value.ToString().Trim();
            return val.Split(',');
        }

        #endregion
    }
}