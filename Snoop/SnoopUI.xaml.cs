// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Infrastructure;
using Snoop.Properties;
using Snoop.Shell;
using Snoop.TreeList;

namespace Snoop {

    #region SnoopUI

    public partial class SnoopUI : INotifyPropertyChanged {
        public static readonly DependencyProperty EnableLiveTreeProperty = DependencyProperty.Register(
            "EnableLiveTree", typeof(bool), typeof(SnoopUI),
            new PropertyMetadata(default(bool), (o, e) => VisualDiagnosticsExtensions.Enabled = (bool) e.NewValue));

        public bool EnableLiveTree {
            get { return (bool) GetValue(EnableLiveTreeProperty); }
            set { SetValue(EnableLiveTreeProperty, value); }
        }

        #region Private Delegates

        delegate void function();

        #endregion

        #region Public Static Routed Commands

        public static readonly RoutedCommand IntrospectCommand = new RoutedCommand("Introspect", typeof(SnoopUI));
        public static readonly RoutedCommand RefreshCommand = new RoutedCommand("Refresh", typeof(SnoopUI));
        public static readonly RoutedCommand HelpCommand = new RoutedCommand("Help", typeof(SnoopUI));
        public static readonly RoutedCommand InspectCommand = new RoutedCommand("Inspect", typeof(SnoopUI));
        public static readonly RoutedCommand SelectFocusCommand = new RoutedCommand("SelectFocus", typeof(SnoopUI));

        public static readonly RoutedCommand SelectFocusScopeCommand = new RoutedCommand("SelectFocusScope",
            typeof(SnoopUI));

        public static readonly RoutedCommand ClearSearchFilterCommand = new RoutedCommand("ClearSearchFilter",
            typeof(SnoopUI));

        public static readonly RoutedCommand CopyPropertyChangesCommand = new RoutedCommand("CopyPropertyChanges",
            typeof(SnoopUI));

        #endregion

        #region Static Constructor

