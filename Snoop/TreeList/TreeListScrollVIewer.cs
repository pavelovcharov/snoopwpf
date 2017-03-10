using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Snoop.TreeList {
    public class TreeListScrollViewer : ScrollViewer {
        ObservableCollection<TreeListItem> containers;

        public TreeListScrollViewer() {
            containers = new ObservableCollection<TreeListItem>();
            containers.CollectionChanged += OnContainersCollectionChanged;
        }

        void OnContainersCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) { UpdateHorizontalScroll(); }

        protected override void OnScrollChanged(ScrollChangedEventArgs e) {
            base.OnScrollChanged(e);
            UpdateHorizontalScroll();
        }

        public void AddContainer(TreeListItem container) { containers.Add(container); }
        public void RemoveContainer(TreeListItem container) { containers.Remove(container); }

        void UpdateHorizontalScroll() {
            
        }
    }
}
