using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Snoop.DebugListenerTab {
    [Serializable]
    public class FiltersViewModel : INotifyPropertyChanged {
        readonly ObservableCollection<SnoopFilter> filters = new ObservableCollection<SnoopFilter>();
        readonly List<SnoopMultipleFilter> multipleFilters = new List<SnoopMultipleFilter>();
        string _filterStatus;

        bool _isSet;
        bool isDirty;

        public FiltersViewModel() {
            filters.Add(new SnoopSingleFilter());
            FilterStatus = _isSet ? "Filter is ON" : "Filter is OFF";
        }

        public FiltersViewModel(IList<SnoopSingleFilter> singleFilters) {
            InitializeFilters(singleFilters);
        }

        public bool IsDirty {
            get {
                if (isDirty)
                    return true;

                foreach (var filter in filters) {
                    if (filter.IsDirty)
                        return true;
                }
                return false;
            }
        }

        public bool IsSet {
            get { return _isSet; }
            set {
                _isSet = value;
                RaisePropertyChanged("IsSet");
                FilterStatus = _isSet ? "Filter is ON" : "Filter is OFF";
            }
        }

        public string FilterStatus {
            get { return _filterStatus; }
            set {
                _filterStatus = value;
                RaisePropertyChanged("FilterStatus");
            }
        }

        public IEnumerable<SnoopFilter> Filters {
            get { return filters; }
        }

        public void ResetDirtyFlag() {
            isDirty = false;
            foreach (var filter in filters) {
                filter.ResetDirtyFlag();
            }
        }

        public void InitializeFilters(IList<SnoopSingleFilter> singleFilters) {
            filters.Clear();

            if (singleFilters == null) {
                filters.Add(new SnoopSingleFilter());
                IsSet = false;
                return;
            }

            foreach (var filter in singleFilters)
                filters.Add(filter);

            var groupings = (from x in singleFilters where x.IsGrouped select x).GroupBy(x => x.GroupId);
            foreach (var grouping in groupings) {
                var multipleFilter = new SnoopMultipleFilter();
                var groupedFilters = grouping.ToArray();
                if (groupedFilters.Length == 0)
                    continue;

                multipleFilter.AddRange(groupedFilters, groupedFilters[0].GroupId);
                multipleFilters.Add(multipleFilter);
            }

            SetIsSet();
        }

        internal void SetIsSet() {
            if (filters == null)
                IsSet = false;

            if (filters.Count == 1 && filters[0] is SnoopSingleFilter &&
                string.IsNullOrEmpty(((SnoopSingleFilter) filters[0]).Text))
                IsSet = false;
            else
                IsSet = true;
        }

        public void ClearFilters() {
            multipleFilters.Clear();
            filters.Clear();
            filters.Add(new SnoopSingleFilter());
            IsSet = false;
        }

        public bool FilterMatches(string str) {
            foreach (var filter in Filters) {
                if (filter.IsGrouped)
                    continue;

                if (filter.FilterMatches(str))
                    return true;
            }

            foreach (var multipleFilter in multipleFilters) {
                if (multipleFilter.FilterMatches(str))
                    return true;
            }

            return false;
        }

        string GetFirstNonUsedGroupId() {
            var index = 1;
            while (true) {
                if (!GroupIdTaken(index.ToString()))
                    return index.ToString();

                index++;
            }
        }

        bool GroupIdTaken(string groupID) {
            foreach (var filter in multipleFilters) {
                if (groupID.Equals(filter.GroupId))
                    return true;
            }
            return false;
        }

        public void GroupFilters(IEnumerable<SnoopFilter> filtersToGroup) {
            var multipleFilter = new SnoopMultipleFilter();
            multipleFilter.AddRange(filtersToGroup, (multipleFilters.Count + 1).ToString());

            multipleFilters.Add(multipleFilter);
        }

        public void AddFilter(SnoopFilter filter) {
            isDirty = true;
            filters.Add(filter);
        }

        public void RemoveFilter(SnoopFilter filter) {
            isDirty = true;
            var singleFilter = filter as SnoopSingleFilter;
            if (singleFilter != null) {
                //foreach (var multipeFilter in this.multipleFilters)
                var index = 0;
                while (index < multipleFilters.Count) {
                    var multipeFilter = multipleFilters[index];
                    if (multipeFilter.ContainsFilter(singleFilter))
                        multipeFilter.RemoveFilter(singleFilter);

                    if (!multipeFilter.IsValidMultipleFilter)
                        multipleFilters.RemoveAt(index);
                    else
                        index++;
                }
            }
            filters.Remove(filter);
        }

        public void ClearFilterGroups() {
            foreach (var filterGroup in multipleFilters) {
                filterGroup.ClearFilters();
            }
            multipleFilters.Clear();
        }

        protected void RaisePropertyChanged(string propertyName) {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}