// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Text.RegularExpressions;
using System.Windows;

namespace Snoop {
    public class PropertyFilter {
        Regex filterRegex;
        string filterString;

        public PropertyFilter(string filterString, bool showDefaults) {
            this.filterString = filterString.ToLower();
            ShowDefaults = showDefaults;
        }

        public string FilterString {
            get { return filterString; }
            set {
                filterString = value.ToLower();
                try {
                    filterRegex = new Regex(filterString, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                }
                catch {
                    filterRegex = null;
                }
            }
        }

        public bool ShowDefaults { get; set; }

        public PropertyFilterSet SelectedFilterSet { get; set; }

        public bool IsPropertyFilterSet {
            get { return SelectedFilterSet != null && SelectedFilterSet.Properties != null; }
        }

        public bool Show(PropertyInformation property) {
            // use a regular expression if we have one and we also have a filter string.
            if (filterRegex != null && !string.IsNullOrEmpty(FilterString)) {
                return
                    filterRegex.IsMatch(property.DisplayName) ||
                    property.Property != null && filterRegex.IsMatch(property.Property.PropertyType.Name);
            }
            // else just check for containment if we don't have a regular expression but we do have a filter string.
            if (!string.IsNullOrEmpty(FilterString)) {
                if (property.DisplayName.ToLower().Contains(FilterString))
                    return true;
                if (property.Property != null && property.Property.PropertyType.Name.ToLower().Contains(FilterString))
                    return true;
                return false;
            }
            // else use the filter set if we have one of those.
            if (IsPropertyFilterSet) {
                if (SelectedFilterSet.IsPropertyInFilter(property.DisplayName))
                    return true;
                return false;
            }
            // finally, if none of the above applies
            // just check to see if we're not showing properties at their default values
            // and this property is actually set to its default value
            if (!ShowDefaults && property.ValueSource.BaseValueSource == BaseValueSource.Default)
                return false;
            return true;
        }
    }

    [Serializable]
    public class PropertyFilterSet {
        public string DisplayName { get; set; }

        public bool IsDefault { get; set; }

        public bool IsEditCommand { get; set; }

        public string[] Properties { get; set; }

        public bool IsPropertyInFilter(string property) {
            var lowerProperty = property.ToLower();
            foreach (var filterProp in Properties) {
                if (lowerProperty.StartsWith(filterProp)) {
                    return true;
                }
            }
            return false;
        }
    }
}