// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Windows;

namespace Snoop.MethodsTab {
    public partial class TypeSelector : ITypeSelector {
        public TypeSelector() {
            InitializeComponent();

            Loaded += TypeSelector_Loaded;
        }

        public List<Type> DerivedTypes { get; set; }

        public Type BaseType { get; set; }

        //TODO: MOVE SOMEWHERE ELSE. MACIEK
        public static List<Type> GetDerivedTypes(Type baseType) {
            var typesAssignable = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in assembly.GetTypes()) {
                    if (baseType.IsAssignableFrom(type)) {
                        typesAssignable.Add(type);
                    }
                }
            }

            if (!baseType.IsAbstract) {
                typesAssignable.Add(baseType);
            }

            typesAssignable.Sort(new TypeComparerByName());

            return typesAssignable;
        }

        void TypeSelector_Loaded(object sender, RoutedEventArgs e) {
            if (DerivedTypes == null)
                DerivedTypes = GetDerivedTypes(BaseType);

            comboBoxTypes.ItemsSource = DerivedTypes;
        }

        void buttonCreateInstance_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Instance = Activator.CreateInstance((Type) comboBoxTypes.SelectedItem);
            Close();
        }

        void buttonCancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        public object Instance { get; private set; }
    }
}