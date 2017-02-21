// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Infrastructure;

namespace Snoop {
    public partial class PropertyInspector : INotifyPropertyChanged {
        public static readonly RoutedCommand SnipXamlCommand = new RoutedCommand("SnipXaml", typeof(PropertyInspector));
        public static readonly RoutedCommand PopTargetCommand = new RoutedCommand("PopTarget", typeof(PropertyInspector));
        public static readonly RoutedCommand DelveCommand = new RoutedCommand();
        public static readonly RoutedCommand DelveBindingCommand = new RoutedCommand();
        public static readonly RoutedCommand DelveBindingExpressionCommand = new RoutedCommand();

        public static readonly DependencyProperty RootTargetProperty =
            DependencyProperty.Register
                (
                    "RootTarget",
                    typeof(object),
                    typeof(PropertyInspector),
                    new PropertyMetadata(HandleRootTargetChanged)
                );

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register
                (
                    "Target",
                    typeof(object),
                    typeof(PropertyInspector),
                    new PropertyMetadata(HandleTargetChanged)
                );

        readonly PropertyFilterSet[] _defaultFilterSets = {
            new PropertyFilterSet {
                DisplayName = "Layout",
                IsDefault = false,
                IsEditCommand = false,
                Properties = new[] {
                    "width", "height", "actualwidth", "actualheight", "margin", "padding", "left", "top"
                }
            },
            new PropertyFilterSet {
                DisplayName = "Grid/Dock",
                IsDefault = false,
                IsEditCommand = false,
                Properties = new[] {
                    "grid", "dock"
                }
            },
            new PropertyFilterSet {
                DisplayName = "Color",
                IsDefault = false,
                IsEditCommand = false,
                Properties = new[] {
                    "color", "background", "foreground", "borderbrush", "fill", "stroke"
                }
            },
            new PropertyFilterSet {
                DisplayName = "ItemsControl",
                IsDefault = false,
                IsEditCommand = false,
                Properties = new[] {
                    "items", "selected"
                }
            }
        };

        readonly List<PropertyInformation> _delvePathList = new List<PropertyInformation>();
        readonly bool _nameValueOnly = false;

        readonly Inspector inspector;

        readonly List<object> inspectStack = new List<object>();
        PropertyFilterSet[] _filterSets;

        public PropertyInspector() {
            PropertyFilter.SelectedFilterSet = AllFilterSets[0];

            InitializeComponent();

            inspector = PropertyGrid;
            inspector.Filter = PropertyFilter;

            CommandBindings.Add(new CommandBinding(SnipXamlCommand, HandleSnipXaml, CanSnipXaml));
            CommandBindings.Add(new CommandBinding(PopTargetCommand, HandlePopTarget, CanPopTarget));
            CommandBindings.Add(new CommandBinding(DelveCommand, HandleDelve, CanDelve));
            CommandBindings.Add(new CommandBinding(DelveBindingCommand, HandleDelveBinding, CanDelveBinding));
            CommandBindings.Add(new CommandBinding(DelveBindingExpressionCommand, HandleDelveBindingExpression,
                CanDelveBindingExpression));

            // watch for mouse "back" button
            MouseDown += MouseDownHandler;
            KeyDown += PropertyInspector_KeyDown;
        }

        public bool NameValueOnly {
            get { return _nameValueOnly; }
            set { PropertyGrid.NameValueOnly = value; }
        }

        public object RootTarget {
            get { return GetValue(RootTargetProperty); }
            set { SetValue(RootTargetProperty, value); }
        }

