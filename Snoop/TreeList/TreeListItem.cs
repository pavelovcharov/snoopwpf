using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Snoop.TreeList {
    public class TreeListItem : ContentControl {
        public static readonly DependencyProperty OffsetProperty;        
        public double Offset {
            get { return (double) GetValue(OffsetProperty); }
            set { SetValue(OffsetProperty, value); }
        }
        public static readonly DependencyProperty IsSelectedProperty =
               Selector.IsSelectedProperty.AddOwner(typeof(TreeListItem),
                       new FrameworkPropertyMetadata(false,
                               FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                               (o, args) => {
                                   ((TreeListItem) o).OnIsSelectedChanged((bool) args.OldValue, (bool) args.NewValue);
                               }));

        private void OnIsSelectedChanged(bool oldValue, bool newValue) {
            if (newValue)
                Header.BringIntoView();
        }
        public bool IsSelected {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }
        static TreeListItem() {
            OffsetProperty = DependencyProperty.Register(
                "Offset", typeof(double), typeof(TreeListItem), new PropertyMetadata(default(double)));            
        }

        private VisualTreeItem visualTreeItem;

        public VisualTreeItem VisualTreeItem {
            get { return visualTreeItem; }
            set {
                if (visualTreeItem == value)
                    return;
                visualTreeItem = value;
                OnVisualTreeItemChanged();

            }
        }

        protected ContentPresenter Header { get; private set; }
        protected FrameworkElement HeaderHost { get; private set; }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            Header = (ContentPresenter) GetTemplateChild("PART_ContentPresenter");
            HeaderHost = (FrameworkElement)GetTemplateChild("PART_ContentHost");
            HeaderHost.MouseLeftButtonDown += Header_MouseLeftButtonDown;
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            RaiseEvent(new RoutedEventArgs(Selector.SelectedEvent));
        }

        private void OnVisualTreeItemChanged() {
            if (VisualTreeItem == null)
                Offset = 0d;
            else
                Offset = VisualTreeItem.Depth*16;
        }
    }
}
