using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Snoop.Shaders.Effects;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Snoop {
    public sealed class QuickWindowChooser {
        public List<ScreenSelectorData> openedWindows = new List<ScreenSelectorData>();

        public QuickWindowChooser() {
            var interestWindows = QWCWindowFinder.GetSortedWindows();
            var indicesByPID = new Dictionary<int, uint>();
            foreach (var screen in Screen.AllScreens.Where((x, i) => true)) {
                var data = new ScreenSelectorData();
                var image2 = new Image {
                    Source = ScreenCaptureHelper.CaptureMonitor(screen, out var bounds, out var scaleX, out var scaleY, out var primaryScaleX, out var primaryScaleY),
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    Effect = new GrayscaleShaderEffect()
                };
                data.GrayScaleImage = image2;
                var vb = new Viewbox {
                    Margin = new Thickness(0)
                };
                var grid = new Grid {ClipToBounds = true, Width = bounds.Width, Height = bounds.Height, Background = Brushes.White};
                var grid3 = new Grid {ClipToBounds = true, Width = bounds.Width, Height = bounds.Height};
                grid3.Children.Add(image2);
                var effect = new ContourShaderEffect {Size = new Point(1 / grid.Width, 1 / grid.Height)};
                grid3.Children.Add(new Viewbox {
                    Child = grid,
                    Effect = effect
                });
                vb.Child = grid3;

                uint index = 10;
                var realBounds = new Rect(screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height);
                var windowsInBounds = interestWindows.Where(x => realBounds.IntersectsWith(x.Bounds)).ToArray();
                foreach (var windowInfo in windowsInBounds) {
                    var scaledBounds = new Rect(windowInfo.Bounds.Left * scaleX, windowInfo.Bounds.Top * scaleY, windowInfo.Bounds.Width * scaleX, windowInfo.Bounds.Height * scaleY);
                    var boundsInScreen = new Rect(new Point(scaledBounds.Left - bounds.Left, scaledBounds.Top - bounds.Top), scaledBounds.Size);

                    if (!indicesByPID.TryGetValue(windowInfo.OwningProcess.Id, out var currentIndex)) {
                        index += 10;
                        currentIndex = index;
                        indicesByPID[windowInfo.OwningProcess.Id] = currentIndex;
                    }

                    var byte4 = BitConverter.GetBytes(currentIndex);
                    var color = Color.FromArgb(255, byte4[0], byte4[1], 255);
                    if (!windowInfo.IsValidProcess) color = Color.FromArgb(255, 255, 0, 255);
                    var border = new Border {
                        Margin = new Thickness(boundsInScreen.Left, boundsInScreen.Top, 0, 0),
                        Width = boundsInScreen.Width,
                        Height = boundsInScreen.Height,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Background = new SolidColorBrush(color)
                    };
                    grid.Children.Add(border);
                    if (windowInfo.IsValidProcess)
                        data.Add(windowInfo, border, effect);
                }

                var captionHeight = SystemParameters.CaptionHeight / (primaryScaleY * scaleY);

                var wnd = new Window {Content = vb, WindowStyle = WindowStyle.None, Topmost = true, ResizeMode = ResizeMode.NoResize, Left = bounds.Left, Top = bounds.Top - captionHeight, Width = bounds.Width * primaryScaleX, Height = bounds.Height * primaryScaleY + captionHeight * 2, UseLayoutRounding = true};
                RenderOptions.SetEdgeMode(wnd, EdgeMode.Aliased);
                RenderOptions.SetBitmapScalingMode(wnd, BitmapScalingMode.NearestNeighbor);
                data.Window = wnd;
                openedWindows.Add(data);
            }

            foreach (var element in openedWindows)
                element.Init();
        }
    }

    public class ScreenSelectorData {
        readonly Dictionary<Border, WindowInfo> infos = new Dictionary<Border, WindowInfo>();
        public Window Window { get; set; }
        public Image GrayScaleImage { get; set; }
        public ContourShaderEffect Effect { get; set; }

        public void Add(WindowInfo windowInfo, Border border, ContourShaderEffect effect) {
            Effect = effect;
            infos.Add(border, windowInfo);
            border.MouseEnter += BorderOnMouseEnter;
            border.MouseLeave += BorderOnMouseLeave;
            border.MouseLeftButtonUp += BorderOnMouseLeftButtonUp;
        }

        void BorderOnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            var border = sender as Border;
            var info = infos[border];
            info.Snoop();
            Application.Current.Shutdown();
        }

        void BorderOnMouseLeave(object sender, MouseEventArgs e) { Update(sender, false); }

        void BorderOnMouseEnter(object sender, MouseEventArgs e) { Update(sender, true); }

        void Update(object sender, bool set) {
            var border = sender as Border;
            var color = ((SolidColorBrush) border.Background).Color;
            Effect.SetSelection(color, set ? (Color?) color : null);
            var effect = (GrayscaleShaderEffect) GrayScaleImage.Effect;
            if (!set) {
                effect.VisibleRect = new Point4D();
            } else {
                var pos = border.TransformToVisual(Window).TransformBounds(new Rect(new Point(), border.RenderSize));
                var w = Window.ActualWidth;
                var h = Window.ActualHeight;
                effect.VisibleRect = new Point4D(pos.Left / w, pos.Top / h, pos.Right / w, pos.Bottom / h);
            }
        }

        public void Init() {
            if (infos.Keys.Count > 1 && infos.Keys.Any())
                foreach (var border in infos.Keys.Where(x => x.Width > 10 && x.Height > 10))
                    if (border.Width <= 10 || border.Height <= 10)
                        border.Visibility = Visibility.Collapsed;

            Window.PreviewKeyDown += WndOnPreviewKeyDown;
            Window.Show();
        }

        void WndOnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Escape)
                return;
            Application.Current.Shutdown();
        }
    }
}