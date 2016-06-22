// (c) Copyright Cory Plotts.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Snoop.Infrastructure;

namespace Snoop {
    public class PropertyInformation : DependencyObject, IComparable, INotifyPropertyChanged {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register
                (
                    "Value",
                    typeof(object),
                    typeof(PropertyInformation),
                    new PropertyMetadata(HandleValueChanged)
                );

        readonly object component;
        readonly bool isCopyable;

        bool breakOnChange;
        bool changedRecently;
        DispatcherTimer changeTimer;
        PropertyFilter filter;
        bool ignoreUpdate;
        int index;

        bool isRunning;

        /// <summary>
        ///     Normal constructor used when constructing PropertyInformation objects for properties.
        /// </summary>
        /// <param name="target">target object being shown in the property grid</param>
        /// <param name="property">the property around which we are contructing this PropertyInformation object</param>
        /// <param name="propertyName">
        ///     the property name for the property that we use in the binding in the case of a
        ///     non-dependency property
        /// </param>
        /// <param name="propertyDisplayName">the display name for the property that goes in the name column</param>
        public PropertyInformation(object target, PropertyDescriptor property, string propertyName,
            string propertyDisplayName) {
            Target = target;
            Property = property;
            DisplayName = propertyDisplayName;

            if (property != null) {
                // create a data binding between the actual property value on the target object
                // and the Value dependency property on this PropertyInformation object
                Binding binding;
                var dp = DependencyProperty;
                if (dp != null) {
                    binding = new Binding();
                    binding.Path = new PropertyPath("(0)", dp);
                }
                else {
                    binding = new Binding {Path = new PropertyPath("(0)", Property)};
                }

                binding.Source = target;
                binding.Mode = property.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay;

                try {
                    BindingOperations.SetBinding(this, ValueProperty, binding);
                }
                catch (Exception) {
                    // cplotts note:
                    // warning: i saw a problem get swallowed by this empty catch (Exception) block.
                    // in other words, this empty catch block could be hiding some potential future errors.
                }
            }

            Update();

            isRunning = true;
        }

        /// <summary>
        ///     Constructor used when constructing PropertyInformation objects for an item in a collection.
        ///     In this case, we set the PropertyDescriptor for this object (in the property Property) to be null.
        ///     This kind of makes since because an item in a collection really isn't a property on a class.
        ///     That is, in this case, we're really hijacking the PropertyInformation class
        ///     in order to expose the items in the Snoop property grid.
        /// </summary>
        /// <param name="target">the item in the collection</param>
        /// <param name="component">the collection</param>
        /// <param name="displayName">the display name that goes in the name column, i.e. this[x]</param>
        public PropertyInformation(object target, object component, string displayName, bool isCopyable = false)
            : this(target, null, displayName, displayName) {
            this.component = component;
            this.isCopyable = isCopyable;
        }

        public object Target { get; }

        public object Value {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public string StringValue {
            get {
                var value = Value;
                if (value != null)
                    return value.ToString();
                return string.Empty;
            }
            set {
                if (Property == null) {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    // then just return, since setting the value via a string doesn't make sense.
                    return;
                }

                var targetType = Property.PropertyType;
                if (targetType.IsAssignableFrom(typeof(string))) {
                    Property.SetValue(Target, value);
                }
                else {
                    var converter = TypeDescriptor.GetConverter(targetType);
                    if (converter != null) {
                        try {
                            Property.SetValue(Target, converter.ConvertFrom(value));
                        }
                        catch (Exception) {}
                    }
                }
            }
        }

