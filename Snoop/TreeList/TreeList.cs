using System;
using System.Collections.Specialized;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Snoop.TreeList {
    public class TreeList : Selector {
        static readonly Action<Selector, bool> set_CanSelectMultiple;
        static readonly Func<ItemsControl, Panel> get_ItemsHost;

        Border indicator;
        ScrollViewer scrollViewer;

        DispatcherOperation updateIndicatorOperation;

        static TreeList() {
            set_CanSelectMultiple = ReflectionHelper.CreateInstanceMethodHandler<Selector, Action<Selector, bool>>(
                null, "set_CanSelectMultiple", BindingFlags.Instance | BindingFlags.NonPublic);
            get_ItemsHost = ReflectionHelper.CreateInstanceMethodHandler<ItemsControl, Func<ItemsControl, Panel>>(null,
                "get_ItemsHost", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public TreeList() {
            IsSynchronizedWithCurrentItem = true;
            ItemsPanel = GetItemsPanelTemplate();
            set_CanSelectMultiple(this, false);
            SizeChanged += OnSizeChanged;
            KeyDown += OnKeyDown;
            MouseLeftButtonDown += TreeList_MouseLeftButtonDown;
        }

        private void TreeList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            Focus();
        }

        void OnKeyDown(object sender, KeyEventArgs e) {
            var selected = SelectedItem as VisualTreeItem;
            switch (e.Key) {
                case Key.Left:
                    e.Handled = true;
                    if (selected==null)
                        break;
                    if (selected.IsExpanded) {
                        selected.IsExpanded = false;                        
                        break;
                    }
                    Items.MoveCurrentTo(selected.Parent);
                    break;
                case Key.Right:
                    e.Handled = true;
                    if (selected == null)
                        break;
                    if (!selected.IsExpanded) {
                        selected.IsExpanded = true;                        
                        break;
                    }
                    Items.MoveCurrentToNext();
                    break;
                case Key.Down:
                    Items.MoveCurrentToNext();
                    e.Handled = true;
                    break;
                case Key.Up:
                    Items.MoveCurrentToPrevious();
                    e.Handled = true;
                    break;
            }
        }

        protected Panel MyItemsHost {
            get { return get_ItemsHost(this); }
        }

        void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateIndicator();
        }                

        ItemsPanelTemplate GetItemsPanelTemplate() {
            var result = new ItemsPanelTemplate();
            result.VisualTree = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            result.Seal();
            return result;
        }

        protected override bool IsItemItsOwnContainerOverride(object item) {
            return false;
        }

        protected override DependencyObject GetContainerForItemOverride() {
            return new TreeListItem();
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item) {
            var tli = element as TreeListItem;
            tli.VisualTreeItem = item as VisualTreeItem;
            base.PrepareContainerForItemOverride(element, item);
        }

        protected override void ClearContainerForItemOverride(DependencyObject element, object item) {
            var tli = element as TreeListItem;
            tli.VisualTreeItem = null;
            base.ClearContainerForItemOverride(element, item);
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            scrollViewer = (ScrollViewer) GetTemplateChild("PART_ScrollViewer");
            var aContainer = new AdornerContainer(this);
            aContainer.Child =
                indicator =
                    new Border {
                        Width = 16,
                        Height = 4,
                        Background = Brushes.DodgerBlue,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        ToolTip = "Current selection"
                    };
            indicator.MouseLeftButtonDown +=
                (o, e) => { (MyItemsHost as VirtualizingStackPanel).BringIndexIntoViewPublic(SelectedIndex); };
            ToolTipService.SetInitialShowDelay(indicator, 0);
            indicator.Cursor = Cursors.Hand;
            AdornerLayer.GetAdornerLayer(this).Add(aContainer);
            UpdateIndicator();
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            UpdateIndicator();
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e) {
            base.OnItemsChanged(e);
            UpdateIndicator();
        }

        void UpdateIndicator() {
            if (updateIndicatorOperation != null)
                return;
            updateIndicatorOperation = Dispatcher.BeginInvoke(new Action(UpdateIndicatorImpl),
                DispatcherPriority.Render);
        }

        void UpdateIndicatorImpl() {
            try {
                if (indicator == null || scrollViewer == null)
                    return;
                if (SelectedItem == null || SelectedIndex == -1 ||
                    scrollViewer.ExtentHeight < scrollViewer.ViewportHeight) {
                    indicator.Visibility = Visibility.Collapsed;
                    return;
                }
                var height = ActualHeight - 16;
                var percent = SelectedIndex/(double) Items.Count;
                indicator.Visibility = Visibility.Visible;
                indicator.Margin = new Thickness(0, height*percent, 0, 0);
            }
            finally {
                updateIndicatorOperation = null;
            }
        }

        public TreeListSource TreeListSource { get { return ItemsSource as TreeListSource; } }

        public void Select(VisualTreeItem item) {
            TreeListSource.MoveCurrentTo(item);
        }

        //public void Filter(string filter) {            
        //}

        //public void FilterBindings() {
        //}        
    }
}