using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Rectangle = System.Drawing.Rectangle;

namespace Snoop {    
    public partial class JlWindow : Window {
        public static readonly DependencyProperty ExtendedVisibilityProperty = DependencyProperty.Register(
            "ExtendedVisibility", typeof(Visibility), typeof(JlWindow), new FrameworkPropertyMetadata(Visibility.Collapsed, null, new CoerceValueCallback((o, value) => ((JlWindow)o).IsPinned ? Visibility.Visible : (Visibility)value)));

        public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(
            "IsPinned", typeof(bool), typeof(JlWindow), new PropertyMetadata(default(bool), (d, e) => IsPinnedChanged(d)));

        static void IsPinnedChanged(DependencyObject d) {
            ((JlWindow)d).CoerceValue(ExtendedVisibilityProperty);
            RegistrySettings.Pinned = ((JlWindow)d).IsPinned;
        }

        public bool IsPinned {
            get { return (bool)GetValue(IsPinnedProperty); }
            set { SetValue(IsPinnedProperty, value); }
        }
        public Visibility ExtendedVisibility {
            get { return (Visibility)GetValue(ExtendedVisibilityProperty); }
            set { SetValue(ExtendedVisibilityProperty, value); }
        }
        HwndSource hwndSource;
        DispatcherTimer flickTimer;
        DispatcherTimer topMostTimer;
        int count = 0;
        Brush cachedBackground;
        Brush contrastBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A3D2FF"));
        public JlWindow() {
            IsPinned = RegistrySettings.Pinned;
            Left = RegistrySettings.Left;
            Top = RegistrySettings.Top;
            CoercePosition();
            InitializeComponent();
            rootPanel.Orientation = RegistrySettings.Orientation;
            DataContext = this;
            MouseEnter += JlWindow_MouseEnter;
            MouseLeave += JlWindow_MouseLeave;
            //ElementsGrid.Visibility = Visibility.Hidden;
            Update();
            Dispatcher.BeginInvoke(new Action(() => {                
                hwndSource = HwndSource.FromVisual(this) as HwndSource;
                hwndSource.AddHook(OnHwndSourceHook);
                flickTimer.Start();
            }), DispatcherPriority.ApplicationIdle);
            topMostTimer = new DispatcherTimer();
            topMostTimer.Interval = TimeSpan.FromSeconds(5);
            topMostTimer.Tick += JlWindow_HandleUpdateTopMost;
            topMostTimer.Start();
            flickTimer = new DispatcherTimer();
            flickTimer.Interval = TimeSpan.FromMilliseconds(200);
            flickTimer.Tick += FlickTimer_Tick;
            cachedBackground = Root.BorderBrush;
            Deactivated += JlWindow_HandleUpdateTopMost;
            LayoutUpdated += JlWindow_HandleUpdateTopMost;
        }        

        void CoercePosition() {
            Point position = new Point((int)Left, (int)Top);
            var screens = Screen.AllScreens.Select(x => new Rect(x.WorkingArea.X, x.WorkingArea.Y, x.WorkingArea.Width, x.WorkingArea.Height));
            if (screens.Any(x => x.Contains(position)))
                return;
            var centers = screens.Select(x => new Point((x.Left + x.Width)/2, (x.Top + x.Height)/2));
            var distances = centers.Select(x => (position - x).Length);
            var screen = distances.Select((x, i) => new {Index = i, Value = x}).OrderBy(x => x.Value).FirstOrDefault()?.Index ?? 0;
            var screenBounds = Screen.AllScreens[screen].WorkingArea;
            Left = Math.Abs(position.X)%screenBounds.Width + screenBounds.Left;
            Top = Math.Abs(position.Y)%screenBounds.Height + screenBounds.Top;
        }

        void JlWindow_HandleUpdateTopMost(object sender, EventArgs e) {
            UpdateTopMost();
        }        

