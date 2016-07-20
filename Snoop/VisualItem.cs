// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Diagnostics;
using System.Windows.Documents;
using System.Windows.Media;
using ReflectionFramework;

namespace Snoop {
    /// <summary>
    ///     Main class that represents a visual in the visual tree
    /// </summary>
    public class VisualItem : ResourceContainerItem {
        AdornerContainer adorner;

        public VisualItem(object visual, VisualTreeItem parent) : base(visual, parent) {
            Visual = visual;
            if (visual is Visual) {
                var vVisual = (Visual) visual;
                VisualDiagnostics.VisualTreeChanged += VisualDiagnostics_VisualTreeChanged;
            }
        }

        public object Visual { get; }


        public override bool HasBindingError {
            get {
                var propertyDescriptors =
                    TypeDescriptor.GetProperties(Visual,
                        new Attribute[] {new PropertyFilterAttribute(PropertyFilterOptions.All)});
                foreach (PropertyDescriptor property in propertyDescriptors) {
                    var dpd = DependencyPropertyDescriptor.FromProperty(property);
                    if (dpd != null) {
                        var expression = Visual is DependencyObject
                            ? BindingOperations.GetBindingExpressionBase((DependencyObject) Visual,
                                dpd.DependencyProperty)
                            : null;
                        if (expression != null && (expression.HasError || expression.Status != BindingStatus.Active))
                            return true;
                    }
                }
                return false;
            }
        }

        public override object MainVisual {
            get { return Visual; }
        }

        public override Brush Foreground {
            get {
                if (Visual.Wrap<IIChrome>()!=null)
                    return Brushes.Green;
                if (Visual.Wrap<IIFrameworkRenderElementContext>()!=null)
                    return Brushes.Red;
                return base.Foreground;
            }
        }

        public override Brush TreeBackgroundBrush {
            get { return Brushes.Transparent; }
        }

        public override Brush VisualBrush {
            get {
                VisualBrush brush = null;
                if (Visual is Visual)
                    brush = new VisualBrush((Visual) Visual);
                if (Visual.Wrap<IFrameworkRenderElementContext>() != null)
                    brush = new VisualBrush(new FREDrawingVisual(Visual));
                if (brush == null)
                    return null;

                brush.Stretch = Stretch.Uniform;
                return brush;
            }
        }


        protected override ResourceDictionary ResourceDictionary {
            get {
                var element = Visual as FrameworkElement;
                if (element != null)
                    return element.Resources;
                return null;
            }
        }

        protected override bool GetHasChildren() {
            return Visual != null && CommonTreeHelper.GetChildrenCount(Visual) > 0 || Visual is Window;
        }

        void VisualDiagnostics_VisualTreeChanged(object sender, VisualTreeChangeEventArgs e) {
            if (!VisualDiagnosticsExtensions.Enabled)
                return;
            if (Equals(e.Parent, Visual))
                Reload();
        }


        protected override void OnSelectionChanged() {
            // Add adorners for the visual this is representing.
            var visual_ = Visual as Visual;
            var offset = new Thickness();
            if (visual_ == null) {
                var frec = Visual.Wrap<IFrameworkRenderElementContext>();
                if (frec != null && CommonTreeHelper.IsVisible(frec)) {
                    FrameworkElement fe = frec.ElementHost.Parent;
                    visual_ = fe;
                    var rect =
                        ((Transform) RenderTreeHelper.TransformToRoot(frec)).TransformBounds(new Rect(frec.RenderSize));
                    offset = new Thickness(rect.Left, rect.Top, fe.RenderSize.Width - rect.Right,
                        fe.RenderSize.Height - rect.Bottom);
                }
            }
            var adorners = visual_ == null ? null : AdornerLayer.GetAdornerLayer(visual_);
            var visualElement = visual_ as UIElement;

            if (adorners != null && visualElement != null) {
                if (IsSelected && adorner == null) {
                    var border = new Border();
                    border.BorderThickness = new Thickness(4);
                    border.Margin = offset;
                    var borderColor = new Color();
                    borderColor.ScA = .3f;
                    borderColor.ScR = 1;
                    border.BorderBrush = new SolidColorBrush(borderColor);

                    border.IsHitTestVisible = false;
                    adorner = new AdornerContainer(visualElement);
                    adorner.Child = border;
                    adorners.Add(adorner);
                }
                else if (adorner != null) {
                    adorners.Remove(adorner);
                    adorner.Child = null;
                    adorner = null;
                }
            }
        }

