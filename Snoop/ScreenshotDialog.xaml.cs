﻿// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Snoop {
    /// <summary>
    ///     Interaction logic for ScreenShotDialog.xaml
    /// </summary>
    public partial class ScreenshotDialog {
        public static readonly RoutedCommand SaveCommand = new RoutedCommand("Save", typeof(ScreenshotDialog));
        public static readonly RoutedCommand CancelCommand = new RoutedCommand("Cancel", typeof(ScreenshotDialog));

        public ScreenshotDialog() {
            InitializeComponent();

            CommandBindings.Add(new CommandBinding(SaveCommand, HandleSave, HandleCanSave));
            CommandBindings.Add(new CommandBinding(CancelCommand, HandleCancel, (x, y) => y.CanExecute = true));
        }

        void HandleCanSave(object sender, CanExecuteRoutedEventArgs e) {
            if (DataContext == null || !(DataContext is Visual)) {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = true;
        }

        void HandleSave(object sender, ExecutedRoutedEventArgs e) {
            var fileDialog = new SaveFileDialog();
            fileDialog.AddExtension = true;
            fileDialog.CheckPathExists = true;
            fileDialog.DefaultExt = "png";
            fileDialog.FileName = FilePath;

            if (fileDialog.ShowDialog(this).Value) {
                FilePath = fileDialog.FileName;
                VisualCaptureUtil.SaveVisual
                    (
                        DataContext as Visual,
                        int.Parse
                            (
                                ((TextBlock) ((ComboBoxItem) dpiBox.SelectedItem).Content).Text
                            ),
                        FilePath
                    );

                Close();
            }
        }

        void HandleCancel(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }

        #region FilePath Dependency Property

        public string FilePath {
            get { return (string) GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register
                (
                    "FilePath",
                    typeof(string),
                    typeof(ScreenshotDialog),
                    new UIPropertyMetadata(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) +
                                           @"\SnoopScreenshot.png")
                );

        #endregion
    }
}