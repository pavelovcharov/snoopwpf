using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Snoop.TreeList {
    public class TreeList : Selector {
        static Action<Selector, bool> set_CanSelectMultiple;
        static TreeList() {
            set_CanSelectMultiple = ReflectionHelper.CreateInstanceMethodHandler<Selector, Action<Selector, bool>>(null, "set_CanSelectMultiple", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }
        public TreeList() {
            IsSynchronizedWithCurrentItem = true;
            this.ItemsPanel = GetItemsPanelTemplate();
            set_CanSelectMultiple(this, false);
        }

        ItemsPanelTemplate GetItemsPanelTemplate() {
            var result = new ItemsPanelTemplate();
            result.VisualTree = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
            result.Seal();            
            return result;
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
            base.ClearContainerForItemOverride(element, item);
        }

        public void Select(VisualTreeItem item) {
            (ItemsSource as TreeListSource).MoveCurrentTo(item);
        }
        public void Filter(string filter) {
            
        }
    }
}
