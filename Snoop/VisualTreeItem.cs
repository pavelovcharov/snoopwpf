// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ReflectionFramework;
using ReflectionFramework.Extensions;
using ReflectionFramework.Internal;

namespace Snoop {
    public class DumbVisualTreeItem : VisualTreeItem {
        public DumbVisualTreeItem(object target, VisualTreeItem parent) : base(target, parent) {}

        protected override bool GetHasChildren() {
            return false;
        }
    }

    public abstract class VisualTreeItem : INotifyPropertyChanged {
        ObservableCollection<VisualTreeItem> children;

        bool fillingChildren;
        bool hasChildren;
        bool isExpanded;
        bool isSelected;


        string name;
        string nameLower = string.Empty;
        string typeNameLower = string.Empty;

        protected VisualTreeItem(object target, VisualTreeItem parent) {
            if (target == null) throw new ArgumentNullException("target");
            Target = target;
            Parent = parent;
            if (parent != null)
                Depth = parent.Depth + 1;
        }


        /// <summary>
        ///     The WPF object that this VisualTreeItem is wrapping
        /// </summary>
        public object Target { get; }

        /// <summary>
        ///     The VisualTreeItem parent of this VisualTreeItem
        /// </summary>
        public VisualTreeItem Parent { get; }

        /// <summary>
        ///     The depth (in the visual tree) of this VisualTreeItem
        /// </summary>
        public int Depth { get; }

        /// <summary>
        ///     The VisualTreeItem children of this VisualTreeItem
        /// </summary>
        public ObservableCollection<VisualTreeItem> Children {
            get {
                if (children != null)
                    return children;
                children = new ObservableCollection<VisualTreeItem>();
                FillChildren();
                return children;
            }
        }


        public bool IsSelected {
            get { return isSelected; }
            set {
                if (isSelected != value) {
                    isSelected = value;

                    // Need to expand all ancestors so this will be visible in the tree.
                    if (isSelected && Parent != null)
                        Parent.ExpandTo();

                    OnPropertyChanged("IsSelected");
                    OnSelectionChanged();
                }
            }
        }

        /// <summary>
        ///     Need this to databind to TreeView so we can display to hidden items.
        /// </summary>
        public bool IsExpanded {
            get { return isExpanded; }
            set {
                if (isExpanded != value) {
                    isExpanded = value;
                    OnPropertyChanged("IsExpanded");
                    if (Parent != null)
                        Parent.OnChildExpanded(this);
                    else {
                        OnChildExpanded(this);
                    }
                }
            }
        }


        public virtual object MainVisual {
            get { return null; }
        }

        public virtual Brush Foreground {
            get { return Brushes.Black; }
        }

        public virtual Brush TreeBackgroundBrush {
            get { return new SolidColorBrush(Color.FromArgb(255, 240, 240, 240)); }
        }

        public virtual Brush VisualBrush {
            get { return null; }
        }

        /// <summary>
        ///     Checks to see if any property on this element has a binding error.
        /// </summary>
        public virtual bool HasBindingError {
            get { return false; }
        }

        public bool HasChildren {
            get { return hasChildren; }
            set {
                hasChildren = value;
                OnPropertyChanged("HasChildren");
            }
        }

        public static VisualTreeItem Construct(object target, VisualTreeItem parent) {
            VisualTreeItem visualTreeItem;
            if (target is IReflectionHelperInterfaceWrapper) {
                visualTreeItem = new VisualItem(((IReflectionHelperInterfaceWrapper)target).Source, parent);
            }else if (target.Wrap<IFrameworkRenderElementContext>()!=null)
                visualTreeItem = new VisualItem(target, parent);
            else if (target is Visual)
                visualTreeItem = new VisualItem((Visual) target, parent);
            else if (target is ResourceDictionary)
                visualTreeItem = new ResourceDictionaryItem((ResourceDictionary) target, parent);
            else if (target is Application)
                visualTreeItem = new ApplicationTreeItem((Application) target, parent);
            else
                return new DumbVisualTreeItem(target, parent);
            //visualTreeItem = new VisualTreeItem(target, parent);

            visualTreeItem.Reload();

            return visualTreeItem;
        }


        public override string ToString() {
            var sb = new StringBuilder(50);

            // [depth] name (type) numberOfChildren
            sb.AppendFormat("[{0}] {1} ({2})", Depth.ToString("D3"), name, Target.GetType().Name);

            return sb.ToString();
        }

        protected virtual void OnSelectionChanged() {}

        public event EventHandler ChildExpandedChanged;
        public event EventHandler BeginUpdate;
        public event EventHandler EndUpdate;

