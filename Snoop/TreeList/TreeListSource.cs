using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using ReflectionFramework.Attributes;
using ReflectionFramework.Extensions;

namespace Snoop.TreeList {
    [Wrapper, BindingFlags(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)]
    public interface IIndexedEnumerable {
        int Count { get; }
    }

    [Wrapper, BindingFlags(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)]
    public interface ITreeListSource {
        IEnumerable EnumerableWrapper { get; }
        void InvalidateEnumerableWrapper();
    }

    public class TreeListSource : ListCollectionView {
        readonly ITreeListSource ProtectedMethods;
        readonly SnoopUI ui;

        VisualTreeItem root;
        ObservableCollection<VisualTreeItem> visibleItems {get { return SourceCollection as ObservableCollection<VisualTreeItem>; } }

        public TreeListSource(SnoopUI ui) : base(new ObservableCollection<VisualTreeItem>()) {
            this.ui = ui;
            ProtectedMethods = this.Wrap<ITreeListSource>();
            ui.PropertyChanged += OnUIPropertyChanged;
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
                visibleItems.Clear();
                visibleItems.Add(root);                
            }
        }

        void OnRootEndUpdateChild(object sender, EventArgs e) {
            var child = (VisualTreeItem) sender;
            if (!child.IsExpanded)
                return;
            OnCollectionChanged2(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                GetDescendants(child), visibleItems.IndexOf(child) + 1));
        }

        void OnRootBeginUpdateChild(object sender, EventArgs e) {
            var child = (VisualTreeItem) sender;
            if (!child.IsExpanded)
                return;
            OnCollectionChanged2(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                GetDescendants(child), visibleItems.IndexOf(child) + 1));
        }

        void OnChildExpandedChanged(object sender, EventArgs e) {
            var vItem = sender as VisualTreeItem;
            var index = visibleItems.IndexOf(vItem);
            var descendants = GetDescendants(vItem);
            OnCollectionChanged2(
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

        protected void OnCollectionChanged2(NotifyCollectionChangedEventArgs args) {
            switch (args.Action) {
                case NotifyCollectionChangedAction.Add:
                    var newIndex = args.NewStartingIndex;
                    foreach (VisualTreeItem item in args.NewItems) {
                        visibleItems.Insert(newIndex++, item);
                    }
                    return;
                case NotifyCollectionChangedAction.Remove:
                    var oldIndex = args.OldStartingIndex;
                    foreach (VisualTreeItem item in args.OldItems.OfType<object>().ToArray()) {
                        visibleItems.RemoveAt(oldIndex);
                    }
                    return;
            }
        }

       
    }
}