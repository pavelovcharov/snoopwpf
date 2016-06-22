// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Snoop.Infrastructure;

namespace Snoop {
    public partial class PropertyGrid2 : INotifyPropertyChanged {
        public static readonly RoutedCommand ShowBindingErrorsCommand = new RoutedCommand();
        public static readonly RoutedCommand ClearCommand = new RoutedCommand();
        public static readonly RoutedCommand SortCommand = new RoutedCommand();

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register
                (
                    "Target",
                    typeof(object),
                    typeof(PropertyGrid2),
                    new PropertyMetadata(HandleTargetChanged)
                );

        readonly ObservableCollection<PropertyInformation> allProperties =
            new ObservableCollection<PropertyInformation>();

        readonly DelayedCall filterCall;

        readonly DispatcherTimer filterTimer;
        readonly DelayedCall processIncrementalCall;

        bool _nameValueOnly;

        ListSortDirection direction = ListSortDirection.Ascending;

        IEnumerator<PropertyInformation> propertiesToAdd;
        PropertyInformation selection;


        object target;
        bool unloaded;
        int visiblePropertyCount;


        public PropertyGrid2() {
            processIncrementalCall = new DelayedCall(ProcessIncrementalPropertyAdd, DispatcherPriority.Background);
            filterCall = new DelayedCall(ProcessFilter, DispatcherPriority.Background);

            InitializeComponent();

            Loaded += HandleLoaded;
            Unloaded += HandleUnloaded;

            CommandBindings.Add(new CommandBinding(ShowBindingErrorsCommand, HandleShowBindingErrors,
                CanShowBindingErrors));
            CommandBindings.Add(new CommandBinding(ClearCommand, HandleClear, CanClear));
            CommandBindings.Add(new CommandBinding(SortCommand, HandleSort));


            filterTimer = new DispatcherTimer();
            filterTimer.Interval = TimeSpan.FromSeconds(0.3);
            filterTimer.Tick += (s, e) => {
                filterCall.Enqueue();
                filterTimer.Stop();
            };
        }


        public bool NameValueOnly {
            get { return _nameValueOnly; }
            set {
                _nameValueOnly = value;
                var gridView = ListView != null && ListView.View != null ? ListView.View as GridView : null;
                if (_nameValueOnly && gridView != null && gridView.Columns.Count != 2) {
                    gridView.Columns.RemoveAt(0);
                    while (gridView.Columns.Count > 2) {
                        gridView.Columns.RemoveAt(2);
                    }
                }
            }
        }

        public ObservableCollection<PropertyInformation> Properties { get; } =
            new ObservableCollection<PropertyInformation>();

        public object Target {
            get { return GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        public PropertyInformation Selection {
            get { return selection; }
            set {
                selection = value;
                OnPropertyChanged("Selection");
            }
        }

        public Type Type {
            get {
                if (target != null)
                    return target.GetType();
                return null;
            }
        }

        static void HandleTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var propertyGrid = (PropertyGrid2) d;
            propertyGrid.ChangeTarget(e.NewValue);
        }

        void ChangeTarget(object newTarget) {
            if (target != newTarget) {
                target = newTarget;

                foreach (var property in Properties) {
                    property.Teardown();
                }
                RefreshPropertyGrid();

                OnPropertyChanged("Type");
            }
        }


        protected override void OnFilterChanged() {
            base.OnFilterChanged();

            filterTimer.Stop();
            filterTimer.Start();
        }


        /// <summary>
        ///     Delayed loading of the property inspector to avoid creating the entire list of property
        ///     editors immediately after selection. Keeps that app running smooth.
        /// </summary>
        /// <param name="performInitialization"></param>
        /// <returns></returns>
        void ProcessIncrementalPropertyAdd() {
            var numberToAdd = 10;

            if (propertiesToAdd == null) {
                propertiesToAdd = PropertyInformation.GetProperties(target).GetEnumerator();

                numberToAdd = 0;
            }
            var i = 0;
            for (; i < numberToAdd && propertiesToAdd.MoveNext(); ++i) {
                // iterate over the PropertyInfo objects,
                // setting the property grid's filter on each object,
                // and adding those properties to the observable collection of propertiesToSort (this.properties)
                var property = propertiesToAdd.Current;
                property.Filter = Filter;

                if (property.IsVisible) {
                    Properties.Add(property);
                }
                allProperties.Add(property);

                // checking whether a property is visible ... actually runs the property filtering code
                if (property.IsVisible)
                    property.Index = visiblePropertyCount++;
            }

            if (i == numberToAdd)
                processIncrementalCall.Enqueue();
            else
                propertiesToAdd = null;
        }

        void HandleShowBindingErrors(object sender, ExecutedRoutedEventArgs eventArgs) {
            var propertyInformation = (PropertyInformation) eventArgs.Parameter;
            var window = new Window();
            var textbox = new TextBox();
            textbox.IsReadOnly = true;
            textbox.Text = propertyInformation.BindingError;
            textbox.TextWrapping = TextWrapping.Wrap;
            window.Content = textbox;
            window.Width = 400;
            window.Height = 300;
            window.Title = "Binding Errors for " + propertyInformation.DisplayName;
            SnoopPartsRegistry.AddSnoopVisualTreeRoot(window);
            window.Closing +=
                (s, e) => {
                    var w = (Window) s;
                    SnoopPartsRegistry.RemoveSnoopVisualTreeRoot(w);
                };
            window.Show();
        }