        void OnChildExpanded(VisualTreeItem child) {
            if (Parent != null) {
                Parent.OnChildExpanded(child);
                return;
            }
            if (child == null || ChildExpandedChanged == null)
                return;
            ChildExpandedChanged(child, EventArgs.Empty);
        }

        public int GetIndex() {
            if (Parent == null)
                return 0;
            return Parent.GetIndex() + Parent.Children.IndexOf(this) + 1;
        }

        /// <summary>
        ///     Expand this element and all elements leading to it.
        ///     Used to show this element in the tree view.
        /// </summary>
        void ExpandTo() {
            if (Parent != null)
                Parent.ExpandTo();

            IsExpanded = true;
        }


        /// <summary>
        ///     Update the view of this visual, rebuild children as necessary
        /// </summary>
        public void Reload() {
            RaiseBeginUpdate(this);
            if (Target is IFrameworkInputElement) {
                name = ((IFrameworkInputElement)Target).Name;
            } else {
                var frec = Target.Wrap<IFrameworkRenderElementContext>();
                if (frec!=null) {                         
                    name = frec.Name;
                    Guid gresult;
                    if (Guid.TryParse(name, out gresult))
                        name = null;
                }
            }
            nameLower = (name ?? "").ToLower();
            typeNameLower = Target != null ? Target.GetType().Name.ToLower() : string.Empty;

            ReloadImpl();
            HasChildren = GetHasChildren();
            if (IsExpanded) {
                FillChildren();
            }
            RaiseEndUpdate(this);
        }

        void RaiseBeginUpdate(VisualTreeItem element) {
            if (Parent != null) {
                Parent.RaiseBeginUpdate(element);
                return;
            }
            BeginUpdate?.Invoke(element, EventArgs.Empty);
        }

        void RaiseEndUpdate(VisualTreeItem element) {
            if (Parent != null) {
                Parent.RaiseEndUpdate(element);
                return;
            }
            EndUpdate?.Invoke(element, EventArgs.Empty);
        }

        protected abstract bool GetHasChildren();

        protected virtual void ReloadImpl() {
            if (children == null)
                return;
            while (Children.Count > 0) {
                var child = Children[0];
                child.Detach();
                Children.RemoveAt(0);
            }
            children = null;
        }

        void FillChildren() {
            if (fillingChildren)
                return;
            fillingChildren = true;
            FillChildrenImpl();
            fillingChildren = false;
        }

        protected virtual void FillChildrenImpl() {}

        protected virtual void Detach() {}


        public VisualTreeItem FindNode(object target) {
            return FindNode(this, target);
        }

        static VisualTreeItem FindNode(VisualTreeItem currentVisualTreeItem, object target) {
            // it might be faster to have a map for the lookup
            // check into this at some point            
            Queue<VisualTreeItem> items = new Queue<VisualTreeItem>();
            items.Enqueue(currentVisualTreeItem);
            while (items.Count>0) {
                currentVisualTreeItem = items.Dequeue();
                if (currentVisualTreeItem.Target == target) {
                    var chrome = currentVisualTreeItem.Target.Wrap<IChrome>();
                    if (chrome != null && target is IInputElement) {
                        var root = chrome.Root;
                        if (root != null) {
                            var child = RenderTreeHelper.HitTest(root, Mouse.GetPosition((IInputElement)target));
                            var node = currentVisualTreeItem.FindNode(child);
                            if (node != null)
                                return node;
                        }
                    }
                    return currentVisualTreeItem;
                }

                foreach (var child in currentVisualTreeItem.Children) {
                    items.Enqueue(child);
                }
            }
            return null;
        }


        /// <summary>
        ///     Used for tree search.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Filter(string value) {
            if (typeNameLower.Contains(value))
                return true;
            if (nameLower.Contains(value))
                return true;
            int n;
            if (int.TryParse(value, out n) && n == Depth)
                return true;
            return false;
        }

        protected void OnPropertyChanged(string propertyName) {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Iterate(Func<VisualTreeItem, bool> enterChildrenPredicate, Action<VisualTreeItem> childAction) {
            childAction(this);
            if (enterChildrenPredicate(this))
                foreach (var child in Children) {
                    child.Iterate(enterChildrenPredicate, childAction);
                }
        }

        public VisualTreeItem GetItemAt(int index) {
            return GetItemAtImpl(ref index);
        }

        VisualTreeItem GetItemAtImpl(ref int index) {
            if (index == 0)
                return this;
            index--;
            VisualTreeItem result = null;
            if (!isExpanded)
                return null;
            foreach (var child in Children) {
                if (result != null)
                    return result;
                result = child.GetItemAtImpl(ref index);
            }
            return result;
        }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}