        public object Target {
            get { return GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        /// <summary>
        ///     Delve Path
        /// </summary>
        public string DelvePath {
            get {
                if (RootTarget == null)
                    return "object is NULL";

                var rootTargetType = RootTarget.GetType();
                var delvePath = GetDelvePath(rootTargetType);
                var type = GetCurrentTypeName(rootTargetType);

                return string.Format("{0}\n({1})", delvePath, type);
            }
        }

        public Type Type {
            get {
                if (Target != null)
                    return Target.GetType();
                return null;
            }
        }

        public PropertyFilter PropertyFilter { get; } = new PropertyFilter(string.Empty, true);

        public string StringFilter {
            get { return PropertyFilter.FilterString; }
            set {
                PropertyFilter.FilterString = value;

                inspector.Filter = PropertyFilter;

                OnPropertyChanged("StringFilter");
            }
        }

        public bool ShowDefaults {
            get { return PropertyFilter.ShowDefaults; }
            set {
                PropertyFilter.ShowDefaults = value;

                inspector.Filter = PropertyFilter;

                OnPropertyChanged("ShowDefaults");
            }
        }

        /// <summary>
        ///     Hold the SelectedFilterSet in the PropertyFilter class, but track it here, so we know
        ///     when to "refresh" the filtering with filterCall.Enqueue
        /// </summary>
        public PropertyFilterSet SelectedFilterSet {
            get { return PropertyFilter.SelectedFilterSet; }
            set {
                PropertyFilter.SelectedFilterSet = value;
                OnPropertyChanged("SelectedFilterSet");

                if (value == null)
                    return;

                if (value.IsEditCommand) {
                    var dlg = new EditUserFilters {UserFilters = CopyFilterSets(UserFilterSets)};

                    // set owning window to center over if we can find it up the tree
                    var snoopWindow = VisualTreeHelper2.GetAncestor<Window>(this);
                    if (snoopWindow != null) {
                        dlg.Owner = snoopWindow;
                        dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }

                    var res = dlg.ShowDialog();
                    if (res.GetValueOrDefault()) {
                        // take the adjusted values from the dialog, setter will SAVE them to user properties
                        UserFilterSets = CleansFilterPropertyNames(dlg.ItemsSource);
                        // trigger the UI to re-bind to the collection, so user sees changes they just made
                        OnPropertyChanged("AllFilterSets");
                    }

                    // now that we're out of the dialog, set current selection back to "(default)"
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, (DispatcherOperationCallback) delegate {
                        // couldnt get it working by setting SelectedFilterSet directly
                        // using the Index to get us back to the first item in the list
                        FilterSetCombo.SelectedIndex = 0;
                        //SelectedFilterSet = AllFilterSets[0];
                        return null;
                    }, null);
                }
                else {
                    inspector.Filter = PropertyFilter;
                    OnPropertyChanged("SelectedFilterSet");
                }
            }
        }

