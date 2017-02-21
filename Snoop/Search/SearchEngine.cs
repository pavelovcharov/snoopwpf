using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Snoop.Annotations;

namespace Snoop {
    public class SearchEngine : INotifyPropertyChanged {
        readonly SnoopUI snoopUi;
        readonly DelayedCall filterCall;
        readonly DispatcherTimer filterTimer;
        bool immediateFilter = false;
        string filter;

        public VisualTreeItem Root {
            get { return root; }
            set {
                if (Equals(value, root)) return;
                root = value;
                OnPropertyChanged();
            }
        }

        public bool IsFiltering {
            get { return isFiltering; }
            set {
                if (value == isFiltering) return;
                isFiltering = value;
                OnPropertyChanged();
            }
        }

        public string Filter {
            get { return filter; }
            set {
                if (value == filter) return;
                filter = value;
                OnPropertyChanged();
            }
        }

        public void SetFilter(string filter) {
            try {
                immediateFilter = true;
                Filter = filter;
            } finally {
                immediateFilter = false;
            }
        }

        void OnFilterChanged() {
            filterTimer.Stop();
            if (immediateFilter) {
                EnqueueAfterSettingFilter();
                return;
            }
            filterTimer.Start();
        }

        public SearchEngine(SnoopUI snoopUi) {
            this.snoopUi = snoopUi;
            filterTimer = new DispatcherTimer();
            filterTimer.Interval = TimeSpan.FromSeconds(0.3);
            filterTimer.Tick += (s, e) => {
                EnqueueAfterSettingFilter();
                filterTimer.Stop();
            };
            filterCall = new DelayedCall(ProcessNewFilter, DispatcherPriority.Background);
        }
        void OnRootChanged() {
            if (selecting != Root || Root == null)
                PrepareNewFilter();
        }
        void ProcessNewFilter() {
            PrepareNewFilter();
            Next();
        }

        void PrepareNewFilter() {
            items = null;
            visitedItems = new HashSet<VisualTreeItem>();
            IsFiltering = Filter != null;
        }

        HashSet<VisualTreeItem> visitedItems;
        IEnumerator<VisualTreeItem> items;
        bool isFiltering;

        IEnumerator<VisualTreeItem> Items {
            get { return items ?? (items = GetItems()); }
        }

        IEnumerator<VisualTreeItem> GetItems() {
            if (Root == null)
                yield break;
            Stack<VisualTreeItem> itemsStack = new Stack<VisualTreeItem>();

            itemsStack.Push(null);
            var currentParent = Root;
            List<VisualTreeItem> parents = new List<VisualTreeItem>();
            while (currentParent != null) {
                parents.Add(currentParent);
                currentParent = currentParent.Parent;
            }
            parents.Reverse();
            foreach (var parent in parents) {
                itemsStack.Push(parent);
            }
            do {
                var current = itemsStack.Peek();
                //visitedItems.Add(current);
                if (itemsStack.Peek() == null) {
                    yield break;
                }
                if (jumpOver) {
                    itemsStack.Pop();
                    //visitedItems.Remove(current);
                    jumpOver = false;
                    continue;
                }
                itemsStack.Pop();
                foreach (var currentChild in current.Children.Reverse()) {
                    //if (!visitedItems.Contains(currentChild))
                    itemsStack.Push(currentChild);
                }
                yield return current;
            } while (true);
        }
        VisualTreeItem selecting;
        public void Next() {
            var lFilter = Filter.ToLower();
            VisualTreeItem current = null;
            while (Items.MoveNext()) {
                current = Items.Current;
                if (current.MatchesFilter(lFilter)) {
                    break;
                }
                current = null;
            }
            if (current != null) {
                var currentParent = current.Parent;
                List<VisualTreeItem> parents = new List<VisualTreeItem>();
                while (currentParent != null) {
                    parents.Add(currentParent);
                    currentParent = currentParent.Parent;
                }
                parents.Reverse();
                foreach (var parent in parents) {
                    parent.IsExpanded = true;
                }
                selecting = current;
                snoopUi.Tree.Select(current);
                selecting = null;
            }
        }

        bool jumpOver = false;
        VisualTreeItem root;
        public void JumpOver() { jumpOver = true; Next(); }

        void EnqueueAfterSettingFilter() { filterCall.Enqueue(); }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            if (propertyName == "Filter") {
                OnFilterChanged();
            }
            if (propertyName == "Root") {
                OnRootChanged();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }        
    }
}