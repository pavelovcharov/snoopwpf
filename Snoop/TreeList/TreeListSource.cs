using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Snoop.TreeList {
    class TreeListSource : CollectionView {
        static readonly Action<CollectionView> invalidateEnumerableWrapper = ReflectionHelper.CreateInstanceMethodHandler<CollectionView, Action<CollectionView>>(null, "InvalidateEnumerableWrapper", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private SnoopUI ui;
        private VisualTreeItem root;
        private List<VisualTreeItem> visibleItems;
        public override int Count {
            get { return GetCount(); }
        }

        private int GetCount() {
            if (root == null)
                return 0;
            int count = 0;
            root.Iterate(x => x.IsExpanded, x => count++);
            return count;
        }

        public override object GetItemAt(int index) {
            if (root == null)
                return null;
            return visibleItems[index];
        }

        public TreeListSource(SnoopUI ui) : base(Enumerable.Empty<object>()) {
            this.ui = ui;
            visibleItems = new List<VisualTreeItem>();
            ui.PropertyChanged += OnUIPropertyChanged;
        }

        void OnUIPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == "Root") {
                if (root != null) {
                    root.ChildExpandedChanged -= OnChildExpandedChanged;
                    root.BeginUpdate -= OnRootBeginUpdateChild;
                    root.EndUpdate -= OnRootEndUpdateChild;
                }                    
                this.root = ui.Root;
                if (root != null) {
                    root.ChildExpandedChanged += OnChildExpandedChanged;
                    root.BeginUpdate += OnRootBeginUpdateChild;
                    root.EndUpdate += OnRootEndUpdateChild;
                }
                visibleItems = new List<VisualTreeItem>() {root};
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private void OnRootEndUpdateChild(object sender, EventArgs e) {
            var child = (VisualTreeItem) sender;
            if (!child.IsExpanded)
                return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                GetDescendants(child), visibleItems.IndexOf(child)+1));
        }

        private void OnRootBeginUpdateChild(object sender, EventArgs e) {
            var child = (VisualTreeItem)sender;
            if (!child.IsExpanded)
                return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                GetDescendants(child), visibleItems.IndexOf(child) + 1));
        }

        private void OnChildExpandedChanged(object sender, EventArgs e) {
            var vItem = sender as VisualTreeItem;
            var index = visibleItems.IndexOf(vItem);
            var descendants = GetDescendants(vItem);
            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                    vItem.IsExpanded ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove,
                    descendants, index + 1));
        }

        private static List<VisualTreeItem> GetDescendants(VisualTreeItem vItem) {
            var descendants = new List<VisualTreeItem>();
            vItem.Iterate(x => x == vItem || x.IsExpanded, descendants.Add);
            descendants.Remove(vItem);
            return descendants;
        }

        public override IEnumerable SourceCollection {
            get { return visibleItems; }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args) {
            try {
                invalidateEnumerableWrapper(this);
                switch (args.Action) {
                    case NotifyCollectionChangedAction.Add:
                        int newIndex = args.NewStartingIndex;
                        foreach (VisualTreeItem item in args.NewItems) {
                            visibleItems.Insert(newIndex, item);
                            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Add, new List<object>() { item }, newIndex++));
                        }
                        return;
                    case NotifyCollectionChangedAction.Remove:
                        int oldIndex = args.OldStartingIndex + args.OldItems.Count - 1;
                        foreach (VisualTreeItem item in args.OldItems.OfType<object>().Reverse()) {
                            visibleItems.RemoveAt(oldIndex);
                            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Remove, new List<object>() { item }, oldIndex--));
                        }
                        return;
                    default:
                        base.OnCollectionChanged(args);
                        return;
                }
            }
            finally {
                
            }            
        }

        //public override bool MoveCurrentTo(object item) {
        //    var vItem = item as VisualTreeItem;
        //    if (vItem == null && item != null)
        //        return false;
        //    var index = visibleItems.IndexOf(vItem);
        //    SetCurrent(vItem, index);
        //    return true;
        //}

        //public override bool MoveCurrentToPosition(int position) {
        //    if (position == -1) {
        //        SetCurrent(null, -1);
        //        return true;
        //    }
        //    if (position > visibleItems.Count) {
        //        return false;
        //    }
        //    SetCurrent(visibleItems[position], position);
        //    return true;            
        //}
    }
}