        /// <summary>
        ///     Get or Set the collection of User filter sets.  These are the filters that are configurable by
        ///     the user, and serialized to/from app Settings.
        /// </summary>
        public PropertyFilterSet[] UserFilterSets {
            get {
                if (_filterSets == null) {
                    var ret = new List<PropertyFilterSet>();

                    try {
                        ret.AddRange(_defaultFilterSets);
                    }
                    catch (Exception ex) {
                        var msg = string.Format("Error reading user filters from settings. Using defaults.\r\n\r\n{0}",
                            ex.Message);
                        MessageBox.Show(msg, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                        ret.Clear();
                        ret.AddRange(_defaultFilterSets);
                    }

                    _filterSets = ret.ToArray();
                }
                return _filterSets;
            }
            set { _filterSets = value; }
        }

        /// <summary>
        ///     Get the collection of "all" filter sets.  This is the UserFilterSets wrapped with
        ///     (Default) at the start and "Edit Filters..." at the end of the collection.
        ///     This is the collection bound to in the UI
        /// </summary>
        public PropertyFilterSet[] AllFilterSets {
            get {
                var ret = new List<PropertyFilterSet>(UserFilterSets);

                // now add the "(Default)" and "Edit Filters..." filters for the ComboBox
                ret.Insert
                    (
                        0,
                        new PropertyFilterSet {
                            DisplayName = "(Default)",
                            IsDefault = true,
                            IsEditCommand = false
                        }
                    );
                ret.Add
                    (
                        new PropertyFilterSet {
                            DisplayName = "Edit Filters...",
                            IsDefault = false,
                            IsEditCommand = true
                        }
                    );
                return ret.ToArray();
            }
        }

        void HandleSnipXaml(object sender, ExecutedRoutedEventArgs e) {
            try {
                var xaml = XamlWriter.Save(((PropertyInformation) e.Parameter).Value);
                Clipboard.SetData(DataFormats.Text, xaml);
                MessageBox.Show("This brush has been copied to the clipboard. You can paste it into your project.",
                    "Brush copied", MessageBoxButton.OK);
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        void CanSnipXaml(object sender, CanExecuteRoutedEventArgs e) {
            if (e.Parameter != null && ((PropertyInformation) e.Parameter).Value is Brush)
                e.CanExecute = true;
            e.Handled = true;
        }

        static void HandleRootTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var inspector = (PropertyInspector) d;

            inspector.inspectStack.Clear();
            inspector.Target = e.NewValue;

            inspector._delvePathList.Clear();
            inspector.OnPropertyChanged("DelvePath");
        }

        static void HandleTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var inspector = (PropertyInspector) d;
            inspector.OnPropertyChanged("Type");

            if (e.NewValue != null)
                inspector.inspectStack.Add(e.NewValue);
        }


        string GetDelvePath(Type rootTargetType) {
            var delvePath = new StringBuilder(rootTargetType.Name);

            foreach (var propInfo in _delvePathList) {
                int collectionIndex;
                if ((collectionIndex = propInfo.CollectionIndex()) >= 0) {
                    delvePath.Append(string.Format("[{0}]", collectionIndex));
                }
                else {
                    delvePath.Append(string.Format(".{0}", propInfo.DisplayName));
                }
            }

            return delvePath.ToString();
        }

        string GetCurrentTypeName(Type rootTargetType) {
            var type = string.Empty;
            if (_delvePathList.Count > 0) {
                var skipDelve = _delvePathList[_delvePathList.Count - 1].Value as ISkipDelve;
                if (skipDelve != null && skipDelve.NextValue != null && skipDelve.NextValueType != null) {
                    return skipDelve.NextValueType.ToString();
                    //we want to make this "future friendly", so we take into account that the string value of the property type may change.
                }
                if (_delvePathList[_delvePathList.Count - 1].Value != null) {
                    type = _delvePathList[_delvePathList.Count - 1].Value.GetType().ToString();
                }
                else {
                    type = _delvePathList[_delvePathList.Count - 1].PropertyType.ToString();
                }
            }
            else if (_delvePathList.Count == 0) {
                type = rootTargetType.FullName;
            }

            return type;
        }

        public void PushTarget(object target) {
            Target = target;
        }

        public void SetTarget(object target) {
            inspectStack.Clear();
            Target = target;
        }

        void HandlePopTarget(object sender, ExecutedRoutedEventArgs e) {
            PopTarget();
        }

        void PopTarget() {
            if (inspectStack.Count > 1) {
                Target = inspectStack[inspectStack.Count - 2];
                inspectStack.RemoveAt(inspectStack.Count - 2);
                inspectStack.RemoveAt(inspectStack.Count - 2);

                if (_delvePathList.Count > 0) {
                    _delvePathList.RemoveAt(_delvePathList.Count - 1);
                    OnPropertyChanged("DelvePath");
                }
            }
        }

        void CanPopTarget(object sender, CanExecuteRoutedEventArgs e) {
            if (inspectStack.Count > 1) {
                e.Handled = true;
                e.CanExecute = true;
            }
        }

        object GetRealTarget(object target) {
            var skipDelve = target as ISkipDelve;
            if (skipDelve != null) {
                return skipDelve.NextValue;
            }
            return target;
        }

        void HandleDelve(object sender, ExecutedRoutedEventArgs e) {
            var realTarget = GetRealTarget(((PropertyInformation) e.Parameter).Value);

            if (realTarget != Target) {
                // top 'if' statement is the delve path.
                // we do this because without doing this, the delve path gets out of sync with the actual delves.
                // the reason for this is because PushTarget sets the new target,
                // and if it's equal to the current (original) target, we won't raise the property-changed event,
                // and therefore, we don't add to our delveStack (the real one).

                _delvePathList.Add((PropertyInformation) e.Parameter);
                OnPropertyChanged("DelvePath");
            }

            PushTarget(realTarget);
        }

        void HandleDelveBinding(object sender, ExecutedRoutedEventArgs e) {
            PushTarget(((PropertyInformation) e.Parameter).Binding);
        }

        void HandleDelveBindingExpression(object sender, ExecutedRoutedEventArgs e) {
            PushTarget(((PropertyInformation) e.Parameter).BindingExpression);
        }

        void CanDelve(object sender, CanExecuteRoutedEventArgs e) {
            if (e.Parameter != null && ((PropertyInformation) e.Parameter).Value != null)
                e.CanExecute = true;
            e.Handled = true;
        }

        void CanDelveBinding(object sender, CanExecuteRoutedEventArgs e) {
            if (e.Parameter != null && ((PropertyInformation) e.Parameter).Binding != null)
                e.CanExecute = true;
            e.Handled = true;
        }

        void CanDelveBindingExpression(object sender, CanExecuteRoutedEventArgs e) {
            if (e.Parameter != null && ((PropertyInformation) e.Parameter).BindingExpression != null)
                e.CanExecute = true;
            e.Handled = true;
        }

        /// <summary>
        ///     Looking for "browse back" mouse button.
        ///     Pop properties context when clicked.
        /// </summary>
        void MouseDownHandler(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.XButton1) {
                PopTarget();
            }
        }

        void PropertyInspector_KeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left) {
                PopTarget();
            }
        }

        /// <summary>
        ///     Make a deep copy of the filter collection.
        ///     This is used when heading into the Edit dialog, so the user is editing a copy of the
        ///     filters, in case they cancel the dialog - we dont want to alter their live collection.
        /// </summary>
        public PropertyFilterSet[] CopyFilterSets(PropertyFilterSet[] source) {
            var ret = new List<PropertyFilterSet>();
            foreach (var src in source) {
                ret.Add
                    (
                        new PropertyFilterSet {
                            DisplayName = src.DisplayName,
                            IsDefault = src.IsDefault,
                            IsEditCommand = src.IsEditCommand,
                            Properties = (string[]) src.Properties.Clone()
                        }
                    );
            }

            return ret.ToArray();
        }

        /// <summary>
        ///     Cleanse the property names in each filter in the collection.
        ///     This includes removing spaces from each one, and making them all lower case
        /// </summary>
        PropertyFilterSet[] CleansFilterPropertyNames(IEnumerable<PropertyFilterSet> collection) {
            foreach (var filterItem in collection) {
                filterItem.Properties = filterItem.Properties.Select(s => s.ToLower().Trim()).ToArray();
            }
            return collection.ToArray();
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