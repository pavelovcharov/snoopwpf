using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Snoop.Controls;

namespace Snoop.TreeList {
    public class TreeListItem : ContentControl {
        public static readonly DependencyProperty OffsetProperty;

        public static readonly DependencyProperty IsSelectedProperty =
            Selector.IsSelectedProperty.AddOwner(typeof(TreeListItem),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                    (o, args) => { ((TreeListItem) o).OnIsSelectedChanged((bool) args.OldValue, (bool) args.NewValue); }));

        VisualTreeItem visualTreeItem;

        static TreeListItem() {
            OffsetProperty = DependencyProperty.Register(
                "Offset", typeof(double), typeof(TreeListItem), new PropertyMetadata(default(double)));
        }

        public TreeListItem() {
            Height = 16d;
        }

        public double Offset {
            get { return (double) GetValue(OffsetProperty); }
            set { SetValue(OffsetProperty, value); }
        }

        public bool IsSelected {
            get { return (bool) GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        public VisualTreeItem VisualTreeItem {
            get { return visualTreeItem; }
            set {
                if (visualTreeItem == value)
                    return;
                visualTreeItem = value;
                OnVisualTreeItemChanged();
            }
        }

        protected HighlightBox Header { get; private set; }
        protected FrameworkElement HeaderHost { get; private set; }

        void OnIsSelectedChanged(bool oldValue, bool newValue) {
            if (newValue)
                Header.BringIntoView();
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            Header = (HighlightBox) GetTemplateChild("PART_ContentPresenter");
            HeaderHost = (FrameworkElement) GetTemplateChild("PART_ContentHost");
            HeaderHost.MouseLeftButtonDown += Header_MouseLeftButtonDown;
        }

        void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            RaiseEvent(new RoutedEventArgs(Selector.SelectedEvent));
        }

        void OnVisualTreeItemChanged() {
            if (VisualTreeItem == null)
                Offset = 0d;
            else
                Offset = VisualTreeItem.Depth*16;
        }
    }
}