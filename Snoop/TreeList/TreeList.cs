using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Snoop.TreeList {
    public class TreeList : Selector {
        static Action<Selector, bool> set_CanSelectMultiple;
        static Func<ItemsControl, Panel> get_ItemsHost;
        static TreeList() {
            set_CanSelectMultiple = ReflectionHelper.CreateInstanceMethodHandler<Selector, Action<Selector, bool>>(null, "set_CanSelectMultiple", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            get_ItemsHost = ReflectionHelper.CreateInstanceMethodHandler<ItemsControl, Func<ItemsControl, Panel>>(null, "get_ItemsHost", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }
        public TreeList() {
            IsSynchronizedWithCurrentItem = true;
            this.ItemsPanel = GetItemsPanelTemplate();
            set_CanSelectMultiple(this, false);
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdateIndicator();
        }

        ItemsPanelTemplate GetItemsPanelTemplate() {
            var result = new ItemsPanelTemplate();
            result.VisualTree = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            result.Seal();            
            return result;
        }

        protected Panel MyItemsHost {
            get { return get_ItemsHost(this); }
        }
        protected override bool IsItemItsOwnContainerOverride(object item) { return false; }
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

        private Border indicator;
        private ScrollViewer scrollViewer;
        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            scrollViewer = (ScrollViewer)GetTemplateChild("PART_ScrollViewer");
            var aContainer = new AdornerContainer(this);
            aContainer.Child =
                (indicator =
                    new Border() {
                        Width = 16,
                        Height = 4,
                        Background = Brushes.DodgerBlue,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        ToolTip = "Current selection"
                    });
            indicator.MouseLeftButtonDown +=
                (o, e) => { (this.MyItemsHost as VirtualizingStackPanel).BringIndexIntoViewPublic(SelectedIndex); };
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

        private DispatcherOperation updateIndicatorOperation;
        private void UpdateIndicator() {
            if (updateIndicatorOperation != null)
                return;
            updateIndicatorOperation = Dispatcher.BeginInvoke(new Action(UpdateIndicatorImpl),
                DispatcherPriority.Render);
        }
        private void UpdateIndicatorImpl() {
            try {
                if (indicator == null || scrollViewer == null)
                    return;
                if (SelectedItem == null || SelectedIndex == -1 || scrollViewer.ExtentHeight < scrollViewer.ViewportHeight) {
                    indicator.Visibility = Visibility.Collapsed;
                    return;
                }
                var height = ActualHeight-16;
                var percent = SelectedIndex / (double)Items.Count;
                indicator.Visibility = Visibility.Visible;
                indicator.Margin = new Thickness(0, height * percent, 0, 0);
            }
            finally {
                updateIndicatorOperation = null;
            }                        
        }
        public void Select(VisualTreeItem item) {
            (ItemsSource as TreeListSource).MoveCurrentTo(item);
        }
        public void Filter(string filter) {
            
        }
    }
}
