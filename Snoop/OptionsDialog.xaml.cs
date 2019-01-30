using System.Windows;
using Microsoft.Win32;

namespace Snoop {
    public partial class OptionsDialog : Window {
        public OptionsDialog() { InitializeComponent(); }

        void OnResetClick(object sender, RoutedEventArgs e) {
            RegistrySettings.Left = 0; 
            RegistrySettings.Top = 0;
        }

        void OnCloseClick(object sender, RoutedEventArgs e) {
            Close();
        }

        void OnPinnedViewChecked(object sender, RoutedEventArgs e) { RegistrySettings.PinnedView = true; }
        void OnQuickViewChecked(object sender, RoutedEventArgs e) { RegistrySettings.PinnedView = false; }
    }
}
