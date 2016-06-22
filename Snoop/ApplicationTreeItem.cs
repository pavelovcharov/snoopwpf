// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;

namespace Snoop {
    public class ApplicationTreeItem : ResourceContainerItem {
        readonly Application application;

        public ApplicationTreeItem(Application application, VisualTreeItem parent)
            : base(application, parent) {
            this.application = application;
        }


        public override object MainVisual {
            get { return application.MainWindow; }
        }

        protected override ResourceDictionary ResourceDictionary {
            get { return application.Resources; }
        }

        protected override bool GetHasChildren() {
            return true;
        }

        protected override void ReloadImpl() {
            // having the call to base.Reload here ... puts the application resources at the very top of the tree view
            base.ReloadImpl();
            // what happens in the case where the application's main window is invisible?
            // in this case, the application will only have one visual item underneath it: the collapsed/hidden window.
            // however, you are still able to ctrl-shift mouse over the visuals in the visible window.
            // when you do this, snoop reloads the visual tree with the visible window as the root (versus the application).

            if (application.MainWindow != null) {
                Children.Add(Construct(application.MainWindow, this));
            }
        }
    }
}