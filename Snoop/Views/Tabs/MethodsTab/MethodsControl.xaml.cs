// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Snoop.Converters;

namespace Snoop.MethodsTab {
    public partial class MethodsControl {
        // Using a DependencyProperty as the backing store for RootTarget.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RootTargetProperty =
            DependencyProperty.Register("RootTarget", typeof(object), typeof(MethodsControl),
                new UIPropertyMetadata(null));

        // Using a DependencyProperty as the backing store for IsSelected.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(MethodsControl),
                new UIPropertyMetadata(false));

        // Using a DependencyProperty as the backing store for Target.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(object), typeof(MethodsControl),
                new UIPropertyMetadata(TargetChanged));

        SnoopMethodInformation _previousMethodInformation;

        public MethodsControl() {
            InitializeComponent();
            DependencyPropertyDescriptor.FromProperty(RootTargetProperty, typeof(MethodsControl))
                .AddValueChanged(this, RootTargetChanged);

            //DependencyPropertyDescriptor.FromProperty(TargetProperty, typeof(MethodsControl)).AddValueChanged(this, TargetChanged);
            DependencyPropertyDescriptor.FromProperty(Selector.SelectedValueProperty, typeof(ComboBox))
                .AddValueChanged(comboBoxMethods, comboBoxMethodChanged);
            DependencyPropertyDescriptor.FromProperty(IsSelectedProperty, typeof(MethodsControl))
                .AddValueChanged(this, IsSelectedChanged);

            _checkBoxUseDataContext.Checked += _checkBoxUseDataContext_Checked;
            _checkBoxUseDataContext.Unchecked += _checkBoxUseDataContext_Unchecked;
        }

        public object RootTarget {
            get { return GetValue(RootTargetProperty); }
            set { SetValue(RootTargetProperty, value); }
        }


        public bool IsSelected {
            get { return (bool) GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public object Target {
            get { return GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        void _checkBoxUseDataContext_Unchecked(object sender, RoutedEventArgs e) {
            ProcessCheckedProperty();
        }

        void _checkBoxUseDataContext_Checked(object sender, RoutedEventArgs e) {
            ProcessCheckedProperty();
        }

        void ProcessCheckedProperty() {
            if (!IsSelected || !_checkBoxUseDataContext.IsChecked.HasValue || !(RootTarget is FrameworkElement))
                return;

            SetTargetToRootTarget();
        }

        void SetTargetToRootTarget() {
            if (_checkBoxUseDataContext.IsChecked.Value && RootTarget is FrameworkElement &&
                ((FrameworkElement) RootTarget).DataContext != null) {
                Target = ((FrameworkElement) RootTarget).DataContext;
            }
            else {
                Target = RootTarget;
            }
        }

        void IsSelectedChanged(object sender, EventArgs args) {
            if (IsSelected) {
                //this.Target = this.RootTarget;
                SetTargetToRootTarget();
            }
        }

        void RootTargetChanged(object sender, EventArgs e) {
            if (IsSelected) {
                _checkBoxUseDataContext.IsEnabled = RootTarget is FrameworkElement &&
                                                    ((FrameworkElement) RootTarget).DataContext != null;
                SetTargetToRootTarget();
            }
        }


        static void TargetChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue != e.OldValue) {
                var methodsControl = (MethodsControl) sender;

                methodsControl.EnableOrDisableDataContextCheckbox();

                var methodInfos = GetMethodInfos(methodsControl.Target);
                methodsControl.comboBoxMethods.ItemsSource = methodInfos;

                methodsControl.resultProperties.Visibility = Visibility.Collapsed;
                methodsControl.resultStringContainer.Visibility = Visibility.Collapsed;
                methodsControl.parametersContainer.Visibility = Visibility.Collapsed;

                //if this target has the previous method info, set it
                for (var i = 0; i < methodInfos.Count && methodsControl._previousMethodInformation != null; i++) {
                    var methodInfo = methodInfos[i];
                    if (methodInfo.Equals(methodsControl._previousMethodInformation)) {
                        methodsControl.comboBoxMethods.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        void EnableOrDisableDataContextCheckbox() {
            if (_checkBoxUseDataContext.IsChecked.HasValue && _checkBoxUseDataContext.IsChecked.Value)
                return;

            if (!(Target is FrameworkElement) || ((FrameworkElement) Target).DataContext == null) {
                _checkBoxUseDataContext.IsEnabled = false;
            }
            else {
                _checkBoxUseDataContext.IsEnabled = true;
            }
        }

        void comboBoxMethodChanged(object sender, EventArgs e) {
            var selectedMethod = comboBoxMethods.SelectedValue as SnoopMethodInformation;
            if (selectedMethod == null || Target == null)
                return;

            var parameters = selectedMethod.GetParameters(Target.GetType());
            itemsControlParameters.ItemsSource = parameters;

            parametersContainer.Visibility = parameters.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            resultProperties.Visibility = resultStringContainer.Visibility = Visibility.Collapsed;

            _previousMethodInformation = selectedMethod;
        }

        public void InvokeMethodClick(object sender, RoutedEventArgs e) {
            var selectedMethod = comboBoxMethods.SelectedValue as SnoopMethodInformation;
            if (selectedMethod == null)
                return;

            var parameters = new object[itemsControlParameters.Items.Count];

            if (!TryToCreateParameters(parameters))
                return;

            TryToInvokeMethod(selectedMethod, parameters);
        }

        bool TryToCreateParameters(object[] parameters) {
            try {
                for (var index = 0; index < itemsControlParameters.Items.Count; index++) {
                    var paramInfo = itemsControlParameters.Items[index] as SnoopParameterInformation;
                    if (paramInfo == null)
                        return false;

                    if (paramInfo.ParameterType.Equals(typeof(DependencyProperty))) {
                        var valuePair = paramInfo.ParameterValue as DependencyPropertyNameValuePair;
                        parameters[index] = valuePair.DependencyProperty;
                    }
                    //else if (paramInfo.IsCustom || paramInfo.IsEnum)
                    else if (paramInfo.ParameterValue == null ||
                             paramInfo.ParameterType.IsAssignableFrom(paramInfo.ParameterValue.GetType())) {
                        parameters[index] = paramInfo.ParameterValue;
                    }
                    else {
                        var converter = TypeDescriptor.GetConverter(paramInfo.ParameterType);
                        parameters[index] = converter.ConvertFrom(paramInfo.ParameterValue);
                    }
                }
                return true;
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Error creating parameter");
                return false;
            }
        }

        void TryToInvokeMethod(SnoopMethodInformation selectedMethod, object[] parameters) {
            try {
                var returnValue = selectedMethod.MethodInfo.Invoke(Target, parameters);

                if (returnValue == null) {
                    SetNullReturnType(selectedMethod);
                    return;
                }
                resultStringContainer.Visibility =
                    textBlockResult.Visibility = textBlockResultLabel.Visibility = Visibility.Visible;

                textBlockResultLabel.Text = "Result as string: ";
                textBlockResult.Text = returnValue.ToString();

                var properties = returnValue.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                //var properties = PropertyInformation.GetAllProperties(returnValue, new Attribute[] { new PropertyFilterAttribute(PropertyFilterOptions.All) });

                if (properties.Length == 0) {
                    resultProperties.Visibility = Visibility.Collapsed;
                }
                else {
                    resultProperties.Visibility = Visibility.Visible;
                    propertyInspector.RootTarget = returnValue;
                }
            }
            catch (Exception ex) {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                MessageBox.Show(message, "Error invoking method");
            }
        }

        void SetNullReturnType(SnoopMethodInformation selectedMethod) {
            if (selectedMethod.MethodInfo.ReturnType == typeof(void)) {
                resultStringContainer.Visibility = resultProperties.Visibility = Visibility.Collapsed;
            }
            else {
                resultProperties.Visibility = Visibility.Collapsed;
                resultStringContainer.Visibility = Visibility.Visible;
                textBlockResult.Text = string.Empty;
                textBlockResultLabel.Text = "Method evaluated to null";
                textBlockResult.Visibility = Visibility.Collapsed;
            }
        }

        static IList<SnoopMethodInformation> GetMethodInfos(object o) {
            if (o == null)
                return new ObservableCollection<SnoopMethodInformation>();

            var t = o.GetType();
            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod);

            var methodsToReturn = new List<SnoopMethodInformation>();

            foreach (var method in methods) {
                if (method.IsSpecialName)
                    continue;

                var info = new SnoopMethodInformation(method);
                info.MethodName = method.Name;

                methodsToReturn.Add(info);
            }
            methodsToReturn.Sort();

            return methodsToReturn;
        }

        void ChangeTarget_Click(object sender, RoutedEventArgs e) {
            if (RootTarget == null)
                return;

            var paramCreator = new ParameterCreator();
            paramCreator.TextBlockDescription.Text =
                "Delve into the new desired target by double-clicking on the property. Clicking OK will select the currently delved property to be the new target.";
            paramCreator.Title = "Change Target";
            paramCreator.RootTarget = RootTarget;
            paramCreator.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            paramCreator.ShowDialog();

            if (paramCreator.DialogResult.HasValue && paramCreator.DialogResult.Value) {
                Target = paramCreator.SelectedTarget;
            }
        }
    }
}