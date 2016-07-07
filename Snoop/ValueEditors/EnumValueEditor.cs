// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace Snoop {
    public class EnumValueEditor : ValueEditor {
        readonly List<object> values = new List<object>();
        readonly ListCollectionView valuesView;
        bool isValid;

        public EnumValueEditor() {
            valuesView = (ListCollectionView) CollectionViewSource.GetDefaultView(values);
            valuesView.CurrentChanged += HandleSelectionChanged;
        }


        public IList<object> Values {
            get { return values; }
        }


        protected override void OnTypeChanged() {
            base.OnTypeChanged();

            isValid = false;

            this.values.Clear();

            var propertyType = PropertyType;
            if (propertyType != null) {
                var values = Enum.GetValues(propertyType);
                foreach (var value in values) {
                    this.values.Add(value);

                    if (Value != null && Value.Equals(value))
                        valuesView.MoveCurrentTo(value);
                }
            }

            isValid = true;
        }

        protected override void OnValueChanged(object newValue) {
            base.OnValueChanged(newValue);

            valuesView.MoveCurrentTo(newValue);

            // sneaky trick here.  only if both are non-null is this a change
            // caused by the user.  If so, set the bool to track it.
            if (PropertyInfo != null && newValue != null) {
                PropertyInfo.IsValueChangedByUser = true;
            }
        }


        void HandleSelectionChanged(object sender, EventArgs e) {
            if (isValid && Value != null) {
                if (!Value.Equals(valuesView.CurrentItem))
                    Value = valuesView.CurrentItem;
            }
        }
    }
}