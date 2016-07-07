﻿// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Snoop.MethodsTab {
    /// <summary>
    ///     Interaction logic for FullTypeSelector.xaml
    /// </summary>
    public partial class FullTypeSelector : ITypeSelector {
        public FullTypeSelector() {
            InitializeComponent();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var listAssemblies = new List<AssemblyNamePair>();
            foreach (var assembly in assemblies) {
                var namePair = new AssemblyNamePair();
                namePair.Name = assembly.FullName;
                namePair.Assembly = assembly;

                listAssemblies.Add(namePair);
            }

            listAssemblies.Sort();

            comboBoxAssemblies.ItemsSource = listAssemblies;
        }

        void comboBoxAssemblies_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var assembly = ((AssemblyNamePair) comboBoxAssemblies.SelectedItem).Assembly;

            var types = assembly.GetTypes();

            var typePairs = new List<TypeNamePair>();

            foreach (var type in types) {
                if (!type.IsPublic || type.IsAbstract)
                    continue;

                var pair = new TypeNamePair();
                pair.Name = type.Name;
                pair.Type = type;

                typePairs.Add(pair);
            }

            typePairs.Sort();

            comboBoxTypes.ItemsSource = typePairs;
        }

        void buttonCreateInstance_Click(object sender, RoutedEventArgs e) {
            var selectedType = ((TypeNamePair) comboBoxTypes.SelectedItem).Type;

            if (string.IsNullOrEmpty(textBoxConvertFrom.Text)) {
                Instance = Activator.CreateInstance(selectedType);
            }
            else {
                var converter = TypeDescriptor.GetConverter(selectedType);
                Instance = converter.ConvertFrom(textBoxConvertFrom.Text);
            }

            DialogResult = true;

            Close();
        }

        void buttonCancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;

            Close();
        }

        public object Instance { get; private set; }
    }

    //public class TypeNamePair : IComparable
    //{
    //    public string Name { get; set; }

    //    public Type Type { get; set; }

    //    public override string ToString()
    //    {
    //        return Name;
    //    }

    //    #region IComparable Members

    //    public int CompareTo(object obj)
    //    {
    //        return Name.CompareTo(((TypeNamePair)obj).Name);
    //    }

    //    #endregion
    //}

    //public class AssemblyNamePair : IComparable
    //{
    //    public string Name { get; set; }

    //    public Assembly Assembly { get; set; }

    //    public override string ToString()
    //    {
    //        return Name;
    //    }

    //    #region IComparable Members

    //    public int CompareTo(object obj)
    //    {
    //        return Name.CompareTo(((AssemblyNamePair)obj).Name);
    //    }

    //    #endregion
    //}
}