using System;
using System.Windows;
using System.Windows.Controls;
using Snoop;

namespace SnoopTestApp {
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
            SnoopUI.SnoopApplication();
        }

        void OnAddButtonClick(object sender, RoutedEventArgs e) {
            sPanel.Children.Add(new TextBlock {Text = DateTime.Now.ToString()});
        }
    }
}