        static SnoopUI() {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            IntrospectCommand.InputGestures.Add(new KeyGesture(Key.I, ModifierKeys.Control));
            RefreshCommand.InputGestures.Add(new KeyGesture(Key.F5));
            HelpCommand.InputGestures.Add(new KeyGesture(Key.F1));
            ClearSearchFilterCommand.InputGestures.Add(new KeyGesture(Key.Escape));
            CopyPropertyChangesCommand.InputGestures.Add(new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift));
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.RequestingAssembly == null)
                return null;
            var file = Path.Combine(Path.GetDirectoryName(args.RequestingAssembly.Location),
                new AssemblyName(args.Name).Name + ".dll");
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        }

        #endregion

        #region Public Constructor

        public SnoopUI() {
            //ThemeManagerHelper.SetThemeName(this, null);
            filterCall = new DelayedCall(ProcessFilter, DispatcherPriority.Background);

            InheritanceBehavior = InheritanceBehavior.SkipToThemeNext;
            InitializeComponent();

            // wrap the following PresentationTraceSources.Refresh() call in a try/catch
            // sometimes a NullReferenceException occurs
            // due to empty <filter> elements in the app.config file of the app you are snooping
            // see the following for more info:
            // http://snoopwpf.codeplex.com/discussions/236503
            // http://snoopwpf.codeplex.com/workitem/6647
            try {
                PresentationTraceSources.Refresh();
                PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            }
            catch (NullReferenceException) {
                // swallow this exception since you can Snoop just fine anyways.
            }

            CommandBindings.Add(new CommandBinding(IntrospectCommand, HandleIntrospection));
            CommandBindings.Add(new CommandBinding(RefreshCommand, HandleRefresh));
            CommandBindings.Add(new CommandBinding(HelpCommand, HandleHelp));

            CommandBindings.Add(new CommandBinding(InspectCommand, HandleInspect));

            CommandBindings.Add(new CommandBinding(SelectFocusCommand, HandleSelectFocus));
            CommandBindings.Add(new CommandBinding(SelectFocusScopeCommand, HandleSelectFocusScope));

            //NOTE: this is up here in the outer UI layer so ESC will clear any typed filter regardless of where the focus is
            // (i.e. focus on a selected item in the tree, not in the property list where the search box is hosted)
            CommandBindings.Add(new CommandBinding(ClearSearchFilterCommand, ClearSearchFilterHandler));

            CommandBindings.Add(new CommandBinding(CopyPropertyChangesCommand, CopyPropertyChangesHandler));

            InputManager.Current.PreProcessInput += HandlePreProcessInput;
            Tree.ItemsSource = new TreeListSource(this);
            Tree.SelectionChanged += HandleTreeSelectedItemChanged;

            // we can't catch the mouse wheel at the ZoomerControl level,
            // so we catch it here, and relay it to the ZoomerControl.
            MouseWheel += SnoopUI_MouseWheel;

            filterTimer = new DispatcherTimer();
            filterTimer.Interval = TimeSpan.FromSeconds(0.3);
            filterTimer.Tick += (s, e) => {
                EnqueueAfterSettingFilter();
                filterTimer.Stop();
            };

            InitShell();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e) {
            if (e.Property.Name == "TreeWalker") {
                SetValue(e.Property, null);
            }
            base.OnPropertyChanged(e);
        }

        void InitShell() {
            if (ShellConstants.IsPowerShellInstalled) {
                var shell = new EmbeddedShellView();
                shell.Start(this);

                PowerShellTab.Content = shell;

                RoutedPropertyChangedEventHandler<object> onSelectedItemChanged =
                    (sender, e) => shell.NotifySelected(CurrentSelection);
                Action<VisualTreeItem> onProviderLocationChanged = item => Dispatcher.BeginInvoke(new Action(() => {
                    item.IsSelected = true;
                    CurrentSelection = item;
                }));

                // sync the current location
                //this.Tree.SelectedItemChanged += onSelectedItemChanged;
                shell.ProviderLocationChanged += onProviderLocationChanged;

                // clean up garbage!
                Closed += delegate {
                    //this.Tree.SelectedItemChanged -= onSelectedItemChanged;
                    shell.ProviderLocationChanged -= onProviderLocationChanged;
                };
            }
        }

        #endregion

        #region Public Static Methods

        public static bool GoBabyGo() {
            try {
                SnoopApplication();
                return true;
            }
            catch (Exception ex) {
                MessageBox.Show(
                    string.Format("There was an error snooping! Message = {0}\n\nStack Trace:\n{1}", ex.Message,
                        ex.StackTrace), "Error Snooping", MessageBoxButton.OK);
                return false;
            }
        }

        public static void SnoopApplication() {
            Dispatcher dispatcher;
            if (Application.Current == null)
                dispatcher = Dispatcher.CurrentDispatcher;
            else
                dispatcher = Application.Current.Dispatcher;

            if (dispatcher.CheckAccess()) {
                var snoop = new SnoopUI();
                var title = TryGetMainWindowTitle();
                if (!string.IsNullOrEmpty(title)) {
                    snoop.Title = string.Format("{0} - Snoop", title);
                }

                snoop.Inspect();

                CheckForOtherDispatchers(dispatcher);
            }
            else {
                dispatcher.Invoke((Action) SnoopApplication);
            }
        }

        static void CheckForOtherDispatchers(Dispatcher mainDispatcher) {
            // check and see if any of the root visuals have a different mainDispatcher
            // if so, ask the user if they wish to enter multiple mainDispatcher mode.
            // if they do, launch a snoop ui for every additional mainDispatcher.
            // see http://snoopwpf.codeplex.com/workitem/6334 for more info.

            var rootVisuals = new List<Visual>();
            var dispatchers = new List<Dispatcher>();
            dispatchers.Add(mainDispatcher);
            foreach (PresentationSource presentationSource in PresentationSource.CurrentSources) {
                var presentationSourceRootVisual = presentationSource.RootVisual;

                if (!(presentationSourceRootVisual is Window))
                    continue;

                var presentationSourceRootVisualDispatcher = presentationSourceRootVisual.Dispatcher;

                if (dispatchers.IndexOf(presentationSourceRootVisualDispatcher) == -1) {
                    rootVisuals.Add(presentationSourceRootVisual);
                    dispatchers.Add(presentationSourceRootVisualDispatcher);
                }
            }

            if (rootVisuals.Count > 0) {
                var result =
                    MessageBox.Show
                        (
                            "Snoop has noticed windows running in multiple dispatchers!\n\n" +
                            "Would you like to enter multiple dispatcher mode, and have a separate Snoop window for each dispatcher?\n\n" +
                            "Without having a separate Snoop window for each dispatcher, you will not be able to Snoop the windows in the dispatcher threads outside of the main dispatcher. " +
                            "Also, note, that if you bring up additional windows in additional dispatchers (after Snooping), you will need to Snoop again in order to launch Snoop windows for those additional dispatchers.",
                            "Enter Multiple Dispatcher Mode",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );

                if (result == MessageBoxResult.Yes) {
                    SnoopModes.MultipleDispatcherMode = true;
                    var thread = new Thread(DispatchOut);
                    thread.Start(rootVisuals);
                }
            }
        }

        static void DispatchOut(object o) {
            var visuals = (List<Visual>) o;
            foreach (var v in visuals) {
                // launch a snoop ui on each dispatcher
                v.Dispatcher.Invoke
                    (
                        (Action)
                            (
                                () => {
                                    var snoopOtherDispatcher = new SnoopUI();
                                    snoopOtherDispatcher.Inspect(v, v as Window);
                                }
                                )
                    );
            }
        }

        delegate void Action();

        #endregion

        #region Public Properties

        #region VisualTreeItems

        /// <summary>
        ///     This is the collection of VisualTreeItem(s) that the visual tree TreeView binds to.
        /// </summary>
        public ObservableCollection<VisualTreeItem> VisualTreeItems { get; } =
            new ObservableCollection<VisualTreeItem>();

        #endregion

        #region Root

        /// <summary>
        ///     Root element of the visual tree
        /// </summary>
        public VisualTreeItem Root {
            get { return rootVisualTreeItem; }
            private set {
                rootVisualTreeItem = value;
                OnPropertyChanged("Root");
            }
        }

        /// <summary>
        ///     rootVisualTreeItem is the VisualTreeItem for the root you are inspecting.
        /// </summary>
        VisualTreeItem rootVisualTreeItem;

        /// <summary>
        ///     root is the object you are inspecting.
        /// </summary>
        object root;

        #endregion

        #region CurrentSelection

        /// <summary>
        ///     Currently selected item in the tree view.
        /// </summary>
        public VisualTreeItem CurrentSelection {
            get { return currentSelection; }
            set {
                if (currentSelection != value) {
                    if (currentSelection != null) {
                        SaveEditedProperties(currentSelection);
                        currentSelection.IsSelected = false;
                    }

                    currentSelection = value;
                    if (currentSelection != null) {
                        currentSelection.IsSelected = true;
                        _lastNonNullSelection = currentSelection;
                    }
                    Tree.Select(value);

                    OnPropertyChanged("CurrentSelection");
                    OnPropertyChanged("CurrentFocusScope");

                    if (VisualTreeItems.Count > 1 ||
                        VisualTreeItems.Count == 1 && VisualTreeItems[0] != rootVisualTreeItem) {
                        // Check whether the selected item is filtered out by the filter,
                        // in which case reset the filter.
                        var tmp = currentSelection;
                        while (tmp != null && !VisualTreeItems.Contains(tmp)) {
                            tmp = tmp.Parent;
                        }
                        if (tmp == null) {
                            // The selected item is not a descendant of any root.
                            RefreshCommand.Execute(null, this);
                        }
                    }
                }
            }
        }

        VisualTreeItem currentSelection;
        VisualTreeItem _lastNonNullSelection;

        #endregion

        #region Filter

        /// <summary>
        ///     This Filter property is bound to the editable combo box that the user can type in to filter the visual tree
        ///     TreeView.
        ///     Every time the user types a key, the setter gets called, enqueueing a delayed call to the ProcessFilter method.
        /// </summary>
        public string Filter {
            get { return filter; }
            set {
                filter = value;

                if (!fromTextBox) {
                    EnqueueAfterSettingFilter();
                }
                else {
                    filterTimer.Stop();
                    filterTimer.Start();
                }
            }
        }

        void SetFilter(string value) {
            fromTextBox = false;
            Filter = value;
            fromTextBox = true;
        }

        void EnqueueAfterSettingFilter() {
            filterCall.Enqueue();

            OnPropertyChanged("Filter");
        }

        string filter = string.Empty;

        #endregion

        #region EventFilter

        public string EventFilter {
            get { return eventFilter; }
            set {
                eventFilter = value;
                EventsListener.Filter = value;
            }
        }

        #endregion

        #region CurrentFocus

        public IInputElement CurrentFocus {
            get {
                var newFocus = Keyboard.FocusedElement;
                if (newFocus != currentFocus) {
                    // Store reference to previously focused element only if focused element was changed.
                    previousFocus = currentFocus;
                }
                currentFocus = newFocus;

                return returnPreviousFocus ? previousFocus : currentFocus;
            }
        }

        #endregion

        #region CurrentFocusScope

        public object CurrentFocusScope {
            get {
                if (CurrentSelection == null)
                    return null;

                var selectedItem = CurrentSelection.Target as DependencyObject;
                if (selectedItem != null) {
                    return FocusManager.GetFocusScope(selectedItem);
                }
                return null;
            }
        }

        #endregion

        #endregion

        #region Public Methods

        public void Inspect() {
            var root = FindRoot();
            if (root == null) {
                if (!SnoopModes.MultipleDispatcherMode) {
                    //SnoopModes.MultipleDispatcherMode is always false for all scenarios except for cases where we are running multiple dispatchers.
                    //If SnoopModes.MultipleDispatcherMode was set to true, then there definitely was a root visual found in another dispatcher, so
                    //the message below would be wrong.
                    MessageBox.Show
                        (
                            "Can't find a current application or a PresentationSource root visual!",
                            "Can't Snoop",
                            MessageBoxButton.OK,
                            MessageBoxImage.Exclamation
                        );
                }

                return;
            }
            Load(root);

            var ownerWindow = SnoopWindowUtils.FindOwnerWindow();
            if (ownerWindow != null) {
                if (ownerWindow.Dispatcher != Dispatcher) {
                    return;
                }
                Owner = ownerWindow;

                // watch for window closing so we can spit out the changed properties
                ownerWindow.Closing += SnoopedWindowClosingHandler;
            }

            SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);
            Dispatcher.UnhandledException += UnhandledExceptionHandler;

            Show();
            Activate();
        }

        public void Inspect(object root, Window ownerWindow) {
            Dispatcher.UnhandledException += UnhandledExceptionHandler;

            Load(root);

            if (ownerWindow != null) {
                Owner = ownerWindow;

                // watch for window closing so we can spit out the changed properties
                ownerWindow.Closing += SnoopedWindowClosingHandler;
            }

            SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);

            Show();
            Activate();
        }

        void UnhandledExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e) {
            if (SnoopModes.IgnoreExceptions) {
                return;
            }

            if (SnoopModes.SwallowExceptions) {
                e.Handled = true;
                return;
            }

            // should we check if the exception came from Snoop? perhaps seeing if any Snoop call is in the stack trace?
            var dialog = new ErrorDialog();
            dialog.Exception = e.Exception;
            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
                e.Handled = true;
        }

        public void ApplyReduceDepthFilter(VisualTreeItem newRoot) {
            if (m_reducedDepthRoot != newRoot) {
                if (m_reducedDepthRoot == null) {
                    Dispatcher.BeginInvoke
                        (
                            DispatcherPriority.Background,
                            (function)
                                delegate {
                                    VisualTreeItems.Clear();
                                    VisualTreeItems.Add(m_reducedDepthRoot);
                                    m_reducedDepthRoot = null;
                                }
                        );
                }
                m_reducedDepthRoot = newRoot;
            }
        }


        /// <summary>
        ///     Loop through the properties in the current PropertyGrid and save away any properties
        ///     that have been changed by the user.
        /// </summary>
        /// <param name="owningObject">
        ///     currently selected object that owns the properties in the grid (before changing selection to
        ///     the new object)
        /// </param>
        void SaveEditedProperties(VisualTreeItem owningObject) {
            foreach (var property in PropertyGrid.PropertyGrid.Properties) {
                if (property.IsValueChangedByUser) {
                    EditedPropertiesHelper.AddEditedProperty(Dispatcher, owningObject, property);
                }
            }
        }

        #endregion

        #region Protected Event Overrides

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
        }

        /// <summary>
        ///     Cleanup when closing the window.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e) {
            base.OnClosing(e);

            // unsubscribe to owner window closing event
            // replaces previous attempts to hookup to MainWindow.Closing on the wrong dispatcher thread
            // This one should be running on the right dispatcher thread since this SnoopUI instance
            // is wired up to the dispatcher thread/window that it owns
            if (Owner != null) {
                Owner.Closing -= SnoopedWindowClosingHandler;
            }

            CurrentSelection = null;

            InputManager.Current.PreProcessInput -= HandlePreProcessInput;
            EventsListener.Stop();

            EditedPropertiesHelper.DumpObjectsWithEditedProperties();

            // persist the window placement details to the user settings.
            var wp = new WINDOWPLACEMENT();
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.GetWindowPlacement(hwnd, out wp);

            // persist whether all properties are shown by default
            Settings.Default.ShowDefaults = PropertyGrid.ShowDefaults;

            // persist whether the previewer is shown by default
            Settings.Default.ShowPreviewer = PreviewArea.IsActive;

            // actually do the persisting
            Settings.Default.Save();

            SnoopPartsRegistry.RemoveSnoopVisualTreeRoot(this);
        }

        /// <summary>
        ///     Event handler for a snooped window closing. This is our chance to spit out
        ///     all the properties that changed during the snoop session for that window.
        ///     Note: there may be multiple snooped windows (when in multiple dispatcher mode)
        ///     and each window is hooked up to it's own instance of SnoopUI and this event.
        /// </summary>
        void SnoopedWindowClosingHandler(object sender, CancelEventArgs e) {
            // changing the selection captures any changes in the selected item at the time of window closing 
            CurrentSelection = null;
            EditedPropertiesHelper.DumpObjectsWithEditedProperties();
        }

        #endregion

        #region Private Routed Event Handlers

        /// <summary>
        ///     Just for fun, the ability to run Snoop on itself :)
        /// </summary>
        void HandleIntrospection(object sender, ExecutedRoutedEventArgs e) {
            Load(this);
        }

        void HandleRefresh(object sender, ExecutedRoutedEventArgs e) {
            var saveCursor = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
            try {
                var currentTarget = CurrentSelection != null ? CurrentSelection.Target : null;

                VisualTreeItems.Clear();

                Root = VisualTreeItem.Construct(root, null);

                if (currentTarget != null) {
                    var visualItem = FindItem(currentTarget);
                    if (visualItem != null)
                        CurrentSelection = visualItem;
                }

                SetFilter(filter);
            }
            finally {
                Mouse.OverrideCursor = saveCursor;
            }
        }

        void HandleHelp(object sender, ExecutedRoutedEventArgs e) {
            //Help help = new Help();
            //help.Show();
        }

        void HandleInspect(object sender, ExecutedRoutedEventArgs e) {
            var visual = e.Parameter as Visual;
            if (visual != null) {
                var node = FindItem(visual);
                if (node != null)
                    CurrentSelection = node;
            }
            else if (e.Parameter != null) {
                PropertyGrid.SetTarget(e.Parameter);
            }
        }

        void HandleSelectFocus(object sender, ExecutedRoutedEventArgs e) {
            // We know we've stolen focus here. Let's use previously focused element.
            returnPreviousFocus = true;
            SelectItem(CurrentFocus as DependencyObject);
            returnPreviousFocus = false;
            OnPropertyChanged("CurrentFocus");
        }

        void HandleSelectFocusScope(object sender, ExecutedRoutedEventArgs e) {
            SelectItem(e.Parameter as DependencyObject);
        }

        void ClearSearchFilterHandler(object sender, ExecutedRoutedEventArgs e) {
            PropertyGrid.StringFilter = string.Empty;
        }

        void CopyPropertyChangesHandler(object sender, ExecutedRoutedEventArgs e) {
            if (currentSelection != null)
                SaveEditedProperties(currentSelection);

            EditedPropertiesHelper.DumpObjectsWithEditedProperties();
        }

        void SelectItem(DependencyObject item) {
            if (item != null) {
                var node = FindItem(item);
                if (node != null)
                    CurrentSelection = node;
            }
        }

        #endregion

        #region Private Event Handlers

        void HandlePreProcessInput(object sender, PreProcessInputEventArgs e) {
            OnPropertyChanged("CurrentFocus");

            var currentModifiers = InputManager.Current.PrimaryKeyboardDevice.Modifiers;
            if (!((currentModifiers & ModifierKeys.Control) != 0 && (currentModifiers & ModifierKeys.Shift) != 0))
                return;

            var directlyOver = Mouse.PrimaryDevice.DirectlyOver as Visual;
            if ((directlyOver == null) || directlyOver.IsDescendantOf(this))
                return;

            var node = FindItem(directlyOver);
            if (node != null)
                CurrentSelection = node;
        }

        void SnoopUI_MouseWheel(object sender, MouseWheelEventArgs e) {
            PreviewArea.Zoomer.DoMouseWheel(sender, e);
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Find the VisualTreeItem for the specified visual.
        ///     If the item is not found and is not part of the Snoop UI,
        ///     the tree will be adjusted to include the window the item is in.
        /// </summary>
        VisualTreeItem FindItem(object target) {
            var node = rootVisualTreeItem.FindNode(target);
            var rootVisual = rootVisualTreeItem.MainVisual;
            if (node == null) {
                var visual = target as Visual;
                if (visual != null && rootVisual != null) {
                    // If target is a part of the SnoopUI, let's get out of here.
                    if (visual.IsDescendantOf(this)) {
                        return null;
                    }

                    // If not in the root tree, make the root be the tree the visual is in.
                    if (!CommonTreeHelper.IsDescendantOf(visual, rootVisual)) {
                        var presentationSource = PresentationSource.FromVisual(visual);
                        if (presentationSource == null) {
                            return null; // Something went wrong. At least we will not crash with null ref here.
                        }

                        Root = new VisualItem(presentationSource.RootVisual, null);
                    }
                }

                rootVisualTreeItem.Reload();

                node = rootVisualTreeItem.FindNode(target);

                SetFilter(filter);
            }
            return node;
        }

        static string TryGetMainWindowTitle() {
            if (Application.Current != null && Application.Current.MainWindow != null) {
                return Application.Current.MainWindow.Title;
            }
            return string.Empty;
        }

        void HandleTreeSelectedItemChanged(object sender, SelectionChangedEventArgs e) {
            var item = Tree.SelectedItem as VisualTreeItem;
            if (item != null)
                CurrentSelection = item;
        }

        void ProcessFilter() {
            if (SnoopModes.MultipleDispatcherMode && !Dispatcher.CheckAccess()) {
                Action action = () => ProcessFilter();
                Dispatcher.BeginInvoke(action);
                return;
            }

            VisualTreeItems.Clear();

            // cplotts todo: we've got to come up with a better way to do this.
            if (filter == "Clear any filter applied to the tree view") {
                SetFilter(string.Empty);
            }
            else if (filter == "Show only visuals with binding errors") {
                FilterBindings(rootVisualTreeItem);
            }
            else if (filter.Length == 0) {
                VisualTreeItems.Add(rootVisualTreeItem);
            }
            else {
                Tree.Filter(filter);
            }
        }

        void FilterBindings(VisualTreeItem node) {
            foreach (var child in node.Children) {
                if (child.HasBindingError)
                    VisualTreeItems.Add(child);
                else
                    FilterBindings(child);
            }
        }

        object FindRoot() {
            object root = null;

            if (SnoopModes.MultipleDispatcherMode) {
                foreach (PresentationSource presentationSource in PresentationSource.CurrentSources) {
                    if
                        (
                        presentationSource.RootVisual != null &&
                        presentationSource.RootVisual is UIElement &&
                        ((UIElement) presentationSource.RootVisual).Dispatcher.CheckAccess()
                        ) {
                        root = presentationSource.RootVisual;
                        break;
                    }
                }
            }
            else if (Application.Current != null) {
                root = Application.Current;
            }
            else {
                // if we don't have a current application,
                // then we must be in an interop scenario (win32 -> wpf or windows forms -> wpf).


                // in this case, let's iterate over PresentationSource.CurrentSources,
                // and use the first non-null, visible RootVisual we find as root to inspect.
                foreach (PresentationSource presentationSource in PresentationSource.CurrentSources) {
                    if
                        (
                        presentationSource.RootVisual != null &&
                        presentationSource.RootVisual is UIElement &&
                        ((UIElement) presentationSource.RootVisual).Visibility == Visibility.Visible
                        ) {
                        root = presentationSource.RootVisual;
                        break;
                    }
                }


                if (System.Windows.Forms.Application.OpenForms.Count > 0) {
                    // this is windows forms -> wpf interop

                    // call ElementHost.EnableModelessKeyboardInterop to allow the Snoop UI window
                    // to receive keyboard messages. if you don't call this method,
                    // you will be unable to edit properties in the property grid for windows forms interop.
                    ElementHost.EnableModelessKeyboardInterop(this);
                }
            }

            return root;
        }

        void Load(object root) {
            this.root = root;

            VisualTreeItems.Clear();

            Root = VisualTreeItem.Construct(root, null);
            CurrentSelection = rootVisualTreeItem;

            SetFilter(filter);

            OnPropertyChanged("Root");
        }

        #endregion

        #region Private Fields

        bool fromTextBox = true;
        readonly DispatcherTimer filterTimer;

        string propertyFilter = string.Empty;
        string eventFilter = string.Empty;

        readonly DelayedCall filterCall;

        VisualTreeItem m_reducedDepthRoot;

        IInputElement currentFocus;
        IInputElement previousFocus;

        /// <summary>
        ///     Indicates whether CurrentFocus should retur previously focused element.
        ///     This fixes problem where Snoop steals the focus from snooped app.
        /// </summary>
        bool returnPreviousFocus;

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #endregion

    #region NoFocusHyperlink

    public class NoFocusHyperlink : Hyperlink {
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
            OnClick();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            e.Handled = true;
        }
    }

    #endregion

    public class PropertyValueInfo {
        public string PropertyName { get; set; }
        public object PropertyValue { get; set; }
    }

    public class EditedPropertiesHelper {
        static readonly object _lock = new object();

        static readonly Dictionary<Dispatcher, Dictionary<VisualTreeItem, List<PropertyValueInfo>>>
            _itemsWithEditedProperties =
                new Dictionary<Dispatcher, Dictionary<VisualTreeItem, List<PropertyValueInfo>>>();

        public static void AddEditedProperty(Dispatcher dispatcher, VisualTreeItem propertyOwner,
            PropertyInformation propInfo) {
            lock (_lock) {
                List<PropertyValueInfo> propInfoList = null;
                Dictionary<VisualTreeItem, List<PropertyValueInfo>> dispatcherList = null;

                // first get the dictionary we're using for the given dispatcher
                if (!_itemsWithEditedProperties.TryGetValue(dispatcher, out dispatcherList)) {
                    dispatcherList = new Dictionary<VisualTreeItem, List<PropertyValueInfo>>();
                    _itemsWithEditedProperties.Add(dispatcher, dispatcherList);
                }

                // now get the property info list for the owning object 
                if (!dispatcherList.TryGetValue(propertyOwner, out propInfoList)) {
                    propInfoList = new List<PropertyValueInfo>();
                    dispatcherList.Add(propertyOwner, propInfoList);
                }

                // if we already have a property of that name on this object, remove it
                var existingPropInfo = propInfoList.FirstOrDefault(l => l.PropertyName == propInfo.DisplayName);
                if (existingPropInfo != null) {
                    propInfoList.Remove(existingPropInfo);
                }

                // finally add the edited property info
                propInfoList.Add(new PropertyValueInfo {
                    PropertyName = propInfo.DisplayName,
                    PropertyValue = propInfo.Value
                });
            }
        }

        public static void DumpObjectsWithEditedProperties() {
            if (_itemsWithEditedProperties.Count == 0) {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendFormat
                (
                    "Snoop dump as of {0}{1}--- OBJECTS WITH EDITED PROPERTIES ---{1}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Environment.NewLine
                );

            var dispatcherCount = 1;

            foreach (var dispatcherKVP in _itemsWithEditedProperties) {
                if (_itemsWithEditedProperties.Count > 1) {
                    sb.AppendFormat("-- Dispatcher #{0} -- {1}", dispatcherCount++, Environment.NewLine);
                }

                foreach (var objectPropertiesKVP in dispatcherKVP.Value) {
                    sb.AppendFormat("Object: {0}{1}", objectPropertiesKVP.Key, Environment.NewLine);
                    foreach (var propInfo in objectPropertiesKVP.Value) {
                        sb.AppendFormat
                            (
                                "\tProperty: {0}, New Value: {1}{2}",
                                propInfo.PropertyName,
                                propInfo.PropertyValue,
                                Environment.NewLine
                            );
                    }
                }

                if (_itemsWithEditedProperties.Count > 1) {
                    sb.AppendLine();
                }
            }

            Debug.WriteLine(sb.ToString());
            Clipboard.SetText(sb.ToString());
        }
    }
}