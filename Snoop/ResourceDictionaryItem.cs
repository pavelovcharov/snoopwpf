// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Windows;
using System.Windows.Markup;

namespace Snoop {
    public class ResourceDictionaryItem : VisualTreeItem {
        readonly ResourceDictionary dictionary;

        public ResourceDictionaryItem(ResourceDictionary dictionary, VisualTreeItem parent) : base(dictionary, parent) {
            this.dictionary = dictionary;
        }

        public override string ToString() {
            return Children.Count + " Resources";
        }

        protected override bool GetHasChildren() {
            return dictionary != null && dictionary.Count > 0;
        }

        protected override void ReloadImpl() {
            base.ReloadImpl();

            foreach (var key in dictionary.Keys) {
                object target;
                try {
                    target = dictionary[key];
                }
                catch (XamlParseException) {
                    // sometimes you can get a XamlParseException ... because the xaml you are Snoop(ing) is bad.
                    // e.g. I got this once when I was Snoop(ing) some xaml that was refering to an image resource that was no longer there.
                    // in this case, just continue to the next resource in the dictionary.
                    continue;
                }

                if (target == null) {
                    // you only get a XamlParseException once. the next time through target just comes back null.
                    // in this case, just continue to the next resource in the dictionary (as before).
                    continue;
                }

                Children.Add(new ResourceItem(target, key, this));
            }
        }
    }

    public class ResourceItem : VisualTreeItem {
        readonly object key;

        public ResourceItem(object target, object key, VisualTreeItem parent) : base(target, parent) {
            this.key = key;
        }

        protected override bool GetHasChildren() {
            return false;
        }

        public override string ToString() {
            return key + " (" + Target.GetType().Name + ")";
        }
    }
}