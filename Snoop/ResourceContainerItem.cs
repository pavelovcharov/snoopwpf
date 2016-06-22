// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;

namespace Snoop {
    public abstract class ResourceContainerItem : VisualTreeItem {
        public ResourceContainerItem(object target, VisualTreeItem parent) : base(target, parent) {}

        protected abstract ResourceDictionary ResourceDictionary { get; }

        protected override void FillChildrenImpl() {
            base.FillChildrenImpl();
            var resources = ResourceDictionary;

            if (resources != null && resources.Count != 0) {
                Children.Add(Construct(resources, this));
            }
        }
    }
}