        void CanShowBindingErrors(object sender, CanExecuteRoutedEventArgs e) {
            if (e.Parameter != null && !string.IsNullOrEmpty(((PropertyInformation) e.Parameter).BindingError))
                e.CanExecute = true;
            e.Handled = true;
        }

        void CanClear(object sender, CanExecuteRoutedEventArgs e) {
            if (e.Parameter != null && ((PropertyInformation) e.Parameter).IsLocallySet)
                e.CanExecute = true;
            e.Handled = true;
        }

        void HandleClear(object sender, ExecutedRoutedEventArgs e) {
            ((PropertyInformation) e.Parameter).Clear();
        }

        ListSortDirection GetNewSortDirection(GridViewColumnHeader columnHeader) {
            if (!(columnHeader.Tag is ListSortDirection))
                return (ListSortDirection) (columnHeader.Tag = ListSortDirection.Descending);

            var direction = (ListSortDirection) columnHeader.Tag;
            return (ListSortDirection) (columnHeader.Tag = (ListSortDirection) (((int) direction + 1)%2));
        }


        void HandleSort(object sender, ExecutedRoutedEventArgs args) {
            var headerClicked = (GridViewColumnHeader) args.OriginalSource;

            direction = GetNewSortDirection(headerClicked);

            switch (((TextBlock) headerClicked.Column.Header).Text) {
                case "Name":
                    Sort(CompareNames, direction);
                    break;
                case "Value":
                    Sort(CompareValues, direction);
                    break;
                case "ValueSource":
                    Sort(CompareValueSources, direction);
                    break;
            }
        }

        void ProcessFilter() {
            foreach (var property in allProperties) {
                if (property.IsVisible) {
                    if (!Properties.Contains(property)) {
                        InsertInPropertOrder(property);
                    }
                }
                else {
                    if (Properties.Contains(property)) {
                        Properties.Remove(property);
                    }
                }
            }

            SetIndexesOfProperties();
        }

        void InsertInPropertOrder(PropertyInformation property) {
            if (Properties.Count == 0) {
                Properties.Add(property);
                return;
            }

            if (PropertiesAreInOrder(property, Properties[0])) {
                Properties.Insert(0, property);
                return;
            }

            for (var i = 0; i < Properties.Count - 1; i++) {
                if (PropertiesAreInOrder(Properties[i], property) && PropertiesAreInOrder(property, Properties[i + 1])) {
                    Properties.Insert(i + 1, property);
                    return;
                }
            }

            Properties.Add(property);
        }

        bool PropertiesAreInOrder(PropertyInformation first, PropertyInformation last) {
            if (direction == ListSortDirection.Ascending) {
                return first.CompareTo(last) <= 0;
            }
            return last.CompareTo(first) <= 0;
        }

        void SetIndexesOfProperties() {
            for (var i = 0; i < Properties.Count; i++) {
                Properties[i].Index = i;
            }
        }

        void HandleLoaded(object sender, EventArgs e) {
            if (unloaded) {
                RefreshPropertyGrid();
                unloaded = false;
            }
        }

        void HandleUnloaded(object sender, EventArgs e) {
            foreach (var property in Properties)
                property.Teardown();

            unloaded = true;
        }

        void HandleNameClick(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                var property = (PropertyInformation) ((FrameworkElement) sender).DataContext;

                object newTarget = null;

                if (Keyboard.Modifiers == ModifierKeys.Shift)
                    newTarget = property.Binding;
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                    newTarget = property.BindingExpression;
                else if (Keyboard.Modifiers == ModifierKeys.None)
                    newTarget = property.Value;

                if (newTarget != null) {
                    PropertyInspector.DelveCommand.Execute(property, this);
                }
            }
        }

        void Sort(Comparison<PropertyInformation> comparator, ListSortDirection direction) {
            Sort(comparator, direction, Properties);
            Sort(comparator, direction, allProperties);
        }

        void Sort(Comparison<PropertyInformation> comparator, ListSortDirection direction,
            ObservableCollection<PropertyInformation> propertiesToSort) {
            var sorter = new List<PropertyInformation>(propertiesToSort);
            sorter.Sort(comparator);

            if (direction == ListSortDirection.Descending)
                sorter.Reverse();

            propertiesToSort.Clear();
            foreach (var property in sorter)
                propertiesToSort.Add(property);
        }

        void RefreshPropertyGrid() {
            allProperties.Clear();
            Properties.Clear();
            visiblePropertyCount = 0;

            propertiesToAdd = null;
            processIncrementalCall.Enqueue();
        }


        static int CompareNames(PropertyInformation one, PropertyInformation two) {
            // use the PropertyInformation CompareTo method, instead of the string.Compare method
            // so that collections get sorted correctly.
            return one.CompareTo(two);
        }

        static int CompareValues(PropertyInformation one, PropertyInformation two) {
            return string.Compare(one.StringValue, two.StringValue);
        }

        static int CompareValueSources(PropertyInformation one, PropertyInformation two) {
            return string.Compare(one.ValueSource.BaseValueSource.ToString(), two.ValueSource.BaseValueSource.ToString());
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}