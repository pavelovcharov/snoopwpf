// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System;
using System.Windows.Controls;
using System.Text;

namespace Snoop
{
    public class DumbVisualTreeItem : VisualTreeItem {
        public DumbVisualTreeItem(object target, VisualTreeItem parent) : base(target, parent) {}
        protected override bool GetHasChildren() {
            return false;
        }
    }
	public abstract class VisualTreeItem : INotifyPropertyChanged
	{
		public static VisualTreeItem Construct(object target, VisualTreeItem parent)
		{
			VisualTreeItem visualTreeItem;
            if(DXMethods.IsFrameworkRenderElementContext(target))
                visualTreeItem = new VisualItem(target, parent);
			else if (target is Visual)
				visualTreeItem = new VisualItem((Visual)target, parent);
			else if (target is ResourceDictionary)
				visualTreeItem = new ResourceDictionaryItem((ResourceDictionary)target, parent);
			else if (target is Application)
				visualTreeItem = new ApplicationTreeItem((Application)target, parent);
			else
                return  new DumbVisualTreeItem(target, parent);
				//visualTreeItem = new VisualTreeItem(target, parent);

			visualTreeItem.Reload();

			return visualTreeItem;
		}
		protected VisualTreeItem(object target, VisualTreeItem parent)
		{
		    if (target == null) throw new ArgumentNullException("target");
		    this.target = target;
			this.parent = parent;
			if (parent != null)
				this.depth = parent.depth + 1;
		}


		public override string ToString()
		{
			StringBuilder sb = new StringBuilder(50);

			// [depth] name (type) numberOfChildren
			sb.AppendFormat("[{0}] {1} ({2})", this.depth.ToString("D3"), this.name, this.Target.GetType().Name);			

			return sb.ToString();
		}


		/// <summary>
		/// The WPF object that this VisualTreeItem is wrapping
		/// </summary>
		public object Target
		{
			get { return this.target; }
		}
		private object target;

		/// <summary>
		/// The VisualTreeItem parent of this VisualTreeItem
		/// </summary>
		public VisualTreeItem Parent
		{
			get { return this.parent; }
		}
		private VisualTreeItem parent;

		/// <summary>
		/// The depth (in the visual tree) of this VisualTreeItem
		/// </summary>
		public int Depth
		{
			get { return this.depth; }
		}
		private int depth;

		/// <summary>
		/// The VisualTreeItem children of this VisualTreeItem
		/// </summary>
		public ObservableCollection<VisualTreeItem> Children
		{
			get {
			    if (children != null)
			        return this.children;
			    children = new ObservableCollection<VisualTreeItem>();
			    FillChildren();
			    return children;
			}
		}

	    private ObservableCollection<VisualTreeItem> children;


		public bool IsSelected
		{
			get { return this.isSelected; }
			set
			{
				if (this.isSelected != value)
				{
					this.isSelected = value;

					// Need to expand all ancestors so this will be visible in the tree.
					if (this.isSelected && this.parent != null)
						this.parent.ExpandTo();

					this.OnPropertyChanged("IsSelected");
					this.OnSelectionChanged();
				}
			}
		}
		protected virtual void OnSelectionChanged()
		{
		}
		private bool isSelected = false;

		/// <summary>
		/// Need this to databind to TreeView so we can display to hidden items.
		/// </summary>
		public bool IsExpanded
		{
			get { return this.isExpanded; }
			set
			{
				if (this.isExpanded != value)
				{
					this.isExpanded = value;
					this.OnPropertyChanged("IsExpanded");
				    if (parent != null)
				        parent.OnChildExpanded(this);
				    else {
				        OnChildExpanded(this);
				    }
				}
			}
		}

	    public event EventHandler ChildExpandedChanged;
	    public event EventHandler BeginUpdate;
        public event EventHandler EndUpdate;
        private void OnChildExpanded(VisualTreeItem child) {
	        if (parent != null) {
                parent.OnChildExpanded(child);
                return;	            
	        }                
	        if (child == null || ChildExpandedChanged == null)
	            return;
            ChildExpandedChanged(child, EventArgs.Empty);
	    }