        public string DescriptiveValue {
            get {
                var value = Value;
                if (value == null) {
                    return string.Empty;
                }

                var stringValue = value.ToString();

                if (stringValue.Equals(value.GetType().ToString())) {
                    // Add brackets around types to distinguish them from values.
                    // Replace long type names with short type names for some specific types, for easier readability.
                    // FUTURE: This could be extended to other types.
                    if (Property != null &&
                        (Property.PropertyType == typeof(Brush) || Property.PropertyType == typeof(Style))) {
                        stringValue = string.Format("[{0}]", value.GetType().Name);
                    }
                    else {
                        stringValue = string.Format("[{0}]", stringValue);
                    }
                }

                // Display #00FFFFFF as Transparent for easier readability
                if (Property != null &&
                    Property.PropertyType == typeof(Brush) &&
                    stringValue.Equals("#00FFFFFF")) {
                    stringValue = "Transparent";
                }

                var dependencyObject = Target as DependencyObject;
                if (dependencyObject != null && DependencyProperty != null) {
                    // Cache the resource key for this item if not cached already. This could be done for more types, but would need to optimize perf.
                    string resourceKey = null;
                    if (Property != null &&
                        (Property.PropertyType == typeof(Style) || Property.PropertyType == typeof(Brush))) {
                        var resourceItem = dependencyObject.GetValue(DependencyProperty);
                        resourceKey = ResourceKeyCache.GetKey(resourceItem);
                        if (string.IsNullOrEmpty(resourceKey)) {
                            resourceKey = ResourceDictionaryKeyHelpers.GetKeyOfResourceItem(dependencyObject,
                                DependencyProperty);
                            ResourceKeyCache.Cache(resourceItem, resourceKey);
                        }
                        Debug.Assert(resourceKey != null);
                    }

                    // Display both the value and the resource key, if there's a key for this property.
                    if (!string.IsNullOrEmpty(resourceKey)) {
                        return string.Format("{0} {1}", resourceKey, stringValue);
                    }

                    // if the value comes from a Binding, show the path in [] brackets
                    if (IsExpression && Binding is Binding) {
                        stringValue = string.Format("{0} {1}", stringValue,
                            BuildBindingDescriptiveString((Binding) Binding, true));
                    }

                    // if the value comes from a MultiBinding, show the binding paths separated by , in [] brackets
                    else if (IsExpression && Binding is MultiBinding) {
                        stringValue = stringValue +
                                      BuildMultiBindingDescriptiveString(
                                          ((MultiBinding) Binding).Bindings.OfType<Binding>().ToArray());
                    }

                    // if the value comes from a PriorityBinding, show the binding paths separated by , in [] brackets
                    else if (IsExpression && Binding is PriorityBinding) {
                        stringValue = stringValue +
                                      BuildMultiBindingDescriptiveString(
                                          ((PriorityBinding) Binding).Bindings.OfType<Binding>().ToArray());
                    }
                }

                return stringValue;
            }
        }

        public Type ComponentType {
            get {
                if (Property == null) {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    // then this.property will be null, but this.component will contain the collection.
                    // use this object to return the type of the collection for the ComponentType.
                    return component.GetType();
                }
                return Property.ComponentType;
            }
        }

        public Type PropertyType {
            get {
                if (Property == null) {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    // just return typeof(object) here, since an item in a collection ... really isn't a property.
                    return typeof(object);
                }
                return Property.PropertyType;
            }
        }

        public Type ValueType {
            get {
                if (Value != null) {
                    return Value.GetType();
                }
                return typeof(object);
            }
        }

        public string BindingError { get; private set; } = string.Empty;

        public PropertyDescriptor Property { get; }

        public string DisplayName { get; }

        public bool IsInvalidBinding { get; private set; }

        public bool IsLocallySet { get; private set; }

        public bool IsValueChangedByUser { get; set; }


        public bool CanEdit {
            get {
                if (Property == null) {
                    // if this is a PropertyInformation object constructed for an item in a collection
                    //return false;
                    return isCopyable;
                }
                return !Property.IsReadOnly;
            }
        }

        public bool IsDatabound { get; private set; }

        public bool IsExpression {
            get { return ValueSource.IsExpression; }
        }

