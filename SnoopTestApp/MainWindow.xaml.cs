using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
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

        void BarItem_OnItemClick(object sender, ItemClickEventArgs e) {
            ThemeManager.SetThemeName(this, Theme.Themes.Random().Name);
        }
    }

    public static class EnumerableExtensions2 {
        public static T Random<T>(this IList<T> value) {
            Random rnd = new Random();
            var cnt = value.Count;
            if (cnt == 0)
                return default(T);
            return value.ElementAt(rnd.Next(0, cnt));
        }
    }
}