	    public int GetIndex() {
	        if (parent == null)
	            return 0;
	        return parent.GetIndex() + parent.Children.IndexOf(this) + 1;
	    }

	    /// <summary>
		/// Expand this element and all elements leading to it.
		/// Used to show this element in the tree view.
		/// </summary>
		private void ExpandTo()
		{
			if (this.parent != null)
				this.parent.ExpandTo();

			this.IsExpanded = true;
		}
		private bool isExpanded = false;


		public virtual object MainVisual
		{
			get { return null; }
		}
        public virtual Brush Foreground { get { return Brushes.Black; } }
		public virtual Brush TreeBackgroundBrush
		{
			get { return new SolidColorBrush(Color.FromArgb(255, 240, 240, 240)); }
		}
		public virtual Brush VisualBrush
		{
			get { return null; }
		}
		/// <summary>
		/// Checks to see if any property on this element has a binding error.
		/// </summary>
		public virtual bool HasBindingError
		{
			get
			{
				return false;
			}
		}


		/// <summary>
		/// Update the view of this visual, rebuild children as necessary
		/// </summary>
		public void Reload() {
		    RaiseBeginUpdate(this);
            if (this.target is IFrameworkInputElement) {
                this.name = ((IFrameworkInputElement)this.target).Name;
            } else if (DXMethods.IsFrameworkRenderElementContext(target)) {
                this.name = DXMethods.GetName(target);
            }
			this.nameLower = (this.name ?? "").ToLower();
			this.typeNameLower = this.Target != null ? this.Target.GetType().Name.ToLower() : string.Empty;

			this.ReloadImpl();
		    HasChildren = GetHasChildren();
		    if (IsExpanded) {
		        FillChildren();
		    }
		    RaiseEndUpdate(this);
		}

        private void RaiseBeginUpdate(VisualTreeItem element) {
            if (parent != null) {
                parent.RaiseBeginUpdate(element);
                return;
            }
            BeginUpdate?.Invoke(element, EventArgs.Empty);
        }

	    private void RaiseEndUpdate(VisualTreeItem element) {
	        if (parent != null) {
	            parent.RaiseEndUpdate(element);
	            return;
	        }
	        EndUpdate?.Invoke(element, EventArgs.Empty);
	    }

	    public bool HasChildren {
	        get { return hasChildren; }
	        set {
	            hasChildren = value;
	            OnPropertyChanged("HasChildren");
	        }
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

	    private bool fillingChildren = false;
	    void FillChildren() {
	        if(fillingChildren)
                return;
	        fillingChildren = true;
            FillChildrenImpl();
	        fillingChildren = false;
	    }
	    protected virtual void FillChildrenImpl() {
	        
	    }

	    protected virtual void Detach() {
	    }


	    public VisualTreeItem FindNode(object target)
		{
			// it might be faster to have a map for the lookup
			// check into this at some point            
            if (this.Target == target) {
                if (DXMethods.IsChrome(Target) && target is IInputElement) {
                    var root = DXMethods.GetRoot(Target);
                    if (root != null) {
                        var child = RenderTreeHelper.HitTest(root, System.Windows.Input.Mouse.GetPosition((IInputElement)target));
                        var node = FindNode(child);
                        if (node != null)
                            return node;
                    }
                }
                return this;
            }				

			foreach (VisualTreeItem child in this.Children)
			{
				VisualTreeItem node = child.FindNode(target);
				if (node != null)
					return node;
			}
			return null;
		}


		/// <summary>
		/// Used for tree search.
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public bool Filter(string value)
		{
			if (this.typeNameLower.Contains(value))
				return true;
			if (this.nameLower.Contains(value))
				return true;
			int n;
			if (int.TryParse(value, out n) && n == this.depth)
				return true;
			return false;
		}		


		private string name;
		private string nameLower = string.Empty;
		private string typeNameLower = string.Empty;
	    private bool hasChildren;


	    public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged(string propertyName)
		{
			Debug.Assert(this.GetType().GetProperty(propertyName) != null);
			if (this.PropertyChanged != null)
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
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
    }
}