        public bool IsAnimated {
            get { return ValueSource.IsAnimated; }
        }

        public int Index {
            get { return index; }
            set {
                if (index != value) {
                    index = value;
                    OnPropertyChanged("Index");
                    OnPropertyChanged("IsOdd");
                }
            }
        }

        public bool IsOdd {
            get { return index%2 == 1; }
        }

        public BindingBase Binding {
            get {
                var dp = DependencyProperty;
                var d = Target as DependencyObject;
                if (dp != null && d != null)
                    return BindingOperations.GetBindingBase(d, dp);
                return null;
            }
        }

        public BindingExpressionBase BindingExpression {
            get {
                var dp = DependencyProperty;
                var d = Target as DependencyObject;
                if (dp != null && d != null)
                    return BindingOperations.GetBindingExpressionBase(d, dp);
                return null;
            }
        }

        public PropertyFilter Filter {
            get { return filter; }
            set {
                filter = value;

                OnPropertyChanged("IsVisible");
            }
        }

        public bool BreakOnChange {
            get { return breakOnChange; }
            set {
                breakOnChange = value;
                OnPropertyChanged("BreakOnChange");
            }
        }

        public bool HasChangedRecently {
            get { return changedRecently; }
            set {
                changedRecently = value;
                OnPropertyChanged("HasChangedRecently");
            }
        }

        public ValueSource ValueSource { get; private set; }

        public bool IsVisible {
            get { return filter.Show(this); }
        }

        /// <summary>
        ///     Returns the DependencyProperty identifier for the property that this PropertyInformation wraps.
        ///     If the wrapped property is not a DependencyProperty, null is returned.
        /// </summary>
        DependencyProperty DependencyProperty {
            get {
                if (Property != null) {
                    // in order to be a DependencyProperty, the object must first be a regular property,
                    // and not an item in a collection.

                    var dpd = DependencyPropertyDescriptor.FromProperty(Property);
                    if (dpd != null)
                        return dpd.DependencyProperty;
                }

                return null;
            }
        }

        public void Teardown() {
            isRunning = false;
            BindingOperations.ClearAllBindings(this);
        }

        static void HandleValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((PropertyInformation) d).OnValueChanged();
        }

        protected virtual void OnValueChanged() {
            Update();

            if (isRunning) {
                if (breakOnChange) {
                    if (!Debugger.IsAttached)
                        Debugger.Launch();
                    Debugger.Break();
                }

                HasChangedRecently = true;
                if (changeTimer == null) {
                    changeTimer = new DispatcherTimer();
                    changeTimer.Interval = TimeSpan.FromSeconds(1.5);
                    changeTimer.Tick += HandleChangeExpiry;
                    changeTimer.Start();
                }
                else {
                    changeTimer.Stop();
                    changeTimer.Start();
                }
            }
        }

        void HandleChangeExpiry(object sender, EventArgs e) {
            changeTimer.Stop();
            changeTimer = null;

            HasChangedRecently = false;
        }

        /// <summary>
        ///     Build up a string of Paths for a MultiBinding separated by ;
        /// </summary>
        string BuildMultiBindingDescriptiveString(IEnumerable<Binding> bindings) {
            var ret = " {Paths=";
            foreach (var binding in bindings) {
                ret += BuildBindingDescriptiveString(binding, false);
                ret += ";";
            }
            ret = ret.Substring(0, ret.Length - 1); // remove trailing ,
            ret += "}";

            return ret;
        }

        /// <summary>
        ///     Build up a string describing the Binding.  Path and ElementName (if present)
        /// </summary>
        string BuildBindingDescriptiveString(Binding binding, bool isSinglePath) {
            var sb = new StringBuilder();
            var bindingPath = binding.Path.Path;
            var elementName = binding.ElementName;

            if (isSinglePath) {
                sb.Append("{Path=");
            }

            sb.Append(bindingPath);
            if (!string.IsNullOrEmpty(elementName)) {
                sb.AppendFormat(", ElementName={0}", elementName);
            }

            if (isSinglePath) {
                sb.Append("}");
            }

            return sb.ToString();
        }

