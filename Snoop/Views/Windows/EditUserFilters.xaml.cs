// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Snoop {
    public partial class EditUserFilters : Window, INotifyPropertyChanged {
        ObservableCollection<PropertyFilterSet> _itemsSource;
        IEnumerable<PropertyFilterSet> _userFilters;

        public EditUserFilters() {
            InitializeComponent();
            DataContext = this;
        }


        public IEnumerable<PropertyFilterSet> UserFilters {
            [DebuggerStepThrough] get { return _userFilters; }
            set {
                if (value != _userFilters) {
                    _userFilters = value;
                    NotifyPropertyChanged("UserFilters");
                    ItemsSource = new ObservableCollection<PropertyFilterSet>(UserFilters);
                }
            }
        }

        public ObservableCollection<PropertyFilterSet> ItemsSource {
            [DebuggerStepThrough] get { return _itemsSource; }
            private set {
                if (value != _itemsSource) {
                    _itemsSource = value;
                    NotifyPropertyChanged("ItemsSource");
                }
            }
        }


        void OkHandler(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        void CancelHandler(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        void AddHandler(object sender, RoutedEventArgs e) {
            var newSet =
                new PropertyFilterSet {
                    DisplayName = "New Filter",
                    IsDefault = false,
                    IsEditCommand = false,
                    Properties = new[] {"prop1,prop2"}
                };
            ItemsSource.Add(newSet);

            // select this new item
            var index = ItemsSource.IndexOf(newSet);
            if (index >= 0) {
                filterSetList.SelectedIndex = index;
            }
        }

        void DeleteHandler(object sender, RoutedEventArgs e) {
            var selected = filterSetList.SelectedItem as PropertyFilterSet;
            if (selected != null) {
                ItemsSource.Remove(selected);
            }
        }

        void UpHandler(object sender, RoutedEventArgs e) {
            var index = filterSetList.SelectedIndex;
            if (index <= 0)
                return;

            var item = ItemsSource[index];
            ItemsSource.RemoveAt(index);
            ItemsSource.Insert(index - 1, item);

            // select the moved item
            filterSetList.SelectedIndex = index - 1;
        }

        void DownHandler(object sender, RoutedEventArgs e) {
            var index = filterSetList.SelectedIndex;
            if (index >= ItemsSource.Count - 1)
                return;

            var item = ItemsSource[index];
            ItemsSource.RemoveAt(index);
            ItemsSource.Insert(index + 1, item);

            // select the moved item
            filterSetList.SelectedIndex = index + 1;
        }

        void SelectionChangedHandler(object sender, SelectionChangedEventArgs e) {
            SetButtonStates();
        }


        void SetButtonStates() {
            MoveUp.IsEnabled = false;
            MoveDown.IsEnabled = false;
            DeleteItem.IsEnabled = false;

            var index = filterSetList.SelectedIndex;
            if (index >= 0) {
                MoveDown.IsEnabled = true;
                DeleteItem.IsEnabled = true;
            }

            if (index > 0)
                MoveUp.IsEnabled = true;

            if (index == filterSetList.Items.Count - 1)
                MoveDown.IsEnabled = false;
        }

        protected void NotifyPropertyChanged(string propertyName) {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}