// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Snoop {
    /// <summary>
    ///     Simple helper class to allow any UIElements to be used as an Adorner.
    /// </summary>
    public class AdornerContainer : Adorner {
        UIElement child;

        public AdornerContainer(UIElement adornedElement) : base(adornedElement) {}

        protected override int VisualChildrenCount {
            get { return child == null ? 0 : 1; }
        }

        public UIElement Child {
            get { return child; }
            set {
                AddVisualChild(value);
                child = value;
            }
        }

        protected override Visual GetVisualChild(int index) {
            if (index == 0 && child != null)
                return child;
            return base.GetVisualChild(index);
        }

        protected override Size ArrangeOverride(Size finalSize) {
            if (child != null)
                child.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}