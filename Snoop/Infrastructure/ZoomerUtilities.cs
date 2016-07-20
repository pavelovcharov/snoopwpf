// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ReflectionFramework;

namespace Snoop.Infrastructure {
    public static class ZoomerUtilities {
        public static UIElement CreateIfPossible(object item) {
            if (item is Window && CommonTreeHelper.GetChildrenCount(item) == 1)
                item = CommonTreeHelper.GetChild(item, 0);
            if (item.Wrap<IFrameworkRenderElementContext>()!=null) {
                return CreateRectangleForFrameworkRenderElement(item);
            }
            if (item is FrameworkElement) {
                var uiElement = (FrameworkElement) item;
                return CreateRectangleForFrameworkElement(uiElement);
            }
            if (item is Visual) {
                var visual = (Visual) item;
                return CreateRectangleForVisual(visual);
            }
            if (item is ResourceDictionary) {
                var stackPanel = new StackPanel();

                foreach (var value in ((ResourceDictionary) item).Values) {
                    var element = CreateIfPossible(value);
                    if (element != null)
                        stackPanel.Children.Add(element);
                }
                return stackPanel;
            }
            if (item is Brush) {
                var rect = new Rectangle();
                rect.Width = 10;
                rect.Height = 10;
                rect.Fill = (Brush) item;
                return rect;
            }
            if (item is ImageSource) {
                var image = new Image();
                image.Source = (ImageSource) item;
                return image;
            }
            return null;
        }

        static UIElement CreateRectangleForFrameworkRenderElement(object frec) {
            var brush = new VisualBrush(new FREDrawingVisual(frec));
            brush.Stretch = Stretch.Uniform;
            var rect = new Rectangle();
            rect.Fill = brush;
            var df = (dynamic) frec;
            if (df.RenderSize.Height == 0 && df.RenderSize.Width == 0)
                //sometimes the actual size might be 0 despite there being a rendered visual with a size greater than 0. This happens often on a custom panel (http://snoopwpf.codeplex.com/workitem/7217). Having a fixed size visual brush remedies the problem.
            {
                rect.Width = 50;
                rect.Height = 50;
            }
            else {
                rect.Width = df.RenderSize.Width;
                rect.Height = df.RenderSize.Height;
            }
            return rect;
        }

        static UIElement CreateRectangleForVisual(Visual uiElement) {
            var brush = new VisualBrush(uiElement);
            brush.Stretch = Stretch.Uniform;
            var rect = new Rectangle();
            rect.Fill = brush;
            rect.Width = 50;
            rect.Height = 50;

            return rect;
        }

        static UIElement CreateRectangleForFrameworkElement(FrameworkElement uiElement) {
            var brush = new VisualBrush(uiElement);
            brush.Stretch = Stretch.Uniform;
            var rect = new Rectangle();
            rect.Fill = brush;
            if (uiElement.ActualHeight == 0 && uiElement.ActualWidth == 0)
                //sometimes the actual size might be 0 despite there being a rendered visual with a size greater than 0. This happens often on a custom panel (http://snoopwpf.codeplex.com/workitem/7217). Having a fixed size visual brush remedies the problem.
            {
                rect.Width = 50;
                rect.Height = 50;
            }
            else {
                rect.Width = uiElement.ActualWidth;
                rect.Height = uiElement.ActualHeight;
            }
            return rect;
        }
    }
}