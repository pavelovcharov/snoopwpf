// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Infrastructure;

namespace Snoop {
    public partial class Zoomer {
        const double ZoomFactor = 1.1;

        public static readonly RoutedCommand ResetCommand;
        public static readonly RoutedCommand ZoomInCommand;
        public static readonly RoutedCommand ZoomOutCommand;
        public static readonly RoutedCommand PanLeftCommand;
        public static readonly RoutedCommand PanRightCommand;
        public static readonly RoutedCommand PanUpCommand;
        public static readonly RoutedCommand PanDownCommand;
        public static readonly RoutedCommand SwitchTo2DCommand;
        public static readonly RoutedCommand SwitchTo3DCommand;
        readonly TransformGroup transform = new TransformGroup();


        readonly TranslateTransform translation = new TranslateTransform();
        readonly ScaleTransform zoom = new ScaleTransform();
        Point downPoint;
        object target;
        VisualTree3DView visualTree3DView;

        static Zoomer() {
            ResetCommand = new RoutedCommand("Reset", typeof(Zoomer));
            ZoomInCommand = new RoutedCommand("ZoomIn", typeof(Zoomer));
            ZoomOutCommand = new RoutedCommand("ZoomOut", typeof(Zoomer));
            PanLeftCommand = new RoutedCommand("PanLeft", typeof(Zoomer));
            PanRightCommand = new RoutedCommand("PanRight", typeof(Zoomer));
            PanUpCommand = new RoutedCommand("PanUp", typeof(Zoomer));
            PanDownCommand = new RoutedCommand("PanDown", typeof(Zoomer));
            SwitchTo2DCommand = new RoutedCommand("SwitchTo2D", typeof(Zoomer));
            SwitchTo3DCommand = new RoutedCommand("SwitchTo3D", typeof(Zoomer));

            ResetCommand.InputGestures.Add(new MouseGesture(MouseAction.LeftDoubleClick));
            ResetCommand.InputGestures.Add(new KeyGesture(Key.F5));
            ZoomInCommand.InputGestures.Add(new KeyGesture(Key.OemPlus));
            ZoomInCommand.InputGestures.Add(new KeyGesture(Key.Up, ModifierKeys.Control));
            ZoomOutCommand.InputGestures.Add(new KeyGesture(Key.OemMinus));
            ZoomOutCommand.InputGestures.Add(new KeyGesture(Key.Down, ModifierKeys.Control));
            PanLeftCommand.InputGestures.Add(new KeyGesture(Key.Left));
            PanRightCommand.InputGestures.Add(new KeyGesture(Key.Right));
            PanUpCommand.InputGestures.Add(new KeyGesture(Key.Up));
            PanDownCommand.InputGestures.Add(new KeyGesture(Key.Down));
            SwitchTo2DCommand.InputGestures.Add(new KeyGesture(Key.F2));
            SwitchTo3DCommand.InputGestures.Add(new KeyGesture(Key.F3));
        }

        public Zoomer() {
            CommandBindings.Add(new CommandBinding(ResetCommand, HandleReset, CanReset));
            CommandBindings.Add(new CommandBinding(ZoomInCommand, HandleZoomIn));
            CommandBindings.Add(new CommandBinding(ZoomOutCommand, HandleZoomOut));
            CommandBindings.Add(new CommandBinding(PanLeftCommand, HandlePanLeft));
            CommandBindings.Add(new CommandBinding(PanRightCommand, HandlePanRight));
            CommandBindings.Add(new CommandBinding(PanUpCommand, HandlePanUp));
            CommandBindings.Add(new CommandBinding(PanDownCommand, HandlePanDown));
            CommandBindings.Add(new CommandBinding(SwitchTo2DCommand, HandleSwitchTo2D));
            CommandBindings.Add(new CommandBinding(SwitchTo3DCommand, HandleSwitchTo3D, CanSwitchTo3D));

            InheritanceBehavior = InheritanceBehavior.SkipToThemeNext;

            InitializeComponent();

            transform.Children.Add(zoom);
            transform.Children.Add(translation);

            Viewbox.RenderTransform = transform;
        }

        public object Target {
            get { return target; }
            set {
                target = value;
                var element = CreateIfPossible(value);
                if (element != null)
                    Viewbox.Child = element;
            }
        }

        public static void GoBabyGo() {
            Dispatcher dispatcher;
            if (Application.Current == null && !SnoopModes.MultipleDispatcherMode)
                dispatcher = Dispatcher.CurrentDispatcher;
            else
                dispatcher = Application.Current.Dispatcher;

            if (dispatcher.CheckAccess()) {
                var zoomer = new Zoomer();
                zoomer.Magnify();
            }
            else {
                dispatcher.Invoke((Action) GoBabyGo);
            }
        }

