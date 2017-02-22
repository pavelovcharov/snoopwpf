using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public string CurrentFilter {
            get { return currentFilter; }
            set {
                if (value == currentFilter) return;
                currentFilter = value;
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
            matchingItems = new ObservableCollection<VisualTreeItem>();
            filterTimer = new DispatcherTimer();
            filterTimer.Interval = TimeSpan.FromSeconds(0.3);
            filterTimer.Tick += (s, e) => {
                EnqueueAfterSettingFilter();
                filterTimer.Stop();
            };
            filterCall = new DelayedCall(ProcessNewFilter, DispatcherPriority.Background);
        }
        void OnRootChanged() {
            CheckNullRoot();
            if (selecting != Root || Root == null)
                PrepareNewFilter();
        }

        void CheckNullRoot() {
            if (Root == null)
                Root = snoopUi.Root;
        }

        void ProcessNewFilter() {
            matchingItems.Clear();
            CurrentFilter = Filter;
            PrepareNewFilter();
            Next();
        }

        void PrepareNewFilter() {
            CheckNullRoot();
            items = null;
            itemsEnumerator = null;
            visitedItems = new HashSet<VisualTreeItem>();
            IsFiltering = !String.IsNullOrEmpty(Filter);
        }
        ObservableCollection<VisualTreeItem> matchingItems;
        HashSet<VisualTreeItem> visitedItems;
        IEnumerable<VisualTreeItem> items;
        IEnumerator<VisualTreeItem> itemsEnumerator;
        bool isFiltering;

        IEnumerable<VisualTreeItem> Items {
            get { return items ?? (items = GetItems()); }
        }

        IEnumerator<VisualTreeItem> ItemsEnumerator {
            get { return itemsEnumerator ?? (itemsEnumerator = Items.GetEnumerator()); }
        }

        IEnumerable<VisualTreeItem> GetItems() {
            if (Root == null)
                yield break;
            Stack<VisualTreeItem> itemsStack = new Stack<VisualTreeItem>();

            itemsStack.Push(null);
            var currentParent = Root.Parent;
            List<VisualTreeItem> parents = new List<VisualTreeItem>();
            while (currentParent != null) {
                parents.Add(currentParent);
                currentParent = currentParent.Parent;
            }
            parents.Reverse();
            itemsStack.Push(Root);            
            do {
                var current = itemsStack.Peek();
                if (itemsStack.Peek() == null) {                    
                    if(parents==null)
                        yield break;
                    foreach (var parent in parents) {
                        itemsStack.Push(parent);
                    }
                    parents = null;
                    continue;                    
                }
                if (jumpOver) {
                    itemsStack.Pop();
                    jumpOver = false;
                    continue;
                }
                itemsStack.Pop();
                foreach (var currentChild in current.Children.Reverse()) {
                    if (!visitedItems.Contains(current))
                        itemsStack.Push(currentChild);
                }
                visitedItems.Add(current);
                yield return current;
            } while (true);
        }

        VisualTreeItem selecting;
        bool reloading = false;

        public void Previous() {
            if (!IsFiltering)
                return;
            if (matchingItems.Count == 0)
                return;
            var index = matchingItems.IndexOf(Root);
            if (index == -1)
                return;
            var newIndex = (index - 1 + matchingItems.Count) % matchingItems.Count;
            SelectElement(matchingItems[newIndex]);
        }

        public void Next() {
            if (!IsFiltering)
                return;
            var lFilter = Filter.ToLower();
            VisualTreeItem current = null;            
            while (ItemsEnumerator.MoveNext()) {
                current = ItemsEnumerator.Current;
                if (current.MatchesFilter(lFilter) && current != Root) {
                    break;
                }
                current = null;
            }
            if (current != null) {
                SelectElement(current);
            } else {
                if(reloading)
                    return;
                reloading = true;
                PrepareNewFilter();
                Next();
                reloading = false;
            }
        }

        void SelectElement(VisualTreeItem current) {
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
            if (!matchingItems.Contains(current))
                matchingItems.Add(current);
            selecting = current;
            snoopUi.Tree.Select(current);
            selecting = null;
        }

        bool jumpOver = false;
        VisualTreeItem root;
        string currentFilter;

        public void JumpOver() {
            if (!IsFiltering)
                return;
            jumpOver = true; Next();
        }

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