        public void Clear() {
            var dp = DependencyProperty;
            var d = Target as DependencyObject;
            if (dp != null && d != null)
                ((DependencyObject) Target).ClearValue(dp);
        }

        void Update() {
            if (ignoreUpdate)
                return;

            IsLocallySet = false;
            IsInvalidBinding = false;
            IsDatabound = false;

            var dp = DependencyProperty;
            var d = Target as DependencyObject;

            if (SnoopModes.MultipleDispatcherMode && d != null && d.Dispatcher != Dispatcher)
                return;

            if (dp != null && d != null) {
                if (d.ReadLocalValue(dp) != DependencyProperty.UnsetValue)
                    IsLocallySet = true;

                var expression = BindingOperations.GetBindingExpressionBase(d, dp);
                if (expression != null) {
                    IsDatabound = true;

                    if (expression.HasError || expression.Status != BindingStatus.Active) {
                        IsInvalidBinding = true;

                        var builder = new StringBuilder();
                        var writer = new StringWriter(builder);
                        var tracer = new TextWriterTraceListener(writer);
                        PresentationTraceSources.DataBindingSource.Listeners.Add(tracer);

                        // reset binding to get the error message.
                        ignoreUpdate = true;
                        d.ClearValue(dp);
                        BindingOperations.SetBinding(d, dp, expression.ParentBindingBase);
                        ignoreUpdate = false;

                        // cplotts note: maciek ... are you saying that this is another, more concise way to dispatch the following code?
                        //Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
                        //    {
                        //        bindingError = builder.ToString();
                        //        this.OnPropertyChanged("BindingError");
                        //        PresentationTraceSources.DataBindingSource.Listeners.Remove(tracer);
                        //        writer.Close();
                        //    });

                        // this needs to happen on idle so that we can actually run the binding, which may occur asynchronously.
                        Dispatcher.BeginInvoke
                            (
                                DispatcherPriority.ApplicationIdle,
                                new DispatcherOperationCallback
                                    (
                                    delegate {
                                        BindingError = builder.ToString();
                                        OnPropertyChanged("BindingError");
                                        PresentationTraceSources.DataBindingSource.Listeners.Remove(tracer);
                                        writer.Close();
                                        return null;
                                    }
                                    ),
                                null
                            );
                    }
                    else {
                        BindingError = string.Empty;
                    }
                }

                ValueSource = DependencyPropertyHelper.GetValueSource(d, dp);
            }

            OnPropertyChanged("IsLocallySet");
            OnPropertyChanged("IsInvalidBinding");
            OnPropertyChanged("StringValue");
            OnPropertyChanged("DescriptiveValue");
            OnPropertyChanged("IsDatabound");
            OnPropertyChanged("IsExpression");
            OnPropertyChanged("IsAnimated");
            OnPropertyChanged("ValueSource");
        }

        public static List<PropertyInformation> GetProperties(object obj) {
            return GetProperties(obj, new PertinentPropertyFilter(obj).Filter);
        }

        public static List<PropertyInformation> GetProperties(object obj, Predicate<PropertyDescriptor> filter) {
            var props = new List<PropertyInformation>();


            // get the properties
            var propertyDescriptors = GetAllProperties(obj,
                new Attribute[] {new PropertyFilterAttribute(PropertyFilterOptions.All)});


            // filter the properties
            foreach (var property in propertyDescriptors) {
                if (filter(property)) {
                    var prop = new PropertyInformation(obj, property, property.Name, property.DisplayName);
                    props.Add(prop);
                }
            }

            //delve path. also, issue 4919
            var extendedProps = GetExtendedProperties(obj);
            props.AddRange(extendedProps);


            // if the object is a collection, add the items in the collection as properties
            var collection = obj as ICollection;
            var index = 0;
            if (collection != null) {
                foreach (var item in collection) {
                    var info = new PropertyInformation(item, collection, "this[" + index + "]");
                    index++;
                    info.Value = item;
                    props.Add(info);
                }
            }


            // sort the properties
            props.Sort();


            return props;
        }

