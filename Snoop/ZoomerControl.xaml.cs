// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Snoop.Infrastructure;

namespace Snoop {
    /// <summary>
    ///     Interaction logic for ZoomerControl.xaml
    /// </summary>
    public partial class ZoomerControl : UserControl {
        const double ZoomFactor = 1.1;
        readonly TransformGroup transform = new TransformGroup();

        readonly TranslateTransform translation = new TranslateTransform();
        readonly ScaleTransform zoom = new ScaleTransform();


        Brush _pooSniffer;
        Point downPoint;

        public ZoomerControl() {
            InitializeComponent();

            transform.Children.Add(zoom);
            transform.Children.Add(translation);

            Viewbox.RenderTransform = transform;

//			DependencyPropertyDescriptor.FromProperty(TargetProperty, typeof(ZoomerControl)).AddValueChanged(this, TargetChanged);
        }


        protected override bool HandlesScrolling {
            get { return base.HandlesScrolling; }
        }


        bool IsValidTarget {
            get { return Target != null && Target != _pooSniffer; }
        }


        void Content_MouseDown(object sender, MouseButtonEventArgs e) {
            Focus();
            downPoint = e.GetPosition(DocumentRoot);
            DocumentRoot.CaptureMouse();
        }

        void Content_MouseMove(object sender, MouseEventArgs e) {
            if (IsValidTarget && DocumentRoot.IsMouseCaptured) {
                var delta = e.GetPosition(DocumentRoot) - downPoint;
                translation.X += delta.X;
                translation.Y += delta.Y;

                downPoint = e.GetPosition(DocumentRoot);
            }
        }

        void Content_MouseUp(object sender, MouseEventArgs e) {
            DocumentRoot.ReleaseMouseCapture();
        }

        public void DoMouseWheel(object sender, MouseWheelEventArgs e) {
            if (IsValidTarget) {
                var zoom = Math.Pow(ZoomFactor, e.Delta/120.0);
                var offset = e.GetPosition(Viewbox);
                Zoom(zoom, offset);
            }
        }


        void ResetZoomAndTranslation() {
            //Zoom(0, new Point(-this.translation.X, -this.translation.Y));
            //Zoom(1.0 / zoom.ScaleX, new Point());
            zoom.ScaleX = 1.0;
            zoom.ScaleY = 1.0;

            translation.X = 0.0;
            translation.Y = 0.0;
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
        //        if (uiElement.ActualHeight == 0 && uiElement.ActualWidth == 0)//sometimes the actual size might be 0 despite there being a rendered visual with a size greater than 0. This happens often on a custom panel (http://snoopwpf.codeplex.com/workitem/7217). Having a fixed size visual brush remedies the problem.
        //        {
        //            rect.Width = 50;
        //            rect.Height = 50;
        //        }
        //        else
        //        {
        //            rect.Width = uiElement.ActualWidth;
        //            rect.Height = uiElement.ActualHeight;
        //        }
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

        #region Target

        /// <summary>
        ///     Gets or sets the Target property.
        /// </summary>
        public object Target {
            get { return GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        /// <summary>
        ///     Target Dependency Property
        /// </summary>
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register
                (
                    "Target",
                    typeof(object),
                    typeof(ZoomerControl),
                    new FrameworkPropertyMetadata
                        (
                        null,
                        OnTargetChanged
                        )
                );

        /// <summary>
        ///     Handles changes to the Target property.
        /// </summary>
        static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((ZoomerControl) d).OnTargetChanged(e);
        }

        /// <summary>
        ///     Provides derived classes an opportunity to handle changes to the Target property.
        /// </summary>
        protected virtual void OnTargetChanged(DependencyPropertyChangedEventArgs e) {
            ResetZoomAndTranslation();

            if (_pooSniffer == null)
                _pooSniffer = TryFindResource("poo_sniffer_xpr") as Brush;

            Cursor = Target == _pooSniffer ? null : Cursors.SizeAll;

            var element = CreateIfPossible(Target);
            if (element != null)
                Viewbox.Child = element;
        }

        #endregion
    }
}