        public void Magnify() {
            var root = FindRoot();
            if (root == null) {
                MessageBox.Show
                    (
                        "Can't find a current application or a PresentationSource root visual!",
                        "Can't Magnify",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation
                    );
            }

            Magnify(root);
        }

        public void Magnify(object root) {
            Target = root;

            var ownerWindow = SnoopWindowUtils.FindOwnerWindow();
            if (ownerWindow != null)
                Owner = ownerWindow;

            SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);

            Show();
            Activate();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
        }


        protected override void OnClosing(CancelEventArgs e) {
            base.OnClosing(e);

            Viewbox.Child = null;

            // persist the window placement details to the user settings.
            var wp = new WINDOWPLACEMENT();
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32.GetWindowPlacement(hwnd, out wp);

            SnoopPartsRegistry.RemoveSnoopVisualTreeRoot(this);
        }

        void HandleReset(object target, ExecutedRoutedEventArgs args) {
            translation.X = 0;
            translation.Y = 0;
            zoom.ScaleX = 1;
            zoom.ScaleY = 1;
            zoom.CenterX = 0;
            zoom.CenterY = 0;

            if (visualTree3DView != null) {
                visualTree3DView.Reset();
                ZScaleSlider.Value = 0;
            }
        }

        void CanReset(object target, CanExecuteRoutedEventArgs args) {
            args.CanExecute = true;
            args.Handled = true;
        }

        void HandleZoomIn(object target, ExecutedRoutedEventArgs args) {
            var offset = Mouse.GetPosition(Viewbox);
            Zoom(ZoomFactor, offset);
        }

        void HandleZoomOut(object target, ExecutedRoutedEventArgs args) {
            var offset = Mouse.GetPosition(Viewbox);
            Zoom(1/ZoomFactor, offset);
        }

        void HandlePanLeft(object target, ExecutedRoutedEventArgs args) {
            translation.X -= 5;
        }

        void HandlePanRight(object target, ExecutedRoutedEventArgs args) {
            translation.X += 5;
        }

        void HandlePanUp(object target, ExecutedRoutedEventArgs args) {
            translation.Y -= 5;
        }

        void HandlePanDown(object target, ExecutedRoutedEventArgs args) {
            translation.Y += 5;
        }

        void HandleSwitchTo2D(object target, ExecutedRoutedEventArgs args) {
            if (visualTree3DView != null) {
                Target = this.target;
                visualTree3DView = null;
                ZScaleSlider.Visibility = Visibility.Collapsed;
            }
        }

        void HandleSwitchTo3D(object target, ExecutedRoutedEventArgs args) {
            var visual = this.target as Visual;
            if (visualTree3DView == null && visual != null) {
                try {
                    Mouse.OverrideCursor = Cursors.Wait;
                    visualTree3DView = new VisualTree3DView(visual);
                    Viewbox.Child = visualTree3DView;
                }
                finally {
                    Mouse.OverrideCursor = null;
                }
                ZScaleSlider.Visibility = Visibility.Visible;
            }
        }

        void CanSwitchTo3D(object target, CanExecuteRoutedEventArgs args) {
            args.CanExecute = this.target is Visual;
            args.Handled = true;
        }

        void Content_MouseDown(object sender, MouseButtonEventArgs e) {
            downPoint = e.GetPosition(DocumentRoot);
            DocumentRoot.CaptureMouse();
        }

        void Content_MouseMove(object sender, MouseEventArgs e) {
            if (DocumentRoot.IsMouseCaptured) {
                var delta = e.GetPosition(DocumentRoot) - downPoint;
                translation.X += delta.X;
                translation.Y += delta.Y;

                downPoint = e.GetPosition(DocumentRoot);
            }
        }

        void Content_MouseUp(object sender, MouseEventArgs e) {
            DocumentRoot.ReleaseMouseCapture();
        }

        void Content_MouseWheel(object sender, MouseWheelEventArgs e) {
            var zoom = Math.Pow(ZoomFactor, e.Delta/120.0);
            var offset = e.GetPosition(Viewbox);
            Zoom(zoom, offset);
        }

