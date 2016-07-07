﻿// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Snoop.Infrastructure;

namespace Snoop.MethodsTab {
    public class SnoopParameterInformation : DependencyObject {
        // Using a DependencyProperty as the backing store for ParameterValue.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ParameterValueProperty =
            DependencyProperty.Register("ParameterValue", typeof(object), typeof(SnoopParameterInformation),
                new UIPropertyMetadata(null));

        ICommand _createCustomParameterCommand;
        ICommand _nullOutParameter;

        ParameterInfo _parameterInfo;

        public SnoopParameterInformation(ParameterInfo parameterInfo, Type declaringType) {
            _parameterInfo = parameterInfo;
            if (parameterInfo == null)
                return;

            DeclaringType = declaringType;
            ParameterName = parameterInfo.Name;
            ParameterType = parameterInfo.ParameterType;
            if (ParameterType.IsValueType) {
                ParameterValue = Activator.CreateInstance(ParameterType);
            }
            TypeConverter = TypeDescriptor.GetConverter(ParameterType);
        }

        public TypeConverter TypeConverter { get; }

        public Type DeclaringType { get; private set; }

        public bool IsCustom {
            get { return !IsEnum && (TypeConverter.GetType() == typeof(TypeConverter)); }
        }

        public bool IsEnum {
            get { return ParameterType.IsEnum; }
        }

        public ICommand CreateCustomParameterCommand {
            get {
                return _createCustomParameterCommand ??
                       (_createCustomParameterCommand = new RelayCommand(x => CreateCustomParameter()));
            }
        }

        public ICommand NullOutParameterCommand {
            get { return _nullOutParameter ?? (_nullOutParameter = new RelayCommand(x => ParameterValue = null)); }
        }

        public string ParameterName { get; set; }

        public Type ParameterType { get; set; }

        public object ParameterValue {
            get { return GetValue(ParameterValueProperty); }
            set { SetValue(ParameterValueProperty, value); }
        }

        static ITypeSelector GetTypeSelector(Type parameterType) {
            ITypeSelector typeSelector = null;
            if (parameterType.Equals(typeof(object))) {
                typeSelector = new FullTypeSelector();
            }
            else {
                typeSelector = new TypeSelector {BaseType = parameterType};
                //typeSelector.BaseType = parameterType;
            }

            typeSelector.Title = "Choose the type to instantiate";
            typeSelector.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return typeSelector;
        }

        public void CreateCustomParameter() {
            var paramCreator = new ParameterCreator();
            paramCreator.Title = "Create parameter";
            paramCreator.TextBlockDescription.Text =
                "Modify the properties of the parameter. Press OK to finalize the parameter";

            if (ParameterValue == null) {
                var typeSelector = GetTypeSelector(ParameterType);
                typeSelector.ShowDialog();

                if (!typeSelector.DialogResult.Value) {
                    return;
                }
                paramCreator.RootTarget = typeSelector.Instance;
            }
            else {
                paramCreator.RootTarget = ParameterValue;
            }

            paramCreator.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            paramCreator.ShowDialog();

            if (paramCreator.DialogResult.HasValue && paramCreator.DialogResult.Value) {
                ParameterValue = null; //To force a property changed
                ParameterValue = paramCreator.RootTarget;
            }
        }
    }
}