        /// <summary>
        ///     4919 + Delve
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        static IList<PropertyInformation> GetExtendedProperties(object obj) {
            var props = new List<PropertyInformation>();

            if (obj != null && ResourceKeyCache.Contains(obj)) {
                var key = ResourceKeyCache.GetKey(obj);
                var prop = new PropertyInformation(key, new object(), "x:Key", true);
                prop.Value = key;
                props.Add(prop);
            }

            return props;
        }

        static List<PropertyDescriptor> GetAllProperties(object obj, Attribute[] attributes) {
            var propertiesToReturn = new List<PropertyDescriptor>();

            // keep looping until you don't have an AmbiguousMatchException exception
            // and you normally won't have an exception, so the loop will typically execute only once.
            var noException = false;
            while (!noException && obj != null) {
                try {
                    // try to get the properties using the GetProperties method that takes an instance
                    var properties = TypeDescriptor.GetProperties(obj, attributes);
                    noException = true;

                    MergeProperties(properties, propertiesToReturn);
                }
                catch (AmbiguousMatchException) {
                    // if we get an AmbiguousMatchException, the user has probably declared a property that hides a property in an ancestor
                    // see issue 6258 (http://snoopwpf.codeplex.com/workitem/6258)
                    //
                    // public class MyButton : Button
                    // {
                    //     public new double? Width
                    //     {
                    //         get { return base.Width; }
                    //         set { base.Width = value.Value; }
                    //     }
                    // }

                    var t = obj.GetType();
                    var properties = TypeDescriptor.GetProperties(t, attributes);

                    MergeProperties(properties, propertiesToReturn);

                    var nextBaseTypeWithDefaultConstructor = GetNextTypeWithDefaultConstructor(t);
                    obj = Activator.CreateInstance(nextBaseTypeWithDefaultConstructor);
                }
            }

            return propertiesToReturn;
        }

        public static bool HasDefaultConstructor(Type type) {
            var constructors = type.GetConstructors();

            foreach (var constructor in constructors) {
                if (constructor.GetParameters().Length == 0)
                    return true;
            }
            return false;
        }

        public static Type GetNextTypeWithDefaultConstructor(Type type) {
            var t = type.BaseType;

            while (!HasDefaultConstructor(t))
                t = t.BaseType;

            return t;
        }

        static void MergeProperties(IEnumerable newProperties, ICollection<PropertyDescriptor> allProperties) {
            foreach (var newProperty in newProperties) {
                var newPropertyDescriptor = newProperty as PropertyDescriptor;
                if (newPropertyDescriptor == null)
                    continue;

                if (!allProperties.Contains(newPropertyDescriptor))
                    allProperties.Add(newPropertyDescriptor);
            }
        }

        public bool IsCollection() {
            var pattern = "^this\\[\\d+\\]$";
            return Regex.IsMatch(DisplayName, pattern);
        }

        public int CollectionIndex() {
            if (IsCollection()) {
                return int.Parse(DisplayName.Substring(5, DisplayName.Length - 6));
            }
            return -1;
        }

        #region IComparable Members

        public int CompareTo(object obj) {
            var thisIndex = CollectionIndex();
            var objIndex = ((PropertyInformation) obj).CollectionIndex();
            if (thisIndex >= 0 && objIndex >= 0) {
                return thisIndex.CompareTo(objIndex);
            }
            return DisplayName.CompareTo(((PropertyInformation) obj).DisplayName);
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) {
            Debug.Assert(GetType().GetProperty(propertyName) != null);
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}