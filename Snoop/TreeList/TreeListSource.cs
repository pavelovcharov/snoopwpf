using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace Snoop.TreeList {
    internal class TreeListSource : CollectionView {
        static readonly Action<CollectionView> invalidateEnumerableWrapper =
            ReflectionHelper.CreateInstanceMethodHandler<CollectionView, Action<CollectionView>>(null,
                "InvalidateEnumerableWrapper", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly SnoopUI ui;

        VisualTreeItem root;
        List<VisualTreeItem> visibleItems;

        public TreeListSource(SnoopUI ui) : base(Enumerable.Empty<object>()) {
            this.ui = ui;
            visibleItems = new List<VisualTreeItem>();
            ui.PropertyChanged += OnUIPropertyChanged;
        }

        public override int Count {
            get { return GetCount(); }
        }

        public override IEnumerable SourceCollection {
            get { return visibleItems; }
        }

        int GetCount() {
            if (root == null)
                return 0;
            var count = 0;
            root.Iterate(x => x.IsExpanded, x => count++);
            return count;
        }

        public override object GetItemAt(int index) {
            if (root == null)
                return null;
            return visibleItems[index];
        }

        void OnUIPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == "Root") {
                if (root != null) {
                    root.ChildExpandedChanged -= OnChildExpandedChanged;
                    root.BeginUpdate -= OnRootBeginUpdateChild;
                    root.EndUpdate -= OnRootEndUpdateChild;
                }
                root = ui.Root;
                if (root != null) {
                    root.ChildExpandedChanged += OnChildExpandedChanged;
                    root.BeginUpdate += OnRootBeginUpdateChild;
                    root.EndUpdate += OnRootEndUpdateChild;
                }
                visibleItems = new List<VisualTreeItem> {root};
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        void OnRootEndUpdateChild(object sender, EventArgs e) {
            var child = (VisualTreeItem) sender;
            if (!child.IsExpanded)
                return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                GetDescendants(child), visibleItems.IndexOf(child) + 1));
        }

        void OnRootBeginUpdateChild(object sender, EventArgs e) {
            var child = (VisualTreeItem) sender;
            if (!child.IsExpanded)
                return;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                GetDescendants(child), visibleItems.IndexOf(child) + 1));
        }

        void OnChildExpandedChanged(object sender, EventArgs e) {
            var vItem = sender as VisualTreeItem;
            var index = visibleItems.IndexOf(vItem);
            var descendants = GetDescendants(vItem);
            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(
                    vItem.IsExpanded ? NotifyCollectionChangedAction.Add : NotifyCollectionChangedAction.Remove,
                    descendants, index + 1));
        }

        static List<VisualTreeItem> GetDescendants(VisualTreeItem vItem) {
            var descendants = new List<VisualTreeItem>();
            vItem.Iterate(x => x == vItem || x.IsExpanded, descendants.Add);
            descendants.Remove(vItem);
            return descendants;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs args) {
            try {
                invalidateEnumerableWrapper(this);
                switch (args.Action) {
                    case NotifyCollectionChangedAction.Add:
                        var newIndex = args.NewStartingIndex;
                        foreach (VisualTreeItem item in args.NewItems) {
                            visibleItems.Insert(newIndex, item);
                            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Add, new List<object> {item}, newIndex++));
                        }
                        return;
                    case NotifyCollectionChangedAction.Remove:
                        var oldIndex = args.OldStartingIndex + args.OldItems.Count - 1;
                        foreach (VisualTreeItem item in args.OldItems.OfType<object>().Reverse()) {
                            visibleItems.RemoveAt(oldIndex);
                            base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                                NotifyCollectionChangedAction.Remove, new List<object> {item}, oldIndex--));
                        }
                        return;
                    default:
                        base.OnCollectionChanged(args);
                        return;
                }
            }
            finally {}
        }

        //        return false;
        //    if (position > visibleItems.Count) {
        //    }
        //        return true;
        //        SetCurrent(null, -1);
        //    if (position == -1) {

        //public override bool MoveCurrentToPosition(int position) {
        //}
        //    return true;
        //    SetCurrent(vItem, index);
        //    var index = visibleItems.IndexOf(vItem);
        //        return false;
        //    if (vItem == null && item != null)
        //    var vItem = item as VisualTreeItem;

        //public override bool MoveCurrentTo(object item) {
        //    }
        //    SetCurrent(visibleItems[position], position);
        //    return true;            
        //}
    }
}