        void ZScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (visualTree3DView != null) {
                visualTree3DView.ZScale = Math.Pow(10, e.NewValue);
            }
        }

        UIElement CreateIfPossible(object item) {
            return ZoomerUtilities.CreateIfPossible(item);
        }

        //private UIElement CreateIfPossible(object item)
        //{
        //    if (item is Window && VisualTreeHelper.GetChildrenCount((Visual)item) == 1)
        //        item = VisualTreeHelper.GetChild((Visual)item, 0);

        //    if (item is FrameworkElement)
        //    {
        //        FrameworkElement uiElement = (FrameworkElement)item;
        //        VisualBrush brush = new VisualBrush(uiElement);
        //        brush.Stretch = Stretch.Uniform;
        //        Rectangle rect = new Rectangle();
        //        rect.Fill = brush;
        //        rect.Width = uiElement.ActualWidth;
        //        rect.Height = uiElement.ActualHeight;
        //        return rect;
        //    }

        //    else if (item is ResourceDictionary)
        //    {
        //        StackPanel stackPanel = new StackPanel();

        //        foreach (object value in ((ResourceDictionary)item).Values)
        //        {
        //            UIElement element = CreateIfPossible(value);
        //            if (element != null)
        //                stackPanel.Children.Add(element);
        //        }
        //        return stackPanel;
        //    }
        //    else if (item is Brush)
        //    {
        //        Rectangle rect = new Rectangle();
        //        rect.Width = 10;
        //        rect.Height = 10;
        //        rect.Fill = (Brush)item;
        //        return rect;
        //    }
        //    else if (item is ImageSource)
        //    {
        //        Image image = new Image();
        //        image.Source = (ImageSource)item;
        //        return image;
        //    }
        //    return null;
        //}

        void Zoom(double zoom, Point offset) {
            var v = new Vector((1 - zoom)*offset.X, (1 - zoom)*offset.Y);

            var translationVector = v*transform.Value;
            translation.X += translationVector.X;
            translation.Y += translationVector.Y;

            this.zoom.ScaleX = this.zoom.ScaleX*zoom;
            this.zoom.ScaleY = this.zoom.ScaleY*zoom;
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
                // try to use the application's main window (if visible) as the root
                if (Application.Current.MainWindow != null &&
                    Application.Current.MainWindow.Visibility == Visibility.Visible) {
                    root = Application.Current.MainWindow;
                }
                else {
                    // else search for the first visible window in the list of the application's windows
                    foreach (Window window in Application.Current.Windows) {
                        if (window.Visibility == Visibility.Visible) {
                            root = window;
                            break;
                        }
                    }
                }
            }
            else {
                // if we don't have a current application,
                // then we must be in an interop scenario (win32 -> wpf or windows forms -> wpf).

                if (System.Windows.Forms.Application.OpenForms.Count > 0) {
                    // this is windows forms -> wpf interop

                    // call ElementHost.EnableModelessKeyboardInterop
                    // to allow the Zoomer window to receive keyboard messages.
                    ElementHost.EnableModelessKeyboardInterop(this);
                }
            }

            if (root == null) {
                // if we still don't have a root to magnify

                // let's iterate over PresentationSource.CurrentSources,
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
            }

            // if the root is a window, let's magnify the window's content.
            // this is better, as otherwise, you will have window background along with the window's content.
            if (root is Window && ((Window) root).Content != null)
                root = ((Window) root).Content;

            return root;
        }

        void SetOwnerWindow() {
            Window ownerWindow = null;

            if (SnoopModes.MultipleDispatcherMode) {
                foreach (PresentationSource presentationSource in PresentationSource.CurrentSources) {
                    if
                        (
                        presentationSource.RootVisual is Window &&
                        ((Window) presentationSource.RootVisual).Dispatcher.CheckAccess()
                        ) {
                        ownerWindow = (Window) presentationSource.RootVisual;
                        break;
                    }
                }
            }
            else if (Application.Current != null) {
                if (Application.Current.MainWindow != null &&
                    Application.Current.MainWindow.Visibility == Visibility.Visible) {
                    // first: set the owner window as the current application's main window, if visible.
                    ownerWindow = Application.Current.MainWindow;
                }
                else {
                    // second: try and find a visible window in the list of the current application's windows
                    foreach (Window window in Application.Current.Windows) {
                        if (window.Visibility == Visibility.Visible) {
                            ownerWindow = window;
                            break;
                        }
                    }
                }
            }

            if (ownerWindow == null) {
                // third: try and find a visible window in the list of current presentation sources
                foreach (PresentationSource presentationSource in PresentationSource.CurrentSources) {
                    if
                        (
                        presentationSource.RootVisual is Window &&
                        ((Window) presentationSource.RootVisual).Visibility == Visibility.Visible
                        ) {
                        ownerWindow = (Window) presentationSource.RootVisual;
                        break;
                    }
                }
            }

            if (ownerWindow != null)
                Owner = ownerWindow;
        }

        delegate void Action();
    }

    public class DoubleToWhitenessConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var val = (float) (double) value;
            var c = new Color();
            c.ScR = val;
            c.ScG = val;
            c.ScB = val;
            c.ScA = 1;

            return new SolidColorBrush(c);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}