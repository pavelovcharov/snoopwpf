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
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Rectangle = System.Drawing.Rectangle;

namespace Snoop {    
    public partial class JlWindow : Window {
        public JlWindow() {
            InitializeComponent();
            ElementsGrid.Visibility = Visibility.Hidden;
            Update();
        }

        void OnDragDelta(object sender, DragDeltaEventArgs e) {
            this.Left += e.HorizontalChange;
            this.Top += e.VerticalChange;
        }

        bool allowShow = true;
        void OnDragStarted(object sender, DragStartedEventArgs e) {
            allowShow = false;
            ElementsGrid.Visibility = Visibility.Collapsed;
            RootGrid.Margin = defaultPositions[currentPosition];
        }

        void OnDragCompleted(object sender, DragCompletedEventArgs e) {
            allowShow = true;
            Update();
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
                    ((RotateTransform) ElementsGrid.RenderTransform).Angle = -90;
                    RootGrid.Margin = new Thickness(-100, 0, 0, 0);
                    break;
                case Position.Top:
                    Top = screen.WorkingArea.Top;
                    ((RotateTransform) ElementsGrid.RenderTransform).Angle = 0;
                    RootGrid.Margin = new Thickness(0, -100, 0, 0);
                    break;
                case Position.Right:
                    Left= screen.WorkingArea.Right-100;
                    ((RotateTransform)ElementsGrid.RenderTransform).Angle = 90;
                    RootGrid.Margin = new Thickness(0, 0, -100, 0);
                    break;
                case Position.Bottom:
                    Top = screen.WorkingArea.Bottom - 100;
                    ((RotateTransform)ElementsGrid.RenderTransform).Angle = 180;
                    RootGrid.Margin = new Thickness(0, 0, 0, -100);
                    break;
            }
        }

        void WindowMouseEnter(object sender, MouseEventArgs e) {
            ElementsGrid.Visibility = Visibility.Visible;
        }

        void ExitButtonClick(object sender, RoutedEventArgs e) {
            App.Current.Shutdown(0);
        }

        void WindowMouseLeave(object sender, MouseEventArgs e) {
            ElementsGrid.Visibility = Visibility.Collapsed;
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
