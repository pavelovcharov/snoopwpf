// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using Snoop.Infrastructure;

namespace Snoop {
    /// <summary>
    ///     Interaction logic for ErrorDialog.xaml
    /// </summary>
    public partial class ErrorDialog : Window {
        public ErrorDialog() {
            InitializeComponent();

            Loaded += ErrorDialog_Loaded;
            Closed += ErrorDialog_Closed;
        }

        public Exception Exception { get; set; }

        void ErrorDialog_Loaded(object sender, RoutedEventArgs e) {
            _textBlockException.Text = GetExceptionMessage();

            SnoopPartsRegistry.AddSnoopVisualTreeRoot(this);
        }

        void ErrorDialog_Closed(object sender, EventArgs e) {
            SnoopPartsRegistry.RemoveSnoopVisualTreeRoot(this);
        }

        void _buttonCopyToClipboard_Click(object sender, RoutedEventArgs e) {
            try {
                Clipboard.SetText(GetExceptionMessage());
            }
            catch (Exception ex) {
                var message =
                    string.Format(
                        "There was an error copying to the clipboard:\nMessage = {0}\n\nPlease copy the exception from the above textbox manually!",
                        ex.Message);
                MessageBox.Show(message, "Error copying to clipboard");
            }
        }

        void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                Process.Start(e.Uri.AbsoluteUri);
            }
            catch (Exception) {
                var message =
                    string.Format("There was an error starting the browser. Please visit \"{0}\" to create the issue.",
                        e.Uri.AbsoluteUri);
                MessageBox.Show(message, "Error starting browser");
            }
        }

        void CloseDoNotMarkHandled_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            if (CheckBoxRememberIsChecked()) {
                SnoopModes.IgnoreExceptions = true;
            }
            Close();
        }

        void CloseAndMarkHandled_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            if (CheckBoxRememberIsChecked()) {
                SnoopModes.SwallowExceptions = true;
            }
            Close();
        }

        string GetExceptionMessage() {
            var builder = new StringBuilder();
            GetExceptionString(Exception, builder);
            return builder.ToString();
        }

        static void GetExceptionString(Exception exception, StringBuilder builder, bool isInner = false) {
            if (exception == null)
                return;

            if (isInner)
                builder.AppendLine("\n\nInnerException:\n");

            builder.AppendLine(string.Format("Message: {0}", exception.Message));
            builder.AppendLine(string.Format("Stacktrace:\n{0}", exception.StackTrace));

            GetExceptionString(exception.InnerException, builder, true);
        }

        bool CheckBoxRememberIsChecked() {
            return _checkBoxRemember.IsChecked.HasValue && _checkBoxRemember.IsChecked.Value;
        }
    }
}