        private void UpdateTopMost() {
            if (hwndSource == null)
                return;
              NativeMethods.SetWindowPos(hwndSource.Handle,
                                       new IntPtr(-1),
                                       0, 0, 0, 0,
                                       NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        void FlickTimer_Tick(object sender, EventArgs e) {
            if (count >= 10) {
                Root.BorderBrush = cachedBackground;
                Foreground = cachedBackground;
                count = 0;
                flickTimer.Stop();
                return;
            }
            count++;
            if (count%2 == 1) {
                Foreground = contrastBackground;
                Root.BorderBrush = contrastBackground;
            } else {
                Root.BorderBrush = cachedBackground;
                Foreground = cachedBackground;
            }                
        }

        IntPtr OnHwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (msg == NativeMethods.WM_SHOWSNOOP /*|| HotKeyHelper.ProcessMessage(msg, wParam)*/) {                
                Activate();
                flickTimer.Start();
            }
            return IntPtr.Zero;
        }

        private void JlWindow_MouseLeave(object sender, MouseEventArgs e) {
            ExtendedVisibility = Visibility.Collapsed;
        }

        private void JlWindow_MouseEnter(object sender, MouseEventArgs e) {
            ExtendedVisibility = Visibility.Visible;
        }

        void OnDragDelta(object sender, DragDeltaEventArgs e) {
            this.Left += e.HorizontalChange;
            this.Top += e.VerticalChange;
        }

        void OnDragStarted(object sender, DragStartedEventArgs e) {
            ExtendedVisibility = Visibility.Collapsed;
        }

        void OnDragCompleted(object sender, DragCompletedEventArgs e) {
            Update();
            ExtendedVisibility = Visibility.Visible;
        }

        enum Position {
            Left,
            Top,
            Right,
            Bottom
        }

        Position currentPosition;

        Dictionary<Position, Thickness> defaultPositions = new Dictionary<Position, Thickness> {
            {Position.Left, new Thickness(-60, 0, 0, 0)},
            {Position.Top, new Thickness(0, -60, 0, 0)},
            {Position.Right, new Thickness(0, 0, -60, 0)},
            {Position.Bottom, new Thickness(0,0,0,-60)}
        };
        void Update() {
            var screen = WpfScreen.GetScreenFrom(this);
            var offsets = new[] {
                new {Pos = Position.Left, Value = Math.Abs(screen.WorkingArea.Left - Left)},
                new {Pos = Position.Top, Value = Math.Abs(screen.WorkingArea.Top - Top)},
                new {Pos = Position.Right, Value = Math.Abs(screen.WorkingArea.Right - Left)},
                new {Pos = Position.Bottom, Value = Math.Abs(screen.WorkingArea.Bottom - Top)}
            };
            var primary = offsets.OrderBy(x => x.Value).FirstOrDefault().Pos;
            currentPosition = primary;
            switch (primary) {
                case Position.Left:
                    Left = screen.WorkingArea.Left;
                    rootPanel.Orientation = Orientation.Vertical;                    
                    break;
                case Position.Top:
                    Top = screen.WorkingArea.Top;
                    rootPanel.Orientation = Orientation.Horizontal;
                    break;
                case Position.Right:
                    Left = screen.WorkingArea.Right - 32;
                    rootPanel.Orientation = Orientation.Vertical;
                    break;
                case Position.Bottom:
                    Top = screen.WorkingArea.Bottom - 32;
                    rootPanel.Orientation = Orientation.Horizontal;
                    break;
            }
            RegistrySettings.Top = (int)Top;
            RegistrySettings.Left = (int)Left;
            RegistrySettings.Orientation = rootPanel.Orientation;
        }        

        void ExitButtonClick(object sender, RoutedEventArgs e) {
            App.Current.Shutdown(0);
        }
        
        public class WpfScreen {
            public static IEnumerable<WpfScreen> AllScreens() {
                foreach (Screen screen in System.Windows.Forms.Screen.AllScreens) {
                    yield return new WpfScreen(screen);
                }
            }

            public static WpfScreen GetScreenFrom(Window window) {
                WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
                Screen screen = System.Windows.Forms.Screen.FromHandle(windowInteropHelper.Handle);
                WpfScreen wpfScreen = new WpfScreen(screen);
                return wpfScreen;
            }

            public static WpfScreen Primary {
                get { return new WpfScreen(System.Windows.Forms.Screen.PrimaryScreen); }
            }

            private readonly Screen screen;

            internal WpfScreen(System.Windows.Forms.Screen screen) {
                this.screen = screen;
            }

            public Rect DeviceBounds {
                get { return this.GetRect(this.screen.Bounds); }
            }

            public Rect WorkingArea {
                get { return this.GetRect(this.screen.WorkingArea); }
            }

            private Rect GetRect(Rectangle value) {
                // should x, y, width, height be device-independent-pixels ??
                return new Rect {
                    X = value.X,
                    Y = value.Y,
                    Width = value.Width,
                    Height = value.Height
                };
            }

            public bool IsPrimary {
                get { return this.screen.Primary; }
            }

            public string DeviceName {
                get { return this.screen.DeviceName; }
            }
        }
    }   
}
