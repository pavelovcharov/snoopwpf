using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Snoop.Infrastructure;

namespace Snoop.DebugListenerTab {
    /// <summary>
    ///     Interaction logic for SetFiltersWindow.xaml
    /// </summary>
    public partial class SetFiltersWindow : Window {
        //private SnoopSingleFilter MakeDeepCopyOfFilter(SnoopSingleFilter filter)
        //{
        //	try
        //	{
        //		BinaryFormatter formatter = new BinaryFormatter();
        //		var ms = new System.IO.MemoryStream();
        //		formatter.Serialize(ms, filter);
        //		SnoopSingleFilter deepCopy = (SnoopSingleFilter)formatter.Deserialize(ms);
        //		ms.Close();
        //		return deepCopy;
        //	}
        //	catch (Exception)
        //	{
        //		return null;
        //	}
        //}


        readonly List<SnoopSingleFilter> initialFilters;
        bool _setFilterClicked;

        public SetFiltersWindow(FiltersViewModel viewModel) {
            DataContext = viewModel;
            viewModel.ResetDirtyFlag();

            InitializeComponent();

            initialFilters = MakeDeepCopyOfFilters(ViewModel.Filters);

            Loaded += SetFiltersWindow_Loaded;
            Closed += SetFiltersWindow_Closed;
        }


        internal FiltersViewModel ViewModel {
            get { return DataContext as FiltersViewModel; }
        }


        void SetFiltersWindow_Loaded(object sender, RoutedEventArgs e) {
            SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);
        }

        void SetFiltersWindow_Closed(object sender, EventArgs e) {
            if (_setFilterClicked || !ViewModel.IsDirty)
                return;

            var saveChanges = MessageBox.Show("Save changes?", "Changes", MessageBoxButton.YesNo) ==
                              MessageBoxResult.Yes;
            if (saveChanges) {
                ViewModel.SetIsSet();
                SaveFiltersToSettings();
                return;
            }

            ViewModel.InitializeFilters(initialFilters);

            SnoopPartsRegistry.RemoveSnoopVisualTreeRoot(this);
        }

        void buttonAddFilter_Click(object sender, RoutedEventArgs e) {
            //ViewModel.Filters.Add(new SnoopSingleFilter());
            ViewModel.AddFilter(new SnoopSingleFilter());
            //this.listBoxFilters.ScrollIntoView(this.listBoxFilters.ItemContainerGenerator.ContainerFromIndex(this.listBoxFilters.Items.Count - 1));
        }

        void buttonRemoveFilter_Click(object sender, RoutedEventArgs e) {
            var frameworkElement = sender as FrameworkElement;
            if (frameworkElement == null)
                return;

            var filter = frameworkElement.DataContext as SnoopFilter;
            if (filter == null)
                return;

            ViewModel.RemoveFilter(filter);
        }

        void buttonSetFilter_Click(object sender, RoutedEventArgs e) {
            SaveFiltersToSettings();

            //this.ViewModel.IsSet = true;
            ViewModel.SetIsSet();
            _setFilterClicked = true;
            Close();
        }

        void textBlockFilter_Loaded(object sender, RoutedEventArgs e) {
            var textBox = sender as TextBox;
            if (textBox != null) {
                textBox.Focus();
                listBoxFilters.ScrollIntoView(textBox);
            }
        }

        void menuItemGroupFilters_Click(object sender, RoutedEventArgs e) {
            var filtersToGroup = new List<SnoopFilter>();
            foreach (var item in listBoxFilters.SelectedItems) {
                var filter = item as SnoopFilter;
                if (filter == null)
                    continue;

                if (filter.SupportsGrouping)
                    filtersToGroup.Add(filter);
            }
            ViewModel.GroupFilters(filtersToGroup);
        }

        void menuItemClearFilterGroups_Click(object sender, RoutedEventArgs e) {
            ViewModel.ClearFilterGroups();
        }

        void menuItemSetInverse_Click(object sender, RoutedEventArgs e) {
            foreach (SnoopFilter filter in listBoxFilters.SelectedItems) {
                if (filter == null)
                    continue;

                filter.IsInverse = !filter.IsInverse;
            }
        }


        void SaveFiltersToSettings() {
            var singleFilters = new List<SnoopSingleFilter>();
            foreach (var filter in ViewModel.Filters) {
                if (filter is SnoopSingleFilter)
                    singleFilters.Add((SnoopSingleFilter) filter);
            }
        }

        List<SnoopSingleFilter> MakeDeepCopyOfFilters(IEnumerable<SnoopFilter> filters) {
            var snoopSingleFilters = new List<SnoopSingleFilter>();

            foreach (var filter in filters) {
                var singleFilter = filter as SnoopSingleFilter;
                if (singleFilter == null)
                    continue;

                var newFilter = (SnoopSingleFilter) singleFilter.Clone();

                snoopSingleFilters.Add(newFilter);
            }

            return snoopSingleFilters;
        }
    }
}