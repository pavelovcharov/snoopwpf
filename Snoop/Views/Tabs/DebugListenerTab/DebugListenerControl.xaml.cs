using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Snoop.Infrastructure;
using Snoop.Properties;

namespace Snoop.DebugListenerTab {
    /// <summary>
    ///     Interaction logic for DebugListenerControl.xaml
    /// </summary>
    public partial class DebugListenerControl : UserControl, IListener {
        readonly FiltersViewModel filtersViewModel; // = new FiltersViewModel();
        readonly SnoopDebugListener snoopDebugListener = new SnoopDebugListener();
        StringBuilder allText = new StringBuilder();

        public DebugListenerControl() {
            InitializeComponent();

            snoopDebugListener.RegisterListener(this);
        }

        void checkBoxStartListening_Checked(object sender, RoutedEventArgs e) {
#if !NETCORE
            Debug.Listeners.Add(snoopDebugListener);
#endif
            PresentationTraceSources.DataBindingSource.Listeners.Add(snoopDebugListener);
        }

        void checkBoxStartListening_Unchecked(object sender, RoutedEventArgs e) {
#if !NETCORE
            Debug.Listeners.Remove(SnoopDebugListener.ListenerName);
#endif
            PresentationTraceSources.DataBindingSource.Listeners.Remove(snoopDebugListener);
        }

        void DoWrite(string str) {
            textBoxDebugContent.AppendText(str + Environment.NewLine);
            textBoxDebugContent.ScrollToEnd();
        }


        void buttonClear_Click(object sender, RoutedEventArgs e) {
            textBoxDebugContent.Clear();
            allText = new StringBuilder();
        }

        void buttonClearFilters_Click(object sender, RoutedEventArgs e) {
            var result = MessageBox.Show("Are you sure you want to clear your filters?", "Clear Filters Confirmation",
                MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes) {
                filtersViewModel.ClearFilters();
                Settings.Default.SnoopDebugFilters = null;
                textBoxDebugContent.Text = allText.ToString();
            }
        }

        void buttonSetFilters_Click(object sender, RoutedEventArgs e) {
            var setFiltersWindow = new SetFiltersWindow(filtersViewModel);
            setFiltersWindow.Topmost = true;
            setFiltersWindow.Owner = Window.GetWindow(this);
            setFiltersWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            setFiltersWindow.ShowDialog();

            var allLines = allText.ToString().Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
            textBoxDebugContent.Clear();
            foreach (var line in allLines) {
                if (filtersViewModel.FilterMatches(line))
                    textBoxDebugContent.AppendText(line + Environment.NewLine);
            }
        }

        void comboBoxPresentationTraceLevel_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (comboBoxPresentationTraceLevel == null || comboBoxPresentationTraceLevel.Items == null ||
                comboBoxPresentationTraceLevel.Items.Count <= comboBoxPresentationTraceLevel.SelectedIndex ||
                comboBoxPresentationTraceLevel.SelectedIndex < 0)
                return;

            var selectedComboBoxItem =
                comboBoxPresentationTraceLevel.Items[comboBoxPresentationTraceLevel.SelectedIndex] as ComboBoxItem;
            if (selectedComboBoxItem == null || selectedComboBoxItem.Tag == null)
                return;


            var sourceLevel = (SourceLevels) Enum.Parse(typeof(SourceLevels), selectedComboBoxItem.Tag.ToString());
            PresentationTraceSources.DataBindingSource.Switch.Level = sourceLevel;
        }

        public void Write(string str) {
            allText.Append(str + Environment.NewLine);
            if (!filtersViewModel.IsSet || filtersViewModel.FilterMatches(str)) {
                Dispatcher.BeginInvoke(DispatcherPriority.Render, () => DoWrite(str));
            }
        }
    }
}