        protected override void FillChildrenImpl() {
            base.FillChildrenImpl();
            for (var i = 0; i < CommonTreeHelper.GetChildrenCount(Visual); i++) {
                var child = CommonTreeHelper.GetChild(Visual, i);
                if (child != null) {
                    Children.Add(Construct(child, this));
                }
            }

            var grid = Visual as Grid;
            if (grid != null) {
                foreach (var row in grid.RowDefinitions)
                    Children.Add(Construct(row, this));
                foreach (var column in grid.ColumnDefinitions)
                    Children.Add(Construct(column, this));
            }
        }
    }

    public class FREDrawingVisual : DrawingVisual {
        IFrameworkRenderElementContext context;

        public FREDrawingVisual(object c) {
            this.context = c.Wrap<IFrameworkRenderElementContext>();            
            using (var dc = RenderOpen()) {
                context.Render(dc);                                
                var controls = new[] {context}.Concat(RenderTreeHelper.RenderDescendants(context));
                foreach (var ctrl in controls) {
                    var rcbc = ctrl.Wrap<IRenderControlBaseContext>();
                    if (rcbc==null)
                        continue;
                    dc.PushTransform(rcbc.GeneralTransform);
                    dc.DrawRectangle(new VisualBrush(rcbc.Control), null, new Rect(new Point(0, 0), rcbc.RenderSize));
                    dc.Pop();
                }
                dc.Close();
            }
        }
    }

    public static class CommonTreeHelper {
        public static int GetChildrenCount(object source) {
            var chrome = source.Wrap<IChrome>();
            if (chrome!=null) {
                var root = chrome.Root;
                if (root != null)
                    return 1;
            }
            var ifrec = source.Wrap<IIFrameworkRenderElementContext>();
            if (ifrec!=null) {
                var rcbc = source.Wrap<IRenderControlBaseContext>();
                var hasControl = rcbc!=null && rcbc.ElementHost!=null && rcbc.Control != null ? 1 : 0;
                return ifrec.RenderChildrenCount + hasControl;
            }
            if (source is Visual)
                return VisualTreeHelper.GetChildrenCount((Visual) source);
            return 0;
        }

        public static object GetChild(object source, int index) {
            var chrome = source.Wrap<IChrome>();
            if (chrome!=null) {
                return chrome.Root;
            }
            var ifrec = source.Wrap<IIFrameworkRenderElementContext>();
            if (ifrec!=null) {
                var control = source.Wrap<IRenderControlBaseContext>()?.Control;
                var rcc = ifrec.RenderChildrenCount;
                if (index >= rcc) {
                    if (index == rcc && control != null)
                        return control;
                    return null;
                }
                return ifrec.GetRenderChild(index);
            }
            if (source is Visual)
                return VisualTreeHelper.GetChild((Visual) source, index);
            return null;
        }

        public static bool IsVisible(IFrameworkRenderElementContext context) {
            return isVisible(context) && context.ElementHost!=null && RenderTreeHelper.RenderAncestors(context).Select(x=>x.Wrap<IFrameworkRenderElementContext>()).All(x => isVisible(x));
        }

        static bool isVisible(IFrameworkRenderElementContext context) {
            return context.Visibility.HasValue
                ? context.Visibility == Visibility.Visible
                : context.Factory.Visibility == Visibility.Visible;
        }

        public static bool IsDescendantOf(object visual, object rootVisual) {
            if (visual is Visual && rootVisual is Visual)
                return ((Visual) visual).IsDescendantOf((Visual) rootVisual);
            var visualFrec = visual.Wrap<IFrameworkRenderElementContext>();
            if (visualFrec!=null &&
                rootVisual.Wrap<IFrameworkRenderElementContext>() != null)
                return RenderTreeHelper.RenderAncestors(visual).Any(x => x == rootVisual);

            if (visualFrec!=null && rootVisual is Visual) {
                return visualFrec.ElementHost.Parent.IsDescendantOf((Visual) rootVisual);
            }
            return false;
        }
    }
}